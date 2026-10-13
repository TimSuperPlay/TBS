using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BuildingController : MonoBehaviour
{
    [SerializeField] Button[] buttons;
    [SerializeField] Canvas canvas;
    [SerializeField] Vector2 offset;
    private RectTransform menuRectTransform, canvasRectTransform;
    private Vector2Int targetTilePosition;
    [SerializeField] PlacementController placementController;


    private void Awake()
    {
        menuRectTransform = GetComponent<RectTransform>();
        if (canvas != null) canvasRectTransform = canvas.GetComponent<RectTransform>();
    }
    public void Init(PlacementController placementController)
    {
        this.placementController = placementController;
        for (int i = 0; i < buttons.Length; i++)
        {
            int buttonIndex = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => OnBuildButtonClicked(buttonIndex));
        }
        Close();
    }

    public void Open(Vector2Int tilePosition, Vector2 screenPosition)
    {
        if (gameObject.activeSelf && targetTilePosition == tilePosition)
        {
            Close();
            return;
        }
        targetTilePosition = tilePosition;
        gameObject.SetActive(true);
        if (menuRectTransform == null) return;
        if (canvasRectTransform == null) return;
        Camera uiCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) uiCamera = canvas.worldCamera;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, uiCamera, out Vector2 localPosition);
        if (!converted) return;
         menuRectTransform.anchoredPosition = localPosition + offset;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnBuildButtonClicked(int buttonIndex)
    {
        placementController.PlaceBuilding(targetTilePosition, buttonIndex);
        Close();
    }
}
