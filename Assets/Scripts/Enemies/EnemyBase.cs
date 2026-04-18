using UnityEngine;

// 모든 적의 공통 기반 클래스. 체력, 점수, 스톰프/피해 처리의 공통 로직을 제공합니다.
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("공통 스탯")]
    public int   maxHealth      = 1;    // 처치에 필요한 스톰프 횟수
    public int   scoreValue     = 100;  // 처치 시 획득 점수
    public bool  CanBeStomped   = true; // 위에서 밟기로 처치 가능한지 (비행형은 false 가능)
    public float moveSpeed      = 2f;   // 기본 이동 속도

    [Header("피격 연출")]
    public float hitFlashTime   = 0.1f; // 피격 시 번쩍이는 시간

    protected int       currentHealth;
    protected Rigidbody rb;
    protected Renderer  rend;
    protected Color     originalColor;
    protected bool      isDead;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        // 적은 자식 중 Renderer를 가진 오브젝트를 시각 표현으로 사용
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalColor = rend.material.color;
    }

    // 플레이어가 위에서 밟았을 때 호출
    public virtual void OnStomped()
    {
        if (isDead) return;
        currentHealth--;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // 아직 체력이 남아있으면 피격 연출만
            StartCoroutine(HitFlash());
        }
    }

    // 플레이어가 측면/아래에서 닿았을 때 호출
    public virtual void OnHitPlayer(PlayerController player)
    {
        if (isDead) return;
        if (GameManager.Instance != null) GameManager.Instance.TakeDamage(1);
        // 플레이어를 살짝 밀어내 반복 피격 방지
        Vector3 push = (player.transform.position - transform.position).normalized;
        push.y = 0.5f;
        Rigidbody prb = player.GetComponent<Rigidbody>();
        if (prb != null) prb.AddForce(push * 5f, ForceMode.Impulse);
    }

    protected virtual void Die()
    {
        isDead = true;
        if (GameManager.Instance != null) GameManager.Instance.AddScore(scoreValue);
        Destroy(gameObject);
    }

    // 피격 시 짧게 흰색으로 깜빡이는 연출
    private System.Collections.IEnumerator HitFlash()
    {
        if (rend == null) yield break;
        rend.material.color = Color.white;
        yield return new WaitForSeconds(hitFlashTime);
        if (rend != null) rend.material.color = originalColor;
    }
}
