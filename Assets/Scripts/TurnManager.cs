using System;
using UnityEngine;
using UnityEngine.Events;

public class TurnManager : MonoBehaviour
{
    public enum Team { White, Black, Red, Blue }
    private Team currentTeam;
    public UnityEvent<Team> OnTurnStart, OnTurnEnd;

    private static bool UsesNetworkRules()
    {
        return LanMultiplayer.Instance != null && LanMultiplayer.Instance.UsesNetworkRules;
    }

    private void Start()
    {
        currentTeam = Team.White;
        if (UsesNetworkRules()) return;
        OnTurnStart.Invoke(currentTeam);
    }

    private void Update()
    {
        if (UsesNetworkRules()) return;
        if (Input.GetKeyDown("2")) EndTurn();
    }

    public void EndTurn()
    {
        if (UsesNetworkRules()) return;
        OnTurnEnd.Invoke(currentTeam);
        currentTeam = (Team)(((int)currentTeam + 1) % Enum.GetValues(typeof(Team)).Length);
        OnTurnStart.Invoke(currentTeam);
    }
}
