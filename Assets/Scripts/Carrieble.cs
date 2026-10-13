using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Carrieble : MonoBehaviour
{
    public UnityAction onChange;
    [SerializeField] int size;
    public List<string> items;
    public bool isFull;

    void Start()
    {
        isFull = items.Count >= size;
        while (items.Count > size) items.RemoveAt(items.Count - 1);
    }

    public void Add(string item)
    {
        if (items.Count < size) items.Add(item);
        onChange?.Invoke();
        isFull = items.Count >= size;
    }

    public void Delete(string item)
    {
        if (!items.Contains(item)) return;
        onChange?.Invoke();
        items.Remove(item);
        isFull = false;
    }

    public void Delete(int index)
    {
        if (index < 0 || index > items.Count - 1) return;
        onChange?.Invoke();
        items.RemoveAt(index);
        isFull = false;
    }
}
