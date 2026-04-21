using System;
using System.Collections.Generic;
using UnityEngine;

public class PerkInventory : MonoBehaviour
{
    public static PerkInventory Instance { get; private set; }

    [SerializeField] private List<PerkData> availablePerks = new List<PerkData>();
    [SerializeField] private PerkData selectedPerk;

    public event Action OnInventoryChanged;
    public event Action<PerkData> OnSelectedPerkChanged;

    public IReadOnlyList<PerkData> AvailablePerks => availablePerks;
    public PerkData SelectedPerk => selectedPerk;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetAvailablePerks(List<PerkData> perks)
    {
        availablePerks = perks;
        OnInventoryChanged?.Invoke();
    }

    public void SelectPerk(PerkData perk)
    {
        if (perk == selectedPerk)
            return;

        selectedPerk = perk;
        OnSelectedPerkChanged?.Invoke(selectedPerk);
    }

    public bool HasSelection()
    {
        return selectedPerk != null;
    }

    public void ClearSelection()
    {
        selectedPerk = null;
        OnSelectedPerkChanged?.Invoke(selectedPerk);
    }
}