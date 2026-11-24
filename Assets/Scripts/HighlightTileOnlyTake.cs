using UnityEngine;

public class HighlightTileOnlyTake : MonoBehaviour
{
    private TilesMovement figureMovement;
    private int targetX;
    private int targetY;
    private bool isBlocked = true;
    private bool moveAfterAttack;
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
            return moveAfterAttack ? LanFigureAction.AttackAndMove : LanFigureAction.AttackOnly;
        }
    }

    public void Init(
        TilesMovement figureMovement,
        int targetX,
        int targetY,
        bool isJump,
        bool moveAfterAttack,
        float damage
    )
    {
        this.figureMovement = figureMovement;
        this.targetX = targetX;
        this.targetY = targetY;
        this.moveAfterAttack = moveAfterAttack;
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
