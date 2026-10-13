using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class ReproducingBuilding : MonoBehaviour, IResourceReceiver
{
    [SerializeField] string resourceProdusing;
    [SerializeField] string[] resourcesNeeded;
    [SerializeField] List<string> resourcesIn;
    [SerializeField] int cooldown;
    [SerializeField] Carrieble carrieble;
    [SerializeField] TurnManager.Team team;
    [SerializeField] PixelRadialController pixelRadialController;
    private TurnManager turnManager;
    private int turnsPassed;

    void Start()
    {
        turnManager = GameObject.FindGameObjectWithTag("TurnManager").GetComponent<TurnManager>();
        turnManager.OnTurnStart.AddListener(TurnStart);
        turnsPassed = 0;
        pixelRadialController.fillAmount = 0;
    }

    void TurnStart(TurnManager.Team currentTeam)
    {
        if (team != currentTeam) return;
        if (carrieble.isFull) return;
        if (!HasEnoughResources()) return;
        if (turnsPassed < cooldown)
        {
            turnsPassed++;
            pixelRadialController.fillAmount = (float) turnsPassed / cooldown;
            return;
        }
        Debug.Log("reprodusing building sucseed");
        carrieble.Add(resourceProdusing);
        resourcesIn.Clear();
        turnsPassed = 0;
        pixelRadialController.fillAmount = 1;
    }

    public void AddResource(string resource)
    {
        Debug.Log("addet: " + resource);
        resourcesIn.Add(resource);
    }

    bool HasEnoughResources()
    {
        var needed = resourcesNeeded
            .GroupBy(resource => resource)
            .ToDictionary(group => group.Key, group => group.Count());
        var current = resourcesIn
            .GroupBy(resource => resource)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var resource in needed)
        {
            if(!current.TryGetValue(resource.Key, out int amount)) return false;
            if(amount<resource.Value) return false;
        }
        return true;
    }

}
