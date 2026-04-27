using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 상태 총괄: 체력, 점수, 게임오버/재시작 로직을 관리하는 싱글톤.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("게임 설정")]
    public int maxHealth = 3;

    public int  CurrentHealth { get; private set; }
    public int  Score         { get; private set; }
    public int  Combo         { get; private set; } // 착지 전까지 누적된 연속 스톰프 횟수
    public bool IsGameOver    { get; private set; }

    // UI가 구독할 이벤트
    public event Action<int> OnHealthChanged;
    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged; // 콤보 카운터 변경
    public event Action<int> OnGameOver;     // 최종 점수 전달

    void Awake()
    {
        // 단순 싱글톤 (씬 전환 없이 한 씬에서 동작)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ResetState();
    }

    private void ResetState()
    {
        CurrentHealth = maxHealth;
        Score         = 0;
        Combo         = 0;
        IsGameOver    = false;
    }

    // 적에게 피격 당했을 때 호출
    public void TakeDamage(int amount)
    {
        if (IsGameOver) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth);

        // 피격 시 콤보 끊김
        ResetCombo();

        if (CurrentHealth <= 0) TriggerGameOver();
    }

    // 적 처치 시 점수 가산. 현재 콤보 배수가 곱해진다(콤보 0이면 1배).
    public void AddScore(int amount)
    {
        if (IsGameOver) return;
        int multiplier = Mathf.Max(1, Combo);
        Score += amount * multiplier;
        OnScoreChanged?.Invoke(Score);
    }

    // 스톰프 한 번이 성공할 때마다 호출 — 처치 여부와 무관하게 누적.
    public void IncrementCombo()
    {
        if (IsGameOver) return;
        Combo++;
        OnComboChanged?.Invoke(Combo);
    }

    // 착지/피격 시 호출
    public void ResetCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        OnComboChanged?.Invoke(0);
    }

    private void TriggerGameOver()
    {
        IsGameOver = true;
        // 시간을 멈춰 모든 물리/적 동작 정지
        Time.timeScale = 0f;
        OnGameOver?.Invoke(Score);
    }

    // UI의 재시작 버튼에서 호출
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
