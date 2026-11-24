using UnityEngine;

public sealed class LanWorldIdentity : MonoBehaviour
{
    public string Id { get; private set; }
    public string PrefabKey { get; private set; }
    public int Team { get; private set; } = -1;

    public void Configure(string id, string prefabKey, int team)
    {
        Id = id;
        PrefabKey = prefabKey;
        Team = team;
    }
}
