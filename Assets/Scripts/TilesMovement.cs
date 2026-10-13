using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;

public class TilesMovement : MonoBehaviour
{
    public UnityAction OnMove;
    public UnityAction OnVisualMovementEnd;
    private StatesStorage statesStorage;
    private int x, y;
    TilesController tilesController;
    [SerializeField] FiguresConfig config;
    [Header("visual settings")] 
    [SerializeField] GameObject visual;
    [SerializeField] float visualMovementSpeed;
    [SerializeField] AnimationCurve visualMovementCurve, visualJumpCurve;
    [SerializeField] ParticleSystem landingParticles;
    [Header("NoMove visual settings")]
    [SerializeField] ParticleSystem shotingParticles;
    [SerializeField] GameObject projectilePrefab;
    private bool selected, isVisualMoving;
    private Vector3 visualMovementOrigin, visualMovementEnd;
    private float visualMovementprocent, selectedTimer;
    private readonly List<GameObject> spawnedHighlights = new();

    void Start()
    {
        statesStorage = GameObject.FindGameObjectWithTag("GameManager").GetComponent<StatesStorage>();
        visualMovementprocent = 0;
        tilesController = GameObject.FindGameObjectWithTag("MapManager").GetComponent<TilesController>();
        Vector2Int pos = tilesController.PositionToCordinats(transform.position.x, transform.position.y);
        x = (int)Math.Round((double)pos.x);
        y = (int)Math.Round((double)pos.y);
    }

    void Update()
    {
        if (!selected) selectedTimer += Time.deltaTime;
        else selectedTimer = 0;
        if (selectedTimer > 0.9) statesStorage.isSelected = false;
        if (isVisualMoving /*&& visualMovementprocent < 1*/)
        {
            float currentJumpHeight = visualJumpCurve.Evaluate(visualMovementprocent);
            float evaluatedPercent = visualMovementCurve.Evaluate(visualMovementprocent);
            visual.transform.localPosition = new Vector3(
            Mathf.Lerp(visualMovementOrigin.x, visualMovementEnd.x, evaluatedPercent),
            Mathf.Lerp(visualMovementOrigin.y, visualMovementEnd.y, evaluatedPercent) + currentJumpHeight);
            //visual.transform.localScale = new(1 + currentJumpHeight/4, 1 + currentJumpHeight/4);

            visualMovementprocent += Time.deltaTime * visualMovementSpeed;

            if (visualMovementprocent >= 1)
            {
                landingParticles.Play();
                isVisualMoving = false;
                visual.transform.localPosition = visualMovementEnd;
                visualMovementprocent = 0;
                OnVisualMovementEnd?.Invoke();
            }

        }
    }
    void Select()
    {
        if (statesStorage.isSelected) return;
        Debug.Log("selected");
        statesStorage.isSelected = true; //почему то не задается
        selected = true;
        foreach (var highlight in config.OnlyMove)
        {
            int targetX = highlight.MoveCells.x + x;
            int targetY = highlight.MoveCells.y + y;

            GameObject spawnedHighlight = Instantiate(
            config.OnlyMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
                );

            HighlightTileOnlyMove highlightTile = spawnedHighlight.GetComponent<HighlightTileOnlyMove>();

            highlightTile.Init(this, targetX, targetY, highlight.isJump);

            spawnedHighlights.Add(spawnedHighlight);
        }


        foreach (var highlight in config.TakeAndMove)
        {
            int targetX = highlight.MoveCells.x + x;
            int targetY = highlight.MoveCells.y + y;

            GameObject spawnedHighlight = Instantiate(
            config.TakeAndMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
                );

            HighlightTileTakeAndMove highlightTile = spawnedHighlight.GetComponent<HighlightTileTakeAndMove>();

            highlightTile.Init(this, targetX, targetY, highlight.isJump);

            spawnedHighlights.Add(spawnedHighlight);
        }


        foreach (var highlight in config.OnlyTake)
        {
            int targetX = highlight.MoveCells.x + x;
            int targetY = highlight.MoveCells.y + y;

            GameObject spawnedHighlight = Instantiate(
            highlight.IsWithMove ? config.OnlyTakeWithMoveHighlight : config.OnlyTakeNoMoveHighlight,
                tilesController.CoordinatesToPosition(targetX, targetY),
                Quaternion.identity
                );

            HighlightTileOnlyTake highlightTile = spawnedHighlight.GetComponent<HighlightTileOnlyTake>();

            highlightTile.Init(this, targetX, targetY, highlight.isJump, highlight.IsWithMove);

            spawnedHighlights.Add(spawnedHighlight);
        }
    }

    public void UnSelect()
    {
        selected = false;
        foreach (GameObject spawnedHighlight in spawnedHighlights)
        {
            Destroy(spawnedHighlight);
        }
        spawnedHighlights.Clear();
    }

    public void MoveToTile(int targetX, int targetY, bool isWithMove = true)
    {
        if(isWithMove)
        {
            Vector3 worldOrigin = visual.transform.position;
            x = targetX;
            y = targetY;
            transform.position = tilesController.CoordinatesToPosition(x, y);

            visualMovementOrigin = visual.transform.parent.InverseTransformPoint(worldOrigin);
            visualMovementEnd = new Vector3(0, 0, 0);
            isVisualMoving = true;
            visualMovementprocent = 0f;
        }

        OnMove?.Invoke();

        UnSelect();
    }

    private void OnMouseDown()
    {
        if(!isActiveAndEnabled) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (selected) UnSelect();
            else Select();
        }
    }
}
