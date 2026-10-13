using System.Collections.Generic;
using UnityEngine;

public class TestTilesMovement : MonoBehaviour
{
    private int x, y;
    [SerializeField] TilesController tilesController;
    [SerializeField] Vector2Int[] highlights;
    [SerializeField] GameObject highlightObj;
    [SerializeField] string highlightsTag;
    private bool selected;
    private readonly List<GameObject> spawnedHighlights = new ();

    void Start()
    {
        x = 0;
        y = 0;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            y++;
        }        
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            y--;
        }        
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            x++;
        }        
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            x--;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (selected) UnSelect();
            else Select();
        }
        transform.position = tilesController.CoordinatesToPosition(x, y);
    }
    void Select()
    {
        selected = true;
        foreach (Vector2Int highlight in highlights)
        {
            int targetX = highlight.x + x;
            int targetY = highlight.y + y;

            GameObject spawnedHighlight = Instantiate(
            highlightObj, 
                tilesController.CoordinatesToPosition(targetX, targetY), 
                Quaternion.identity
                );

            HighlightTileOnlyMove highlightTile = spawnedHighlight.GetComponent<HighlightTileOnlyMove>();
            if (highlightTile == null) highlightTile = spawnedHighlight.AddComponent<HighlightTileOnlyMove>();

            //highlightTile.Init(this, targetX, targetY);

            spawnedHighlights.Add(spawnedHighlight);
        }
    }

    void UnSelect()
    {
        selected = false;
        foreach (GameObject spawnedHighlight in spawnedHighlights)
        {
            Destroy(spawnedHighlight);
        }
        spawnedHighlights.Clear();
    }

    public void MoveToTile(int targetX, int targetY)
    {
        x = targetX;
        y = targetY;
        transform.position = tilesController.CoordinatesToPosition(x, y);
        UnSelect();
    }
}
