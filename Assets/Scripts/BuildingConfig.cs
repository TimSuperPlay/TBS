using System.ComponentModel;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingConfig", menuName = "Scriptable Objects/BuildingConfig")]
public class BuildingConfig : ScriptableObject
{
    public string buildingName;
    public GameObject prefab;
}
