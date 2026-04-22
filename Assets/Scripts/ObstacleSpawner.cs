using Unity.Netcode;
using UnityEngine;

public class ObstacleSpawner : NetworkBehaviour
{
    [System.Serializable]
    public class ObstacleEntry
    {
        public GameObject prefab;
        [Tooltip("Higher weight = more likely. 0 = never picked.")]
        public float weight = 1f;
    }

    [Header("Obstacle Pool")]
    public ObstacleEntry[] obstacles;

    [Tooltip("Chance (0-1) a spawn point spawns nothing. 0 = always spawns something.")]
    [Range(0f, 1f)] public float emptyChance = 0f;

    [Header("Debug")]
    [Tooltip("Set non-zero to force a specific seed for reproducible testing. 0 = random per match.")]
    public int debugFixedSeed = 0;

    NetworkVariable<int> seed = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    bool generated;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            seed.Value = debugFixedSeed != 0
                ? debugFixedSeed
                : new System.Random().Next(1, int.MaxValue);
        }

        seed.OnValueChanged += OnSeedReceived;

        if (seed.Value != 0) Generate(seed.Value);
    }

    public override void OnNetworkDespawn()
    {
        seed.OnValueChanged -= OnSeedReceived;
    }

    void OnSeedReceived(int oldVal, int newVal) => Generate(newVal);

    void Generate(int seedValue)
    {
        if (generated || seedValue == 0) return;
        generated = true;

        var rng = new System.Random(seedValue);
        int spawned = 0;

        foreach (Transform child in transform)
        {
            GameObject prefab = PickPrefab(rng);
            if (prefab == null) continue;

            Instantiate(prefab, child.position, child.rotation);
            spawned++;
        }

        Debug.Log($"[ObstacleSpawner] Seed={seedValue}, spawned {spawned} obstacles across {transform.childCount} points.");
    }

    GameObject PickPrefab(System.Random rng)
    {
        if (obstacles == null || obstacles.Length == 0) return null;

        if (emptyChance > 0f && rng.NextDouble() < emptyChance) return null;

        float totalWeight = 0f;
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i].prefab != null)
                totalWeight += Mathf.Max(0f, obstacles[i].weight);
        }
        if (totalWeight <= 0f) return null;

        double roll = rng.NextDouble() * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i].prefab == null) continue;
            cumulative += Mathf.Max(0f, obstacles[i].weight);
            if (roll <= cumulative) return obstacles[i].prefab;
        }

        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (Transform child in transform)
        {
            Gizmos.DrawWireSphere(child.position, 0.5f);
        }
    }
}
