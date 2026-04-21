using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PerkSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image highlightImage;

    private PerkData perkData;
    private PerkLoadoutUI loadoutUI;

    public void Setup(PerkData perk, PerkLoadoutUI parentUI)
    {
        perkData = perk;
        loadoutUI = parentUI;

        if (nameText != null)
            nameText.text = perk.perkName;

        if (iconImage != null)
        {
            iconImage.sprite = perk.icon;
            iconImage.enabled = perk.icon != null;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClicked);

        SetHighlighted(false);
    }

    private void OnClicked()
    {
        loadoutUI.SelectPerk(perkData);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (highlightImage != null)
            highlightImage.enabled = isHighlighted;
    }

    public PerkData GetPerkData()
    {
        return perkData;
    }
}