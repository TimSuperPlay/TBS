using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class TilesGenerator : MonoBehaviour
{
    [SerializeField]
    private Tilemap tilemap;
    [SerializeField]
    [FormerlySerializedAs("size")]
    private Vector2Int mapSize;
    [SerializeField]
    private TileConfig[] tileConfigs;
    private readonly Dictionary<Vector2Int, TileConfig> spawnedTiles = new Dictionary<Vector2Int, TileConfig>();
    private readonly Dictionary<TileConfig, Tile> unityTiles = new Dictionary<TileConfig, Tile>();
    public Dictionary<Vector2Int, TileConfig> SpawnedTiles
    {
        get
        {
            return spawnedTiles;
        }
    }

    public TileConfig[] Configs
    {
        get
        {
            return tileConfigs;
        }
    }

    public int Revision { get; private set; }

    private bool networkBoardApplied;
    public bool HasWorldTile(Vector3 world)
    {
        return tilemap != null && tilemap.HasTile(tilemap.WorldToCell(world));
    }

    public bool TryGetWorldTile(Vector3 world, out TileConfig config)
    {
        config = null;
        return tilemap != null && spawnedTiles.TryGetValue((Vector2Int)tilemap.WorldToCell(world), out config);
    }

    public void ReplaceWorldTile(Vector3 world, TileConfig config)
    {
        if (!LanGameState.CanSimulate || tilemap == null || config == null)
        {
            return;
        }

        Vector3Int cell = tilemap.WorldToCell(world);
        spawnedTiles[(Vector2Int)cell] = config;
        tilemap.SetTile(cell, GetUnityTile(config));
        Revision++;
    }

    public void ApplyNetworkTiles(LanTileState[] cells, Dictionary<string, TileConfig> catalog)
    {
        foreach (var cell in cells)
        {
            if (!catalog.ContainsKey(cell.config))
            {
                throw new System.InvalidOperationException("Не найден TileConfig: " + cell.config);
            }
        }

        networkBoardApplied = true;
        tilemap.ClearAllTiles();
        spawnedTiles.Clear();
        foreach (var cell in cells)
        {
            var config = catalog[cell.config];
            spawnedTiles[new Vector2Int(cell.x, cell.y)] = config;
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), GetUnityTile(config));
        }

        tilemap.RefreshAllTiles();
    }

    private void Start()
    {
        if (!networkBoardApplied)
        {
            Generate();
        }
    }

    private void Generate()
    {
        tilemap.ClearAllTiles();
        spawnedTiles.Clear();
        unityTiles.Clear();
        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                TileConfig config = GetRandomTile(position);
                if (config == null && tileConfigs.Length > 0)
                {
                    config = tileConfigs[0];
                }

                spawnedTiles.Add(position, config);
            }
        }

        foreach (KeyValuePair<Vector2Int, TileConfig> pair in spawnedTiles)
        {
            Vector3Int tilemapPosition = new Vector3Int(pair.Key.x, pair.Key.y, 0);
            Tile unityTile = GetUnityTile(pair.Value);
            tilemap.SetTile(tilemapPosition, unityTile);
        }

        for (int x = 1; x < 5; x++)
        {
            for (int y = 1; y < 5; y++)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        for (int x = mapSize.x; x > mapSize.x - 4; x--)
        {
            for (int y = 1; y < 5; y++)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        for (int x = 1; x < 5; x++)
        {
            for (int y = mapSize.y; y > mapSize.x - 4; y--)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        for (int x = mapSize.x; x > mapSize.x - 4; x--)
        {
            for (int y = mapSize.y; y > mapSize.x - 4; y--)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        ReplaceLonelyTile();
        tilemap.RefreshAllTiles();
    }

    private TileConfig GetRandomTile(Vector2Int position)
    {
        int totalChance = 0;
        foreach (TileConfig config in tileConfigs)
        {
            totalChance += GetChance(config, position);
        }

        if (totalChance <= 0)
        {
            return null;
        }

        int randomValue = Random.Range(0, totalChance);
        int currentChance = 0;
        foreach (TileConfig config in tileConfigs)
        {
            currentChance += GetChance(config, position);
            if (randomValue < currentChance)
            {
                return config;
            }
        }

        return null;
    }

    private int GetChance(TileConfig config, Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);
        if (neighbours.Count == 0)
        {
            return config.defaultChance;
        }

        int finalChance = config.defaultChance;
        foreach (TileConfig neighbourConfig in neighbours)
        {
            int chanceFromNewTile = GetChanceNear(config, neighbourConfig.tileName);
            int chanceFromNeighbour = GetChanceNear(neighbourConfig, config.tileName);
            if (chanceFromNewTile <= 0 || chanceFromNeighbour <= 0)
            {
                return 0;
            }

            finalChance += chanceFromNewTile;
        }

        return finalChance;
    }

    private List<TileConfig> GetNeighbours(Vector2Int position)
    {
        List<TileConfig> neighbours = new List<TileConfig>();
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbourPosition = position + direction;
            if (spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourConfig))
            {
                neighbours.Add(neighbourConfig);
            }
        }

        return neighbours;
    }

    private int GetChanceNear(TileConfig config, string neighbourName)
    {
        foreach (Chances chance in config.neighbourSpawnChances)
        {
            if (chance.neighbour == neighbourName)
            {
                return chance.chance;
            }
        }

        return 0;
    }

    private List<Vector2Int> GetConnectedGroup(Vector2Int startPosition, string tileName, HashSet<Vector2Int> visited)
    {
        List<Vector2Int> group = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startPosition);
        visited.Add(startPosition);
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.left
        };
        while (queue.Count > 0)
        {
            Vector2Int currentPosition = queue.Dequeue();
            group.Add(currentPosition);
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbourPosition = currentPosition + direction;
                if (visited.Contains(neighbourPosition))
                {
                    continue;
                }

                if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourTile))
                {
                    continue;
                }

                if (neighbourTile.tileName != tileName)
                {
                    continue;
                }

                visited.Add(neighbourPosition);
                queue.Enqueue(neighbourPosition);
            }
        }

        return group;
    }

    private TileConfig GetMostCommonNeighbourAroundGroup(List<Vector2Int> group, string groupTileName)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, TileConfig> configs = new Dictionary<string, TileConfig>();
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.left
        };
        foreach (Vector2Int groupPosition in group)
        {
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbourPosition = groupPosition + direction;
                if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourTile))
                {
                    continue;
                }

                if (neighbourTile.tileName == groupTileName)
                {
                    continue;
                }

                if (counts.ContainsKey(neighbourTile.tileName))
                {
                    counts[neighbourTile.tileName]++;
                }
                else
                {
                    counts.Add(neighbourTile.tileName, 1);
                    configs.Add(neighbourTile.tileName, neighbourTile);
                }
            }
        }

        string bestTileName = null;
        int bestCount = 0;
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestTileName = pair.Key;
            }
        }

        if (bestTileName == null)
        {
            return null;
        }

        return configs[bestTileName];
    }

    private void ReplaceLonelyTile()
    {
        int changed = 0;
        foreach (Vector2Int position in spawnedTiles.Keys.ToList())
        {
            TileConfig current = spawnedTiles[position];
            if (current == null)
            {
                continue;
            }

            List<TileConfig> neighbours = GetNeighbours(position).Where(neighbour => neighbour != null).ToList();
            if (neighbours.Count == 0)
            {
                continue;
            }

            if (neighbours.Any(neighbour => neighbour.tileName == current.tileName))
            {
                continue;
            }

            TileConfig replacement = neighbours.GroupBy(neighbour => neighbour.tileName).OrderByDescending(group => group.Count()).First().First();
            spawnedTiles[position] = replacement;
            tilemap.SetTile(new Vector3Int(position.x, position.y, 0), GetUnityTile(replacement));
            changed++;
        }

        Debug.Log($"ReplaceLonelyTile changed: {changed}");
    }

    private TileConfig ReplaceAt3(Vector2Int position)
    {
        TileConfig currentTile = spawnedTiles[position];
        List<TileConfig> neighbours = GetNeighbours(position);
        if (neighbours.Count < 3)
        {
            return null;
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, TileConfig> configs = new Dictionary<string, TileConfig>();
        foreach (TileConfig neighbour in neighbours)
        {
            if (neighbour.tileName == currentTile.tileName)
            {
                continue;
            }

            if (counts.ContainsKey(neighbour.tileName))
            {
                counts[neighbour.tileName]++;
            }
            else
            {
                counts.Add(neighbour.tileName, 1);
                configs.Add(neighbour.tileName, neighbour);
            }
        }

        foreach (KeyValuePair<string, int> pair in counts)
        {
            string tileName = pair.Key;
            int count = pair.Value;
            if (count >= 3)
            {
                Debug.Log(position);
                return configs[tileName];
            }
        }

        return null;
    }

    private void RemoveSmallGroups(int minGroupSize)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, TileConfig> tilesToReplace = new Dictionary<Vector2Int, TileConfig>();
        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int startPosition = tile.Key;
            if (visited.Contains(startPosition))
            {
                continue;
            }

            TileConfig startTile = tile.Value;
            List<Vector2Int> group = GetConnectedGroup(startPosition, startTile.tileName, visited);
            if (group.Count >= minGroupSize)
            {
                continue;
            }

            TileConfig replacementTile = GetMostCommonNeighbourAroundGroup(group, startTile.tileName);
            if (replacementTile == null)
            {
                continue;
            }

            foreach (Vector2Int position in group)
            {
                tilesToReplace[position] = replacementTile;
            }
        }

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            spawnedTiles[tile.Key] = tile.Value;
        }

        Debug.Log($"RemoveSmallGroups changed: {tilesToReplace.Count}");
    }

    public void ReplaceTile(Vector2Int position, TileConfig tile)
    {
        spawnedTiles[position - new Vector2Int(1, 1)] = tile;
        tilemap.SetTile(((Vector3Int)position) - new Vector3Int(1, 1), GetUnityTile(tile));
        Revision++;
    }

    private Tile GetUnityTile(TileConfig config)
    {
        if (unityTiles.TryGetValue(config, out Tile existingTile))
        {
            return existingTile;
        }

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = config.sprite;
        tile.name = config.tileName;
        unityTiles.Add(config, tile);
        return tile;
    }
}
