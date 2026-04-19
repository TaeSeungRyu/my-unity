using UnityEngine;

// 임포트한 캐릭터 모델(Kenney Blocky Characters 등)에 애니메이션 클립이 없을 때
// 팔/다리 뼈를 속도에 맞춰 흔들어 걷는 느낌을 내주는 간이 프로시저럴 애니메이션.
//
// 사용:
//   - Rigidbody가 있는 루트 GameObject에 부착
//   - 자식 계층에서 뼈 이름(arm/leg + L/R 패턴)을 자동으로 찾아 회전
//   - SceneSetup의 TryAttachVisualOverride가 SkinnedMeshRenderer 발견 시 자동 부착
//
// 제대로 된 모션이 필요하면 Mixamo 등에서 FBX 애니메이션을 받아 Animator로 교체.
[RequireComponent(typeof(Rigidbody))]
public class ProceduralWalkAnimation : MonoBehaviour
{
    [Tooltip("최대 걷기 속도 도달 시 팔/다리 스윙 각도(도)")]
    public float swingAmplitude = 45f;

    [Tooltip("걷는 중 위상 변화율 가중치. 클수록 빠르게 흔든다. (속도 6m/s일 때 약 1.8Hz)")]
    public float swingFrequency = 1.8f;

    [Tooltip("정지 시 미세 흔들림 각도(도) — 살아있는 느낌용")]
    public float idleAmplitude = 3f;

    [Tooltip("정지 시 위상 변화율(rad/s)")]
    public float idleFrequency = 2f;

    [Tooltip("이 속도(m/s) 이상에서 완전한 걷기 모션으로 보간")]
    public float walkSpeedReference = 2f;

    private Transform armL, armR, legL, legR;
    private Quaternion armLRest, armRRest, legLRest, legRRest;
    private Rigidbody rb;
    private float phase;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        FindBones();
        CacheRestPoses();

        // FBX에 애니메이션 클립이 내장된 경우 Unity가 자동으로 Animator를 넣는다.
        // 컨트롤러 없는 Animator는 보통 무해하지만, 혹시 모를 간섭을 원천 차단하기 위해
        // 본 직접 회전으로만 움직이도록 Animator는 비활성화.
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.enabled = false;

        int foundCount = (armL ? 1 : 0) + (armR ? 1 : 0) + (legL ? 1 : 0) + (legR ? 1 : 0);
        if (foundCount == 0)
        {
            Debug.Log($"[ProceduralWalkAnimation] '{name}': 팔/다리 본을 찾지 못함 (UFO 등 비캐릭터 모델이면 정상).");
        }
        else
        {
            Debug.Log($"[ProceduralWalkAnimation] '{name}': 본 {foundCount}개 발견 " +
                      $"— armL={(armL ? armL.name : "-")}, armR={(armR ? armR.name : "-")}, " +
                      $"legL={(legL ? legL.name : "-")}, legR={(legR ? legR.name : "-")}");
        }
    }

    void FindBones()
    {
        // 자식 Transform 전체를 순회하며 이름으로 팔/다리 뼈를 식별.
        // 같은 쪽(예: 왼팔)이 여러 개 발견되면 상위(=어깨 쪽)의 첫 매치를 우선.
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();

            // 전완/하박/무릎 아래 같은 하위 뼈는 제외(상위만 흔들면 자연스러움)
            bool isLower = n.Contains("lower") || n.Contains("forearm") ||
                           n.Contains("shin") || n.Contains("knee") ||
                           n.Contains("hand") || n.Contains("foot");
            if (isLower) continue;

            bool hasArmWord = n.Contains("arm") || n.Contains("shoulder");
            bool hasLegWord = n.Contains("leg") || n.Contains("thigh") || n.Contains("hip");

            if (hasArmWord)
            {
                if (armL == null && IsLeft(n))  armL = t;
                if (armR == null && IsRight(n)) armR = t;
            }
            else if (hasLegWord)
            {
                if (legL == null && IsLeft(n))  legL = t;
                if (legR == null && IsRight(n)) legR = t;
            }
        }
    }

    // 이름에 왼쪽 표식이 있는가: "left", 또는 .l / _l / -l 접미사
    static bool IsLeft(string n)
    {
        if (n.Contains("left")) return true;
        return n.EndsWith(".l") || n.EndsWith("_l") || n.EndsWith("-l");
    }

    static bool IsRight(string n)
    {
        if (n.Contains("right")) return true;
        return n.EndsWith(".r") || n.EndsWith("_r") || n.EndsWith("-r");
    }

    void CacheRestPoses()
    {
        if (armL != null) armLRest = armL.localRotation;
        if (armR != null) armRRest = armR.localRotation;
        if (legL != null) legLRest = legL.localRotation;
        if (legR != null) legRRest = legR.localRotation;
    }

    // Animator가 있는 경우를 대비해 LateUpdate에서 회전 적용 (Animator 이후 실행 순서).
    void LateUpdate()
    {
        // 뼈를 하나도 못 찾았으면 무동작(UFO 같은 비캐릭터 모델 케이스)
        if (armL == null && armR == null && legL == null && legR == null) return;

        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
        float speed = new Vector2(v.x, v.z).magnitude;
        float walking = Mathf.Clamp01(speed / walkSpeedReference);
        float amp  = Mathf.Lerp(idleAmplitude, swingAmplitude, walking);
        float freq = Mathf.Lerp(idleFrequency, swingFrequency * Mathf.Max(speed, 0.5f), walking);
        phase += Time.deltaTime * freq;

        float s = Mathf.Sin(phase) * amp;

        // X축 회전으로 앞뒤 스윙. 좌/우와 팔/다리는 위상 반대.
        if (armL != null) armL.localRotation = armLRest * Quaternion.Euler( s, 0f, 0f);
        if (armR != null) armR.localRotation = armRRest * Quaternion.Euler(-s, 0f, 0f);
        if (legL != null) legL.localRotation = legLRest * Quaternion.Euler(-s, 0f, 0f);
        if (legR != null) legR.localRotation = legRRest * Quaternion.Euler( s, 0f, 0f);
    }
}
