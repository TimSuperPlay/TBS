using System;
using System.Collections.Generic;
using UnityEngine;

public class TilesController : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField]
    private int xLength;
    [SerializeField]
    private int yLength;
    [Header("Tiles Step")]
    [SerializeField]
    private float xStep;
    [SerializeField]
    private float yStep;
    [Header("Start Grid Position")]
    [SerializeField]
    private Vector3 startingPoint;
    [Header("Biomes")]
    [SerializeField]
    private TileConfig[] TileConfigs;
    [Header("Biomes cordinats")]
    public List<Tile> Tiles = new();
    public float XStep
    {
        get
        {
            return xStep;
        }
    }

    public float YStep
    {
        get
        {
            return yStep;
        }
    }

    public Vector3 StartingPoint
    {
        get
        {
            return startingPoint;
        }
    }

    public Vector3 CoordinatesToPosition(int column, int row)
    {
        float worldX = startingPoint.x + column * xStep;
        float worldY = startingPoint.y + row * yStep;
        return new Vector3(worldX, worldY);
    }

    public Vector3 CoordinatesToPosition(Vector2Int coordinates)
    {
        float worldX = startingPoint.x + coordinates.x * xStep;
        float worldY = startingPoint.y + coordinates.y * yStep;
        return new Vector3(worldX, worldY);
    }

    public Vector2Int PositionToCordinats(float worldX, float worldY)
    {
        int column = (int)Math.Round((worldX - startingPoint.x) / xStep);
        int row = (int)Math.Round((worldY - startingPoint.y) / yStep);
        return (new(column, row));
    }

    public Vector2Int PositionToCordinats(Vector2 worldPosition)
    {
        return PositionToCordinats(worldPosition.x, worldPosition.y);
    }

    [Serializable]
    public class Tile
    {
        public int X;
        public int Y;
        public string type;
    }
}
