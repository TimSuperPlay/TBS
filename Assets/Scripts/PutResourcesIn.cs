using UnityEngine;

public class PutResourcesIn : MonoBehaviour
{
    [SerializeField] Carrieble carrieble;
    [SerializeField] LayerMask layerMask;
    [SerializeField] TilesMovement tilesMovement;

    void Start()
    {
        tilesMovement.OnMove += TryToPut;
    }

    void TryToPut()
    {
        RaycastHit2D onSomething = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.zero, 0, layerMask);
        if (!onSomething) onSomething = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.zero, 0, layerMask);
        if (!onSomething) return;

        IResourceReceiver receiver = onSomething.collider.GetComponent<IResourceReceiver>();
        if (receiver == null) return;
        if (carrieble.items.Count <= 0) return;
        receiver.AddResource(carrieble.items[0]);
        carrieble.Delete(0);
    }

}