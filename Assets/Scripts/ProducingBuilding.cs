using UnityEngine;

public class ProducingBuilding : MonoBehaviour
{
    [SerializeField] string Resource;
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
        if (turnsPassed < cooldown)
        {
            turnsPassed++;
            pixelRadialController.fillAmount = (float) turnsPassed / cooldown;
            return;
        }
        carrieble.Add(Resource);
        turnsPassed = 0;
        pixelRadialController.fillAmount = 1;
    }

}
