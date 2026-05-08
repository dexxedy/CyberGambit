using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Полноэкранная иконка юнита (карточка): WorldToScreenPoint задаёт контроллером.
/// </summary>
public class TacticalWorldUnitIcon : MonoBehaviour, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("Bindings (назначить в префабе или заполняет контроллер)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Image frameImage;

    [Header("Цвет рамки")]
    [SerializeField] private Color allyFrameColor = new Color(0.25f, 0.65f, 1f, 1f);
    [SerializeField] private Color enemyFrameColor = new Color(1f, 0.35f, 0.25f, 1f);

    private Unit unit;
    private TacticalWorldIconsController owner;

    public Unit TargetUnit => unit;

    public void SetSquareSize(float sizePixels)
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null) return;
        float s = Mathf.Max(0f, sizePixels);
        rt.sizeDelta = new Vector2(s, s);
    }

    public void Setup(Unit targetUnit, TacticalWorldIconsController controller)
    {
        unit = targetUnit;
        owner = controller;
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (unit == null) return;

        if (portraitImage != null)
        {
            Sprite p = unit.GetTacticalPortrait();
            portraitImage.sprite = p;
            portraitImage.enabled = p != null;
        }

        if (frameImage != null && GameManager.Instance != null)
        {
            bool ally = IsFriendlyUnit(unit);
            frameImage.color = ally ? allyFrameColor : enemyFrameColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (unit == null || owner == null) return;
        owner.NotifyIconClicked(unit);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (unit == null) return;
        owner?.NotifyIconPointerEnter(unit);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.NotifyIconPointerExit(unit);
    }

    private static bool IsFriendlyUnit(Unit unit)
    {
        if (GameManager.Instance == null) return true;
        if (GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot)
            return unit.owner == Player.Player1;
        return unit.owner == GameManager.Instance.currentPlayer;
    }
}
