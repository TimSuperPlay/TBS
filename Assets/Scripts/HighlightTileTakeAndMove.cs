using UnityEngine;

public class HighlightTileTakeAndMove : MonoBehaviour
{
    private TilesMovement figureMovement;
    private int targetX;
    private int targetY;
    private bool isBlocked = true;
    [SerializeField]
    private LayerMask figuresMask;
    public int FiguresMask
    {
        get
        {
            return figuresMask.value;
        }
    }

    private LanFigureAction Kind
    {
        get
        {
            return LanFigureAction.MoveOrAttack;
        }
    }

    public void Init(TilesMovement figureMovement, int targetX, int targetY, bool isJump, float damage)
    {
        this.figureMovement = figureMovement;
        this.targetX = targetX;
        this.targetY = targetY;
    }

    private void Start()
    {
        isBlocked = !LanActionRules.Resolve(figureMovement, Kind, targetX, targetY, out _, out _, out _, out _);
        if (isBlocked)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void OnMouseDown()
    {
        if (isBlocked || figureMovement == null || !figureMovement.CanLocalInput || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        isBlocked = true;
        figureMovement.RequestAction(Kind, targetX, targetY);
    }
}
