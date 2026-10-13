using UnityEngine;

public class TakeFromCarriebleUnder : MonoBehaviour
{
    [SerializeField] LayerMask layerMask;
    [SerializeField] Carrieble myCarrieble;
    [SerializeField] TilesMovement tilesMovement;
    void Start()
    {
        tilesMovement.OnVisualMovementEnd += TryToTake;
    }

    void TryToTake()
    {
        RaycastHit2D hit = Physics2D.BoxCast(transform.position, new(0.9f, 0.9f), 0, Vector2.up, 0, layerMask);
        Debug.Log((bool)hit);
        if (!hit) return;
        Carrieble carrieble = hit.collider.GetComponent<Carrieble>();
        if (carrieble == null) return;
        if (carrieble.items.Count < 1) return;
        myCarrieble.Add(carrieble.items[0]);
        carrieble.Delete(0);
    }
}
