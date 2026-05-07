using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Маркер юнита, поставленного в фазе расстановки: ПКМ — вернуть в пул (очки возвращаются).
/// </summary>
[RequireComponent(typeof(Unit))]
public class DeploymentPlacedUnitMarker : MonoBehaviour
{
    private ArmyDeploymentController controller;
    private Unit unit;

    public void Initialize(ArmyDeploymentController ctrl)
    {
        controller = ctrl;
        unit = GetComponent<Unit>();

        EnsurePickCollider();
    }

    private void EnsurePickCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        var cap = gameObject.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 1f, 0f);
        cap.height = 2f;
        cap.radius = 0.45f;
    }

    void Update()
    {
        if (!enabled || controller == null || unit == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (GameManager.Instance.IsPaused()) return;
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;

        Camera cam = CameraManager.Instance != null ? CameraManager.Instance.GetTacticalCamera() : null;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 800f, ~0, QueryTriggerInteraction.Collide))
            return;

        Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
        if (hitUnit != unit) return;

        controller.RemoveDeployedUnit(unit);
    }
}
