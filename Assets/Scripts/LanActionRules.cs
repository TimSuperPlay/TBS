using System.Linq;
using UnityEngine;

public static class LanActionRules
{
    public static bool Resolve(
        TilesMovement figureMovement,
        LanFigureAction actionType,
        int targetX,
        int targetY,
        out Health targetHealth,
        out float attackDamage,
        out bool moveAfterAttack,
        out string rejectionReason
    )
    {
        targetHealth = null;
        attackDamage = 0;
        moveAfterAttack = actionType != LanFigureAction.AttackOnly;
        rejectionReason = "Недоступное действие или клетка.";
        if (figureMovement == null || figureMovement.Config == null || figureMovement.Grid == null)
        {
            return false;
        }

        Vector2Int offset = new Vector2Int(targetX, targetY) - figureMovement.Coordinates;
        if (offset == Vector2Int.zero)
        {
            return false;
        }

        bool canJump;
        GameObject highlightPrefab;
        if (actionType == LanFigureAction.MoveOnly || actionType == LanFigureAction.MoveOrAttack)
        {
            var movementRules = actionType == LanFigureAction.MoveOnly ? figureMovement.Config.OnlyMove : figureMovement.Config.TakeAndMove;
            var movementRule = movementRules?.FirstOrDefault(movementOption => movementOption.MoveCells == offset);
            if (movementRule == null)
            {
                return false;
            }

            canJump = movementRule.isJump;
            attackDamage = movementRule.damage;
            highlightPrefab = actionType == LanFigureAction.MoveOnly
                ? figureMovement.Config.OnlyMoveHighlight
                : figureMovement.Config.TakeAndMoveHighlight;
        }
        else if (actionType == LanFigureAction.AttackAndMove || actionType == LanFigureAction.AttackOnly)
        {
            bool requiresMovement = moveAfterAttack;
            var movementRule = figureMovement.Config.OnlyTake?.FirstOrDefault(movementOption =>
                movementOption.MoveCells == offset && movementOption.IsWithMove == requiresMovement);
            if (movementRule == null)
            {
                return false;
            }

            canJump = movementRule.isJump;
            attackDamage = movementRule.damage;
            highlightPrefab = moveAfterAttack ? figureMovement.Config.OnlyTakeWithMoveHighlight : figureMovement.Config.OnlyTakeNoMoveHighlight;
        }
        else
        {
            return false;
        }

        if (highlightPrefab == null || float.IsNaN(attackDamage) || float.IsInfinity(attackDamage) || attackDamage < 0)
        {
            return false;
        }

        int figuresMask = 0;
        if (highlightPrefab.TryGetComponent(out HighlightTileOnlyMove moveHighlight))
        {
            figuresMask = moveHighlight.FiguresMask;
        }
        else if (highlightPrefab.TryGetComponent(out HighlightTileTakeAndMove attackMoveHighlight))
        {
            figuresMask = attackMoveHighlight.FiguresMask;
        }
        else if (highlightPrefab.TryGetComponent(out HighlightTileOnlyTake attackHighlight))
        {
            figuresMask = attackHighlight.FiguresMask;
        }

        if (figuresMask == 0)
        {
            rejectionReason = "В префабе хайлайта не задан Figures Mask.";
            return false;
        }

        Vector3 destination = figureMovement.Grid.CoordinatesToPosition(targetX, targetY);
        var tilesGenerator = Object.FindFirstObjectByType<TilesGenerator>();
        if (tilesGenerator != null && !tilesGenerator.HasWorldTile(destination))
        {
            return false;
        }

        Physics2D.SyncTransforms();
        var targetColliders = Physics2D.OverlapBoxAll(destination, new Vector2(.9f, .9f), 0, figuresMask)
            .Where(colliderHit => !colliderHit.transform.IsChildOf(figureMovement.transform))
            .ToArray();
        foreach (var colliderHit in targetColliders)
        {
            Health hitHealth = colliderHit.GetComponentInParent<Health>();
            if (hitHealth == null || (targetHealth != null && targetHealth != hitHealth))
            {
                return false;
            }

            targetHealth = hitHealth;
        }

        if (actionType == LanFigureAction.MoveOnly && targetColliders.Length > 0)
        {
            return false;
        }

        if ((actionType == LanFigureAction.AttackOnly || actionType == LanFigureAction.AttackAndMove) && targetHealth == null)
        {
            return false;
        }

        if (targetHealth != null)
        {
            if (LanWorldReplicator.TeamOf(targetHealth.gameObject) == LanWorldReplicator.TeamOf(figureMovement.gameObject))
            {
                rejectionReason = "Нельзя атаковать свою команду.";
                return false;
            }

            if (targetHealth.Current <= 0 || (moveAfterAttack && targetHealth.Current > attackDamage))
            {
                return false;
            }
        }

        if (!canJump)
        {
            Vector2 directionToTarget = destination - figureMovement.transform.position;
            foreach (var colliderHit in Physics2D.RaycastAll(
                figureMovement.transform.position,
                directionToTarget.normalized,
                directionToTarget.magnitude,
                figuresMask
            ))
            {
                if (colliderHit.transform.IsChildOf(figureMovement.transform))
                {
                    continue;
                }

                if (targetHealth != null && colliderHit.transform.IsChildOf(targetHealth.transform))
                {
                    continue;
                }

                return false;
            }
        }

        rejectionReason = string.Empty;
        return true;
    }
}
