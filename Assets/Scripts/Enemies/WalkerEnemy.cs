using UnityEngine;

// 타입 1: 워커 — 스폰 지점 주변을 좌우로 순찰. 가장 약하고 점수도 낮음.
public class WalkerEnemy : EnemyBase
{
    [Header("순찰 설정")]
    public float patrolRange   = 5f;   // 스폰 지점에서 좌우로 이동할 최대 거리
    public float turnInterval  = 2f;   // 방향 전환 주기

    private Vector3 origin;
    private Vector3 direction;
    private float   turnTimer;

    protected override void Awake()
    {
        base.Awake();
        maxHealth    = 1;
        scoreValue   = 100;
        moveSpeed    = 2.5f;
        currentHealth = maxHealth;
        origin    = transform.position;
        // 무작위 초기 방향(좌/우)
        direction = Random.value > 0.5f ? Vector3.right : Vector3.left;
    }

    void FixedUpdate()
    {
        if (isDead) return;

        turnTimer += Time.fixedDeltaTime;
        // 주기마다 방향 반전하거나, 순찰 범위를 넘기면 복귀 방향으로 반전
        if (turnTimer >= turnInterval || Vector3.Distance(transform.position, origin) > patrolRange)
        {
            direction = -direction;
            turnTimer = 0f;
        }

        Vector3 vel = direction * moveSpeed;
        rb.linearVelocity = new Vector3(vel.x, rb.linearVelocity.y, vel.z);
    }
}
