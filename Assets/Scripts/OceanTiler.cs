using UnityEngine;

public class OceanTiler : MonoBehaviour
{
    [Header("Setup")]
    public Transform target;          // player/camera target
    public GameObject oceanTilePrefab;

    [Header("Tiling")]
    public int tilesPerAxis = 3;      // 3 = 3x3
    public float tileSize = 50f;      // world size of ONE tile (set this correctly!)

    GameObject[,] tiles;

    void Start()
    {
        if (!target || !oceanTilePrefab)
        {
            Debug.LogError("OceanTiler: assign target and oceanTilePrefab.");
            enabled = false;
            return;
        }

        tilesPerAxis = Mathf.Max(1, tilesPerAxis);
        tiles = new GameObject[tilesPerAxis, tilesPerAxis];

        int half = tilesPerAxis / 2;

        for (int x = 0; x < tilesPerAxis; x++)
        {
            for (int z = 0; z < tilesPerAxis; z++)
            {
                Vector3 pos = new Vector3(
                    (x - half) * tileSize,
                    0f,
                    (z - half) * tileSize
                );

                GameObject tile = Instantiate(oceanTilePrefab, pos, Quaternion.identity, transform);
                tiles[x, z] = tile;
            }
        }
    }

    void LateUpdate()
    {
        Vector3 t = target.position;

        // Find which "tile cell" the target is currently over
        int cx = Mathf.RoundToInt(t.x / tileSize);
        int cz = Mathf.RoundToInt(t.z / tileSize);

        int half = tilesPerAxis / 2;

        // Reposition each tile to stay centered around the target's cell
        for (int x = 0; x < tilesPerAxis; x++)
        {
            for (int z = 0; z < tilesPerAxis; z++)
            {
                int offsetX = x - half;
                int offsetZ = z - half;

                float px = (cx + offsetX) * tileSize;
                float pz = (cz + offsetZ) * tileSize;

                Transform tileTf = tiles[x, z].transform;
                tileTf.position = new Vector3(px, tileTf.position.y, pz);
            }
        }
    }
}