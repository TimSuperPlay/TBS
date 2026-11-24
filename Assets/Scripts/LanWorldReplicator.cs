using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LanWorldReplicator : MonoBehaviour
{
    public static LanWorldReplicator Instance { get; private set; }

    private readonly Dictionary<string, GameObject> objectsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> prefabsByName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<GameObject, string> prefabKeysBySource = new Dictionary<GameObject, string>();
    private Transform templatesParent;
    private readonly Dictionary<string, TileConfig> tileConfigsByName = new Dictionary<string, TileConfig>(StringComparer.Ordinal);
    private TilesGenerator tilesGenerator;
    private bool isInitialized;
    private int lastBoardRevision = -1;
    public string Signature { get; private set; }

    public void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        Instance = this;
        tilesGenerator = FindFirstObjectByType<TilesGenerator>();
        if (tilesGenerator != null)
        {
            foreach (TileConfig tileConfig in tilesGenerator.Configs)
            {
                RegisterTile(tileConfig);
            }
        }

        var placementController = FindFirstObjectByType<PlacementController>();
        if (placementController != null && placementController.Configs != null)
        {
            foreach (var buildingConfig in placementController.Configs)
            {
                if (buildingConfig != null)
                {
                    RegisterPrefab(buildingConfig.prefab);
                }
            }
        }

        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component.gameObject.scene != SceneManager.GetActiveScene() || !IsEntityComponent(component))
            {
                continue;
            }

            GameObject entityObject = component.gameObject;
            if (entityObject.GetComponent<LanWorldIdentity>() != null)
            {
                continue;
            }

            string objectId;
            if (entityObject.TryGetComponent(out FigureTurn figureTurn))
            {
                objectId = figureTurn.NetworkId;
            }
            else
            {
                Vector3 position = entityObject.transform.position;
                string objectDescription = entityObject.name
                    + ":" + position.x.ToString("R", CultureInfo.InvariantCulture)
                    + ":" + position.y.ToString("R", CultureInfo.InvariantCulture)
                    + ":" + position.z.ToString("R", CultureInfo.InvariantCulture);
                objectId = "scene_" + Hash(objectDescription);
            }

            Register(entityObject, objectId, string.Empty, TeamOf(entityObject));
            RegisterDependencies(entityObject);
        }

        string objectDescriptions = string.Join(
            "\n",
            objectsById.OrderBy(entry => entry.Key).Select(entry => entry.Key + ":" + TeamOf(entry.Value) + ":" + FigureRules(entry.Value))
        );
        string prefabNames = string.Join(";", prefabsByName.Keys.OrderBy(prefabName => prefabName));
        string buildingButtons = string.Empty;
        if (placementController != null && placementController.Configs != null)
        {
            buildingButtons = string.Join(
                ";",
                placementController.Configs.Select(buildingConfig => buildingConfig == null || buildingConfig.prefab == null ? "-" : buildingConfig.prefab.name)
            );
        }

        string tileNames = string.Join(";", tileConfigsByName.Keys.OrderBy(tileName => tileName));
        Signature = Hash(objectDescriptions + "\nprefabs:" + prefabNames + "\nbuttons:" + buildingButtons + "\ntiles:" + tileNames);
        isInitialized = true;
    }

    private static bool IsEntityComponent(MonoBehaviour component)
    {
        return component is TilesMovement || component is Health || component is Carrieble || component is ProducingBuilding
            || component is ReproducingBuilding
            || component is AttackingBuilding
            || component is BuildingFoundation
            || component is FiguresProducingBuilding
            || component is TerraformingBuilding;
    }

    private static string Hash(string value)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }
    }

    private static string FigureRules(GameObject entityObject)
    {
        if (!entityObject.TryGetComponent(out TilesMovement figureMovement) || figureMovement.Config == null)
        {
            return string.Empty;
        }

        FiguresConfig figureConfig = figureMovement.Config;
        string movementRules = FormatMovementRules(figureConfig.OnlyMove);
        string captureRules = FormatMovementRules(figureConfig.TakeAndMove);
        var attackDescriptions = new List<string>();
        if (figureConfig.OnlyTake != null)
        {
            foreach (var attackRule in figureConfig.OnlyTake)
            {
                string description = attackRule.MoveCells.x + "," + attackRule.MoveCells.y
                    + "," + attackRule.isJump + "," + attackRule.IsWithMove
                    + "," + attackRule.damage.ToString("R", CultureInfo.InvariantCulture);
                attackDescriptions.Add(description);
            }
        }

        return movementRules + "/" + captureRules + "/" + string.Join(";", attackDescriptions);
    }

    private static string FormatMovementRules(FiguresConfig.MoveOrTake[] movementRules)
    {
        if (movementRules == null)
        {
            return string.Empty;
        }

        var descriptions = new List<string>();
        foreach (var movementRule in movementRules)
        {
            string description = movementRule.MoveCells.x + "," + movementRule.MoveCells.y
                + "," + movementRule.isJump
                + "," + movementRule.damage.ToString("R", CultureInfo.InvariantCulture);
            descriptions.Add(description);
        }

        return string.Join(";", descriptions);
    }

    private void RegisterTile(TileConfig tileConfig)
    {
        if (tileConfig == null)
        {
            return;
        }

        if (tileConfigsByName.TryGetValue(tileConfig.name, out TileConfig existingConfig) && existingConfig != tileConfig)
        {
            throw new InvalidOperationException("Два TileConfig имеют одинаковое имя: " + tileConfig.name);
        }

        tileConfigsByName[tileConfig.name] = tileConfig;
    }

    private void RegisterPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        if (prefabsByName.TryGetValue(prefab.name, out GameObject existingObject))
        {
            if (!prefabKeysBySource.ContainsKey(prefab))
            {
                throw new InvalidOperationException("Два сетевых префаба имеют одинаковое имя: " + prefab.name);
            }

            return;
        }

        string prefabKey = prefab.name;
        prefabKeysBySource[prefab] = prefabKey;
        if (prefab.scene.IsValid())
        {
            if (templatesParent == null)
            {
                var templateContainer = new GameObject("LAN Prefab Templates");
                templateContainer.SetActive(false);
                templatesParent = templateContainer.transform;
            }

            GameObject prefabTemplate = Instantiate(prefab, Vector3.zero, Quaternion.identity, templatesParent);
            prefabTemplate.name = prefabKey;
            prefabsByName.Add(prefabKey, prefabTemplate);
        }
        else
        {
            prefabsByName.Add(prefabKey, prefab);
        }

        RegisterDependencies(prefab);
    }

    private void RegisterDependencies(GameObject entityObject)
    {
        foreach (var foundation in entityObject.GetComponentsInChildren<BuildingFoundation>(true))
        {
            foreach (var prefab in foundation.Prefabs)
            {
                RegisterPrefab(prefab);
            }
        }

        foreach (var factory in entityObject.GetComponentsInChildren<FiguresProducingBuilding>(true))
        {
            foreach (var prefab in factory.Prefabs)
            {
                RegisterPrefab(prefab);
            }
        }

        foreach (var terraform in entityObject.GetComponentsInChildren<TerraformingBuilding>(true))
        {
            RegisterTile(terraform.Config);
        }
    }

    private void Register(GameObject entityObject, string objectId, string prefabKey, int ownerTeam)
    {
        if (string.IsNullOrWhiteSpace(objectId)
            || (objectsById.TryGetValue(objectId, out GameObject existingObject) && existingObject != entityObject))
        {
            throw new InvalidOperationException("Повтор или отсутствие сетевого ID: " + entityObject.name);
        }

        var identity = entityObject.GetComponent<LanWorldIdentity>() ?? entityObject.AddComponent<LanWorldIdentity>();
        identity.Configure(objectId, prefabKey, ownerTeam);
        ConfigureTeam(entityObject, ownerTeam);
        if (entityObject.GetComponent<TilesMovement>() != null)
        {
            var figureTurn = entityObject.GetComponent<FigureTurn>() ?? entityObject.AddComponent<FigureTurn>();
            figureTurn.ConfigureNetworkFigure(objectId, (TurnManager.Team)ownerTeam);
            LanSceneReplicator.Instance?.RegisterFigure(entityObject.GetComponent<TilesMovement>());
        }

        objectsById[objectId] = entityObject;
    }

    public static int TeamOf(GameObject entityObject)
    {
        var identity = entityObject.GetComponentInParent<LanWorldIdentity>();
        if (identity != null && identity.Team >= 0)
        {
            return identity.Team;
        }

        var figureTurn = entityObject.GetComponentInParent<FigureTurn>();
        if (figureTurn != null)
        {
            return (int)figureTurn.Team;
        }

        if (entityObject.TryGetComponent(out ProducingBuilding producer))
        {
            return (int)producer.Team;
        }

        if (entityObject.TryGetComponent(out ReproducingBuilding reproducer))
        {
            return (int)reproducer.Team;
        }

        if (entityObject.TryGetComponent(out AttackingBuilding attacker))
        {
            return (int)attacker.Team;
        }

        return Enum.TryParse(entityObject.tag, out TurnManager.Team ownerTeam) ? (int)ownerTeam : -1;
    }

    private static void ConfigureTeam(GameObject entityObject, int ownerTeam)
    {
        if (ownerTeam < 0 || ownerTeam > 3)
        {
            return;
        }

        foreach (var building in entityObject.GetComponentsInChildren<ProducingBuilding>(true))
        {
            building.ConfigureTeam((TurnManager.Team)ownerTeam);
        }

        foreach (var building in entityObject.GetComponentsInChildren<ReproducingBuilding>(true))
        {
            building.ConfigureTeam((TurnManager.Team)ownerTeam);
        }

        foreach (var building in entityObject.GetComponentsInChildren<AttackingBuilding>(true))
        {
            building.ConfigureTeam((TurnManager.Team)ownerTeam);
        }
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, int ownerTeam)
    {
        if (!LanGameState.CanSimulate || ReferenceEquals(prefab, null))
        {
            return null;
        }

        if (!LanGameState.UsesNetwork)
        {
            var localObject = Instantiate(prefab, position, Quaternion.identity);
            ConfigureTeam(localObject, ownerTeam);
            if (localObject.TryGetComponent(out FigureTurn turn))
            {
                turn.ConfigureNetworkFigure(turn.NetworkId, (TurnManager.Team)ownerTeam);
            }

            return localObject;
        }

        if (Instance == null || !Instance.prefabKeysBySource.TryGetValue(prefab, out string prefabKey))
        {
            Debug.LogError("[LAN] Префаб отсутствует в каталоге строительства.");
            return null;
        }

        if (ownerTeam < 0 || ownerTeam > 3)
        {
            ownerTeam = TeamOf(Instance.prefabsByName[prefabKey]);
        }

        string objectId = "spawn_" + Guid.NewGuid().ToString("N");
        var entityObject = Instantiate(Instance.prefabsByName[prefabKey], position, Quaternion.identity);
        Instance.Register(entityObject, objectId, prefabKey, ownerTeam);
        entityObject.SetActive(true);
        return entityObject;
    }

    public static bool CanSpawn(GameObject prefab)
    {
        return !ReferenceEquals(prefab, null)
            && (!LanGameState.UsesNetwork ? prefab != null : Instance != null && Instance.prefabKeysBySource.ContainsKey(prefab));
    }

    public bool IsBuildingAt(Vector3 position)
    {
        return objectsById.Values.Any(entityObject => entityObject != null
            && entityObject.activeInHierarchy
            && entityObject.GetComponent<TilesMovement>() == null
            && (entityObject.transform.position - position).sqrMagnitude < .1f);
    }

    public static bool HasResources(List<string> availableResources, string[] requiredResources)
    {
        if (requiredResources == null || requiredResources.Length == 0)
        {
            return false;
        }

        var remainingResources = new List<string>(availableResources);
        foreach (string resourceName in requiredResources)
        {
            if (!remainingResources.Remove(resourceName))
            {
                return false;
            }
        }

        return true;
    }

    public string Capture(bool includeBoard)
    {
        Initialize();
        var worldState = new LanWorldState();
        var entityStates = new List<LanEntityState>();
        foreach (var pair in objectsById.OrderBy(pair => pair.Key))
        {
            GameObject entityObject = pair.Value;
            if (entityObject == null || !entityObject.activeInHierarchy)
            {
                continue;
            }

            var identity = entityObject.GetComponent<LanWorldIdentity>();
            var entityState = new LanEntityState
            {
                id = pair.Key,
                prefab = identity.PrefabKey,
                name = entityObject.name,
                position = entityObject.transform.position,
                team = identity.Team
            };
            if (entityObject.TryGetComponent(out Health health))
            {
                entityState.hasHealth = true;
                entityState.health = health.Current;
            }

            if (entityObject.TryGetComponent(out Carrieble inventory))
            {
                entityState.hasInventory = true;
                entityState.items = inventory.items.ToArray();
            }

            if (entityObject.TryGetComponent(out ProducingBuilding producer))
            {
                entityState.productionTicks = producer.ProductionTicks;
            }

            if (entityObject.TryGetComponent(out ReproducingBuilding reproducer))
            {
                entityState.reproductionTicks = reproducer.ProductionTicks;
                entityState.inputs = reproducer.Inputs;
            }

            if (entityObject.TryGetComponent(out BuildingFoundation foundation))
            {
                entityState.foundationInputs = foundation.Inputs;
            }

            if (entityObject.TryGetComponent(out FiguresProducingBuilding factory))
            {
                entityState.figureInputs = factory.Inputs;
            }

            entityStates.Add(entityState);
        }

        worldState.entities = entityStates.ToArray();
        if (tilesGenerator != null && (includeBoard || lastBoardRevision != tilesGenerator.Revision))
        {
            worldState.tiles = tilesGenerator.SpawnedTiles
                .OrderBy(tileEntry => tileEntry.Key.x)
                .ThenBy(tileEntry => tileEntry.Key.y)
                .Select(tileEntry => new LanTileState
                {
                    x = tileEntry.Key.x,
                    y = tileEntry.Key.y,
                    config = tileEntry.Value.name
                })
                .ToArray();
            lastBoardRevision = tilesGenerator.Revision;
        }

        return JsonUtility.ToJson(worldState);
    }

    public void Apply(string worldJson)
    {
        Initialize();
        LanWorldState worldState = JsonUtility.FromJson<LanWorldState>(worldJson);
        if (worldState == null || worldState.entities == null)
        {
            throw new InvalidOperationException("Пустое состояние мира.");
        }

        if (worldState.tiles != null && worldState.tiles.Length > 0 && tilesGenerator != null)
        {
            tilesGenerator.ApplyNetworkTiles(worldState.tiles, tileConfigsByName);
        }

        var activeObjectIds = new HashSet<string>();
        foreach (var entityState in worldState.entities)
        {
            if (!activeObjectIds.Add(entityState.id))
            {
                throw new InvalidOperationException("Повтор ID в состоянии мира.");
            }

            if (!objectsById.TryGetValue(entityState.id, out GameObject entityObject) || entityObject == null)
            {
                if (string.IsNullOrEmpty(entityState.prefab) || !prefabsByName.TryGetValue(entityState.prefab, out GameObject prefab))
                {
                    throw new InvalidOperationException("Не найден объект/префаб " + entityState.id + " / " + entityState.prefab);
                }

                entityObject = Instantiate(prefab, entityState.position, Quaternion.identity);
                Register(entityObject, entityState.id, entityState.prefab, entityState.team);
                entityObject.SetActive(true);
            }

            entityObject.name = entityState.name;
            if (entityObject.TryGetComponent(out TilesMovement movement))
            {
                Vector2Int tileCoordinates = movement.Grid.PositionToCordinats(entityState.position);
                movement.ApplyNetworkPosition(tileCoordinates.x, tileCoordinates.y);
            }
            else
            {
                entityObject.transform.position = entityState.position;
            }

            if (entityState.hasInventory && entityObject.TryGetComponent(out Carrieble inventory))
            {
                inventory.ApplyNetworkItems(entityState.items);
            }

            if (entityObject.TryGetComponent(out ProducingBuilding producer))
            {
                producer.ApplyProductionTicks(entityState.productionTicks);
            }

            if (entityObject.TryGetComponent(out ReproducingBuilding reproducer))
            {
                reproducer.ApplyProductionTicks(entityState.reproductionTicks);
                reproducer.ApplyInputs(entityState.inputs);
            }

            if (entityObject.TryGetComponent(out BuildingFoundation foundation))
            {
                foundation.ApplyInputs(entityState.foundationInputs);
            }

            if (entityObject.TryGetComponent(out FiguresProducingBuilding factory))
            {
                factory.ApplyInputs(entityState.figureInputs);
            }

            if (entityState.hasHealth && entityObject.TryGetComponent(out Health health))
            {
                health.ApplyNetworkHealth(entityState.health);
            }
        }

        foreach (var pair in objectsById.ToArray())
        {
            if (activeObjectIds.Contains(pair.Key))
            {
                continue;
            }

            GameObject entityObject = pair.Value;
            objectsById.Remove(pair.Key);
            if (entityObject == null)
            {
                continue;
            }

            if (entityObject.TryGetComponent(out Health health))
            {
                health.ApplyNetworkHealth(0);
            }

            entityObject.SetActive(false);
            Destroy(entityObject);
        }

        Physics2D.SyncTransforms();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
