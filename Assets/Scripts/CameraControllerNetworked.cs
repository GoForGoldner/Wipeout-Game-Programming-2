using UnityEngine;
using Unity.Netcode;

public class CameraControllerNetworked : NetworkBehaviour
{
    void Start()
    {
        if (!IsOwner) return;

        OrbitCamera orbitCam = FindAnyObjectByType<OrbitCamera>();
        if (orbitCam != null)
        {
            orbitCam.target = transform;
            orbitCam.playerController = GetComponent<PlayerController>();

            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null)
                pc.cameraPivot = orbitCam.transform;
        }
    }
}