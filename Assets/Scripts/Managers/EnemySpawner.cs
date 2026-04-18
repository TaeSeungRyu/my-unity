using System.Collections.Generic;
using UnityEngine;

// 여러 유형의 적을 주기적으로 스폰해 플레이어 쪽으로 몰려오게 한다.
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject prefab; // 스폰할 적 프리팹(또는 런타임 생성된 모델)
        [Range(0f, 10f)] public float weight = 1f; // 스폰 확률 가중치
    }

    [Header("스폰 대상")]
    public List<SpawnEntry> entries = new List<SpawnEntry>();

    [Header("스폰 주기")]
    public float startInterval = 2.5f; // 초기 간격
    public float minInterval   = 0.6f; // 최소 간격(난이도 상승 후)
    public float rampPerSecond = 0.01f; // 초당 감소량

    [Header("스폰 위치")]
    public Transform player;             // 플레이어 참조(주변에 스폰)
    public float spawnRadius   = 18f;    // 플레이어에서 얼마나 떨어진 곳에
    public float minSpawnRadius = 12f;
    public float groundY       = 0.5f;   // 지상형 적의 스폰 Y
    public int   maxAlive      = 25;     // 동시 최대 생존 수

    private float timer;
    private float currentInterval;
    private readonly List<GameObject> alive = new List<GameObject>();

    void Start()
    {
        currentInterval = startInterval;
        if (player == null)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (entries == null || entries.Count == 0) return;

        // 시간이 지날수록 스폰 간격이 짧아져 난이도 상승
        currentInterval = Mathf.Max(minInterval, currentInterval - rampPerSecond * Time.deltaTime);

        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            timer = 0f;
            CleanupDead();
            if (alive.Count < maxAlive) SpawnOne();
        }
    }

    private void CleanupDead()
    {
        alive.RemoveAll(e => e == null);
    }

    private void SpawnOne()
    {
        GameObject prefab = PickWeighted();
        if (prefab == null || player == null) return;

        // 플레이어 주변 랜덤 링에서 위치 결정
        Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, spawnRadius);
        Vector3 pos = player.position + new Vector3(circle.x, 0f, circle.y);
        pos.y = groundY;

        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.SetActive(true);

        // 비행형이면 공중 높이로 보정
        FlyerEnemy flyer = go.GetComponent<FlyerEnemy>();
        if (flyer != null) flyer.InitFlyHeight(groundY);

        alive.Add(go);
    }

    // 가중치 기반으로 entries 중 하나를 선택
    private GameObject PickWeighted()
    {
        float total = 0f;
        foreach (var e in entries) if (e != null && e.prefab != null) total += Mathf.Max(0f, e.weight);
        if (total <= 0f) return null;

        float r = Random.value * total;
        float acc = 0f;
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;
            acc += Mathf.Max(0f, e.weight);
            if (r <= acc) return e.prefab;
        }
        return entries[entries.Count - 1].prefab;
    }
}
