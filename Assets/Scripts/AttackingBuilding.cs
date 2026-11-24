using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class AttackingBuilding : MonoBehaviour
{
    [SerializeField]
    private int radius;
    [SerializeField]
    private float damage;
    [SerializeField]
    private TurnManager.Team team;
    [SerializeField]
    private bool isHitAll;
    private TurnManager turnManager;
    public TurnManager.Team Team
    {
        get
        {
            return team;
        }
    }

    public void ConfigureTeam(TurnManager.Team ownerTeam)
    {
        team = ownerTeam;
    }

    private void Start()
    {
        turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager != null)
        {
            turnManager.OnTurnStart.AddListener(TryAttack);
        }
    }

    private void OnDestroy()
    {
        if (turnManager != null)
        {
            turnManager.OnTurnStart.RemoveListener(TryAttack);
        }
    }

    private void TryAttack(TurnManager.Team currentTeam)
    {
        if (!LanGameState.CanSimulate || currentTeam != team)
        {
            return;
        }

        var targets = Physics2D.OverlapCircleAll(transform.position, radius).Select(targetCollider => targetCollider.GetComponentInParent<Health>())
            .Where(health => health != null && health.Current > 0 && health.gameObject != gameObject
            && LanWorldReplicator.TeamOf(health.gameObject) != (int)team)
            .Distinct()
            .OrderBy(health => Vector2.Distance(transform.position, health.transform.position))
            .ToArray();
        foreach (Health target in targets)
        {
            target.DealDamage(damage);
            if (!isHitAll)
            {
                break;
            }
        }
    }
}
