using UnityEngine;
using UnityEngine.Serialization;

public class SimpleDie : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("particles")]
    private ParticleSystem deathParticles;
    [SerializeField]
    private Health health;
    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.OnDie += Die;
        }
    }

    private void Die(Health _)
    {
        if (deathParticles != null)
        {
            ParticleSystem deathEffect = Instantiate(deathParticles, transform.position, transform.rotation);
            foreach (Component component in deathEffect.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is ParticleSystem || component is ParticleSystemRenderer)
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }

                if (component is Renderer renderer)
                {
                    renderer.enabled = false;
                }

                if (component is Collider2D collider)
                {
                    collider.enabled = false;
                }

                Destroy(component);
            }

            deathEffect.Play();
            Destroy(deathEffect.gameObject, 5f);
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDie -= Die;
        }
    }
}
