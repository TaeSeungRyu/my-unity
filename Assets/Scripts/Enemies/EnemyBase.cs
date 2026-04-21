using System.Collections;
using UnityEngine;

// 모든 적의 공통 기반 클래스. 체력, 점수, 스톰프/피해 처리를 담당.
// 적은 여러 개의 Renderer(몸통/눈/뿔 등)를 가질 수 있으므로 배열로 관리.
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("공통 스탯")]
    public int   maxHealth    = 1;    // 처치에 필요한 스톰프 횟수
    public int   scoreValue   = 100;  // 처치 시 획득 점수
    public bool  CanBeStomped = true; // 위에서 밟아 처치 가능한지
    public float moveSpeed    = 2f;   // 기본 이동 속도

    [Header("피격 연출")]
    public float hitFlashTime = 0.1f; // 피격 시 번쩍이는 시간

    [Header("착지 표시(스톰프 타겟) 색상")]
    public Color stompHighlightColor = new Color(1f, 0.95f, 0.3f); // 플레이어 착지점에 들어왔을 때 표시할 색

    protected int       currentHealth;
    protected Rigidbody rb;
    protected Renderer[] rends;       // 본인 + 모든 자식의 렌더러
    protected Color[]    originalColors;
    protected bool       isDead;
    protected bool       isStompHighlighted;

    public bool IsDead => isDead;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();

        // 복합 모양이라 렌더러가 여러 개일 수 있음 — 모두 캐시
        rends = GetComponentsInChildren<Renderer>();
        originalColors = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            // sharedMaterial에서 색을 읽되(원본 보존), 쓰기는 material로 인스턴스화해 개별 제어
            originalColors[i] = ReadMaterialColor(rends[i].sharedMaterial);
        }
    }

    // URP Lit은 _BaseColor, BiRP Standard/Unlit은 _Color 로 색을 가진다.
    // 둘 다 체크해서 실제 존재하는 속성을 읽는다.
    private static Color ReadMaterialColor(Material m)
    {
        if (m == null) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color"))     return m.GetColor("_Color");
        return Color.white;
    }

    // 양쪽 속성 모두 세팅 — 셰이더에 따라 하나만 실제로 쓰인다.
    private static void ApplyMaterialColor(Renderer r, Color c)
    {
        if (r == null) return;
        Material m = r.material; // 인스턴스화됨
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }

    // 플레이어가 위에서 밟았을 때 호출
    public virtual void OnStomped()
    {
        if (isDead) return;
        currentHealth--;
        if (currentHealth <= 0) Die();
        else                    StartCoroutine(HitFlash());
    }

    // 플레이어가 측면/아래에서 닿았을 때 호출
    public virtual void OnHitPlayer(PlayerController player)
    {
        if (isDead) return;
        if (GameManager.Instance != null) GameManager.Instance.TakeDamage(1);
        // 반복 피격 방지를 위해 플레이어를 반대 방향으로 살짝 밀어냄
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

    // 착지 예상 지점 내에 들어오면 켜지는 하이라이트. LandingIndicator가 호출.
    public void SetStompHighlight(bool on)
    {
        if (on == isStompHighlighted) return;
        isStompHighlighted = on;
        if (rends == null) return;

        for (int i = 0; i < rends.Length; i++)
            ApplyMaterialColor(rends[i], on ? stompHighlightColor : originalColors[i]);
    }

    // 모든 자식 렌더러를 흰색으로 번쩍였다가 원래 색으로 복원
    private IEnumerator HitFlash()
    {
        if (rends == null) yield break;

        for (int i = 0; i < rends.Length; i++)
            ApplyMaterialColor(rends[i], Color.white);

        yield return new WaitForSeconds(hitFlashTime);

        // 하이라이트 중이면 원색이 아닌 하이라이트 색으로 복원
        for (int i = 0; i < rends.Length; i++)
            ApplyMaterialColor(rends[i], isStompHighlighted ? stompHighlightColor : originalColors[i]);
    }
}
