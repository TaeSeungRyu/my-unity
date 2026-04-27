using UnityEngine;

// 타입 4: 플라이어 — 공중에 떠서 플레이어에게 다가옴.
// 점프로 뛰어올라 밟아야 처치 가능. 땅에 서서는 닿을 수 없다.
// 벽과 충돌해야 하므로 velocity 기반 이동(useGravity = false만 끄고 동역학은 유지).
public class FlyerEnemy : EnemyBase
{
    [Header("비행 설정")]
    // 플레이어 점프 1회로 위에 닿을 수 있는 높이 — jumpForce=8, gravity=9.81 기준 발 apex≈3.36m
    public float flyHeight    = 1.8f;  // 지면으로부터 유지할 높이 (콜라이더 상단≈2.3m)
    public float bobAmplitude = 0.15f; // 위아래로 떠다니는 폭 — 작아야 스톰프 타이밍이 안정
    public float bobFrequency = 1.2f;  // 떠다니는 주기 (느리게)
    public float chaseSpeed   = 2f;    // 수평 추적 속도 (플레이어보다 충분히 느리게)
    public float verticalLerp = 3f;    // 기준 높이로 수렴하는 속도

    private Transform target;
    private float     baseY;

    protected override void Awake()
    {
        base.Awake();
        maxHealth    = 1;
        scoreValue   = 300;
        moveSpeed    = 3f;
        CanBeStomped = true;
        currentHealth = maxHealth;

        // 비행형은 중력의 영향을 받지 않지만, 동역학 자체는 유지하여 벽과 충돌
        rb.useGravity = false;

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) target = pc.transform;
        baseY = transform.position.y;
    }

    void FixedUpdate()
    {
        if (isDead || target == null) return;

        // 수평: 플레이어 방향으로 추적
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.zero;

        // 수직: 기준 높이에서 사인파로 부유 — 목표 y와의 차이를 속도로 환산
        float targetY = baseY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        float yVel = (targetY - transform.position.y) * verticalLerp;

        // velocity로 이동시켜 Physics가 벽 충돌을 처리하도록 함
        rb.linearVelocity = new Vector3(dir.x * chaseSpeed, yVel, dir.z * chaseSpeed);
    }

    // 스폰 시 지면 높이를 받아 부유 기준 Y를 설정
    public void InitFlyHeight(float groundY)
    {
        baseY = groundY + flyHeight;
        Vector3 p = transform.position;
        p.y = baseY;
        transform.position = p;
    }
}
