using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Scriptable Objects/PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    public Color Color;
    public Movement[] movements;

    [Serializable]
    public class Movement
    {
        public Vector3 vector;
        public float TimeSeconds, Speed;
    }
}
