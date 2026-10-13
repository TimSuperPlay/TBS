using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TileConfig", menuName = "Scriptable Objects/TileConfig")]
public class TileConfig : ScriptableObject
{
    public string tileName, resourceName;
    public Sprite sprite;
    public Chances[] neighbourSpawnChances;
    public int defaultChance;
    [Header("Changes depending on figures weight")]
    public int normalSpeedOn, lightSpeedOn, heavySpeedOn;
    public bool isCanBuild = true;

}

[Serializable]
public class Chances
{
    public string neighbour;
    [Range(0, 100)] public int chance;
}
