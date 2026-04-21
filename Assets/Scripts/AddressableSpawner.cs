using UnityEngine;
using UnityEngine.AddressableAssets;

public class AddressableSpawner : MonoBehaviour
{
    public AssetReference prefabReference;

    void Start()
    {
        Addressables.InstantiateAsync(prefabReference, transform.position, transform.rotation);
    }
}
