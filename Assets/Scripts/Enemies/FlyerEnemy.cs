using UnityEngine;

// 타입 4: 플라이어 — 공중에 떠서 플레이어에게 다가옴.
// 점프로 뛰어올라 밟을 수 있지만 타이밍이 까다롭다. 땅에 서서는 처치 불가.
public class FlyerEnemy : EnemyBase
{
    [Header("비행 설정")]
    public float flyHeight     = 2.5f; // 지면으로부터 유지할 높이
    public float bobAmplitude  = 0.3f; // 위아래로 떠다니는 폭
    public float bobFrequency  = 2f;   // 떠다니는 주기
    public float chaseSpeed    = 3f;   // 수평 추적 속도

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

        // 비행형은 중력의 영향을 받지 않음
        rb.useGravity = false;

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) target = pc.transform;
        baseY = transform.position.y;
    }

    void FixedUpdate()
    {
        if (isDead || target == null) return;

        // 수평 이동: 플레이어 방향으로 추적
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.zero;

        // 수직 위치: 기준 높이에서 사인파로 부유
        float targetY = baseY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;

        Vector3 horizontal = dir * chaseSpeed;
        // y는 직접 제어하여 부유감 연출
        Vector3 newPos = transform.position + new Vector3(horizontal.x, 0, horizontal.z) * Time.fixedDeltaTime;
        newPos.y = Mathf.Lerp(transform.position.y, targetY, Time.fixedDeltaTime * 5f);
        rb.MovePosition(newPos);
    }

    // 지면보다 훨씬 높이 떠 있으므로 baseY도 스폰 시 높게 보정
    public void InitFlyHeight(float groundY)
    {
        baseY = groundY + flyHeight;
        Vector3 p = transform.position;
        p.y = baseY;
        transform.position = p;
    }
}
