using UnityEngine;
using Unity.Netcode;
public class JoinUIManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!NetworkManager.Singleton)
        {
            return;
        }
    }
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        HideUI();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        HideUI();
    }

    public void HideUI()
    {
        gameObject.SetActive(false);
    }
}
