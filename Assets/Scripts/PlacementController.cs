using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class PlacementController : MonoBehaviour
{
    [SerializeField] Tilemap tilemap;
    [SerializeField] BuildingConfig[] buildingConfigs;
    [SerializeField] BuildingController buildingController;
    [SerializeField] TilesGenerator tilesGenerator;
    [SerializeField] Transform buildingsParent;
    private StatesStorage statesStorage;
    private List<Vector2Int> busyTiles = new();
    private Camera mainCamera;
    
    void Awake()
    {
        statesStorage = GameObject.FindGameObjectWithTag("GameManager").GetComponent<StatesStorage>();
        mainCamera = Camera.main;
        buildingController.Init(this);
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0)) TryOpenBuildMenu();
    }

    private void TryOpenBuildMenu()
    {
        if (tilemap == null
           || mainCamera == null
           || buildingController == null
           ) return;
        if (IsPointerOverUI()) return;
        if (statesStorage.isSelected) return;

        Vector2Int cellPosition = GetCellPositionUnderMouse();
        if (!IsCanBuildOnTile(cellPosition)) return;

        buildingController.Open(cellPosition, mainCamera.WorldToScreenPoint((Vector3Int)cellPosition));
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private Vector2Int GetCellPositionUnderMouse()
    {
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int tilemapCellPosition = tilemap.WorldToCell(mousePosition);
        return (Vector2Int)tilemapCellPosition;
    }

    private bool IsCanBuildOnTile(Vector2Int position)
    {
        if (!tilemap.HasTile((Vector3Int)position)) return false;
        if (busyTiles.Contains(position)) return false;
        if (!tilesGenerator.SpawnedTiles[position].isCanBuild) return false;
        return true;
    }

    public void PlaceBuilding(Vector2Int cellPosition, int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= buildingConfigs.Length) return;

        BuildingConfig selectedBuilding = buildingConfigs[buildingIndex];
        if (selectedBuilding == null || selectedBuilding.prefab == null) return;

        Vector3Int tilemapPosition = (Vector3Int)cellPosition;
        Vector3 spawnPosition = tilemap.GetCellCenterWorld(tilemapPosition);
        GameObject PlacedBuilding = Instantiate(selectedBuilding.prefab, spawnPosition, Quaternion.identity, buildingsParent);

        PlacedBuilding.name = selectedBuilding.buildingName + " " + cellPosition;
        busyTiles.Add(cellPosition);
    }
}
