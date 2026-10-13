using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FiguresConfig", menuName = "Scriptable Objects/FiguresConfig")]
public class FiguresConfig : ScriptableObject
{
    public MoveOrTake[] OnlyMove, TakeAndMove;
    public OnlyAttack[] OnlyTake;
    public GameObject OnlyMoveHighlight, TakeAndMoveHighlight, OnlyTakeWithMoveHighlight, OnlyTakeNoMoveHighlight;
    public bool IsCanSwim;

    [Serializable]
    public class MoveOrTake
    {
        public Vector2Int MoveCells;
        public bool isJump;
    }
    [Serializable]
    public class OnlyAttack
    {
        public Vector2Int MoveCells;
        public bool isJump, IsWithMove;
    }
}
