using UnityEngine;
using UnityEngine.Serialization;

public class FigureTurn : MonoBehaviour
{
    private TurnManager turnManager;
    [SerializeField]
    private TurnManager.Team team;
    [SerializeField]
    [FormerlySerializedAs("tilesMovement")]
    private TilesMovement figureMovement;
    [SerializeField]
    private string networkId;
    private bool couldActLastFrame;
    public TurnManager.Team Team
    {
        get
        {
            return team;
        }
    }

    public string NetworkId
    {
        get
        {
            return networkId;
        }
    }

    public TilesMovement Movement
    {
        get
        {
            return figureMovement != null ? figureMovement : GetComponent<TilesMovement>();
        }
    }

    public void ConfigureNetworkFigure(string figureId, TurnManager.Team ownerTeam)
    {
        networkId = figureId;
        team = ownerTeam;
        if (figureMovement == null)
        {
            figureMovement = GetComponent<TilesMovement>();
        }
    }

    private void Awake()
    {
        if (figureMovement == null)
        {
            figureMovement = GetComponent<TilesMovement>();
        }
    }

    private void Start()
    {
        if (figureMovement == null || figureMovement.gameObject != gameObject)
        {
            Debug.LogError("[LAN] " + name + ": FigureTurn должен ссылаться на TilesMovement этой же фигуры.");
            enabled = false;
            return;
        }

        GameObject turnManagerObject = GameObject.FindGameObjectWithTag("TurnManager");
        turnManager = turnManagerObject != null ? turnManagerObject.GetComponent<TurnManager>() : null;
        if (turnManager != null)
        {
            turnManager.OnTurnStart.AddListener(TurnStart);
            turnManager.OnTurnEnd.AddListener(TurnEnd);
        }

        figureMovement.OnVisualMovementEnd += EndTurn;
        if (UsesNetworkRules())
        {
            figureMovement.enabled = true;
            couldActLastFrame = LanMultiplayer.Instance.CanLocalTeamAct(team);
            if (!couldActLastFrame)
            {
                figureMovement.UnSelect();
            }
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
            figureMovement.enabled = true;
            return;
        }

        if (team == currentTeam)
        {
            figureMovement.enabled = true;
        }
    }

    private void TurnEnd(TurnManager.Team currentTeam)
    {
        if (UsesNetworkRules())
        {
            return;
        }

        if (team == currentTeam)
        {
            figureMovement.enabled = false;
        }
    }

    private void EndTurn()
    {
        if (UsesNetworkRules())
        {
            return;
        }

        if (turnManager != null)
        {
            turnManager.EndTurn();
        }
    }

    private void Update()
    {
        if (!UsesNetworkRules())
        {
            return;
        }

        figureMovement.enabled = true;
        bool canAct = LanMultiplayer.Instance.CanLocalTeamAct(team);
        if (couldActLastFrame && !canAct)
        {
            figureMovement.UnSelect();
        }

        couldActLastFrame = canAct;
    }

    private void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnTurnStart.RemoveListener(TurnStart);
            turnManager.OnTurnEnd.RemoveListener(TurnEnd);
        }

        if (figureMovement != null)
        {
            figureMovement.OnVisualMovementEnd -= EndTurn;
        }
    }
}
