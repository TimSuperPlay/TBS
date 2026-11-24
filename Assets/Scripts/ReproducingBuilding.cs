using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;

public class ReproducingBuilding : MonoBehaviour, IResourceReceiver
{
    [SerializeField]
    [FormerlySerializedAs("resourceProdusing")]
    private string producedResourceName;
    [SerializeField]
    private string[] resourcesNeeded;
    [SerializeField]
    [FormerlySerializedAs("resourcesIn")]
    private List<string> storedResources = new List<string>();
    [SerializeField]
    [FormerlySerializedAs("cooldown")]
    private int productionIntervalTurns;
    [SerializeField]
    [FormerlySerializedAs("carrieble")]
    private Carrieble inventory;
    [SerializeField]
    private TurnManager.Team team;
    [SerializeField]
    private PixelRadialController pixelRadialController;
    private TurnManager turnManager;
    private int elapsedProductionTurns;
    public TurnManager.Team Team
    {
        get
        {
            return team;
        }
    }

    public int ProductionTicks
    {
        get
        {
            return elapsedProductionTurns;
        }
    }

    public void ConfigureTeam(TurnManager.Team ownerTeam)
    {
        team = ownerTeam;
    }

    public void ApplyProductionTicks(int elapsedTurns)
    {
        elapsedProductionTurns = elapsedTurns;
        if (pixelRadialController != null)
        {
            pixelRadialController.fillAmount = (float)elapsedProductionTurns / Mathf.Max(1, productionIntervalTurns);
        }
    }

    private void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnTurnStart.RemoveListener(TurnStart);
        }
    }

    private void Start()
    {
        turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager != null)
        {
            turnManager.OnTurnStart.AddListener(TurnStart);
        }

        if (inventory == null)
        {
            inventory = GetComponent<Carrieble>();
        }

        if (storedResources == null)
        {
            storedResources = new List<string>();
        }

        ApplyProductionTicks(elapsedProductionTurns);
    }

    private void TurnStart(TurnManager.Team currentTeam)
    {
        if (!LanGameState.CanSimulate || team != currentTeam || inventory == null)
        {
            return;
        }

        if (inventory.isFull)
        {
            return;
        }

        if (!HasEnoughResources())
        {
            return;
        }

        ApplyProductionTicks(elapsedProductionTurns + 1);
        if (elapsedProductionTurns < Mathf.Max(1, productionIntervalTurns))
        {
            return;
        }

        inventory.Add(producedResourceName);
        foreach (string resourceName in resourcesNeeded)
        {
            storedResources.Remove(resourceName);
        }

        ApplyProductionTicks(0);
    }

    public void AddResource(string resourceName)
    {
        if (!LanGameState.CanSimulate)
        {
            return;
        }

        storedResources.Add(resourceName);
    }

    public string[] Inputs
    {
        get
        {
            return storedResources.ToArray();
        }
    }

    public void ApplyInputs(string[] receivedResources)
    {
        storedResources = new List<string>(receivedResources ?? Array.Empty<string>());
    }

    private bool HasEnoughResources()
    {
        if (resourcesNeeded == null)
        {
            return false;
        }

        var requiredAmounts = resourcesNeeded.GroupBy(resourceName => resourceName).ToDictionary(group => group.Key, group => group.Count());
        var storedAmounts = storedResources.GroupBy(resourceName => resourceName).ToDictionary(group => group.Key, group => group.Count());
        foreach (var resourceName in requiredAmounts)
        {
            if (!storedAmounts.TryGetValue(resourceName.Key, out int amount))
            {
                return false;
            }

            if (amount < resourceName.Value)
            {
                return false;
            }
        }

        return true;
    }
}
