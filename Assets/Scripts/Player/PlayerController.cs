using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 캐릭터 조작: 이동, 점프, 적 밟기 판정까지 담당합니다.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 6f;          // 수평 이동 속도
    public float jumpForce = 8f;          // 점프 힘
    public float doubleJumpMultiplier = 1.25f; // 2단 점프 힘 배수 (기존 점프 x 1.25)
    public int maxJumps = 2;              // 착지 전까지 가능한 최대 점프 횟수
    public float rotationSpeed = 12f;     // 이동 방향으로 회전하는 속도

    [Header("스톰프(밟기) 판정")]
    public float stompBounceForce = 6f;       // 적을 밟은 뒤 튕겨 오를 힘
    public float stompMinFallSpeed = 0.05f;   // 이 이상으로 아래로 떨어지고 있어야 스톰프 인정 (양수값) — 작을수록 타이밍 관대
    public float stompAboveMargin  = 0.35f;   // 플레이어 발바닥이 적 상단 아래로 이만큼까지 내려가도 스톰프 인정 — 클수록 위치 관대
    public float stompAssistRadius = 0.6f;    // 발 아래 이 반경 안에 스톰프 가능 적이 있으면 콜라이더 접촉 없이도 스톰프 인정 — 0 이하면 어시스트 비활성

    [Header("피격 쿨다운")]
    public float damageCooldown = 0.8f;       // 측면 접촉 연속 피해 방지

    [Header("타격감 (스톰프 시)")]
    public float hitStopDuration  = 0.06f;    // 히트 스톱 길이(초, 비스케일)
    public float hitStopTimeScale = 0.05f;    // 히트 스톱 동안의 timeScale
    public float shakeDuration    = 0.18f;    // 카메라 흔들림 길이(초)
    public float shakeMagnitude   = 0.25f;    // 카메라 흔들림 강도(콤보가 늘면 약간 강해짐)

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private bool isGrounded;        // 현재 프레임의 지면 접촉 여부
    private bool groundedThisFrame; // OnCollisionStay에서 매 프레임 갱신
    private int jumpsUsed;          // 현재 공중에서 사용한 점프 횟수(착지 시 0으로 리셋)
    private Camera cam;
    private CameraFollow camFollow;
    private float lastDamageTime = -999f;
    private static readonly Collider[] stompAssistBuffer = new Collider[16];
    private static int hitStopDepth; // 동시 히트 스톱이 겹쳐도 정확히 복원되도록 카운트

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        // 캐릭터가 넘어지지 않도록 X,Z 축 회전을 고정
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        cam = Camera.main;
        if (cam != null) camFollow = cam.GetComponent<CameraFollow>();
        // 씬 재로드 후 정적 카운터가 남아 있을 수 있어 초기화
        hitStopDepth = 0;
    }

    void Update()
    {
        // 게임 오버 상태면 입력 무시
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        HandleJump();
    }

    void FixedUpdate()
    {
        // 매 FixedUpdate 시작 시 접지 플래그를 확정하고 다음 프레임을 위해 리셋
        bool wasGrounded = isGrounded;
        isGrounded = groundedThisFrame;
        groundedThisFrame = false;

        // 착지 순간(공중 → 지면): 콤보 종료
        if (isGrounded && !wasGrounded)
        {
            if (GameManager.Instance != null) GameManager.Instance.ResetCombo();
        }

        // 착지하면 점프 카운터 리셋 (2단 점프 사용 가능 상태로 복귀)
        if (isGrounded) jumpsUsed = 0;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }
        HandleMovement();
        TryStompAssist();
    }

    // 지면/적 접촉 공통 처리. 적이면 스톰프/피해 판정, 아니면 접지 여부 갱신.
    private void OnCollisionStay(Collision collision)
    {
        EnemyBase enemy = collision.collider.GetComponentInParent<EnemyBase>();
        if (enemy != null)
        {
            HandleEnemyContact(collision, enemy);
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                groundedThisFrame = true;
                return;
            }
        }
    }

    // 새 Input System을 직접 폴링해 WASD/방향키 입력을 읽어 이동
    private void HandleMovement()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;

        // 카메라 기준 상대 이동 (카메라 전방 기준으로 W가 앞)
        Vector3 camF = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 camR = cam != null ? Vector3.ProjectOnPlane(cam.transform.right,   Vector3.up).normalized : Vector3.right;
        Vector3 input = (camF * v + camR * h);
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 targetVel = input * moveSpeed;
        // y 속도(중력/점프)는 보존하고 수평 속도만 교체
        rb.linearVelocity = new Vector3(targetVel.x, rb.linearVelocity.y, targetVel.z);

        // 이동 방향을 바라보도록 부드럽게 회전
        if (input.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (!kb.spaceKey.wasPressedThisFrame) return;

        // 착지 중에 누르면 jumpsUsed=0 → 1번째 점프 소모. 공중에서 다시 누르면 2번째.
        // 절벽에서 떨어지며 처음 누르면 1번째 점프로 소비되어 총 2회 사용 가능.
        if (jumpsUsed >= maxJumps) return;

        // 2번째 점프부터는 기존 점프 힘의 doubleJumpMultiplier(1.25)배
        float force = jumpsUsed == 0 ? jumpForce : jumpForce * doubleJumpMultiplier;

        // 기존 수직 속도를 0으로 리셋해 일정한 점프 높이 보장 (낙하 중 2단 점프도 깔끔하게)
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        jumpsUsed++;
    }

    // 적과의 충돌: 위에서 떨어지며 밟았으면 스톰프, 아니면 피해
    private void OnCollisionEnter(Collision collision)
    {
        EnemyBase enemy = collision.collider.GetComponentInParent<EnemyBase>();
        if (enemy != null) HandleEnemyContact(collision, enemy);
    }

    private void HandleEnemyContact(Collision collision, EnemyBase enemy)
    {
        if (enemy.IsDead) return;

        // 스톰프 조건: (1) 플레이어 발바닥이 적의 상단보다 위에 있고
        //             (2) 실제로 아래로 낙하 중이며
        //             (3) 적이 스톰프 가능할 때
        float playerBottomY = transform.position.y + capsule.center.y - capsule.height * 0.5f;
        float enemyTopY     = collision.collider.bounds.max.y;
        bool aboveEnemy = playerBottomY > enemyTopY - stompAboveMargin;
        bool falling    = rb.linearVelocity.y < -stompMinFallSpeed;

        if (aboveEnemy && falling && enemy.CanBeStomped)
        {
            DoStomp(enemy);
            return;
        }

        // 측면/아래 접촉: 쿨다운 기반으로 피해
        if (Time.time - lastDamageTime < damageCooldown) return;
        lastDamageTime = Time.time;
        enemy.OnHitPlayer(this);
    }

    // 실제 스톰프 처리 + 반동 점프. 직접 접촉(HandleEnemyContact)과 근접 어시스트(TryStompAssist) 모두가 호출.
    private void DoStomp(EnemyBase enemy)
    {
        // 콤보 먼저 증가시킨 뒤 OnStomped를 호출 — Die() 안의 AddScore가 새 콤보 배수를 받도록.
        if (GameManager.Instance != null) GameManager.Instance.IncrementCombo();

        enemy.OnStomped();

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
        rb.AddForce(Vector3.up * stompBounceForce, ForceMode.Impulse);

        // 타격감: 짧은 히트 스톱 + 카메라 흔들림 (콤보가 늘면 살짝 강해짐)
        int combo = GameManager.Instance != null ? GameManager.Instance.Combo : 1;
        float magBoost = 1f + 0.15f * Mathf.Min(combo - 1, 6); // 7콤보 이상 캡
        if (camFollow != null) camFollow.Shake(shakeDuration, shakeMagnitude * magBoost);
        StartCoroutine(HitStop(hitStopDuration, hitStopTimeScale));
    }

    // Time.timeScale을 잠깐 낮춰 강한 임팩트 느낌을 만든다. 코루틴이 겹쳐도 깊이 카운트로 복원.
    private IEnumerator HitStop(float duration, float scale)
    {
        if (hitStopDepth == 0) Time.timeScale = scale;
        hitStopDepth++;
        yield return new WaitForSecondsRealtime(duration);
        hitStopDepth--;
        if (hitStopDepth <= 0)
        {
            hitStopDepth = 0;
            // 게임 오버 중에는 GameManager가 timeScale=0을 잡고 있으므로 건드리지 않음
            if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
                Time.timeScale = 1f;
        }
    }

    // 낙하 중 발 아래 반경 내에 스톰프 가능 적이 있으면 콜라이더 접촉 없이도 스톰프 인정.
    // 수평 정렬이 조금 어긋나 실제 캡슐끼리 스치지 않는 상황을 구제하기 위한 보조 판정.
    private void TryStompAssist()
    {
        if (stompAssistRadius <= 0f) return;
        if (isGrounded) return;
        if (rb.linearVelocity.y >= -stompMinFallSpeed) return;

        float playerBottomY = transform.position.y + capsule.center.y - capsule.height * 0.5f;
        Vector3 center = new Vector3(transform.position.x, playerBottomY, transform.position.z);

        int count = Physics.OverlapSphereNonAlloc(center, stompAssistRadius, stompAssistBuffer, ~0, QueryTriggerInteraction.Ignore);
        EnemyBase bestEnemy = null;
        float bestDrop = float.PositiveInfinity; // 발바닥과 적 상단 높이 차가 가장 작은(=가장 근접한) 적 선택

        for (int i = 0; i < count; i++)
        {
            Collider col = stompAssistBuffer[i];
            if (col == null || col.attachedRigidbody == rb) continue;

            EnemyBase enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead || !enemy.CanBeStomped) continue;

            float enemyTopY = col.bounds.max.y;
            // 발이 적 상단 위~stompAboveMargin 아래 구간일 때만 인정 (측면 접촉 구제는 금지)
            if (playerBottomY < enemyTopY - stompAboveMargin) continue;

            float drop = Mathf.Abs(playerBottomY - enemyTopY);
            if (drop < bestDrop)
            {
                bestDrop = drop;
                bestEnemy = enemy;
            }
        }

        if (bestEnemy != null) DoStomp(bestEnemy);
    }

    // 플레이어 위치 초기화(게임 재시작 시 사용)
    public void ResetPlayer(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = position;
        transform.rotation = Quaternion.identity;
    }
}
