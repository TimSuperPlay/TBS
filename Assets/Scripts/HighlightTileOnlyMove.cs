using UnityEngine;

public class HighlightTileOnlyMove : MonoBehaviour
{
    private TilesMovement owner;
    private int targetX;
    private int targetY;
    private bool isJump;
    private bool isBlocked = true;
    private Collider2D onFigure;
    [SerializeField] LayerMask figuresMask;

    public void Init(TilesMovement owner, int targetX, int targetY, bool isJump)
    {
        this.owner = owner;
        this.targetX = targetX;
        this.targetY = targetY;
        this.isJump = isJump;
    }

    void Start()
    {
        if (owner == null) { Destroy(gameObject); return; }
        Vector2 deltaPos = owner.transform.position - transform.position;
        Vector2 raycastPos = Vector2.MoveTowards(transform.position, owner.transform.position, 1);
        bool nearTheOwner = ((raycastPos - (Vector2)owner.transform.position).magnitude) <= 1;
        bool isCantGoStraightToOwner = Physics2D.Raycast((nearTheOwner ? transform.position : raycastPos), deltaPos.normalized, deltaPos.magnitude - 2, figuresMask);
        if (isCantGoStraightToOwner && !nearTheOwner && !isJump) { Destroy(gameObject); return; }


        onFigure = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.one, 0f, figuresMask).collider;
        if (onFigure != null) { Destroy(gameObject); return; }
        isBlocked = false;
    }

    private void OnMouseDown()
    {
        if (owner == null || !owner.CanLocalInput || isBlocked) return;
        if (Input.GetMouseButtonDown(0))
        {
            isBlocked = true;
            owner.MoveToTile(targetX, targetY);
        }
    }
}
