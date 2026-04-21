using System.Collections.Generic;
using UnityEngine;

// 공중에 있을 때 플레이어의 포물선 궤적을 예측해, 착지 예상 지점 근처에
// 스톰프 가능한 적이 있으면 그 적을 하이라이트 색으로 바꿔 타겟임을 알린다.
// 착지점에 스톰프 대상이 없으면 아무것도 표시하지 않는다.
// 플레이어 루트에 부착 (Rigidbody 필요).
[RequireComponent(typeof(Rigidbody))]
public class LandingIndicator : MonoBehaviour
{
    [Header("판정")]
    public float enemyHighlightRadius = 0.9f; // 착지점으로부터 이 수평 거리 이내 적을 타겟으로 인정
    public float maxPredictTime       = 4f;   // 이 시간 내 착지하지 않으면 미표시
    public float groundCheckDistance  = 0.35f; // 이보다 가까이 지면이 있으면 공중 아님으로 판단
    public LayerMask groundMask = ~0;

    private Rigidbody rb;
    private readonly List<EnemyBase> highlighted = new List<EnemyBase>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnDestroy()
    {
        ClearHighlights();
    }

    void OnDisable()
    {
        ClearHighlights();
    }

    void LateUpdate()
    {
        if (rb == null) return;

        // 접지 상태거나 착지 예측 불가면 모든 하이라이트 해제
        if (IsGrounded() || !PredictLanding(out Vector3 landing))
        {
            ClearHighlights();
            return;
        }

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
        float r2 = enemyHighlightRadius * enemyHighlightRadius;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase e = enemies[i];
            if (e == null || e.IsDead || !e.CanBeStomped) continue;

            Vector3 ep = e.transform.position;
            float dx = ep.x - landing.x;
            float dz = ep.z - landing.z;
            if (dx * dx + dz * dz <= r2)
            {
                e.SetStompHighlight(true);
                highlighted.Add(e);
            }
        }
    }

    void ClearHighlights()
    {
        for (int i = 0; i < highlighted.Count; i++)
            if (highlighted[i] != null) highlighted[i].SetStompHighlight(false);
        highlighted.Clear();
    }
}
