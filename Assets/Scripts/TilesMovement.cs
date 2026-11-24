using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

public class TilesMovement : MonoBehaviour
{
    public UnityAction OnMove;
    public UnityAction OnVisualMovementEnd;
    private StatesStorage statesStorage;
    private int currentX;
    private int currentY;
    private TilesController tilesController;
    [SerializeField]
    [FormerlySerializedAs("config")]
    private FiguresConfig figureConfig;
    [Header("visual settings")]
    [SerializeField]
    [FormerlySerializedAs("visual")]
    private GameObject figureVisual;
    [SerializeField]
    [FormerlySerializedAs("visualMovementSpeed")]
    private float movementAnimationSpeed;
    [SerializeField]
    [FormerlySerializedAs("visualMovementCurve")]
    private AnimationCurve movementCurve;
    [SerializeField]
    [FormerlySerializedAs("visualJumpCurve")]
    private AnimationCurve jumpCurve;
    [SerializeField]
    private ParticleSystem landingParticles;
    [Header("NoMove visual settings")]
    [SerializeField]
    [FormerlySerializedAs("shotingParticles")]
    private ParticleSystem shootingParticles;
    [SerializeField]
    private GameObject projectilePrefab;
    private bool isSelected;
    private bool isVisualMoving;
    private Vector3 animationStartPosition;
    private Vector3 animationEndPosition;
    private float movementProgress;
    private Vector3 visualDefaultPosition;
    private bool isInitialized;
    private bool isAttackAnimation;
    public bool LastActionMoved { get; private set; }

    private static TilesMovement selectedFigure;
    public FiguresConfig Config
    {
        get
        {
            return figureConfig;
        }
    }

    public TilesController Grid
    {
        get
        {
            EnsureInitialized();
            return tilesController;
        }
    }

    public Vector2Int Coordinates
    {
        get
        {
            EnsureInitialized();
            return new Vector2Int(currentX, currentY);
        }
    }

    public bool IsAnimating
    {
        get
        {
            return isVisualMoving;
        }
    }

    public float ActionDuration
    {
        get
        {
            if (isAttackAnimation)
            {
                return .3f;
            }

            if (figureVisual != null && figureVisual != gameObject)
            {
                return 1f / Mathf.Max(.5f, movementAnimationSpeed);
            }

            return .05f;
        }
    }

    private readonly List<GameObject> spawnedHighlights = new();
    public bool CanLocalInput
    {
        get
        {
            return LanMultiplayer.Instance == null || !LanMultiplayer.Instance.UsesNetworkRules
                || LanMultiplayer.Instance.CanLocalFigureAct(this);
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        statesStorage = FindFirstObjectByType<StatesStorage>();
        tilesController = FindFirstObjectByType<TilesController>();
        if (tilesController == null)
        {
            return;
        }

        Vector2Int currentCell = tilesController.PositionToCordinats(transform.position);
        currentX = currentCell.x;
        currentY = currentCell.y;
        if (figureVisual != null)
        {
            visualDefaultPosition = figureVisual.transform.localPosition;
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (!isVisualMoving)
        {
            return;
        }

        movementProgress = Mathf.Min(1, movementProgress + Time.deltaTime / ActionDuration);
        float movementAmount = movementCurve != null && movementCurve.length > 0 ? movementCurve.Evaluate(movementProgress) : movementProgress;
        float jumpHeight = jumpCurve != null && jumpCurve.length > 0 ? jumpCurve.Evaluate(movementProgress) : 0;
        if (isAttackAnimation)
        {
            float attackOffset = Mathf.Sin(movementProgress * Mathf.PI);
            figureVisual.transform.localPosition = visualDefaultPosition + animationStartPosition * attackOffset;
        }
        else
        {
            Vector3 movementPosition = Vector3.LerpUnclamped(animationStartPosition, animationEndPosition, movementAmount);
            figureVisual.transform.localPosition = movementPosition + Vector3.up * jumpHeight;
        }

        if (movementProgress < 1)
        {
            return;
        }

        figureVisual.transform.localPosition = animationEndPosition;
        isVisualMoving = false;
        if (!isAttackAnimation)
        {
            landingParticles?.Play();
        }

        OnVisualMovementEnd?.Invoke();
    }

    private void Select()
    {
        if (!CanLocalInput || isVisualMoving || figureConfig == null || Grid == null)
        {
            return;
        }

        if (selectedFigure != null && selectedFigure != this)
        {
            selectedFigure.UnSelect();
        }

        selectedFigure = this;
        Debug.Log("selected");
        if (statesStorage != null)
        {
            statesStorage.isSelected = true;
        }

        isSelected = true;
        foreach (var movementRule in figureConfig.OnlyMove ?? Array.Empty<FiguresConfig.MoveOrTake>())
        {
            if (figureConfig.OnlyMoveHighlight == null || figureConfig.OnlyMoveHighlight.GetComponent<HighlightTileOnlyMove>() == null)
            {
                continue;
            }

            int targetX = movementRule.MoveCells.x + currentX;
            int targetY = movementRule.MoveCells.y + currentY;
            GameObject spawnedHighlight = Instantiate(
                figureConfig.OnlyMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
            );
            HighlightTileOnlyMove highlightComponent = spawnedHighlight.GetComponent<HighlightTileOnlyMove>();
            highlightComponent.Init(this, targetX, targetY, movementRule.isJump);
            spawnedHighlights.Add(spawnedHighlight);
        }

        foreach (var movementRule in figureConfig.TakeAndMove ?? Array.Empty<FiguresConfig.MoveOrTake>())
        {
            if (figureConfig.TakeAndMoveHighlight == null
                || figureConfig.TakeAndMoveHighlight.GetComponent<HighlightTileTakeAndMove>() == null)
            {
                continue;
            }

            int targetX = movementRule.MoveCells.x + currentX;
            int targetY = movementRule.MoveCells.y + currentY;
            GameObject spawnedHighlight = Instantiate(
                figureConfig.TakeAndMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
            );
            HighlightTileTakeAndMove highlightComponent = spawnedHighlight.GetComponent<HighlightTileTakeAndMove>();
            highlightComponent.Init(this, targetX, targetY, movementRule.isJump, movementRule.damage);
            spawnedHighlights.Add(spawnedHighlight);
        }

        foreach (var movementRule in figureConfig.OnlyTake ?? Array.Empty<FiguresConfig.OnlyAttack>())
        {
            GameObject highlightPrefab = movementRule.IsWithMove ? figureConfig.OnlyTakeWithMoveHighlight : figureConfig.OnlyTakeNoMoveHighlight;
            if (highlightPrefab == null || highlightPrefab.GetComponent<HighlightTileOnlyTake>() == null)
            {
                continue;
            }

            int targetX = movementRule.MoveCells.x + currentX;
            int targetY = movementRule.MoveCells.y + currentY;
            GameObject spawnedHighlight = Instantiate(
                movementRule.IsWithMove ? figureConfig.OnlyTakeWithMoveHighlight : figureConfig.OnlyTakeNoMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
            );
            HighlightTileOnlyTake highlightComponent = spawnedHighlight.GetComponent<HighlightTileOnlyTake>();
            highlightComponent.Init(this, targetX, targetY, movementRule.isJump, movementRule.IsWithMove, movementRule.damage);
            spawnedHighlights.Add(spawnedHighlight);
        }
    }

    public void UnSelect()
    {
        isSelected = false;
        if (selectedFigure == this)
        {
            selectedFigure = null;
            if (statesStorage != null)
            {
                statesStorage.isSelected = false;
            }
        }

        foreach (GameObject spawnedHighlight in spawnedHighlights)
        {
            Destroy(spawnedHighlight);
        }

        spawnedHighlights.Clear();
    }

    public void RequestAction(LanFigureAction actionType, int targetX, int targetY)
    {
        if (!CanLocalInput || isVisualMoving)
        {
            return;
        }

        if (LanGameState.UsesNetwork)
        {
            LanMultiplayer.Instance.NotifyLocalAction(this, actionType, targetX, targetY);
            UnSelect();
            return;
        }

        if (!LanActionRules.Resolve(
            this,
            actionType,
            targetX,
            targetY,
            out Health targetHealth,
            out float attackDamage,
            out bool moveAfterAttack,
            out _
        ))
        {
            return;
        }

        if (targetHealth != null)
        {
            targetHealth.DealDamage(attackDamage);
        }

        PlayAcceptedAction(targetX, targetY, moveAfterAttack);
    }

    public void MoveToTile(int targetX, int targetY, bool moveAfterAttack = true)
    {
        LanFigureAction actionType = moveAfterAttack ? LanFigureAction.MoveOrAttack : LanFigureAction.AttackOnly;
        if (moveAfterAttack && !LanActionRules.Resolve(this, actionType, targetX, targetY, out _, out _, out _, out _))
        {
            actionType = LanFigureAction.MoveOnly;
        }

        RequestAction(actionType, targetX, targetY);
    }

    public void PlayAcceptedAction(int targetX, int targetY, bool moveAfterAttack)
    {
        EnsureInitialized();
        if (tilesController == null)
        {
            return;
        }

        UnSelect();
        LastActionMoved = moveAfterAttack;
        isAttackAnimation = !moveAfterAttack;
        Vector3 destination = tilesController.CoordinatesToPosition(targetX, targetY);
        if (moveAfterAttack)
        {
            Vector3 originalVisualPosition = figureVisual != null ? figureVisual.transform.position : transform.position;
            currentX = targetX;
            currentY = targetY;
            transform.position = destination;
            isVisualMoving = figureVisual != null && figureVisual != gameObject && figureVisual.transform.parent != null;
            if (isVisualMoving)
            {
                animationStartPosition = figureVisual.transform.parent.InverseTransformPoint(originalVisualPosition);
                animationEndPosition = visualDefaultPosition;
                movementProgress = 0;
                figureVisual.transform.localPosition = animationStartPosition;
            }
        }
        else
        {
            isVisualMoving = figureVisual != null && figureVisual != gameObject && figureVisual.transform.parent != null;
            if (isVisualMoving)
            {
                animationStartPosition = figureVisual.transform.parent.InverseTransformVector((destination - transform.position).normalized * .2f);
                animationEndPosition = visualDefaultPosition;
                movementProgress = 0;
            }

            if (projectilePrefab != null)
            {
                GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
                projectile.GetComponent<Projectile>()?.Init(destination);
                Destroy(projectile, 10f);
            }

            shootingParticles?.Play();
        }

        Physics2D.SyncTransforms();
        if (moveAfterAttack && LanGameState.CanSimulate)
        {
            OnMove?.Invoke();
        }

        if (!isVisualMoving)
        {
            OnVisualMovementEnd?.Invoke();
        }
    }

    public void ApplyNetworkPosition(int targetX, int targetY)
    {
        EnsureInitialized();
        if (currentX == targetX && currentY == targetY)
        {
            return;
        }

        currentX = targetX;
        currentY = targetY;
        if (tilesController != null)
        {
            transform.position = tilesController.CoordinatesToPosition(currentX, currentY);
        }

        isVisualMoving = false;
        if (figureVisual != null && figureVisual != gameObject)
        {
            figureVisual.transform.localPosition = visualDefaultPosition;
        }

        UnSelect();
    }

    private void OnDisable()
    {
        UnSelect();
    }

    private void OnMouseDown()
    {
        if (!CanLocalInput)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isSelected)
            {
                UnSelect();
            }
            else
            {
                Select();
            }
        }
    }
}
