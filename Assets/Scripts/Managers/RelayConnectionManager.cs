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
        
        // sign in to Unity Authentication as anonymous user
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        
        try
        {
            // get an allocation for the relay server
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // set the settings of UP based on the allocation
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if(isWebGLGame)
                unityTransport.UseWebSockets = true;
            
            // get the join code for the allocation
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // return the join code for the host to share with others
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
            // join the allocation corresponding to the code provided
            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);

            // change UP settings based on the allocation
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            if(isWebGLGame)
                unityTransport.UseWebSockets = true;

            // return true if relay connection succeeds
            return !string.IsNullOrEmpty(joinCode);
        }
        catch(RelayServiceException e)
        {
            Debug.LogWarning(e);
            return false;
        }
        
    }


}
