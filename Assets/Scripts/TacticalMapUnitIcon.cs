using UnityEngine;
using UnityEngine.EventSystems;

public class TacticalMapUnitIcon : MonoBehaviour, IPointerClickHandler
{
    private Unit unit;
    private TacticalMapUIController ownerController;

    public void Initialize(Unit targetUnit, TacticalMapUIController controller)
    {
        unit = targetUnit;
        ownerController = controller;
    }

    public Unit GetUnit()
    {
        return unit;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (unit == null || ownerController == null) return;
        ownerController.OnUnitIconClicked(unit);
    }
}
