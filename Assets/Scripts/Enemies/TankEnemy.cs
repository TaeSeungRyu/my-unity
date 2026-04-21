using UnityEngine;

// 타입 5: 탱크 — 느리지만 체력 2. 두 번 밟아야 처치. 점수가 가장 높다.
public class TankEnemy : EnemyBase
{
    [Header("추적 설정")]
    public float detectRadius = 25f;
    private Transform target;

    protected override void Awake()
    {
        base.Awake();
        maxHealth    = 2;    // 스톰프 2회 필요
        scoreValue   = 500;  // 고득점
        moveSpeed    = 1.5f; // 느림
        currentHealth = maxHealth;

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) target = pc.transform;
    }

    void FixedUpdate()
    {
        if (isDead || target == null) return;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > detectRadius) return;

        Vector3 dir = toPlayer.normalized;
        rb.linearVelocity = new Vector3(dir.x * moveSpeed, rb.linearVelocity.y, dir.z * moveSpeed);
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // 첫 스톰프 시 모든 렌더러 색상을 어둡게 바꿔 체력이 깎였음을 시각적으로 표시
    public override void OnStomped()
    {
        base.OnStomped();
        if (isDead || rends == null) return;

        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            Color c = originalColors[i] * 0.6f;
            c.a = 1f;
            Material m = rends[i].material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            originalColors[i] = c;
        }
    }
}
