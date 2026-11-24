using System;
using UnityEngine;

public enum LanFigureAction : byte
{
    MoveOnly,
    MoveOrAttack,
    AttackAndMove,
    AttackOnly
}

public static class LanGameState
{
    public static bool UsesNetwork
    {
        get
        {
            return LanMultiplayer.Instance != null && LanMultiplayer.Instance.UsesNetworkRules;
        }
    }

    public static bool CanSimulate
    {
        get
        {
            return !UsesNetwork || LanMultiplayer.Instance.IsAuthority;
        }
    }
}

[Serializable]
public sealed class LanEntityState
{
    public string id;
    public string prefab;
    public string name;
    public Vector3 position;
    public int team = -1;
    public bool hasHealth;
    public bool hasInventory;
    public float health;
    public string[] items;
    public string[] inputs;
    public string[] foundationInputs;
    public string[] figureInputs;
    public int productionTicks;
    public int reproductionTicks;
}

[Serializable]
public sealed class LanTileState
{
    public int x;
    public int y;
    public string config;
}

[Serializable]
public sealed class LanWorldState
{
    public int revision;
    public LanEntityState[] entities;
    public LanTileState[] tiles;
}
