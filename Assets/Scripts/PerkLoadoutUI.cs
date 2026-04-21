using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PerkLoadoutUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private PerkSlotUI slotPrefab;

    [Header("Detail Panel")]
    [SerializeField] private TMP_Text perkNameText;
    [SerializeField] private TMP_Text perkDescriptionText;
    [SerializeField] private TMP_Text perkBonusText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;

    private readonly List<PerkSlotUI> spawnedSlots = new List<PerkSlotUI>();
    private PerkData currentlySelectedPerk;

    private void Start()
    {
        BuildSlots();
        RefreshFromInventory();

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(ConfirmSelection);
            confirmButton.interactable = PerkInventory.Instance != null && PerkInventory.Instance.HasSelection();
        }

        if (PerkInventory.Instance != null)
        {
            PerkInventory.Instance.OnSelectedPerkChanged += HandleSelectedPerkChanged;
        }
    }

    private void OnDestroy()
    {
        if (PerkInventory.Instance != null)
        {
            PerkInventory.Instance.OnSelectedPerkChanged -= HandleSelectedPerkChanged;
        }
    }

    private void BuildSlots()
    {
        if (PerkInventory.Instance == null || slotPrefab == null || slotContainer == null)
            return;

        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }

        spawnedSlots.Clear();

        foreach (PerkData perk in PerkInventory.Instance.AvailablePerks)
        {
            PerkSlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.Setup(perk, this);
            spawnedSlots.Add(slot);
        }
    }

    private void RefreshFromInventory()
    {
        if (PerkInventory.Instance == null)
            return;

        currentlySelectedPerk = PerkInventory.Instance.SelectedPerk;
        UpdateDetailPanel();
        UpdateSlotHighlights();
    }

    public void SelectPerk(PerkData perk)
    {
        if (PerkInventory.Instance == null || perk == null)
            return;

        PerkInventory.Instance.SelectPerk(perk);
    }

    private void HandleSelectedPerkChanged(PerkData perk)
    {
        currentlySelectedPerk = perk;
        UpdateDetailPanel();
        UpdateSlotHighlights();

        if (confirmButton != null)
            confirmButton.interactable = currentlySelectedPerk != null;
    }

    private void UpdateDetailPanel()
    {
        if (currentlySelectedPerk == null)
        {
            if (perkNameText != null)
                perkNameText.text = "No Perk Selected";

            if (perkDescriptionText != null)
                perkDescriptionText.text = "Choose one perk before the round begins.";

            if (perkBonusText != null)
                perkBonusText.text = "";

            return;
        }

        if (perkNameText != null)
            perkNameText.text = currentlySelectedPerk.perkName;

        if (perkDescriptionText != null)
            perkDescriptionText.text = currentlySelectedPerk.description;

        if (perkBonusText != null)
            perkBonusText.text = BuildBonusText(currentlySelectedPerk);
    }

    private string BuildBonusText(PerkData perk)
    {
        List<string> lines = new List<string>();

        if (perk.speedBonus != 0f)
            lines.Add($"+ Speed: {perk.speedBonus}");

        if (perk.jumpBonus != 0f)
            lines.Add($"+ Jump: {perk.jumpBonus}");

        if (perk.diveBonus != 0f)
            lines.Add($"+ Dive: {perk.diveBonus}");

        if (perk.antiGravityJumpBonus != 0f)
            lines.Add($"+ Anti-Gravity Jump: {perk.antiGravityJumpBonus}");

        if (perk.antiGravityGravityBonus != 0f)
            lines.Add($"+ Anti-Gravity Control: {perk.antiGravityGravityBonus}");

        return string.Join("\n", lines);
    }

    private void UpdateSlotHighlights()
    {
        foreach (PerkSlotUI slot in spawnedSlots)
        {
            bool isSelected = slot.GetPerkData() == currentlySelectedPerk;
            slot.SetHighlighted(isSelected);
        }
    }

    private void ConfirmSelection()
    {
        Debug.Log("Perk confirmed: " + (currentlySelectedPerk != null ? currentlySelectedPerk.perkName : "None"));

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        RefreshFromInventory();
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}