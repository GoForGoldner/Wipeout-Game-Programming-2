using Unity.Netcode;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("FinishLine triggered by: " + other.gameObject.name
            + " at position: " + other.transform.position
            + " | FinishLine position: " + transform.position);

        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj == null) return;
        if (!netObj.IsOwner) return;

        GameManager.Instance?.ReportFinishServerRpc();
    }
}