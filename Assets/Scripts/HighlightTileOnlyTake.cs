using UnityEngine;

public class HighlightTileOnlyTake : MonoBehaviour
{
    private TilesMovement owner;
    private bool isBlocked;
    private int targetX;
    private int targetY;
    private bool isJump;
    private bool withMove;
    private float damage;
    private Collider2D onFigure;
    [SerializeField] LayerMask figuresMask;

    public void Init(TilesMovement owner, int targetX, int targetY, bool isJump, bool withMove, float damage)
    {
        this.owner = owner;
        this.targetX = targetX;
        this.targetY = targetY;
        this.isJump = isJump;
        this.withMove = withMove;
        this.damage = damage;
    }

    private void Start()
    {
        if (owner == null) { Destroy(gameObject); return; }
        Vector2 deltaPos = owner.transform.position - transform.position;
        Vector2 raycastPos = Vector2.MoveTowards(transform.position, owner.transform.position, 1);
        bool nearTheOwner = ((raycastPos - (Vector2)owner.transform.position).magnitude) <= 1;
        bool isCantGoStraightToOwner = Physics2D.Raycast((nearTheOwner ? transform.position : raycastPos), deltaPos.normalized, deltaPos.magnitude - 2, figuresMask);
        if (isCantGoStraightToOwner && !nearTheOwner && !isJump)
        {
            isBlocked = true;
            Destroy(gameObject);
            return;
        }

        onFigure = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.one, 0f, figuresMask).collider;
        if (onFigure == null) { Destroy(gameObject); return; }
        Health targetHealth = onFigure.GetComponent<Health>();
        if (targetHealth == null) { Destroy(gameObject); return; }
        if (!withMove) return;
        if (targetHealth.Current <= damage) return;
        isBlocked = true;
        SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.color = new(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0.5f);
    }

    private void OnMouseDown()
    {
        if (owner == null || !owner.CanLocalInput) return;
        if (LanMultiplayer.Instance != null && LanMultiplayer.Instance.UsesNetworkRules && !withMove) return;
        if (isBlocked || onFigure == null) return;
        Health targetHealth = onFigure.GetComponent<Health>();
        if (targetHealth == null || (withMove && targetHealth.Current > damage)) return;
        if (Input.GetMouseButtonDown(0))
        {
            isBlocked = true;
            owner.MoveToTile(targetX, targetY, withMove);
            targetHealth.DealDamage(damage);
        }
    }
}
