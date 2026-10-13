using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] private float max;
    private float current;
    public UnityAction OnDamage, OnHeal, OnChange;
    public UnityAction<Health> OnDie;

    public float Current { get => current; }
    public float Max { get => max; }
    public float Normalized { get => current / max; }
    void Start()
    {
        current = max;
    }
    public void DealDamage(float damage)
    {
        if (damage < 0) Debug.LogError("Damage is negative!");
        current -= damage;
        OnDamage?.Invoke();
        OnChange?.Invoke();
        if (current <= 0) OnDie?.Invoke(this);
    }

    public void DealHeal(float heal)
    {
        if (heal < 0) Debug.LogError("Heal is negative!");
        current += heal;
        OnHeal?.Invoke();
        OnChange?.Invoke();
        if (current >= max) current = max;
    }
}
