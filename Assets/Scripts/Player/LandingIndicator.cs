using System.Collections.Generic;
using UnityEngine;

// 공중에 있을 때 플레이어의 포물선 궤적을 예측해 지면에 착지 예상 지점을 원반으로 표시.
// 착지점 근처에 스톰프 가능 적이 있으면 해당 적을 하이라이트 색으로 바꿔 타겟임을 알린다.
// 플레이어 루트에 부착 (Rigidbody 필요).
[RequireComponent(typeof(Rigidbody))]
public class LandingIndicator : MonoBehaviour
{
    [Header("표시")]
    public float indicatorRadius = 0.6f;                         // 원반 반지름 (미터)
    public Color safeColor   = new Color(0.2f, 0.9f, 1f, 1f);    // 비어있는 착지점 색
    public Color stompColor  = new Color(1f, 0.95f, 0.3f, 1f);   // 적이 타겟일 때 색
    public float groundLift  = 0.03f;                            // 지면 Z-파이팅 방지용 살짝 띄우기

    [Header("판정")]
    public float enemyHighlightRadius = 0.9f; // 착지점으로부터 이 수평 거리 이내 적을 타겟으로 인정
    public float maxPredictTime       = 4f;   // 이 시간 내 착지하지 않으면 미표시
    public float groundCheckDistance  = 0.35f; // 이보다 가까이 지면이 있으면 공중 아님으로 판단
    public LayerMask groundMask = ~0;

    private Rigidbody rb;
    private GameObject indicator;
    private Renderer indicatorRenderer;
    private Material indicatorMaterial;
    private readonly List<EnemyBase> highlighted = new List<EnemyBase>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        BuildIndicator();
    }

    void BuildIndicator()
    {
        // Cylinder를 y축으로 납작하게 눌러 원반 모양으로 사용 (Quad를 누이는 것보다 양면에서 잘 보임)
        indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "LandingIndicator";

        // 플레이어에 딸린 물리/콜라이더 충돌 방지
        Collider c = indicator.GetComponent<Collider>();
        if (c != null) Destroy(c);

        indicator.transform.SetParent(null);
        indicator.transform.localScale = new Vector3(indicatorRadius * 2f, 0.01f, indicatorRadius * 2f);

        indicatorRenderer = indicator.GetComponent<Renderer>();

        // URP Lit이 우선, 그 다음 BiRP Standard, 최후 Unlit/Color
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");

        indicatorMaterial = new Material(sh);
        indicatorRenderer.sharedMaterial = indicatorMaterial;
        ApplyIndicatorColor(safeColor);

        indicator.SetActive(false);
    }

    void OnDestroy()
    {
        ClearHighlights();
        if (indicator != null) Destroy(indicator);
    }

    void OnDisable()
    {
        ClearHighlights();
        if (indicator != null) indicator.SetActive(false);
    }

    void LateUpdate()
    {
        if (rb == null) return;

        // 접지 상태면 숨김
        if (IsGrounded())
        {
            indicator.SetActive(false);
            ClearHighlights();
            return;
        }

        if (!PredictLanding(out Vector3 landing))
        {
            indicator.SetActive(false);
            ClearHighlights();
            return;
        }

        indicator.SetActive(true);
        indicator.transform.position = landing + Vector3.up * groundLift;
        indicator.transform.rotation = Quaternion.identity;

        RefreshEnemyHighlights(landing);
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.05f,
                               Vector3.down, groundCheckDistance,
                               groundMask, QueryTriggerInteraction.Ignore);
    }

    // 현재 위치·속도·중력으로 포물선 궤적을 풀어 지면과 만나는 지점을 구한다.
    // y(t) = p0.y + v.y*t + 0.5*g.y*t^2 = groundY 의 양수 해 중 가장 큰 것.
    bool PredictLanding(out Vector3 landing)
    {
        landing = Vector3.zero;
        Vector3 p0 = transform.position;
        Vector3 v  = rb.linearVelocity;
        Vector3 g  = Physics.gravity;

        // 바로 아래 지면 높이를 찾는다 (없으면 예측 불가)
        if (!Physics.Raycast(p0, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
            return false;
        float groundY = hit.point.y;

        float a = 0.5f * g.y;
        float b = v.y;
        float c = p0.y - groundY;
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float sq = Mathf.Sqrt(disc);
        float t1 = (-b - sq) / (2f * a);
        float t2 = (-b + sq) / (2f * a);
        float t  = Mathf.Max(t1, t2);
        if (t <= 0f || t > maxPredictTime) return false;

        Vector3 pt = p0 + v * t + 0.5f * g * (t * t);
        pt.y = groundY;
        landing = pt;
        return true;
    }

    void RefreshEnemyHighlights(Vector3 landing)
    {
        // 이전 프레임 하이라이트 우선 해제 (이동으로 범위를 벗어났을 수 있음)
        ClearHighlights();

        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        bool anyStomp = false;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase e = enemies[i];
            if (e == null || e.IsDead || !e.CanBeStomped) continue;

            Vector3 ep = e.transform.position;
            float dx = ep.x - landing.x;
            float dz = ep.z - landing.z;
            if (dx * dx + dz * dz <= enemyHighlightRadius * enemyHighlightRadius)
            {
                e.SetStompHighlight(true);
                highlighted.Add(e);
                anyStomp = true;
            }
        }

        ApplyIndicatorColor(anyStomp ? stompColor : safeColor);
    }

    void ClearHighlights()
    {
        for (int i = 0; i < highlighted.Count; i++)
            if (highlighted[i] != null) highlighted[i].SetStompHighlight(false);
        highlighted.Clear();
    }

    void ApplyIndicatorColor(Color col)
    {
        if (indicatorMaterial == null) return;
        if (indicatorMaterial.HasProperty("_BaseColor")) indicatorMaterial.SetColor("_BaseColor", col);
        if (indicatorMaterial.HasProperty("_Color"))     indicatorMaterial.SetColor("_Color", col);
    }
}
