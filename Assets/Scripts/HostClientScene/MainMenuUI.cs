using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Stats Display")]
    [SerializeField] private TMP_Text winsText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text lastPlayedText;

    [Header("Settings")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueLabel;

    void OnEnable()
    {
        if (PlayerProgressManager.Instance != null)
        {
            PlayerProgressManager.Instance.OnProgressUpdated += RefreshUI;
            RefreshUI();
        }

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
    }

    void OnDisable()
    {
        if (PlayerProgressManager.Instance != null)
            PlayerProgressManager.Instance.OnProgressUpdated -= RefreshUI;

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
    }

    void RefreshUI()
    {
        var data = PlayerProgressManager.Instance?.Data;
        if (data == null) return;

        if (winsText) winsText.text = $"Wins: {data.totalWins}";
        if (playTimeText) playTimeText.text = $"Play Time: {FormatTime(data.totalPlayTimeSeconds)}";
        if (lastPlayedText) lastPlayedText.text = string.IsNullOrEmpty(data.lastPlayedDate)
            ? "Last Played: —"
            : $"Last Played: {data.lastPlayedDate}";

        if (mouseSensitivitySlider)
            mouseSensitivitySlider.SetValueWithoutNotify(data.settings.mouseSensitivity);

        if (sensitivityValueLabel)
            sensitivityValueLabel.text = data.settings.mouseSensitivity.ToString("F2");
    }

    string FormatTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);
        return h > 0 ? $"{h:D2}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
    }

    public void OnResetProgressClicked()
    {
        PlayerProgressManager.Instance?.ResetProgress();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnMouseSensitivityChanged(float value)
    {
        PlayerProgressManager.Instance?.SetMouseSensitivity(value);
    }
}
