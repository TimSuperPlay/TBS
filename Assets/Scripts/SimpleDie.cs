using UnityEngine;

public class SimpleDie : MonoBehaviour
{
    [SerializeField] ParticleSystem particles;
    [SerializeField] Health health;
    void Start()
    {
        health.OnDie += Die;
    }

    // Update is called once per frame
    void Die(Health _)
    {
        if(particles != null) particles.Play();
        Destroy(gameObject);
    }
}
