using Unity.VisualScripting;
using UnityEngine;

public class FigureTurn : MonoBehaviour
{
    private TurnManager turnManager;
    [SerializeField] TurnManager.Team team;
    [SerializeField] TilesMovement tilesMovement;
    void Start()
    {
        turnManager = GameObject.FindGameObjectWithTag("TurnManager").GetComponent<TurnManager>();
        turnManager.OnTurnStart.AddListener(TurnStart);
        turnManager.OnTurnEnd.AddListener(TurnEnd);
        tilesMovement.OnVisualMovementEnd += EndTurn;
    }

    void TurnStart(TurnManager.Team currentTeam)
    {
        if (team != currentTeam) return;
        tilesMovement.enabled = true;
    }

    void TurnEnd(TurnManager.Team currentTeam)
    {
        if (team != currentTeam) return;
        tilesMovement.enabled = false;
    }

    void EndTurn()
    {
        turnManager.EndTurn();
    }
}
