using UnityEngine;
using UnityEngine.Rendering;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;
    [SerializeField] private SpriteRenderer sprite;
    private Transform myTransform;
    private float timePased;
    private int index;
    void Start()
    {
        index = 0;
        myTransform = GetComponent<Transform>();
        sprite.color = config.Color;
    }

    private void Update()
    {
        if (timePased >= config.movements[index].TimeSeconds)
        {
            index++;
            timePased = 0;
            //if (index >= config.movements.Length) index = 0;
        }
        if (index >= config.movements.Length) index = 0;
        myTransform.position += config.movements[index].vector * config.movements[index].Speed;
        timePased += Time.deltaTime;
    }
}
