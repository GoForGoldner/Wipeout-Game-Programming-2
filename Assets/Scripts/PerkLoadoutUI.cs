using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

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

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.State.OnValueChanged += OnMatchStateChanged;
            OnMatchStateChanged(MatchManager.Instance.State.Value, MatchManager.Instance.State.Value);
        }
    }

    private void UnlockCursorForUi()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursorForGameplay()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnMatchStateChanged(MatchState oldState, MatchState newState)
    {
        if (newState == MatchState.PerkSelection)
        {
            ulong localId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;

            // After Round 1 has started, eliminated players should not see or use perk selection.
            if (MatchManager.Instance != null &&
                MatchManager.Instance.PlayersInLevel.Value > 0 &&
                MatchManager.Instance.IsEliminated(localId))
            {
                HidePanel();
                return;
            }

            UnlockCursorForUi();

            if (confirmButton != null)
                confirmButton.interactable = PerkInventory.Instance != null && PerkInventory.Instance.HasSelection();

            // Restore previous selection if one exists for this client
            if (MatchManager.Instance != null && PerkInventory.Instance != null)
            {
                int perkIndex = MatchManager.Instance.GetLocalPerkIndex();
                if (perkIndex >= 0 && perkIndex < PerkInventory.Instance.AvailablePerks.Count)
                {
                    PerkInventory.Instance.SelectPerk((PerkData)PerkInventory.Instance.AvailablePerks[perkIndex]);
                }
            }

            ShowPanel();
        }
        else
        {
            HidePanel();

            if (newState == MatchState.Playing)
            {
                LockCursorForGameplay();
            }
        }
    }

    private void OnDestroy()
    {
        if (PerkInventory.Instance != null)
        {
            PerkInventory.Instance.OnSelectedPerkChanged -= HandleSelectedPerkChanged;
        }

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.State.OnValueChanged -= OnMatchStateChanged;
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

    private int GetSelectedPerkIndex()
    {
        if (PerkInventory.Instance == null || currentlySelectedPerk == null)
            return -1;

        var perks = PerkInventory.Instance.AvailablePerks;
        for (int i = 0; i < perks.Count; i++)
        {
            if (perks[i] == currentlySelectedPerk)
                return i;
        }

        return -1;
    }

    private void ConfirmSelection()
    {
        int selectedIndex = GetSelectedPerkIndex();
        if (selectedIndex < 0)
            return;

        Debug.Log("Perk confirmed: " + currentlySelectedPerk.perkName);

        if (MatchManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            MatchManager.Instance.SubmitPerkSelectionServerRpc(selectedIndex);
        }

        if (confirmButton != null)
            confirmButton.interactable = false;

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