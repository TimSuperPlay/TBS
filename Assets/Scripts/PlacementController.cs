using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    [SerializeField]
    private Tilemap tilemap;
    [SerializeField]
    private BuildingConfig[] buildingConfigs;
    [SerializeField]
    private BuildingController buildingController;
    [SerializeField]
    private TilesGenerator tilesGenerator;
    [SerializeField]
    private Transform buildingsParent;
    private StatesStorage statesStorage;
    private List<Vector2Int> busyTiles = new();
    private Camera mainCamera;
    public BuildingConfig[] Configs
    {
        get
        {
            return buildingConfigs;
        }
    }

    public bool CanBuildLocally
    {
        get
        {
            return !LanGameState.UsesNetwork || LanMultiplayer.Instance.CanLocalBuild;
        }
    }

    private void Awake()
    {
        statesStorage = FindFirstObjectByType<StatesStorage>();
        mainCamera = Camera.main;
        if (buildingController != null)
        {
            buildingController.Init(this);
        }
    }

    private void Update()
    {
        if (!CanBuildLocally)
        {
            if (buildingController != null)
            {
                buildingController.Close();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            TryOpenBuildMenu();
        }
    }

    private void TryOpenBuildMenu()
    {
        if (tilemap == null || mainCamera == null || buildingController == null)
        {
            return;
        }

        if (IsPointerOverUI())
        {
            return;
        }

        if (statesStorage != null && statesStorage.isSelected)
        {
            return;
        }

        foreach (var hit in Physics2D.OverlapPointAll(mainCamera.ScreenToWorldPoint(Input.mousePosition)))
        {
            if (hit.GetComponentInParent<TilesMovement>() != null)
            {
                return;
            }
        }

        Vector2Int cellPosition = GetCellPositionUnderMouse();
        if (!IsCanBuildOnTile(cellPosition))
        {
            return;
        }

        buildingController.Open(cellPosition, mainCamera.WorldToScreenPoint(tilemap.GetCellCenterWorld((Vector3Int)cellPosition)));
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private Vector2Int GetCellPositionUnderMouse()
    {
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition) - new Vector3(1, 1);
        Vector3Int tilemapCellPosition = tilemap.WorldToCell(mousePosition);
        return (Vector2Int)tilemapCellPosition;
    }

    public bool IsCanBuildOnTile(Vector2Int position)
    {
        if (tilemap == null || tilesGenerator == null || !tilemap.HasTile((Vector3Int)position))
        {
            return false;
        }

        if (!tilesGenerator.SpawnedTiles.TryGetValue(position, out TileConfig tile) || tile == null || !tile.isCanBuild)
        {
            return false;
        }

        if (LanGameState.UsesNetwork)
        {
            return LanWorldReplicator.Instance != null
                && !LanWorldReplicator.Instance.IsBuildingAt(tilemap.GetCellCenterWorld((Vector3Int)position));
        }

        return !busyTiles.Contains(position);
    }

    public void PlaceBuilding(Vector2Int cellPosition, int buildingIndex)
    {
        if (!CanBuildLocally)
        {
            return;
        }

        if (LanGameState.UsesNetwork)
        {
            LanMultiplayer.Instance.RequestBuild(cellPosition, buildingIndex);
            return;
        }

        var turnManager = FindFirstObjectByType<TurnManager>();
        BuildAuthoritative(cellPosition, buildingIndex, turnManager == null ? 0 : (int)turnManager.CurrentTeam);
    }

    public bool BuildAuthoritative(Vector2Int cellPosition, int buildingIndex, int ownerTeam)
    {
        if (!LanGameState.CanSimulate || buildingConfigs == null || buildingIndex < 0 || buildingIndex >= buildingConfigs.Length
            || !IsCanBuildOnTile(cellPosition))
        {
            return false;
        }

        BuildingConfig buildingConfig = buildingConfigs[buildingIndex];
        if (buildingConfig == null || buildingConfig.prefab == null)
        {
            return false;
        }

        var placedBuilding = LanWorldReplicator.Spawn(buildingConfig.prefab, tilemap.GetCellCenterWorld((Vector3Int)cellPosition), ownerTeam);
        if (placedBuilding == null)
        {
            return false;
        }

        if (!LanGameState.UsesNetwork)
        {
            placedBuilding.transform.SetParent(buildingsParent, true);
            busyTiles.Add(cellPosition);
        }

        return true;
    }
}
