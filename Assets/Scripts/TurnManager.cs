using System;
using UnityEngine;
using UnityEngine.Events;

public class TurnManager : MonoBehaviour
{
    public enum Team
        {White,
        Black,
        Red,
        Blue}
    private Team currentTeam;
    public UnityEvent<Team> OnTurnStart, OnTurnEnd;
    private void Start()
    {
        currentTeam = Team.White;
        OnTurnStart.Invoke(currentTeam);
    }

    void Update()
    {
        if (Input.GetKeyDown("2")) EndTurn();
    }

    public void EndTurn()
    {
        OnTurnEnd.Invoke(currentTeam);
        currentTeam = (Team)(((int)currentTeam + 1)% Enum.GetValues(typeof(Team)).Length);
        OnTurnStart.Invoke(currentTeam);
    }
}
