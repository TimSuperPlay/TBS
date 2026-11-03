using UnityEngine;

public class FigureTurn : MonoBehaviour
{
    private TurnManager turnManager;
    [SerializeField] private TurnManager.Team team;
    [SerializeField] private TilesMovement tilesMovement;
    private bool couldActLastFrame;
    public TurnManager.Team Team => team;

    private void Start()
    {
        turnManager = GameObject.FindGameObjectWithTag("TurnManager").GetComponent<TurnManager>();
        turnManager.OnTurnStart.AddListener(TurnStart);
        turnManager.OnTurnEnd.AddListener(TurnEnd);
        tilesMovement.OnVisualMovementEnd += EndTurn;

        if (UsesNetworkRules())
        {
            tilesMovement.enabled = true;
            couldActLastFrame = LanMultiplayer.Instance.CanLocalTeamAct(team);
            if (!couldActLastFrame) tilesMovement.UnSelect();
        }
    }

    private static bool UsesNetworkRules()
    {
        return LanMultiplayer.Instance != null && LanMultiplayer.Instance.UsesNetworkRules;
    }

    private void TurnStart(TurnManager.Team currentTeam)
    {
        if (UsesNetworkRules())
        {
            tilesMovement.enabled = true;
            return;
        }
        if (team == currentTeam) tilesMovement.enabled = true;
    }

    private void TurnEnd(TurnManager.Team currentTeam)
    {
        if (UsesNetworkRules()) return;
        if (team == currentTeam) tilesMovement.enabled = false;
    }

    private void EndTurn()
    {
        if (UsesNetworkRules()) return;
        turnManager.EndTurn();
    }

    private void Update()
    {
        if (!UsesNetworkRules()) return;

        tilesMovement.enabled = true;
        bool canAct = LanMultiplayer.Instance.CanLocalTeamAct(team);
        if (couldActLastFrame && !canAct) tilesMovement.UnSelect();
        couldActLastFrame = canAct;
    }

    private void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnTurnStart.RemoveListener(TurnStart);
            turnManager.OnTurnEnd.RemoveListener(TurnEnd);
        }
        if (tilesMovement != null) tilesMovement.OnVisualMovementEnd -= EndTurn;
    }
}
