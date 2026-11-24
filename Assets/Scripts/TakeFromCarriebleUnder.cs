using UnityEngine;
using UnityEngine.Serialization;

public class TakeFromCarriebleUnder : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("layerMask")]
    private LayerMask resourceLayers;
    [SerializeField]
    [FormerlySerializedAs("myCarrieble")]
    private Carrieble inventory;
    [SerializeField]
    [FormerlySerializedAs("tilesMovement")]
    private TilesMovement figureMovement;
    private void Start()
    {
        if (figureMovement != null)
        {
            figureMovement.OnVisualMovementEnd += TryToTake;
        }
    }

    private void OnDestroy()
    {
        if (figureMovement != null)
        {
            figureMovement.OnVisualMovementEnd -= TryToTake;
        }
    }

    private void TryToTake()
    {
        if (!LanGameState.CanSimulate || figureMovement == null || !figureMovement.LastActionMoved || inventory == null)
        {
            return;
        }

        foreach (var targetCollider in Physics2D.OverlapBoxAll(transform.position, new Vector2(.9f, .9f), 0, resourceLayers))
        {
            Carrieble sourceInventory = targetCollider.GetComponentInParent<Carrieble>();
            if (sourceInventory == null || sourceInventory == inventory || sourceInventory.items.Count == 0)
            {
                continue;
            }

            if (inventory.TryAdd(sourceInventory.items[0]))
            {
                sourceInventory.Delete(0);
            }

            return;
        }
    }
}
