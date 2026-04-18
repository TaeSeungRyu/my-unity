using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD(체력/점수)와 게임오버 패널을 관리. UI 오브젝트는 SceneSetup에서 자동 생성.
public class UIManager : MonoBehaviour
{
    [Header("HUD 요소")]
    public TMP_Text   healthText;   // "HP: ♥♥♥"
    public TMP_Text   scoreText;    // "Score: 0"

    [Header("게임오버 패널")]
    public GameObject gameOverPanel;  // 전체 패널 루트
    public TMP_Text   finalScoreText; // "최종 점수: 1234"
    public Button     restartButton;  // 재시작 버튼

    void Start()
    {
        // 초기 UI 상태
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            UpdateHealth(gm.CurrentHealth);
            UpdateScore(gm.Score);

            // 이벤트 구독
            gm.OnHealthChanged += UpdateHealth;
            gm.OnScoreChanged  += UpdateScore;
            gm.OnGameOver      += ShowGameOver;
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null) GameManager.Instance.RestartGame();
            });
        }
    }

    void OnDestroy()
    {
        // 씬 재로드 시 이벤트 누수 방지
        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnHealthChanged -= UpdateHealth;
            gm.OnScoreChanged  -= UpdateScore;
            gm.OnGameOver      -= ShowGameOver;
        }
    }

    private void UpdateHealth(int hp)
    {
        if (healthText == null) return;
        // 하트 이모지 대신 간단한 문자로 표시
        string hearts = "";
        for (int i = 0; i < hp; i++) hearts += "<color=#ff4d4d>♥</color>";
        int max = GameManager.Instance != null ? GameManager.Instance.maxHealth : 3;
        for (int i = hp; i < max; i++) hearts += "<color=#444444>♥</color>";
        healthText.text = $"HP {hearts}";
    }

    private void UpdateScore(int score)
    {
        if (scoreText == null) return;
        scoreText.text = $"Score: {score}";
    }

    private void ShowGameOver(int finalScore)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = $"최종 점수\n{finalScore}";
    }
}
