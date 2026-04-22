using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgressData
{
    public int totalWins;
    public float totalPlayTimeSeconds;
    public string lastPlayedDate;

    public List<LevelProgress> levels = new List<LevelProgress>();

    public GameSettings settings = new GameSettings();
}

[Serializable]
public class LevelProgress
{
    public string levelName;
    public int completions;
    public float bestTimeSeconds = -1f; // -1 = not yet completed
}

[Serializable]
public class GameSettings
{
    public float mouseSensitivity = 1.0f;
    public float musicVolume = 0.7f;
    public float sfxVolume = 1.0f;
}
