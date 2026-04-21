using UnityEngine;

[CreateAssetMenu(fileName = "NewPerk", menuName = "Wipeout/Perk Data")]
public class PerkData : ScriptableObject
{
    [Header("Display")]
    public string perkId;
    public string perkName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Bonuses")]
    public float speedBonus;
    public float jumpBonus;
    public float diveBonus;

    [Header("Anti-Gravity Bonuses")]
    public float antiGravityJumpBonus;
    public float antiGravityGravityBonus;
}