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

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private bool isGrounded;        // 현재 프레임의 지면 접촉 여부
    private bool groundedThisFrame; // OnCollisionStay에서 매 프레임 갱신
    private int jumpsUsed;          // 현재 공중에서 사용한 점프 횟수(착지 시 0으로 리셋)
    private Camera cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        // 캐릭터가 넘어지지 않도록 X,Z 축 회전을 고정
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        cam = Camera.main;
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
        isGrounded = groundedThisFrame;
        groundedThisFrame = false;

        // 착지하면 점프 카운터 리셋 (2단 점프 사용 가능 상태로 복귀)
        if (isGrounded) jumpsUsed = 0;

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }
        HandleMovement();
    }

    // 적이 아닌 오브젝트와의 접촉 중 법선이 위를 향하면 지면으로 간주
    private void OnCollisionStay(Collision collision)
    {
        // 적과의 접촉은 지면으로 치지 않음(스톰프 연속 점프는 별도 로직에서 처리)
        if (collision.collider.GetComponentInParent<EnemyBase>() != null) return;

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

    // 적과의 충돌: 위에서 밟았으면 스톰프, 아니면 피해
    private void OnCollisionEnter(Collision collision)
    {
        EnemyBase enemy = collision.collider.GetComponentInParent<EnemyBase>();
        if (enemy == null) return;

        // 접촉점의 평균 법선으로 "위에서 밟았는지" 판정
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++) normal += collision.GetContact(i).normal;
        normal.Normalize();

        // 법선이 위쪽을 향하고(우리가 위에 있고), 플레이어가 아래로 떨어지는 중이며, 날 수 없는 적일 때만 스톰프 성공
        bool comingDown = rb.linearVelocity.y <= 0.1f;
        bool fromAbove  = normal.y > 0.5f;

        if (fromAbove && comingDown && enemy.CanBeStomped)
        {
            enemy.OnStomped();
            // 플레이어를 살짝 튕겨 올려 연속 점프가 가능하도록
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
            rb.AddForce(Vector3.up * stompBounceForce, ForceMode.Impulse);
        }
        else
        {
            // 측면/아래에서 부딪힌 경우 플레이어가 피해를 입음
            enemy.OnHitPlayer(this);
        }
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
