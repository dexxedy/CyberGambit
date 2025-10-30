using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Для TextMeshProUGUI (таймер)
using System.Collections; // Для Coroutine

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
        // По умолчанию тактическая камера
        tacticalCamera.enabled = true;
        actionCamera.enabled = false;

        // Курсор видим в тактическом режиме
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Устанавливаем начальную позицию и ротацию камеры (для Player1)
        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);

        // Скрываем таймер изначально
        if (timerText != null) timerText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isActionMode)
        {
            // Обновляем таймер в UI
            if (timerText != null)
            {
                timerText.text = $"Осталось: {remainingTime:F1} сек";
            }

            // Возврат к тактической камере по ESC (опционально, можно убрать)
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                StopAllCoroutines();
                SwitchToTacticalMode();
            }
        }
        else
        {
            // Выбор юнита по клику мыши только в свой ход
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = tacticalCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitLayer))
                {
                    Unit unit = hit.collider.GetComponent<Unit>();
                    if (unit != null && unit.owner == GameManager.Instance.currentPlayer) // Только свои юниты!
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
        // Отключаем управление у предыдущего юнита, если он был
        if (currentUnit != null)
        {
            currentUnit.SetControlled(false);
        }

        // Устанавливаем новый юнит
        currentUnit = unit;
        currentUnit.SetControlled(true);

        // Прикрепляем камеру к точке юнита
        actionCamera.transform.SetParent(unit.cameraAttachPoint);
        actionCamera.transform.localPosition = Vector3.zero;
        actionCamera.transform.localRotation = Quaternion.identity;

        // Переключаем камеры
        tacticalCamera.enabled = false;
        actionCamera.enabled = true;

        // Блокируем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isActionMode = true;

        // Запускаем таймер
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
        // Отключаем управление у текущего юнита
        if (currentUnit != null)
        {
            currentUnit.SetControlled(false);
            currentUnit = null;
        }

        // Открепляем камеру
        actionCamera.transform.SetParent(null);

        // Переключаем камеры
        tacticalCamera.enabled = true;
        actionCamera.enabled = false;

        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isActionMode = false;

        // Скрываем таймер
        if (timerText != null) timerText.gameObject.SetActive(false);

        // Переход хода и смена позиции/ротации камеры
        GameManager.Instance.SwitchTurn();
        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
    }

    private void SetTacticalCameraPosition(Player player)
    {
        if (player == Player.Player1)
        {
            tacticalCamera.transform.position = player1TacticalPosition;
            tacticalCamera.transform.rotation = player1TacticalRotation; // Ротация для Player1
        }
        else
        {
            tacticalCamera.transform.position = player2TacticalPosition;
            tacticalCamera.transform.rotation = player2TacticalRotation; // Ротация для Player2
        }
    }
}