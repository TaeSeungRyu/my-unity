using UnityEngine;

// 타입 2: 체이서 — 플레이어를 추적. 스톰프 한 번이면 죽지만 빠르고 위험.
public class ChaserEnemy : EnemyBase
{
    [Header("추적 설정")]
    public float detectRadius = 20f; // 이 거리 안일 때만 추적
    private Transform target;

    protected override void Awake()
    {
        base.Awake();
        maxHealth    = 1;
        scoreValue   = 200;
        moveSpeed    = 4f;
        currentHealth = maxHealth;

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) target = pc.transform;
    }

    void FixedUpdate()
    {
        if (isDead || target == null) return;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist > detectRadius) return;

        Vector3 dir = toPlayer.normalized;
        rb.linearVelocity = new Vector3(dir.x * moveSpeed, rb.linearVelocity.y, dir.z * moveSpeed);
        // 진행 방향을 바라보도록 회전
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
