using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Для TextMeshProUGUI (таймер)
using System.Collections;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera tacticalCamera; // Камера вида сверху
    [SerializeField] private Camera actionCamera; // Камера от первого лица
    [SerializeField] private LayerMask unitLayer; // Слой для юнитов
    [SerializeField] private TextMeshProUGUI timerText; // UI текст для таймера
    [SerializeField] private Vector3 player1TacticalPosition; // Позиция камеры для Player1
    [SerializeField] private Vector3 player2TacticalPosition; // Позиция камеры для Player2
    [SerializeField] private Quaternion player1TacticalRotation = Quaternion.Euler(45f, -90f, 0f); // Ротация для Player1
    [SerializeField] private Quaternion player2TacticalRotation = Quaternion.Euler(45f, 90f, 0f); // Ротация для Player2

    private Unit currentUnit; // Текущий выбранный юнит
    private bool isActionMode = false; // Флаг режима (false - тактический, true - экшен)
    private float actionTime = 5f; // 5 секунд на ход
    private float remainingTime; // Остаток времени

    void Start()
    {
        tacticalCamera.enabled = true;
        actionCamera.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);

        if (timerText != null) timerText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isActionMode)
        {
            if (timerText != null)
            {
                timerText.text = $"Осталось: {remainingTime:F1} сек";
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopAllCoroutines();
                SwitchToTacticalMode();
            }
        }
        else
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = tacticalCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitLayer))
                {
                    Unit unit = hit.collider.GetComponent<Unit>();
                    if (unit != null && unit.owner == GameManager.Instance.currentPlayer)
                    {
                        SwitchToActionMode(unit);
                    }
                    else
                    {
                        Debug.Log("Нельзя выбрать: не твой юнит или не твой ход!");
                    }
                }
            }
        }
    }

    private void SwitchToActionMode(Unit unit)
    {
        if (currentUnit != null)
        {
            currentUnit.SetControlled(false);
        }

        currentUnit = unit;
        currentUnit.SetControlled(true);

        actionCamera.transform.SetParent(unit.cameraAttachPoint);
        actionCamera.transform.localPosition = Vector3.zero;
        actionCamera.transform.localRotation = Quaternion.identity;

        tacticalCamera.enabled = false;
        actionCamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isActionMode = true;

        remainingTime = actionTime;
        if (timerText != null) timerText.gameObject.SetActive(true);
        StartCoroutine(ActionTimer());
    }

    private IEnumerator ActionTimer()
    {
        while (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;
            yield return null;
        }
        SwitchToTacticalMode();
    }

    private void SwitchToTacticalMode()
    {
        if (currentUnit != null)
        {
            currentUnit.ResetAnimation();
            currentUnit.SetControlled(false);
            currentUnit = null;
        }

        actionCamera.transform.SetParent(null);

        tacticalCamera.enabled = true;
        actionCamera.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isActionMode = false;

        if (timerText != null) timerText.gameObject.SetActive(false);

        GameManager.Instance.SwitchTurn();
        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
    }

    private void SetTacticalCameraPosition(Player player)
    {
        if (player == Player.Player1)
        {
            tacticalCamera.transform.position = player1TacticalPosition;
            tacticalCamera.transform.rotation = player1TacticalRotation;
        }
        else
        {
            tacticalCamera.transform.position = player2TacticalPosition;
            tacticalCamera.transform.rotation = player2TacticalRotation;
        }
    }
}