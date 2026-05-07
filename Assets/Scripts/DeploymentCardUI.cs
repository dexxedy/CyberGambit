using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Карточка юнита для фазы расстановки: перетащить на поле — спавн в клетке под курсором.
/// </summary>
public class DeploymentCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;

    private ArmyDeploymentController controller;
    private int offerIndex;
    private Canvas rootCanvas;
    private RectTransform dragGhost;

    /// <summary> Создание карточки из кода (без префаба): назначить ссылки на Image/TMP. </summary>
    public void SetRuntimeRefs(Image icon, TMP_Text title, TMP_Text cost)
    {
        iconImage = icon;
        titleText = title;
        costText = cost;
    }

    public void Setup(ArmyDeploymentController ctrl, int index, DeploymentOfferEntry offer)
    {
        controller = ctrl;
        offerIndex = index;

        if (titleText != null)
            titleText.text = offer.unitPrefab != null ? offer.unitPrefab.name.Replace("(Clone)", "").Trim() : "?";
        if (costText != null)
            costText.text = offer.costPoints.ToString();

        Unit preview = offer.unitPrefab != null ? offer.unitPrefab.GetComponent<Unit>() : null;
        if (preview != null && iconImage != null && preview.GetTacticalPortrait() != null)
            iconImage.sprite = preview.GetTacticalPortrait();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || GameManager.Instance == null || !GameManager.Instance.IsArmyDeploymentPhase())
            return;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        if (dragGhost == null)
        {
            GameObject go = new GameObject("DeploymentDragGhost");
            go.transform.SetParent(rootCanvas.transform, false);
            dragGhost = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.6f);
            if (iconImage != null && iconImage.sprite != null)
                img.sprite = iconImage.sprite;
            RectTransform selfRt = (RectTransform)transform;
            dragGhost.sizeDelta = selfRt.sizeDelta;
            dragGhost.pivot = selfRt.pivot;
        }

        dragGhost.gameObject.SetActive(true);
        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateGhostPosition(eventData);
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (dragGhost == null || rootCanvas == null) return;

        RectTransform canvasRt = rootCanvas.transform as RectTransform;
        Camera uiCam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, eventData.position, uiCam, out Vector2 localPoint))
        {
            dragGhost.SetParent(canvasRt, false);
            dragGhost.anchoredPosition = localPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        if (controller == null || GameManager.Instance == null || !GameManager.Instance.IsArmyDeploymentPhase())
            return;

        Vector2 screenPos = eventData.position;

        // Снятие на панель «руки» — отмена без спавна (карточка возвращается визуально сама)
        RectTransform drop = controller.GetReturnDropZone();
        if (drop != null &&
            RectTransformUtility.RectangleContainsScreenPoint(drop, screenPos, rootCanvas != null ? rootCanvas.worldCamera : null))
        {
            return;
        }

        if (ArmyDeploymentController.TryScreenPointToGridCell(screenPos, out Vector2Int cell))
            controller.TryPlaceOfferAtGridCell(offerIndex, cell);
    }
}
