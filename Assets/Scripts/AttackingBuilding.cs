using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class AttackingBuilding : MonoBehaviour
{
    [SerializeField] int radius;
    [SerializeField] float damage;
    [SerializeField] TurnManager.Team team;
    [SerializeField] bool isHitAll;
    private TurnManager turnManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        turnManager = GameObject.FindGameObjectWithTag("TurnManager").GetComponent<TurnManager>();
        turnManager.OnTurnStart.AddListener(TryAttack);
    }

    // Update is called once per frame
    void TryAttack(TurnManager.Team currentTeam)
    {
        if (currentTeam != team) return;
        List<RaycastHit2D> targets = new(Physics2D.CircleCastAll(transform.position, radius, Vector2.zero));
        targets.RemoveAll(hit => hit.transform.CompareTag(gameObject.tag));
        if (targets.Count <= 0) return;
        if (isHitAll)
        {
            foreach (RaycastHit2D hit in targets)
            {
                hit.transform.gameObject.GetComponent<Health>()?.DealDamage(damage);
            }
            return;
        }
        GameObject target = null;
        float lowestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in targets)
        {
            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance >= lowestDistance) continue;
            lowestDistance = distance;
            target = hit.transform.gameObject;
        }
        target.transform.gameObject.GetComponent<Health>()?.DealDamage(damage);
    }
}
