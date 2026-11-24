using UnityEngine;
using UnityEngine.Serialization;

public class ProducingBuilding : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("Resource")]
    private string resourceName;
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

        ApplyProductionTicks(elapsedProductionTurns + 1);
        if (elapsedProductionTurns < Mathf.Max(1, productionIntervalTurns))
        {
            return;
        }

        inventory.Add(resourceName);
        ApplyProductionTicks(0);
    }
}
