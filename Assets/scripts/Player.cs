using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;
    [SerializeField] private SpriteRenderer sprite;
    void Start()
    {
        sprite.color = config.color;
    }
}
