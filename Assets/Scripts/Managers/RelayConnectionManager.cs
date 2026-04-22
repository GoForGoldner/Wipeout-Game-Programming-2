using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class RelayConnectionManager : MonoBehaviour
{
    [SerializeField] string joinCode;
    public bool isWebGLGame = true;

    // WebGL requires connectionType = "wss"
    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType)
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if(isWebGLGame)
                unityTransport.UseWebSockets = true;

            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            return joinCode;
        }
        catch(RelayServiceException e)
        {
            Debug.LogWarning(e);
            return null;
        }
    }

    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType)
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        try
        {
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);

            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if(isWebGLGame)
                unityTransport.UseWebSockets = true;

            return !string.IsNullOrEmpty(joinCode);
        }
        catch(RelayServiceException e)
        {
            Debug.LogWarning(e);
            return false;
        }
    }
}
