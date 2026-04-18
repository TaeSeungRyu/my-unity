#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메뉴 한 번 클릭으로 플레이 가능한 씬을 자동 구성.
// 상단 메뉴: Tools > Setup Game Scene
public static class SceneSetup
{
    [MenuItem("Tools/Setup Game Scene")]
    public static void Setup()
    {
        // 1) 기존 오브젝트를 정리할지 확인
        if (!EditorUtility.DisplayDialog(
                "Setup Game Scene",
                "현재 씬에 지면, 플레이어, 적, UI 등을 자동 생성합니다. 진행할까요?",
                "진행", "취소")) return;

        // ---------- 지면 ----------
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f); // 100x100 크기
        Renderer gr = ground.GetComponent<Renderer>();
        gr.sharedMaterial = CreateColoredMaterial(new Color(0.25f, 0.55f, 0.25f));

        // ---------- 조명(있으면 생략) ----------
        if (Object.FindFirstObjectByType<Light>() == null)
        {
            GameObject light = new GameObject("Directional Light");
            Light l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---------- 플레이어 ----------
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1.1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.2f, 0.5f, 1f));
        Rigidbody prb = player.AddComponent<Rigidbody>();
        prb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        prb.interpolation = RigidbodyInterpolation.Interpolate;
        player.AddComponent<PlayerController>();

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
        GameObject gmGo = new GameObject("GameManager");
        GameManager gm = gmGo.AddComponent<GameManager>();

        // ---------- 적 템플릿 5종 (비활성 상태로 씬에 배치, 스포너가 Instantiate) ----------
        GameObject templates = new GameObject("EnemyTemplates");
        templates.SetActive(false); // 루트 비활성화 -> 모든 템플릿 비활성

        GameObject t1 = CreateWalker(templates.transform);
        GameObject t2 = CreateChaser(templates.transform);
        GameObject t3 = CreateJumper(templates.transform);
        GameObject t4 = CreateFlyer(templates.transform);
        GameObject t5 = CreateTank(templates.transform);

        // ---------- 스포너 ----------
        GameObject spGo = new GameObject("EnemySpawner");
        EnemySpawner sp = spGo.AddComponent<EnemySpawner>();
        sp.player = player.transform;
        sp.entries = new System.Collections.Generic.List<EnemySpawner.SpawnEntry>
        {
            new EnemySpawner.SpawnEntry { prefab = t1, weight = 4f }, // 워커가 제일 흔함
            new EnemySpawner.SpawnEntry { prefab = t2, weight = 2f },
            new EnemySpawner.SpawnEntry { prefab = t3, weight = 1.5f },
            new EnemySpawner.SpawnEntry { prefab = t4, weight = 1.5f },
            new EnemySpawner.SpawnEntry { prefab = t5, weight = 1f }, // 탱크가 제일 드뭄
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
            "완료! ▶ 버튼을 눌러 플레이 해보세요.\n\n조작:\n- 이동: WASD / 방향키\n- 점프: Space\n- 적 위에서 떨어져 밟으면 처치\n- 측면/아래에서 닿으면 HP 감소",
            "확인");
    }

    // ===== 적 템플릿 생성 헬퍼 =====

    private static GameObject CreateWalker(Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Enemy_Walker";
        g.transform.SetParent(parent, false);
        g.transform.localScale = new Vector3(1f, 1f, 1f);
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.9f, 0.5f, 0.2f));
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        g.AddComponent<WalkerEnemy>();
        return g;
    }

    private static GameObject CreateChaser(Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        g.name = "Enemy_Chaser";
        g.transform.SetParent(parent, false);
        g.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.9f, 0.2f, 0.2f));
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        g.AddComponent<ChaserEnemy>();
        return g;
    }

    private static GameObject CreateJumper(Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Enemy_Jumper";
        g.transform.SetParent(parent, false);
        g.transform.localScale = Vector3.one;
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.9f, 0.85f, 0.2f));
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        g.AddComponent<JumperEnemy>();
        return g;
    }

    private static GameObject CreateFlyer(Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Enemy_Flyer";
        g.transform.SetParent(parent, false);
        g.transform.localScale = new Vector3(1.4f, 0.4f, 1.4f); // 납작한 UFO 느낌
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.6f, 0.3f, 0.9f));
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        g.AddComponent<FlyerEnemy>();
        return g;
    }

    private static GameObject CreateTank(Transform parent)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Enemy_Tank";
        g.transform.SetParent(parent, false);
        g.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f); // 크고 단단해 보이게
        g.GetComponent<Renderer>().sharedMaterial = CreateColoredMaterial(new Color(0.4f, 0.4f, 0.45f));
        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.mass = 4f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        g.AddComponent<TankEnemy>();
        return g;
    }

    // ===== UI 자동 구성 =====

    private static void BuildUI(UIManager uim)
    {
        // Canvas
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 상단 HP 텍스트
        TMP_Text hp = CreateTMPText(canvasGo.transform, "HPText", "HP ♥♥♥",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), TextAlignmentOptions.TopLeft, 36);
        uim.healthText = hp;

        // 상단 Score 텍스트
        TMP_Text sc = CreateTMPText(canvasGo.transform, "ScoreText", "Score: 0",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), TextAlignmentOptions.TopRight, 36);
        uim.scoreText = sc;

        // Game Over 패널
        GameObject panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(canvasGo.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        TMP_Text title = CreateTMPText(panel.transform, "Title", "GAME OVER",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 140), TextAlignmentOptions.Center, 80);
        title.color = Color.white;

        TMP_Text final = CreateTMPText(panel.transform, "FinalScore", "최종 점수\n0",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), TextAlignmentOptions.Center, 48);
        final.color = Color.white;
        uim.finalScoreText = final;

        // Restart 버튼
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

    // TextMeshProUGUI 생성 헬퍼
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
        rt.pivot     = new Vector2(
            align == TextAlignmentOptions.TopLeft  || align == TextAlignmentOptions.Left  ? 0f :
            align == TextAlignmentOptions.TopRight || align == TextAlignmentOptions.Right ? 1f : 0.5f,
            align == TextAlignmentOptions.TopLeft  || align == TextAlignmentOptions.TopRight ? 1f : 0.5f);
        rt.sizeDelta = new Vector2(600, 80);
        rt.anchoredPosition = anchoredPos;
        return t;
    }

    // 간단한 단색 재질 생성(URP/BiRP 어느 파이프라인이든 기본 셰이더를 찾아 적용)
    private static Material CreateColoredMaterial(Color color)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        // URP Lit은 _BaseColor, Standard는 _Color 사용
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
        return m;
    }
}
#endif
