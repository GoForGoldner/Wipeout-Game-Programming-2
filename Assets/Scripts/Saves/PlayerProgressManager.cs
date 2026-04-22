using System;
using System.IO;
using UnityEngine;

public class PlayerProgressManager : MonoBehaviour
{
    public static PlayerProgressManager Instance { get; private set; }

    [SerializeField] private string fileName = "player_progress.json";

    public PlayerProgressData Data { get; private set; }

    public event Action OnProgressUpdated;

    string FilePath => Path.Combine(Application.persistentDataPath, fileName);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadProgress();
    }

    void Update()
    {
        if (Data != null)
        {
            Data.totalPlayTimeSeconds += Time.unscaledDeltaTime;
        }
    }

    void OnApplicationQuit()
    {
        SaveProgress();
    }

    // Important for WebGL / mobile — OnApplicationQuit isn't reliable there
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveProgress();
    }

    public void LoadProgress()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                Data = JsonUtility.FromJson<PlayerProgressData>(json);
                if (Data == null) Data = new PlayerProgressData();
                Debug.Log($"[Progress] Loaded: {Data.totalWins} wins, {Data.totalPlayTimeSeconds:F0}s played");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Progress] Failed to load, starting fresh. {e.Message}");
                Data = new PlayerProgressData();
            }
        }
        else
        {
            Data = new PlayerProgressData();
            Debug.Log("[Progress] No save file found, created new progress.");
        }

        OnProgressUpdated?.Invoke();
    }

    public void SaveProgress()
    {
        if (Data == null) return;

        try
        {
            Data.lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Progress] Save failed: {e.Message}");
        }
    }

    public void AddWin()
    {
        Data.totalWins++;
        SaveProgress();
        OnProgressUpdated?.Invoke();
    }

    public void RecordLevelCompletion(string levelName, float completionTime)
    {
        LevelProgress lvl = Data.levels.Find(l => l.levelName == levelName);
        if (lvl == null)
        {
            lvl = new LevelProgress
            {
                levelName = levelName,
                completions = 1,
                bestTimeSeconds = completionTime
            };
            Data.levels.Add(lvl);
        }
        else
        {
            lvl.completions++;
            if (lvl.bestTimeSeconds < 0 || completionTime < lvl.bestTimeSeconds)
                lvl.bestTimeSeconds = completionTime;
        }
        SaveProgress();
        OnProgressUpdated?.Invoke();
    }

    public void SetMouseSensitivity(float value)
    {
        Data.settings.mouseSensitivity = value;
        SaveProgress();
        OnProgressUpdated?.Invoke();
    }

    public void ResetProgress()
    {
        Data = new PlayerProgressData();
        SaveProgress();
        OnProgressUpdated?.Invoke();
    }
}
