using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class StartMatchUIManager : MonoBehaviour
{
    public static StartMatchUIManager Instance { get; private set; }
    public string targetScene;
    public MatchManager matchManager;
    public PlayerSpawner playerSpawner;
    public Button hostButton;
    public Button clientButton;

    public TMP_InputField clientCodeTxt;
    RelayConnectionManager relayManager;
    string clientJoinCode;
    void Start()
    {
        if(!NetworkManager.Singleton)
            return;

        matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager == null) return;

        playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        if (playerSpawner == null) return;

        relayManager = FindFirstObjectByType<RelayConnectionManager>();
        if (relayManager == null) return;
        
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);

        matchManager.ActivateJoining();

    }

    void OnHostButtonClicked()
    {
        // StartHost();
        StartHostRelay();
    }

    void OnClientButtonClicked()
    {
        // StartClient();
        clientJoinCode = clientCodeTxt.text;
        if(!string.IsNullOrEmpty(clientJoinCode))
            StartClientRelay(clientJoinCode);
        else 
            clientCodeTxt.text = "Enter Code First";
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        playerSpawner.SubscribeSceneManager();

        if(targetScene != null)
        {
            matchManager.ActivateLobby("");
            SceneLoader.LoadNetworked(targetScene);
        }
            
    }

    
    public async void StartHostRelay()
    {   

        var joinCode = await relayManager.StartHostWithRelay(matchManager.MaxPlayerCount, "wss");

        Debug.Log("join code: " + joinCode);
        NetworkManager.Singleton.StartHost();
        playerSpawner.SubscribeSceneManager();

        if(targetScene != null && !string.IsNullOrEmpty(joinCode))
        {
            NetworkManager.Singleton.StartHost();
            playerSpawner.SubscribeSceneManager();
            matchManager.ActivateLobby(joinCode);
            SceneLoader.LoadNetworked(targetScene);
        }
            
    }
    public async void StartClientRelay(string joinCode)
    {
        var result = await relayManager.StartClientWithRelay(joinCode, "wss");

        
        if(result)
        {
            NetworkManager.Singleton.StartClient();
            playerSpawner.SubscribeSceneManager();
        }
        else
        {
            Debug.LogWarning("Couldn't connect to the lobby");
        }
    }

    public void StartClient()
    {
        playerSpawner.SubscribeSceneManager();
        NetworkManager.Singleton.StartClient();

    }


}
