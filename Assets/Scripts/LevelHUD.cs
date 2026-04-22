using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LevelHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("If null, will be auto-found in the scene.")]
    public GameManager gameManager;

    [Header("UI Text")]
    public TMP_Text levelText;       // "Level 1 / 3"
    public TMP_Text qualifierText;   // "Qualified: 2/4"

    void Start()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
            gameManager.SubscribeToHUD(this);

        Refresh();
    }

    void OnDestroy()
    {
        if (gameManager != null)
            gameManager.UnsubscribeFromHUD(this);
    }

    public void Refresh()
    {
        if (MatchManager.Instance == null || gameManager == null) return;

        int levelNum = MatchManager.Instance.CurrentLevelIndex.Value + 1;
        int totalLevels = 3;

        if (levelText)
            levelText.text = $"Level {levelNum} / {totalLevels}";

        int qualifierGoal = MatchManager.Instance.GetQualifierCount();
        int qualifiedSoFar = gameManager.GetFinishCount();

        if (qualifierText)
        {
            // On the final level, frame it as "First to finish wins!"
            if (MatchManager.Instance.IsFinalLevel)
                qualifierText.text = "First to finish wins!";
            else
                qualifierText.text = $"Qualified: {qualifiedSoFar}/{qualifierGoal}";
        }
    }
}
