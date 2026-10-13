using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilesGenerator2 : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap tilemap;

    [Header("Map Size")]
    [SerializeField] private Vector2Int size = new Vector2Int(64, 64);

    [Header("Tile Configs")]
    [SerializeField] private TileConfig[] tileConfigs;

    [Header("Post Processing")]
    [SerializeField] private int replaceLonelyIterations = 2;
    [SerializeField] private int removeSmallGroupsIterations = 1;
    [SerializeField] private int minGroupSize = 3;

    // Логическая карта.
    // Ключ — координата клетки.
    // Значение — какой TileConfig стоит на этой клетке.
    private readonly Dictionary<Vector2Int, TileConfig> spawnedTiles =
        new Dictionary<Vector2Int, TileConfig>();

    // Кэш Unity Tile.
    // Нужен, чтобы не создавать новый Tile для каждой клетки заново.
    private readonly Dictionary<TileConfig, Tile> unityTiles =
        new Dictionary<TileConfig, Tile>();

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Map")]
    private void Generate()
    {
        if (tilemap == null)
        {
            Debug.LogError("Tilemap is not assigned!");
            return;
        }

        if (tileConfigs == null || tileConfigs.Length == 0)
        {
            Debug.LogError("Tile configs are empty!");
            return;
        }

        // Очищаем старую карту
        tilemap.ClearAllTiles();
        spawnedTiles.Clear();
        unityTiles.Clear();

        // 1. Генерируем базовую карту
        GenerateBaseMap();

        // 2. Закрываем дырки:
        // если клетка окружена 3+ одинаковыми соседями, меняем её
        for (int i = 0; i < replaceLonelyIterations; i++)
        {
            ReplaceLonelyTiles();
        }

        // 3. Удаляем маленькие группы:
        // например одиночный снег или пару пустынь
        for (int i = 0; i < removeSmallGroupsIterations; i++)
        {
            RemoveSmallGroups(minGroupSize);
        }

        // 4. Ещё раз закрываем дырки после удаления маленьких групп
        ReplaceLonelyTiles();

        // 5. Только теперь рисуем карту
        RenderMap();
    }

    private void GenerateBaseMap()
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                TileConfig config = GetRandomTile(position);

                // Если по правилам ничего не выпало,
                // ставим первый доступный тайл, чтобы не было пустой клетки.
                if (config == null)
                    config = GetFallbackTile();

                spawnedTiles[position] = config;
            }
        }
    }

    private TileConfig GetFallbackTile()
    {
        foreach (TileConfig config in tileConfigs)
        {
            if (config != null)
                return config;
        }

        return null;
    }

    private TileConfig GetRandomTile(Vector2Int position)
    {
        List<TileConfig> possibleTiles = new List<TileConfig>();
        List<int> chances = new List<int>();

        int totalChance = 0;

        foreach (TileConfig config in tileConfigs)
        {
            if (config == null)
                continue;

            int chance = GetChance(config, position);

            if (chance <= 0)
                continue;

            possibleTiles.Add(config);
            chances.Add(chance);

            totalChance += chance;
        }

        if (totalChance <= 0)
            return null;

        int randomValue = Random.Range(0, totalChance);
        int currentChance = 0;

        for (int i = 0; i < possibleTiles.Count; i++)
        {
            currentChance += chances[i];

            if (randomValue < currentChance)
                return possibleTiles[i];
        }

        return null;
    }

    private int GetChance(TileConfig config, Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);

        // Если соседей ещё нет, используем обычный шанс.
        // Это важно для первых клеток генерации.
        if (neighbours.Count == 0)
            return config.defaultChance;

        int finalChance = config.defaultChance;

        foreach (TileConfig neighbourConfig in neighbours)
        {
            if (neighbourConfig == null)
                continue;

            int chanceFromNewTile = GetChanceNear(config, neighbourConfig.tileName);
            int chanceFromNeighbour = GetChanceNear(neighbourConfig, config.tileName);

            // ВАЖНО:
            // Если один из тайлов не разрешает такое соседство,
            // этот тайл здесь появиться не может.
            //
            // Например:
            // Snow не указал Desert
            // или Desert не указал Snow
            // значит они не появятся рядом.
            if (chanceFromNewTile <= 0 || chanceFromNeighbour <= 0)
                return 0;

            // Чем больше рядом подходящих соседей,
            // тем выше шанс появления этого тайла.
            finalChance += chanceFromNewTile;
        }

        return Mathf.Max(0, finalChance);
    }

    private int GetChanceNear(TileConfig config, string neighbourName)
    {
        if (config == null || config.neighbourSpawnChances == null)
            return 0;

        foreach (Chances chance in config.neighbourSpawnChances)
        {
            if (chance.neighbour == neighbourName)
                return chance.chance;
        }

        // 0 значит: такого соседа нет в разрешённых правилах
        return 0;
    }

    private List<TileConfig> GetNeighbours(Vector2Int position)
    {
        List<TileConfig> neighbours = new List<TileConfig>();

        foreach (Vector2Int direction in GetDirections())
        {
            Vector2Int neighbourPosition = position + direction;

            if (spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourConfig))
            {
                neighbours.Add(neighbourConfig);
            }
        }

        return neighbours;
    }

    private void ReplaceLonelyTiles()
    {
        Dictionary<Vector2Int, TileConfig> tilesToReplace =
            new Dictionary<Vector2Int, TileConfig>();

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int position = tile.Key;
            TileConfig currentTile = tile.Value;

            TileConfig replacementTile = GetReplacementIfSurrounded(position);

            if (replacementTile == null)
                continue;

            // Сравниваем tileName, а не сам TileConfig.
            // Почему:
            // GrassConfig, GrassConfig 1 и GrassConfig 2 — разные объекты,
            // но логически это один биом Grass.
            if (replacementTile.tileName == currentTile.tileName)
                continue;

            tilesToReplace[position] = replacementTile;
        }

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            spawnedTiles[tile.Key] = tile.Value;
        }

        Debug.Log($"ReplaceLonelyTiles changed: {tilesToReplace.Count}");
    }

    private TileConfig GetReplacementIfSurrounded(Vector2Int position)
    {
        if (!spawnedTiles.TryGetValue(position, out TileConfig currentTile))
            return null;

        List<TileConfig> neighbours = GetNeighbours(position);

        // Если соседей меньше 3, клетка не может быть окружена 3 одинаковыми тайлами.
        if (neighbours.Count < 3)
            return null;

        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, TileConfig> configs = new Dictionary<string, TileConfig>();

        foreach (TileConfig neighbour in neighbours)
        {
            if (neighbour == null)
                continue;

            // Такой же биом не считаем.
            // Нам интересна ситуация, когда вокруг много ДРУГОГО биома.
            if (neighbour.tileName == currentTile.tileName)
                continue;

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

        string bestTileName = null;
        int bestCount = 0;

        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > bestCount)
            {
                bestTileName = pair.Key;
                bestCount = pair.Value;
            }
        }

        // Если рядом есть 3 или 4 одинаковых чужих тайла,
        // заменяем текущую клетку на этот тайл.
        if (bestTileName != null && bestCount >= 3)
            return configs[bestTileName];

        return null;
    }

    private void RemoveSmallGroups(int minimalGroupSize)
    {
        if (minimalGroupSize <= 1)
            return;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Dictionary<Vector2Int, TileConfig> tilesToReplace =
            new Dictionary<Vector2Int, TileConfig>();

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int startPosition = tile.Key;

            if (visited.Contains(startPosition))
                continue;

            TileConfig startTile = tile.Value;

            if (startTile == null)
                continue;

            // Находим всю связанную группу одинакового tileName
            List<Vector2Int> group = GetConnectedGroup(
                startPosition,
                startTile.tileName,
                visited
            );

            // Большие группы оставляем
            if (group.Count >= minimalGroupSize)
                continue;

            // Маленькую группу заменяем на самый частый соседний биом
            TileConfig replacementTile = GetMostCommonNeighbourAroundGroup(
                group,
                startTile.tileName
            );

            if (replacementTile == null)
                continue;

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

    private List<Vector2Int> GetConnectedGroup(
        Vector2Int startPosition,
        string tileName,
        HashSet<Vector2Int> visited)
    {
        List<Vector2Int> group = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(startPosition);
        visited.Add(startPosition);

        while (queue.Count > 0)
        {
            Vector2Int currentPosition = queue.Dequeue();

            group.Add(currentPosition);

            foreach (Vector2Int direction in GetDirections())
            {
                Vector2Int neighbourPosition = currentPosition + direction;

                if (visited.Contains(neighbourPosition))
                    continue;

                if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourTile))
                    continue;

                if (neighbourTile == null)
                    continue;

                // В группу входят только клетки с таким же tileName
                if (neighbourTile.tileName != tileName)
                    continue;

                visited.Add(neighbourPosition);
                queue.Enqueue(neighbourPosition);
            }
        }

        return group;
    }

    private TileConfig GetMostCommonNeighbourAroundGroup(
        List<Vector2Int> group,
        string groupTileName)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<string, TileConfig> configs = new Dictionary<string, TileConfig>();

        // Чтобы не считать одну и ту же соседнюю клетку несколько раз
        HashSet<Vector2Int> checkedNeighbours = new HashSet<Vector2Int>();

        foreach (Vector2Int groupPosition in group)
        {
            foreach (Vector2Int direction in GetDirections())
            {
                Vector2Int neighbourPosition = groupPosition + direction;

                if (checkedNeighbours.Contains(neighbourPosition))
                    continue;

                checkedNeighbours.Add(neighbourPosition);

                if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourTile))
                    continue;

                if (neighbourTile == null)
                    continue;

                // Не считаем саму маленькую группу
                if (neighbourTile.tileName == groupTileName)
                    continue;

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
                bestTileName = pair.Key;
                bestCount = pair.Value;
            }
        }

        if (bestTileName == null)
            return null;

        return configs[bestTileName];
    }

    private void RenderMap()
    {
        tilemap.ClearAllTiles();

        foreach (KeyValuePair<Vector2Int, TileConfig> pair in spawnedTiles)
        {
            Vector2Int position = pair.Key;
            TileConfig config = pair.Value;

            if (config == null)
                continue;

            Vector3Int tilemapPosition = new Vector3Int(position.x, position.y, 0);

            tilemap.SetTile(tilemapPosition, GetUnityTile(config));
        }

        tilemap.RefreshAllTiles();
    }

    private Tile GetUnityTile(TileConfig config)
    {
        if (config == null)
            return null;

        if (unityTiles.TryGetValue(config, out Tile existingTile))
            return existingTile;

        Tile tile = ScriptableObject.CreateInstance<Tile>();

        tile.sprite = config.sprite;
        tile.name = config.tileName;

        unityTiles.Add(config, tile);

        return tile;
    }

    private Vector2Int[] GetDirections()
    {
        return new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };
    }
}