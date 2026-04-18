using UnityEngine;

// 타입 3: 점퍼 — 주기적으로 점프하며 다가옴. 공중에 있을 때 밟기 타이밍을 맞춰야 함.
public class JumperEnemy : EnemyBase
{
    [Header("점프 설정")]
    public float jumpInterval = 1.5f; // 점프 주기
    public float jumpForce    = 7f;   // 점프 힘
    public float hopSpeed     = 3f;   // 점프 시 수평 가속
    public LayerMask groundMask = ~0;

    private Transform target;
    private float     timer;

    protected override void Awake()
    {
        base.Awake();
        maxHealth    = 1;
        scoreValue   = 250;
        moveSpeed    = 3f;
        currentHealth = maxHealth;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) target = pc.transform;
    }

    void Update()
    {
        if (isDead || target == null) return;
        timer += Time.deltaTime;
        if (timer >= jumpInterval && IsGrounded())
        {
            timer = 0f;
            Jump();
        }
    }

    // 바닥에 닿았을 때만 점프 가능
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.6f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void Jump()
    {
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.forward;

        // 위로 점프하면서 플레이어 쪽으로 밀어줌
        rb.linearVelocity = new Vector3(dir.x * hopSpeed, 0f, dir.z * hopSpeed);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
}
