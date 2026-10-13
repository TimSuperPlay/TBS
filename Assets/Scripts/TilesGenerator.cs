using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
public class TilesGenerator : MonoBehaviour
{
    // Tilemap, на который будут устанавливаться тайлы
    [SerializeField] private Tilemap tilemap;

    // Размер генерируемой области
    [SerializeField] private Vector2Int size;

    // Набор конфигураций тайлов, из которых будет происходить генерация
    [SerializeField] private TileConfig[] tileConfigs;

    // Словарь уже сгенерированных тайлов:
    // ключ — позиция тайла, значение — его конфигурация
    private readonly Dictionary<Vector2Int, TileConfig> spawnedTiles = new Dictionary<Vector2Int, TileConfig>();

    // Словарь Unity Tile-объектов, чтобы не создавать новый Tile каждый раз
    private readonly Dictionary<TileConfig, Tile> unityTiles = new Dictionary<TileConfig, Tile>();

    public Dictionary<Vector2Int, TileConfig> SpawnedTiles => spawnedTiles;

    private void Start()
    {
        // Запускаем генерацию при старте сцены
        Generate();
    }

    private void Generate()
    {
        // Очищаем Tilemap перед новой генерацией
        tilemap.ClearAllTiles();

        // Очищаем данные о ранее созданных тайлах
        spawnedTiles.Clear();

        unityTiles.Clear();

        // Проходим по всем координатам в пределах заданного размера
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                // Выбираем случайный тайл с учётом соседей
                TileConfig config = GetRandomTile(position);

                // Если подходящий тайл не найден — пропускаем клетку
                if (config == null && tileConfigs.Length > 0)
                    config = tileConfigs[0]; // Насильно затыкаем дыру первым тайлом из списка


                // Сохраняем информацию о созданном тайле
                spawnedTiles.Add(position, config);

                // Устанавливаем тайл на Tilemap
                /*tilemap.SetTile(
                    new Vector3Int(position.x, position.y, 0),
                    GetUnityTile(config)
                );*/
            }
        }

        //ReplaceLonelyTile();
        //ReplaceLonelyTile();

        foreach (KeyValuePair<Vector2Int, TileConfig> pair in spawnedTiles)
        {
            Vector3Int tilemapPosition = new Vector3Int(pair.Key.x, pair.Key.y, 0);
            Tile unityTile = GetUnityTile(pair.Value);

            // Насильно приказываем Unity отобразить измененный тайл
            tilemap.SetTile(tilemapPosition, unityTile);
        }

        for (int x = 1; x < 5; x++)
        {
            for (int y = 1; y < 5; y++)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        for (int x = size.x; x > size.x -4; x--)
        {
            for (int y = 1; y < 5; y++)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }


        for (int x = 1; x < 5; x++)
        {
            for (int y = size.y; y > size.x - 4; y--)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }

        for (int x = size.x; x > size.x - 4; x--)
        {
            for (int y = size.y; y > size.x - 4; y--)
            {
                ReplaceTile(new Vector2Int(x, y), tileConfigs[0]);
            }
        }
        ReplaceLonelyTile();

        tilemap.RefreshAllTiles();
    }

    /*private void Generate()
    {
        // ЖЕСТКИЙ СБРОС СДВИГОВ: гарантируем, что сетка не смещена в Unity
        tilemap.transform.localPosition = Vector3.zero;
        tilemap.transform.localScale = Vector3.one;
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0); // Стандартный якорь по центру клетки

        tilemap.ClearAllTiles();
        spawnedTiles.Clear();
        unityTiles.Clear();

        // Заполняем абсолютно все клетки без пропусков
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                TileConfig config = GetRandomTile(position);

                // Если выпал null — берем самый первый тайл (например, пустыню или траву), 
                // главное не делать continue, чтобы не ломать индексы!
                if (config == null && tileConfigs.Length > 0)
                    config = tileConfigs[0];

                if (config == null) continue;

                spawnedTiles.Add(position, config);
            }
        }

        // Запускаем сглаживание в памяти, когда все координаты синхронизированы
        ReplaceLonelyTile();
        ReplaceLonelyTile();

        // Финальный проход отрисовки строго по координатам словаря
        foreach (KeyValuePair<Vector2Int, TileConfig> pair in spawnedTiles)
        {
            Vector3Int tilemapPosition = new Vector3Int(pair.Key.x, pair.Key.y, 0);
            tilemap.SetTile(tilemapPosition, GetUnityTile(pair.Value));
        }

        tilemap.RefreshAllTiles();
    }*/


    private TileConfig GetRandomTile(Vector2Int position)
    {
        int totalChance = 0;

        // Считаем общую сумму шансов всех тайлов
        foreach (TileConfig config in tileConfigs)
        {
            totalChance += GetChance(config, position);
        }

        // Если все шансы равны нулю или меньше — тайл не создаём
        if (totalChance <= 0)
            return null;

        // Выбираем случайное значение в пределах общей суммы шансов
        int randomValue = Random.Range(0, totalChance);
        int currentChance = 0;

        // Определяем, какой тайл выпал по весу
        foreach (TileConfig config in tileConfigs)
        {
            currentChance += GetChance(config, position);

            if (randomValue < currentChance)
                return config;
        }

        return null;
    }

    private int GetChance(TileConfig config, Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);

        if (neighbours.Count == 0) return config.defaultChance;

        int finalChance = config.defaultChance;
        // Проверяем специальные шансы появления рядом с определёнными соседями
        foreach (TileConfig neighbourConfig in neighbours)
        {
            int chanceFromNewTile = GetChanceNear(config, neighbourConfig.tileName);
            int chanceFromNeighbour = GetChanceNear(neighbourConfig, config.tileName);

            if (chanceFromNewTile <= 0 || chanceFromNeighbour <= 0) return 0;

            finalChance += chanceFromNewTile;
        }

        // Если подходящих соседей нет — используем шанс по умолчанию
        return finalChance;
    }

    /*private int GetChance(TileConfig config, Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);

        if (neighbours.Count == 0) return config.defaultChance;

        int finalChance = config.defaultChance;

        foreach (TileConfig neighbourConfig in neighbours)
        {
            int chanceFromNewTile = GetChanceNear(config, neighbourConfig.tileName);
            int chanceFromNeighbour = GetChanceNear(neighbourConfig, config.tileName);

            // Вместо жесткого return 0, просто пропускаем или плавно снижаем шанс, 
            // чтобы не создавать пустые клетки на карте
            finalChance += chanceFromNewTile + chanceFromNeighbour;
        }

        // Возвращаем минимум 0, чтобы избежать отрицательных весов
        return Mathf.Max(0, finalChance);
    }*/

    private List<TileConfig> GetNeighbours(Vector2Int position)
    {
        List<TileConfig> neighours = new List<TileConfig>();
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
                neighours.Add(neighbourConfig);
            }
        }

        return neighours;
    }

    private int GetChanceNear(TileConfig config, string neighbourName)
    {
        foreach (Chances chance in config.neighbourSpawnChances)
        {
            if (chance.neighbour == neighbourName) return chance.chance;
        }
        return 0;
    }


    private List<Vector2Int> GetConnectedGroup(
    Vector2Int startPosition,
    string tileName,
    HashSet<Vector2Int> visited)
    {
        // Итоговый список клеток группы
        List<Vector2Int> group = new List<Vector2Int>();

        // Очередь для проверки соседей
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(startPosition);
        visited.Add(startPosition);

        Vector2Int[] directions =
        {   Vector2Int.up,
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
                    continue;

                if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourTile))
                    continue;

                // В группу входят только тайлы с таким же tileName
                if (neighbourTile.tileName != tileName)
                    continue;

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
        {   Vector2Int.up,
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
                    continue;

                // Не считаем саму группу
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
                bestCount = pair.Value;
                bestTileName = pair.Key;
            }
        }

        if (bestTileName == null)
            return null;

        return configs[bestTileName];
    }



    /*private void ReplaceLonelyTile()
    {
        Dictionary<Vector2Int, TileConfig> tilesToReplace = new Dictionary<Vector2Int, TileConfig>();

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int position = tile.Key;
            TileConfig currentTile = tile.Value;

            TileConfig newTile = ReplaceAt3(position);

            if (newTile == null) continue;
            if (newTile.tileName == currentTile.tileName) continue;

            tilesToReplace[position] = newTile;
        }

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            Vector2Int position = tile.Key;
            TileConfig newTile = tile.Value;

            //Debug.Log($"{position} , {newTile}");

            spawnedTiles[position] = newTile;

            tilemap.SetTile(new Vector3Int(position.x, position.y), GetUnityTile(newTile));
        }

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            Vector2Int pos = tile.Key;
            TileConfig newTile = tile.Value;

            // 1. Берем то, что СЕЙЧАС физически отображается на тайлмапе до изменения
            Vector3Int tilemapPos = new Vector3Int(pos.x, pos.y, 0);
            var tileBefore = tilemap.GetTile(tilemapPos);
            string nameBefore = tileBefore != null ? tileBefore.name : "null";

            // 2. Меняем данные в словаре (readonly это разрешает)
            spawnedTiles[pos] = newTile;

            // 3. Насильно пихаем новый тайл в Unity Tilemap
            Tile unityTile = GetUnityTile(newTile); // Или newTile.unityTile, если переделал по прошлому шагу
            tilemap.SetTile(tilemapPos, unityTile);

            // 4. Проверяем, изменился ли тайл внутри самого движка Unity после команды SetTile
            var tileAfter = tilemap.GetTile(tilemapPos);
            string nameAfter = tileAfter != null ? tileAfter.name : "null";

            Debug.Log($"[Проверка движка] Клетка {pos}: Было на Tilemap: '{nameBefore}', Стало на Tilemap: '{nameAfter}'");
        }

        // Применяем изменения и СРАЗУ отрисовываем
        /*foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            Vector2Int pos = tile.Key;
            TileConfig newConfig = tile.Value;

            // 1. Обновляем данные в словаре
            spawnedTiles[pos] = newConfig;

            // 2. Генерируем Unity-тайл заново (в обход кэша, для теста!)
            Tile visualTile = ScriptableObject.CreateInstance<Tile>();
            visualTile.sprite = newConfig.sprite;

            // 3. Выводим в консоль точные координаты и имя ассета, который МЫ СЕЙЧАС СТАВИМ
            Debug.Log($"[Отрисовка] Ставлю в {pos} тайл {newConfig.tileName} со спрайтом {newConfig.sprite.name}");

            // 4. Пихаем напрямую в Tilemap
            Vector3Int tilemapPos = new Vector3Int(pos.x, pos.y, 0);
            tilemap.SetTile(tilemapPos, visualTile);
            tilemap.RefreshTile(tilemapPos); // Обновляем конкретно эту клетку
        }
        tilemap.gameObject.name = "ЭТОТ_ТАЙЛМАП_ИЗМЕНЯЕТСЯ!";
    }*/

    private void ReplaceLonelyTile()
    {
        // Сюда временно складываем замены.
        // Почему временно?
        // Нельзя безопасно менять spawnedTiles прямо во время foreach по spawnedTiles.
        Dictionary<Vector2Int, TileConfig> tilesToReplace = new Dictionary<Vector2Int, TileConfig>();

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int position = tile.Key;
            TileConfig currentTile = tile.Value;

            // Проверяем, надо ли заменить эту клетку
            TileConfig newTile = ReplaceAt3(position);

            // null значит: заменять не надо
            if (newTile == null)
                continue;

            // Если биом тот же самый — менять не надо
            if (newTile.tileName == currentTile.tileName)
                continue;

            tilesToReplace[position] = newTile;
        }

        // Теперь применяем замены
        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            ReplaceTile(tile.Key, tile.Value);
        }

        Debug.Log($"ReplaceLonelyTile changed: {tilesToReplace.Count}");
    }

    /*private TileConfig ReplaceAt3(Vector2Int position)
    {
        // Направления проверки соседних клеток:
        // вверх, вниз, влево и вправо
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        List<TileConfig> neighours = GetNeighbours(position);

        /*foreach (Vector2Int direction in directions)
        {
            Vector2Int neighbourPosition = position + direction;

            if (!spawnedTiles.TryGetValue(neighbourPosition, out TileConfig neighbourConfig)) continue;

            if (spawnedTiles[position + direction] == spawnedTiles[position]) return null;

            neighours.Add(spawnedTiles[position + direction]);
        }

        return
            neighours.GroupBy(tile => tile.tileName).
            FirstOrDefault(group => group.Count() >= 3).
            First();

        var bestGroup = neighours
            .Where(tile => tile.tileName != spawnedTiles[position].tileName)
            .GroupBy(tile => tile.tileName)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (bestGroup == null) return null;
        if (bestGroup.Count() < 3) return null;

        Debug.Log("old:" + spawnedTiles[position]);
        Debug.Log("new: " + bestGroup.First());
        return bestGroup.First();
    }*/


    private TileConfig ReplaceAt3(Vector2Int position)
    {
        // Текущий тайл на этой клетке
        TileConfig currentTile = spawnedTiles[position];

        // Берём соседей: верх, низ, лево, право
        List<TileConfig> neighbours = GetNeighbours(position);

        // Если соседей меньше 3, клетка физически не может быть окружена 3 одинаковыми тайлами
        if (neighbours.Count < 3)
            return null;

        // Считаем, сколько раз каждый ДРУГОЙ биом встречается среди соседей
        Dictionary<string, int> counts = new Dictionary<string, int>();

        // Запоминаем TileConfig для каждого tileName,
        // чтобы потом вернуть конкретный конфиг для замены
        Dictionary<string, TileConfig> configs = new Dictionary<string, TileConfig>();

        foreach (TileConfig neighbour in neighbours)
        {
            // Если сосед такого же биома, как текущий тайл, не считаем его.
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

        // Ищем биом, который встретился 3 или больше раз
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
        // visited нужен, чтобы не проверять одну и ту же клетку много раз
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Сюда складываем клетки, которые надо заменить
        Dictionary<Vector2Int, TileConfig> tilesToReplace = new Dictionary<Vector2Int, TileConfig>();

        foreach (KeyValuePair<Vector2Int, TileConfig> tile in spawnedTiles)
        {
            Vector2Int startPosition = tile.Key;

            if (visited.Contains(startPosition))
                continue;

            TileConfig startTile = tile.Value;

            // Находим связанную группу одного биома
            List<Vector2Int> group = GetConnectedGroup(startPosition, startTile.tileName, visited);

            // Если группа достаточно большая — оставляем её
            if (group.Count >= minGroupSize)
                continue;

            // Если группа маленькая — ищем, каким соседним биомом её заменить
            TileConfig replacementTile = GetMostCommonNeighbourAroundGroup(group, startTile.tileName);

            if (replacementTile == null)
                continue;

            // Запоминаем замену для всех клеток маленькой группы
            foreach (Vector2Int position in group)
            {
                tilesToReplace[position] = replacementTile;
            }
        }

        // Применяем замены
        foreach (KeyValuePair<Vector2Int, TileConfig> tile in tilesToReplace)
        {
            spawnedTiles[tile.Key] = tile.Value;
        }

        Debug.Log($"RemoveSmallGroups changed: {tilesToReplace.Count}");
    }



    /*private TileConfig ReplaceAt3(Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);

        // Если вокруг клетки почти нет данных (края карты или дыры), пропускаем
        if (neighbours.Count < 3) return null;

        TileConfig currentTile = spawnedTiles[position];

        // Считаем, какой тип тайла вообще преобладает среди ВСЕХ соседей
        var mostCommonNeighbourGroup = neighbours
            .GroupBy(tile => tile.tileName)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (mostCommonNeighbourGroup == null) return null;

        // Если какого-то одного типа соседей 3 или больше (например, 3 травы вокруг 1 земли)
        if (mostCommonNeighbourGroup.Count() >= 3)
        {
            Debug.Log($"[ReplaceAt3] -> НАШЕЛ ЗАМЕНУ для {position}: меняем '{currentTile.tileName}' на '{mostCommonNeighbourGroup.Key}'");
            string dominantTileName = mostCommonNeighbourGroup.Key;

            // Если этот доминирующий тип отличается от того, что лежит в клетке сейчас — МЕНЯЕМ!
            if (dominantTileName != currentTile.tileName)
            {
                // Возвращаем конфиг этого доминирующего соседа
                return mostCommonNeighbourGroup.First();
            }
        }

        return null;
    }

    private TileConfig ReplaceAt3(Vector2Int position)
    {
        List<TileConfig> neighbours = GetNeighbours(position);
        if (neighbours.Count < 3) return null;

        TileConfig currentTile = spawnedTiles[position];

        // Выводим в консоль имя текущего тайла и имена всех его соседей
        string neighboursDebug = string.Join(", ", neighbours.Select(n => $"'{n.tileName}'"));
        Debug.Log($"[ReplaceAt3] Точка {position}. Текущий: '{currentTile.tileName}'. Соседи: {neighboursDebug}");

        var bestGroup = neighbours
            .Where(tile => tile.tileName != currentTile.tileName)
            .GroupBy(tile => tile.tileName)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        if (bestGroup == null) return null;

        if (bestGroup.Count() >= 3)
        {
            Debug.Log($"[ReplaceAt3] -> НАШЕЛ ЗАМЕНУ для {position}: меняем '{currentTile.tileName}' на '{bestGroup.Key}'");
            return bestGroup.First();
        }

        return null;
    }*/

    public void ReplaceTile(Vector2Int position, TileConfig tile)
    {
        spawnedTiles[position - new Vector2Int(1, 1)] = tile;
        tilemap.SetTile(((Vector3Int) position) - new Vector3Int(1, 1), GetUnityTile(tile));
    }

    private Tile GetUnityTile(TileConfig config)
    {
        // Если Unity Tile уже был создан для этой конфигурации,
        // возвращаем его из словаря
        if (unityTiles.TryGetValue(config, out Tile existingTile))
            return existingTile;

        // Создаём новый Unity Tile
        Tile tile = ScriptableObject.CreateInstance<Tile>();

        // Назначаем спрайт из конфигурации
        tile.sprite = config.sprite;

        tile.name = config.tileName;

        // Сохраняем Tile в словарь
        unityTiles.Add(config, tile);

        return tile;
    }
}