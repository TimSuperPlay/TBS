using UnityEngine;

public class TestMoveTowards : MonoBehaviour
{
    [SerializeField] Vector3 target;
    [SerializeField] float speed;

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}
