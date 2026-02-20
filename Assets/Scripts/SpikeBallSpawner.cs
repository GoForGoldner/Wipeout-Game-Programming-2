using System.Collections;
using UnityEngine;

public class SpikeBallSpawner : MonoBehaviour
{
    [Header("Prefab + Spawn Points")]
    public GameObject spikeBallPrefab;
    public Transform leftSpawnPoint;
    public Transform rightSpawnPoint;

    [Header("Timing")]
    public float spawnDelaySeconds = 1.25f;

    [Header("Optional")]
    public bool spawnOnStart = true;

    bool spawnLeftNext = true;
    Coroutine spawnRoutine;

    void Start()
    {
        if (spawnOnStart)
            StartSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null) return;
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine == null) return;
        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    IEnumerator SpawnLoop()
    {
        if (!spikeBallPrefab || !leftSpawnPoint || !rightSpawnPoint)
        {
            Debug.LogError("SpikeBallSpawner: Assign prefab + both spawn points.");
            yield break;
        }

        while (true)
        {
            Transform spawnPoint = spawnLeftNext ? leftSpawnPoint : rightSpawnPoint;
            spawnLeftNext = !spawnLeftNext;

            Instantiate(spikeBallPrefab, spawnPoint.position, spawnPoint.rotation);

            yield return new WaitForSeconds(spawnDelaySeconds);
        }
    }
}