using System;
using System.Collections.Generic;
using UnityEngine;

public class TilesController : MonoBehaviour
{
    [Header("Grid Size")]
    [SerializeField] private int xLength, yLength;

    [Header("Tiles Step")]
    [SerializeField] private float xStep, yStep;

    [Header("Start Grid Position")]
    [SerializeField] private Vector3 startingPoint;

    [Header("Biomes")]
    [SerializeField] private TileConfig[] TileConfigs;

    [Header("Biomes cordinats")]
    public List<Tile> Tiles = new();

    public float XStep { get => xStep;}
    public float YStep { get => yStep; }
    public Vector3 StartingPoint { get => startingPoint;}
    
    public Vector3 CoordinatesToPosition(int X, int Y)
    {
        float x = startingPoint.x + X * xStep;
        float y = startingPoint.y + Y * yStep;
        return (new(x, y));
    }

    public Vector3 CoordinatesToPosition(Vector2Int coordinates)
    {
        float x = startingPoint.x + coordinates.x * xStep;
        float y = startingPoint.y + coordinates.y * yStep;
        return (new(x, y));
    }

    public Vector2Int PositionToCordinats(float X, float Y)
    {
        int x = (int) Math.Round((startingPoint.x + X) / xStep);
        int y = (int) Math.Round((startingPoint.y + Y) / yStep);
        return (new(x, y));
    }

    public Vector2Int PositionToCordinats(Vector2 position)
    {
        int x = (int)Math.Round((startingPoint.x + position.x) / xStep);
        int y = (int)Math.Round((startingPoint.y + position.y) / yStep);
        return (new(x, y));
    }

    [Serializable]
    public class Tile
    {
        public int X, Y;
        public string type;
    }
}
