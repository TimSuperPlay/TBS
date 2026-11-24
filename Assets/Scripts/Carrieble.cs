using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

public class Carrieble : MonoBehaviour
{
    public UnityAction onChange;
    [SerializeField]
    [FormerlySerializedAs("size")]
    private int capacity;
    public List<string> items = new List<string>();
    public bool isFull;
    private void Awake()
    {
        ApplyNetworkItems(items?.ToArray());
    }

    public void ApplyNetworkItems(string[] receivedItems)
    {
        items = new List<string>(receivedItems ?? System.Array.Empty<string>());
        while (items.Count > Mathf.Max(0, capacity))
        {
            items.RemoveAt(items.Count - 1);
        }

        Changed();
    }

    private void Changed()
    {
        isFull = items.Count >= capacity;
        onChange?.Invoke();
    }

    public bool TryAdd(string resourceName)
    {
        if (!LanGameState.CanSimulate || items.Count >= capacity)
        {
            return false;
        }

        items.Add(resourceName);
        Changed();
        return true;
    }

    public void Add(string resourceName)
    {
        TryAdd(resourceName);
    }

    public void Delete(string resourceName)
    {
        if (LanGameState.CanSimulate && items.Remove(resourceName))
        {
            Changed();
        }
    }

    public void Delete(int itemIndex)
    {
        if (!LanGameState.CanSimulate || itemIndex < 0 || itemIndex >= items.Count)
        {
            return;
        }

        items.RemoveAt(itemIndex);
        Changed();
    }
}
