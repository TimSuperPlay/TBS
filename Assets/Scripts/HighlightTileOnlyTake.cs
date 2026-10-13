using UnityEngine;

public class HighlightTileOnlyTake : MonoBehaviour
{
    private TilesMovement owner;
    private int targetX;
    private int targetY;
    private bool isJump;
    private bool withMove;
    private Collider2D onFigure;
    [SerializeField] LayerMask figuresMask;

    public void Init(TilesMovement owner, int targetX, int targetY, bool isJump, bool withMove)
    {
        this.owner = owner;
        this.targetX = targetX;
        this.targetY = targetY;
        this.isJump = isJump;
        this.withMove = withMove;
    }

    private void Start()
    {
        Vector2 deltaPos = owner.transform.position - transform.position;
        Vector2 raycastPos = Vector2.MoveTowards(transform.position, owner.transform.position, 1);
        bool nearTheOwner = ((raycastPos - (Vector2)owner.transform.position).magnitude) <= 1;
        bool isCantGoStraightToOwner = Physics2D.Raycast((nearTheOwner ? transform.position : raycastPos), deltaPos.normalized, deltaPos.magnitude - 2, figuresMask);
        if (isCantGoStraightToOwner && !nearTheOwner && !isJump) Destroy(gameObject);

        onFigure = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.one, 0f, figuresMask).collider;
        if (onFigure == null) Destroy(gameObject);
    }

    private void OnMouseDown()
    {
        if (Input.GetMouseButtonDown(0))
        {
            owner.MoveToTile(targetX, targetY, withMove);
            onFigure.GetComponent<Health>().DealDamage(1);
        }
    }
}
