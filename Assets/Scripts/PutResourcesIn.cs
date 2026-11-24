using UnityEngine;
using UnityEngine.Serialization;

public class PutResourcesIn : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("carrieble")]
    private Carrieble inventory;
    [SerializeField]
    [FormerlySerializedAs("layerMask")]
    private LayerMask resourceLayers;
    [SerializeField]
    [FormerlySerializedAs("tilesMovement")]
    private TilesMovement figureMovement;
    private void Start()
    {
        if (figureMovement != null)
        {
            figureMovement.OnMove += TryToPut;
        }
    }

    private void OnDestroy()
    {
        if (figureMovement != null)
        {
            figureMovement.OnMove -= TryToPut;
        }
    }

    private void TryToPut()
    {
        if (!LanGameState.CanSimulate || inventory == null || inventory.items.Count == 0)
        {
            return;
        }

        foreach (var targetCollider in Physics2D.OverlapBoxAll(transform.position, new Vector2(.9f, .9f), 0, resourceLayers))
        {
            if (targetCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            IResourceReceiver resourceReceiver = targetCollider.GetComponentInParent<IResourceReceiver>();
            if (resourceReceiver == null)
            {
                continue;
            }

            resourceReceiver.AddResource(inventory.items[0]);
            inventory.Delete(0);
            return;
        }
    }
}
