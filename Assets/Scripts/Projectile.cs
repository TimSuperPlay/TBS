using UnityEngine;
using UnityEngine.Serialization;

public class Projectile : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("speed")]
    private float movementSpeed, destroyDelay;
    private Vector3 targetPosition;
    private bool isInitialized;
    public float TravelSpeed
    {
        get
        {
            return movementSpeed > 0 ? movementSpeed : 8f;
        }
    }

    public void Init(Vector2 targetPosition)
    {
        this.targetPosition = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, TravelSpeed * Time.deltaTime);
        if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
        {
            Destroy(gameObject.GetComponent<SpriteRenderer>());
            Destroy(gameObject, destroyDelay);
        }
    }
}
