using UnityEngine;

[CreateAssetMenu(fileName = "MaterialsConfig", menuName = "Scriptable Objects/MaterialsConfig")]
public class MaterialsConfig : ScriptableObject
{
    [Header("How much turns it can move instead of one")]
    [Tooltip("if less than one, it determines how many turns it need to wait until moves")]
    public float TurnSpeed;
    public int Health;
    [Tooltip("1 - light (can swim, and based on figure), 2 - normal (no changes), 3 - heavy (cannot swim even a litle, and based on figure)")] 
    [Range(1, 3)] public int weight;
}
