using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("max")]
    private float maxHealth;
    private float currentHealth;
    private bool isInitialized;
    private bool isDead;
    public UnityAction OnDamage;
    public UnityAction OnHeal;
    public UnityAction OnChange;
    public UnityAction<Health> OnDie;
    public float Current
    {
        get
        {
            EnsureInitialized();
            return currentHealth;
        }
    }

    public float Max
    {
        get
        {
            return maxHealth;
        }
    }

    public float Normalized
    {
        get
        {
            return maxHealth > 0 ? Current / maxHealth : 0;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        currentHealth = Mathf.Max(0, maxHealth);
    }

    public void DealDamage(float damageAmount)
    {
        if (!LanGameState.CanSimulate || float.IsNaN(damageAmount) || float.IsInfinity(damageAmount) || damageAmount < 0)
        {
            return;
        }

        EnsureInitialized();
        if (isDead)
        {
            return;
        }

        SetCurrent(Mathf.Max(0, currentHealth - damageAmount));
    }

    public void DealHeal(float healingAmount)
    {
        if (!LanGameState.CanSimulate || float.IsNaN(healingAmount) || float.IsInfinity(healingAmount) || healingAmount < 0)
        {
            return;
        }

        EnsureInitialized();
        if (isDead)
        {
            return;
        }

        SetCurrent(Mathf.Min(maxHealth, currentHealth + healingAmount));
    }

    public void ApplyNetworkHealth(float healthValue)
    {
        EnsureInitialized();
        SetCurrent(Mathf.Clamp(healthValue, 0, Mathf.Max(0, maxHealth)));
    }

    private void SetCurrent(float healthValue)
    {
        float previousHealth = currentHealth;
        currentHealth = healthValue;
        if (currentHealth < previousHealth)
        {
            OnDamage?.Invoke();
        }

        if (currentHealth > previousHealth)
        {
            OnHeal?.Invoke();
        }

        if (currentHealth != previousHealth)
        {
            OnChange?.Invoke();
        }

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            OnDie?.Invoke(this);
            if (LanGameState.UsesNetwork)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
