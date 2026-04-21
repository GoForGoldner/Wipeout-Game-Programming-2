using UnityEngine;

public class PlayerPerkApplier : MonoBehaviour
{
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        ApplyCurrentPerk();
    }

    public void ApplyCurrentPerk()
    {
        if (playerController == null)
            return;

        if (PerkInventory.Instance == null)
        {
            playerController.ResetToBaseStats();
            return;
        }

        playerController.ApplyPerk(PerkInventory.Instance.SelectedPerk);
    }
}