#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메뉴 한 번 클릭으로 플레이 가능한 씬을 자동 구성.
// 상단 메뉴: Tools > Setup Game Scene
//
// 생성물:
//   - 지면 + 4면 경계 벽(플레이 영역 40x40)
//   - 복합 모양 플레이어 (캡슐 몸통 + 구 머리 + 눈)
//   - 5가지 복합 모양 적 템플릿 (비활성; 스포너가 Instantiate)
//   - GameManager / EnemySpawner / UIManager / Canvas / EventSystem / 3인칭 카메라
public static class SceneSetup
{
    // 플레이 영역(정사각) 한 변 길이. 벽은 ±halfSize 위치에 선다.
    private const float PlayAreaSize  = 40f;
    private const float WallHeight    = 5f;  // 점프로 넘지 못할 정도
    private const float WallThickness = 1f;

    [MenuItem("Tools/Setup Game Scene")]
    public static void Setup()
    {
        if (!EditorUtility.DisplayDialog(
                "Setup Game Scene",
                "현재 씬에 지면/벽/플레이어/적/UI를 자동 생성합니다.\n" +
                "기존에 같은 이름의 오브젝트가 있으면 교체됩니다. 진행할까요?",
                "진행", "취소")) return;

        // ---------- 기존 오브젝트 정리(재실행 대응) ----------
        CleanupExisting();

        // ---------- 지면 ----------
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100x100
        ground.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.25f, 0.55f, 0.25f));

        // ---------- 경계 벽 ----------
        BuildWalls();

        // ---------- 조명 ----------
        if (Object.FindFirstObjectByType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---------- 플레이어(복합 모양) ----------
        GameObject player = BuildPlayer();

        // ---------- 메인 카메라 + 추적 스크립트 ----------
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        CameraFollow cf = cam.GetComponent<CameraFollow>();
        if (cf == null) cf = cam.gameObject.AddComponent<CameraFollow>();
        cf.target = player.transform;
        cam.transform.position = player.transform.position + new Vector3(0f, 6f, -8f);

        // ---------- 게임 매니저 ----------
        new GameObject("GameManager").AddComponent<GameManager>();

        // ---------- 적 템플릿 5종(비활성 루트 아래에 배치; 스포너가 복제) ----------
        GameObject templates = new GameObject("EnemyTemplates");
        templates.SetActive(false);

        GameObject t1 = BuildWalker(templates.transform);
        GameObject t2 = BuildChaser(templates.transform);
        GameObject t3 = BuildJumper(templates.transform);
        GameObject t4 = BuildFlyer(templates.transform);
        GameObject t5 = BuildTank(templates.transform);

        // ---------- 스포너 ----------
        GameObject spGo = new GameObject("EnemySpawner");
        EnemySpawner sp = spGo.AddComponent<EnemySpawner>();
        sp.player = player.transform;
        float half = PlayAreaSize * 0.5f - 1f; // 벽 두께 여유
        sp.playAreaMin = new Vector3(-half, 0f, -half);
        sp.playAreaMax = new Vector3( half, 0f,  half);
        sp.entries = new System.Collections.Generic.List<EnemySpawner.SpawnEntry>
        {
            new EnemySpawner.SpawnEntry { prefab = t1, weight = 4f },   // 워커 빈도 최대
            new EnemySpawner.SpawnEntry { prefab = t2, weight = 2f },
            new EnemySpawner.SpawnEntry { prefab = t3, weight = 1.5f },
            new EnemySpawner.SpawnEntry { prefab = t4, weight = 1.5f },
            new EnemySpawner.SpawnEntry { prefab = t5, weight = 1f },   // 탱크 최소
        };

        // ---------- UI ----------
        GameObject uiMgrGo = new GameObject("UIManager");
        UIManager uim = uiMgrGo.AddComponent<UIManager>();
        BuildUI(uim);

        // ---------- EventSystem (UI 버튼 입력용) ----------
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        EditorUtility.DisplayDialog(
            "Setup Game Scene",
            "완료! ▶ 버튼을 눌러 플레이 해보세요.\n\n" +
            "조작: WASD/방향키 이동, Space 점프\n" +
            "벽으로 막힌 40×40 영역 안에서 플레이합니다.",
            "확인");
    }

    // 이전 실행으로 생성된 오브젝트들을 이름으로 찾아 제거
    private static void CleanupExisting()
    {
        string[] names = { "Ground", "Walls", "Player", "GameManager",
                           "EnemyTemplates", "EnemySpawner", "UIManager",
                           "Canvas", "EventSystem" };
        foreach (var n in names)
        {
            GameObject g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }
    }

    // ============================================================
    // 지면 경계 벽 4면 생성
    // ============================================================
    private static void BuildWalls()
    {
        GameObject walls = new GameObject("Walls");
        float half = PlayAreaSize * 0.5f;
        Color wallColor = new Color(0.55f, 0.45f, 0.35f);

        // 남/북 벽은 X축 방향으로 길게, 동/서 벽은 Z축 방향으로 길게
        CreateWall(walls.transform, "Wall_N",
            new Vector3(0f, WallHeight * 0.5f,  half + WallThickness * 0.5f),
            new Vector3(PlayAreaSize + WallThickness * 2f, WallHeight, WallThickness), wallColor);
        CreateWall(walls.transform, "Wall_S",
            new Vector3(0f, WallHeight * 0.5f, -half - WallThickness * 0.5f),
            new Vector3(PlayAreaSize + WallThickness * 2f, WallHeight, WallThickness), wallColor);
        CreateWall(walls.transform, "Wall_E",
            new Vector3( half + WallThickness * 0.5f, WallHeight * 0.5f, 0f),
            new Vector3(WallThickness, WallHeight, PlayAreaSize), wallColor);
        CreateWall(walls.transform, "Wall_W",
            new Vector3(-half - WallThickness * 0.5f, WallHeight * 0.5f, 0f),
            new Vector3(WallThickness, WallHeight, PlayAreaSize), wallColor);
    }

    private static void CreateWall(Transform parent, string name, Vector3 worldPos, Vector3 size, Color color)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = name;
        w.transform.SetParent(parent, true);
        w.transform.position = worldPos;
        w.transform.localScale = size;
        w.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(color);
    }

    // ============================================================
    // 플레이어 (복합 모양) — 캡슐 몸통 + 구 머리 + 흰자+검은 동공
    // ============================================================
    private static GameObject BuildPlayer()
    {
        // 루트: 빈 GameObject (물리/스크립트 담당)
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.1f, 0f);

        // 물리 콜라이더(수동으로 캡슐 크기 지정)
        CapsuleCollider col = player.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.5f;
        col.center = new Vector3(0f, 0f, 0f); // 루트 기준 몸통 중심이 원점

        Rigidbody prb = player.AddComponent<Rigidbody>();
        prb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        prb.interpolation = RigidbodyInterpolation.Interpolate;
        prb.mass = 1f;

        player.AddComponent<PlayerController>();

        // 시각적 자식들 (콜라이더 없음)
        Color blue  = new Color(0.25f, 0.55f, 1f);
        Color skin  = new Color(1f, 0.85f, 0.7f);
        // 몸통 캡슐
        AddVisual(player, PrimitiveType.Capsule, new Vector3(0, 0, 0), new Vector3(1f, 1f, 1f), blue, Quaternion.identity);
        // 머리 (캡슐 상단 위)
        AddVisual(player, PrimitiveType.Sphere,  new Vector3(0, 1.1f, 0), new Vector3(0.8f, 0.8f, 0.8f), skin, Quaternion.identity);
        // 흰자 (머리 앞쪽)
        AddVisual(player, PrimitiveType.Sphere,  new Vector3(-0.18f, 1.15f, 0.33f), new Vector3(0.22f, 0.22f, 0.22f), Color.white, Quaternion.identity);
        AddVisual(player, PrimitiveType.Sphere,  new Vector3( 0.18f, 1.15f, 0.33f), new Vector3(0.22f, 0.22f, 0.22f), Color.white, Quaternion.identity);
        // 동공
        AddVisual(player, PrimitiveType.Sphere,  new Vector3(-0.18f, 1.15f, 0.42f), new Vector3(0.1f, 0.1f, 0.1f), Color.black, Quaternion.identity);
        AddVisual(player, PrimitiveType.Sphere,  new Vector3( 0.18f, 1.15f, 0.42f), new Vector3(0.1f, 0.1f, 0.1f), Color.black, Quaternion.identity);

        return player;
    }

    // ============================================================
    // 적 5종 — 모두 "루트는 빈 오브젝트 + 콜라이더(수동)" 규칙.
    // 루트 피봇은 발바닥(y=0). 콜라이더 center는 (0, 높이/2, 0).
    // 스포너가 groundY=0.1에 스폰하면 약간 떠 있다 중력으로 안착.
    // ============================================================

    // ① 워커: 귀엽고 둔한 갈색 블록형
    private static GameObject BuildWalker(Transform parent)
    {
        GameObject g = CreateEnemyRoot("Enemy_Walker", parent);
        BoxCollider c = g.AddComponent<BoxCollider>();
        c.size = new Vector3(0.9f, 1.0f, 0.9f);
        c.center = new Vector3(0f, 0.5f, 0f);
        g.AddComponent<WalkerEnemy>();

        Color body = new Color(0.75f, 0.45f, 0.2f);
        Color dark = new Color(0.4f, 0.22f, 0.1f);
        // 몸통
        AddVisual(g, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.9f, 0.9f), body, Quaternion.identity);
        // 발 2개
        AddVisual(g, PrimitiveType.Cube, new Vector3(-0.3f, 0.1f, 0.1f), new Vector3(0.3f, 0.2f, 0.5f), dark, Quaternion.identity);
        AddVisual(g, PrimitiveType.Cube, new Vector3( 0.3f, 0.1f, 0.1f), new Vector3(0.3f, 0.2f, 0.5f), dark, Quaternion.identity);
        // 뿔 2개 (작은 캡슐)
        AddVisual(g, PrimitiveType.Capsule, new Vector3(-0.25f, 1.05f, 0f), new Vector3(0.12f, 0.18f, 0.12f), dark, Quaternion.identity);
        AddVisual(g, PrimitiveType.Capsule, new Vector3( 0.25f, 1.05f, 0f), new Vector3(0.12f, 0.18f, 0.12f), dark, Quaternion.identity);
        // 눈: 흰자 + 동공
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.2f, 0.6f, 0.42f), new Vector3(0.22f, 0.22f, 0.22f), Color.white, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.2f, 0.6f, 0.42f), new Vector3(0.22f, 0.22f, 0.22f), Color.white, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.2f, 0.6f, 0.49f), new Vector3(0.11f, 0.11f, 0.11f), Color.black, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.2f, 0.6f, 0.49f), new Vector3(0.11f, 0.11f, 0.11f), Color.black, Quaternion.identity);
        return g;
    }

    // ② 체이서: 공격적으로 생긴 붉은 돌진형 — 캡슐 몸통 + 송곳니 느낌의 뿔
    private static GameObject BuildChaser(Transform parent)
    {
        GameObject g = CreateEnemyRoot("Enemy_Chaser", parent);
        CapsuleCollider c = g.AddComponent<CapsuleCollider>();
        c.height = 1.2f;
        c.radius = 0.35f;
        c.center = new Vector3(0f, 0.6f, 0f);
        g.AddComponent<ChaserEnemy>();

        Color body  = new Color(0.85f, 0.2f, 0.25f);
        Color dark  = new Color(0.5f, 0.1f, 0.12f);
        Color glow  = new Color(1f, 0.9f, 0.2f);
        // 몸통(캡슐)
        AddVisual(g, PrimitiveType.Capsule, new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.6f, 0.7f), body, Quaternion.identity);
        // 등의 가시 4개 (작은 큐브를 45도로 회전)
        Quaternion tilt = Quaternion.Euler(0f, 45f, 0f);
        AddVisual(g, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.18f, 0.25f, 0.18f), dark, tilt);
        AddVisual(g, PrimitiveType.Cube, new Vector3(0.25f, 0.95f, 0f), new Vector3(0.14f, 0.2f, 0.14f), dark, tilt);
        AddVisual(g, PrimitiveType.Cube, new Vector3(-0.25f, 0.95f, 0f), new Vector3(0.14f, 0.2f, 0.14f), dark, tilt);
        AddVisual(g, PrimitiveType.Cube, new Vector3(0f, 0.95f, 0.25f), new Vector3(0.14f, 0.2f, 0.14f), dark, tilt);
        // 눈 2개 (노란 발광색 느낌)
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.15f, 0.75f, 0.3f), new Vector3(0.15f, 0.15f, 0.15f), glow, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.15f, 0.75f, 0.3f), new Vector3(0.15f, 0.15f, 0.15f), glow, Quaternion.identity);
        return g;
    }

    // ③ 점퍼: 노란 통통 구 + 안테나 + 짧은 다리
    private static GameObject BuildJumper(Transform parent)
    {
        GameObject g = CreateEnemyRoot("Enemy_Jumper", parent);
        SphereCollider c = g.AddComponent<SphereCollider>();
        c.radius = 0.45f;
        c.center = new Vector3(0f, 0.45f, 0f);
        g.AddComponent<JumperEnemy>();

        Color body   = new Color(0.95f, 0.85f, 0.2f);
        Color accent = new Color(0.3f, 0.2f, 0.1f);
        Color tip    = new Color(0.9f, 0.2f, 0.2f);
        // 몸통(구)
        AddVisual(g, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), body, Quaternion.identity);
        // 안테나(얇은 캡슐)
        AddVisual(g, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.08f, 0.2f, 0.08f), accent, Quaternion.identity);
        // 안테나 끝 빨간 공
        AddVisual(g, PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f), new Vector3(0.18f, 0.18f, 0.18f), tip, Quaternion.identity);
        // 짧은 다리 2개
        AddVisual(g, PrimitiveType.Cylinder, new Vector3(-0.2f, 0.1f, 0f), new Vector3(0.1f, 0.15f, 0.1f), accent, Quaternion.identity);
        AddVisual(g, PrimitiveType.Cylinder, new Vector3( 0.2f, 0.1f, 0f), new Vector3(0.1f, 0.15f, 0.1f), accent, Quaternion.identity);
        // 눈
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.18f, 0.55f, 0.36f), new Vector3(0.16f, 0.16f, 0.16f), Color.white, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.18f, 0.55f, 0.36f), new Vector3(0.16f, 0.16f, 0.16f), Color.white, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.18f, 0.55f, 0.43f), new Vector3(0.08f, 0.08f, 0.08f), Color.black, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.18f, 0.55f, 0.43f), new Vector3(0.08f, 0.08f, 0.08f), Color.black, Quaternion.identity);
        return g;
    }

    // ④ 플라이어: UFO — 납작 원반 + 상단 돔 + 하단 발광등
    private static GameObject BuildFlyer(Transform parent)
    {
        GameObject g = CreateEnemyRoot("Enemy_Flyer", parent);
        BoxCollider c = g.AddComponent<BoxCollider>();
        c.size = new Vector3(1.4f, 0.5f, 1.4f);
        c.center = new Vector3(0f, 0.25f, 0f);
        g.AddComponent<FlyerEnemy>();
        // FlyerEnemy.Awake에서 useGravity=false로 설정됨

        Color body = new Color(0.55f, 0.35f, 0.85f);
        Color dome = new Color(0.8f, 0.85f, 1f);
        Color light = new Color(0.2f, 1f, 0.9f);
        // 디스크 본체 (눌린 원통 느낌)
        AddVisual(g, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(1.4f, 0.12f, 1.4f), body, Quaternion.identity);
        // 상단 유리 돔
        AddVisual(g, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.55f, 0.8f), dome, Quaternion.identity);
        // 하단 라이트(4방향)
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.55f, 0.1f, 0f), new Vector3(0.18f, 0.18f, 0.18f), light, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.55f, 0.1f, 0f), new Vector3(0.18f, 0.18f, 0.18f), light, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3(0f, 0.1f,  0.55f), new Vector3(0.18f, 0.18f, 0.18f), light, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.55f), new Vector3(0.18f, 0.18f, 0.18f), light, Quaternion.identity);
        return g;
    }

    // ⑤ 탱크: 우람한 회색 몸체 + 상단 터렛 + 포신 + 모서리 스파이크
    private static GameObject BuildTank(Transform parent)
    {
        GameObject g = CreateEnemyRoot("Enemy_Tank", parent);
        BoxCollider c = g.AddComponent<BoxCollider>();
        c.size = new Vector3(1.6f, 1.3f, 1.6f);
        c.center = new Vector3(0f, 0.65f, 0f);
        Rigidbody rb = g.GetComponent<Rigidbody>();
        rb.mass = 4f;
        g.AddComponent<TankEnemy>();

        Color body   = new Color(0.45f, 0.47f, 0.5f);
        Color dark   = new Color(0.28f, 0.29f, 0.32f);
        Color accent = new Color(0.15f, 0.16f, 0.18f);
        // 몸통
        AddVisual(g, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(1.6f, 1.3f, 1.6f), body, Quaternion.identity);
        // 상단 터렛(짧은 실린더)
        AddVisual(g, PrimitiveType.Cylinder, new Vector3(0f, 1.45f, 0f), new Vector3(0.8f, 0.18f, 0.8f), dark, Quaternion.identity);
        // 포신(길쭉한 큐브)
        AddVisual(g, PrimitiveType.Cube, new Vector3(0f, 1.45f, 0.55f), new Vector3(0.2f, 0.2f, 0.8f), accent, Quaternion.identity);
        // 모서리 스파이크 4개
        AddVisual(g, PrimitiveType.Cube, new Vector3( 0.75f, 1.35f,  0.75f), new Vector3(0.25f, 0.3f, 0.25f), accent, Quaternion.Euler(0, 45, 0));
        AddVisual(g, PrimitiveType.Cube, new Vector3(-0.75f, 1.35f,  0.75f), new Vector3(0.25f, 0.3f, 0.25f), accent, Quaternion.Euler(0, 45, 0));
        AddVisual(g, PrimitiveType.Cube, new Vector3( 0.75f, 1.35f, -0.75f), new Vector3(0.25f, 0.3f, 0.25f), accent, Quaternion.Euler(0, 45, 0));
        AddVisual(g, PrimitiveType.Cube, new Vector3(-0.75f, 1.35f, -0.75f), new Vector3(0.25f, 0.3f, 0.25f), accent, Quaternion.Euler(0, 45, 0));
        // 눈(정면)
        AddVisual(g, PrimitiveType.Sphere, new Vector3(-0.35f, 0.8f, 0.81f), new Vector3(0.2f, 0.2f, 0.2f), Color.red, Quaternion.identity);
        AddVisual(g, PrimitiveType.Sphere, new Vector3( 0.35f, 0.8f, 0.81f), new Vector3(0.2f, 0.2f, 0.2f), Color.red, Quaternion.identity);
        return g;
    }

    // ============================================================
    // 공용 헬퍼
    // ============================================================

    // 빈 루트 + Rigidbody(회전 고정). 콜라이더/스크립트는 호출측에서 추가.
    private static GameObject CreateEnemyRoot(string name, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        return root;
    }

    // 시각 전용 자식 프리미티브(콜라이더 제거)
    private static GameObject AddVisual(GameObject parent, PrimitiveType shape, Vector3 localPos, Vector3 localScale, Color color, Quaternion localRot)
    {
        GameObject g = GameObject.CreatePrimitive(shape);
        g.transform.SetParent(parent.transform, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = localRot;
        g.transform.localScale = localScale;
        // 콜라이더는 루트에서 관리하므로 자식은 제거(중복 충돌 방지)
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(color);
        return g;
    }

    // ============================================================
    // UI 자동 구성
    // ============================================================
    private static void BuildUI(UIManager uim)
    {
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 상단 좌: HP
        TMP_Text hp = CreateTMPText(canvasGo.transform, "HPText", "HP ♥♥♥",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), TextAlignmentOptions.TopLeft, 36);
        uim.healthText = hp;

        // 상단 우: Score
        TMP_Text sc = CreateTMPText(canvasGo.transform, "ScoreText", "Score: 0",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), TextAlignmentOptions.TopRight, 36);
        uim.scoreText = sc;

        // 게임오버 패널 (반투명 검정 배경)
        GameObject panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

        TMP_Text title = CreateTMPText(panel.transform, "Title", "GAME OVER",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), TextAlignmentOptions.Center, 80);
        title.color = Color.white;

        TMP_Text final = CreateTMPText(panel.transform, "FinalScore", "최종 점수\n0",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), TextAlignmentOptions.Center, 48);
        final.color = Color.white;
        uim.finalScoreText = final;

        // 다시 시작 버튼
        GameObject btnGo = new GameObject("RestartButton");
        btnGo.transform.SetParent(panel.transform, false);
        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 1f);
        Button btn = btnGo.AddComponent<Button>();
        RectTransform brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(240, 70);
        brt.anchoredPosition = new Vector2(0, -130);

        TMP_Text btnText = CreateTMPText(btnGo.transform, "Label", "다시 시작",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, TextAlignmentOptions.Center, 32);
        btnText.color = Color.white;
        RectTransform btrt = btnText.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;

        uim.gameOverPanel = panel;
        uim.restartButton = btn;
        panel.SetActive(false);
    }

    private static TMP_Text CreateTMPText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, TextAlignmentOptions align, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        // 앵커에 맞춰 피봇도 조정(좌상단 앵커엔 피봇 좌상단)
        rt.pivot = new Vector2(
            align == TextAlignmentOptions.TopLeft  || align == TextAlignmentOptions.Left  ? 0f :
            align == TextAlignmentOptions.TopRight || align == TextAlignmentOptions.Right ? 1f : 0.5f,
            align == TextAlignmentOptions.TopLeft  || align == TextAlignmentOptions.TopRight ? 1f : 0.5f);
        rt.sizeDelta = new Vector2(600, 80);
        rt.anchoredPosition = anchoredPos;
        return t;
    }

    // URP/BiRP/Unlit 중 사용 가능한 기본 셰이더를 찾아 단색 재질 생성
    private static Material CreateColoredMaterial(Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); // URP Lit
        if (m.HasProperty("_Color"))     m.SetColor("_Color", color);     // Built-in Standard
        return m;
    }
}
#endif
