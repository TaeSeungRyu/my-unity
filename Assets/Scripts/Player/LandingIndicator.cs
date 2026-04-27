using System.Collections.Generic;
using UnityEngine;

// 공중에 있을 때 플레이어의 포물선 궤적을 예측해, 그 궤적이 통과하는 시점에
// 발이 적의 상단과 만나는 위치 근방에 스톰프 가능한 적이 있으면 하이라이트한다.
// 지면 적뿐 아니라 Flyer처럼 공중에 있는 적도 같은 로직으로 자연스럽게 잡힌다.
[RequireComponent(typeof(Rigidbody))]
public class LandingIndicator : MonoBehaviour
{
    [Header("판정")]
    public float enemyHighlightRadius = 0.9f;  // 적의 XZ 위치와 궤적상 통과점의 허용 거리
    public float maxPredictTime       = 4f;    // 이 시간 내 발이 도달 가능한 적만 검사
    public float groundCheckDistance  = 0.35f; // 이보다 가까이 지면이 있으면 공중 아님으로 판단
    public LayerMask groundMask = ~0;

    private Rigidbody       rb;
    private CapsuleCollider playerCapsule;
    private readonly List<EnemyBase> highlighted = new List<EnemyBase>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCapsule = GetComponent<CapsuleCollider>();
    }

    void OnDestroy() { ClearHighlights(); }
    void OnDisable() { ClearHighlights(); }

    void LateUpdate()
    {
        if (rb == null) return;

        if (IsGrounded())
        {
            ClearHighlights();
            return;
        }

        Vector3 p0 = transform.position;
        Vector3 v  = rb.linearVelocity;
        Vector3 g  = Physics.gravity;

        // 검사 종료 시간: 지면에 닿는 순간이 있으면 그 시점, 없으면 maxPredictTime
        float tEnd = ResolveEndTime(p0, v, g);
        if (tEnd <= 0f)
        {
            ClearHighlights();
            return;
        }

        RefreshHighlights(p0, v, g, tEnd);
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.05f,
                               Vector3.down, groundCheckDistance,
                               groundMask, QueryTriggerInteraction.Ignore);
    }

    // 지면에 닿는 시점 또는 maxPredictTime 반환. 둘 다 못 찾으면 -1.
    float ResolveEndTime(Vector3 p0, Vector3 v, Vector3 g)
    {
        if (!Physics.Raycast(p0, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
            return maxPredictTime;

        if (TrySolveDescentTime(p0.y, v.y, g.y, hit.point.y, out float t) && t <= maxPredictTime)
            return t;
        return maxPredictTime;
    }

    // y(t) = startY + vy*t + 0.5*gy*t² = targetY 의 양수 해 중 가장 큰 값(=떨어지며 만나는 시간).
    static bool TrySolveDescentTime(float startY, float vy, float gy, float targetY, out float t)
    {
        t = -1f;
        float a = 0.5f * gy;
        float b = vy;
        float c = startY - targetY;

        if (Mathf.Abs(a) < 1e-5f)
        {
            if (Mathf.Abs(b) < 1e-5f) return false;
            t = -c / b;
            return t > 0f;
        }

        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float sq = Mathf.Sqrt(disc);
        float t1 = (-b - sq) / (2f * a);
        float t2 = (-b + sq) / (2f * a);
        t = Mathf.Max(t1, t2);
        return t > 0f;
    }

    void RefreshHighlights(Vector3 p0, Vector3 v, Vector3 g, float tEnd)
    {
        ClearHighlights();

        // 발 오프셋: 루트 위치에서 캡슐 바닥까지의 거리
        float footOffset = playerCapsule != null
            ? playerCapsule.height * 0.5f - playerCapsule.center.y
            : 1f;

        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        float r2 = enemyHighlightRadius * enemyHighlightRadius;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBase e = enemies[i];
            if (e == null || e.IsDead || !e.CanBeStomped) continue;

            Collider col = e.GetComponent<Collider>();
            if (col == null) continue;

            // 플레이어 발이 적 상단에 도달하는 시점 t를 푼다 → transform.y(t) = enemyTop + footOffset
            float requiredCenterY = col.bounds.max.y + footOffset;
            if (!TrySolveDescentTime(p0.y, v.y, g.y, requiredCenterY, out float t)) continue;
            if (t <= 0f || t > tEnd) continue;

            float px = p0.x + v.x * t;
            float pz = p0.z + v.z * t;

            Vector3 ep = e.transform.position;
            float dx = px - ep.x;
            float dz = pz - ep.z;
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
