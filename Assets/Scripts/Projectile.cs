using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] float speed;
    private Vector2 target;

    public void Init(Vector2 target)
    {
        this.target = target;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}
