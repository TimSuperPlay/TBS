using UnityEngine;

public class FigureTurn : MonoBehaviour
{
    private TurnManager turnManager;
    [SerializeField] private TurnManager.Team team;
    [SerializeField] private TilesMovement tilesMovement;
    [SerializeField] private string networkId;
    private bool couldActLastFrame;
    public TurnManager.Team Team => team;
    public string NetworkId => networkId;
    public TilesMovement Movement => tilesMovement != null ? tilesMovement : GetComponent<TilesMovement>();

    private void Awake()
    {
        if (tilesMovement == null) tilesMovement = GetComponent<TilesMovement>();
    }

    private void Start()
    {
        if (tilesMovement == null || tilesMovement.gameObject != gameObject)
        {
            Debug.LogError("[LAN] " + name + ": FigureTurn должен ссылаться на TilesMovement этой же фигуры.");
            enabled = false;
            return;
        }
        GameObject managerObject = GameObject.FindGameObjectWithTag("TurnManager");
        turnManager = managerObject != null ? managerObject.GetComponent<TurnManager>() : null;
        if (turnManager != null)
        {
            turnManager.OnTurnStart.AddListener(TurnStart);
            turnManager.OnTurnEnd.AddListener(TurnEnd);
        }
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
        if (turnManager != null) turnManager.EndTurn();
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
