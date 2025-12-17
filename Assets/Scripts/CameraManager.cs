using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Для TextMeshProUGUI (таймер)
using System.Collections;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;
    [SerializeField] private Camera tacticalCamera; // Камера вида сверху
    [SerializeField] private Camera actionCamera; // Камера от первого лица
    [SerializeField] private LayerMask unitLayer; // Слой для юнитов
    [SerializeField] private TextMeshProUGUI timerText; // UI текст для таймера
    [SerializeField] private Vector3 player1TacticalPosition; // Позиция камеры для Player1
    [SerializeField] private Vector3 player2TacticalPosition; // Позиция камеры для Player2
    [SerializeField] private Quaternion player1TacticalRotation = Quaternion.Euler(45f, -90f, 0f); // Ротация для Player1
    [SerializeField] private Quaternion player2TacticalRotation = Quaternion.Euler(45f, 90f, 0f); // Ротация для Player2
    [Header("Tactical Camera Limits")] // <-- НОВЫЕ ПОЛЯ ОГРАНИЧЕНИЙ
    [SerializeField] private float minArenaX = 0f;  // Минимальная координата X
    [SerializeField] private float maxArenaX = 16f; // Максимальная координата X
    [SerializeField] private float minArenaZ = 0f;  // Минимальная координата Z
    [SerializeField] private float maxArenaZ = 16f; // Максимальная координата Z

    [SerializeField] private float flySpeed = 10f; 
    [SerializeField] private float rotationSpeed = 0.5f; 
    [SerializeField] private float minCameraHeight = 1f; 
    [SerializeField] private float maxCameraHeight = 15f;
    [SerializeField] private TextMeshProUGUI integrityText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI turnText;
    
    
    private Unit currentUnit; // Текущий выбранный юнит
    private bool isActionMode = false; // Флаг режима (false - тактический, true - экшен)
    private float actionTime = 5f; // 5 секунд на ход
    private float remainingTime; // Остаток времени
    private bool isGameEnded = false;

    void Awake()
    {
        Instance = this; // Singleton
    }

    void Start()
    {
        tacticalCamera.enabled = true;
        actionCamera.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        
        // Инициализируем Audio Listener на тактической камере (начальное состояние)
        SwitchAudioListener(tacticalCamera, actionCamera);

        timerText.gameObject.SetActive(false);
        turnText.gameObject.SetActive(true);
        turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";

    }

    void Update()
    {
        if (isGameEnded) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (GameManager.Instance.IsPaused())
                GameManager.Instance.ResumeGame();   // если уже на паузе — снимаем её
            else
                GameManager.Instance.PauseGame();    // если играем — ставим на паузу

            return; // чтобы дальше ничего не обрабатывалось в этом кадре
        }
        if (isActionMode)
        {
            if (timerText != null)
            {
                timerText.text = $"Осталось: {remainingTime:F1} сек";
            }
            if (integrityText != null)
            {
            // :F0 форматирует число до целого без нулей после запятой
            integrityText.text = $"Очки перемещения: {currentUnit.GetRuleIntegrityPoints():F0}";
            }
            if (healthText != null && currentUnit != null) 
            {
                healthText.text = $"HP: {currentUnit.GetHealth()}"; // Используем GetHealth()
            }

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                StopAllCoroutines();
                SwitchToTacticalMode();
            }
        }
        else
        {
            HandleTacticalCameraMovement();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = tacticalCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitLayer))
                {
                    Unit unit = hit.collider.GetComponent<Unit>();
                    if (unit != null && unit.owner == GameManager.Instance.currentPlayer)
                    {
                        // Воспроизводим звук выбора юнита
                        if (AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlayUnitSelect();
                        }
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

        // НОВОЕ: Показать разрешенные ходы при входе в экшен-режим
        if (GridHighlighter.Instance != null)
        {
            GridHighlighter.Instance.ShowAllowedMoves(unit);
        }

        actionCamera.transform.SetParent(unit.cameraAttachPoint);
        actionCamera.transform.localPosition = Vector3.zero;
        actionCamera.transform.localRotation = Quaternion.identity;

        tacticalCamera.enabled = false;
        actionCamera.enabled = true;
        
        // Переключаем Audio Listener на экшен-камеру
        SwitchAudioListener(actionCamera, tacticalCamera);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isActionMode = true;
        
        // Воспроизводим звук перехода в экшен-режим
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEnterActionMode();
        }

        remainingTime = actionTime;
        if (timerText != null) timerText.gameObject.SetActive(true);
        if (healthText != null) healthText.gameObject.SetActive(true);
        if (integrityText != null) integrityText.gameObject.SetActive(true);
        if (turnText != null) turnText.gameObject.SetActive(false);
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
        // Отменяем QTE, если он активен (например, если закончился таймер хода)
        if (QTESystem.Instance != null && QTESystem.Instance.IsQTEActive())
        {
            QTESystem.Instance.CancelQTE();
        }
        
        if (currentUnit != null)
        {
            currentUnit.ResetAnimation();
            currentUnit.SetControlled(false);
            // НЕ уничтожайте юнит здесь. Он должен быть уничтожен только в Unit.Die().
            currentUnit = null; 
        }
        GridHighlighter.Instance.ClearHighlights();
        // 🚨 ФИКС: Проверяем, существует ли actionCamera перед использованием
        if (actionCamera != null)
        {
            actionCamera.transform.SetParent(null);

            // Включаем/Выключаем только если они не были уничтожены
            if (tacticalCamera != null) tacticalCamera.enabled = true;
            actionCamera.enabled = false;
            
            // Переключаем Audio Listener на тактическую камеру
            SwitchAudioListener(tacticalCamera, actionCamera);
        }
        else
        {
            // 🚨 ФИКС: Если actionCamera была уничтожена, нужно включить tacticalCamera вручную.
            if (tacticalCamera != null)
            {
                tacticalCamera.enabled = true;
                // Переключаем Audio Listener на тактическую камеру
                SwitchAudioListener(tacticalCamera, null);
            }
            Debug.LogWarning("Action Camera была уничтожена. Переключение в Тактический режим.");
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isActionMode = false;

        if (timerText != null) timerText.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (integrityText != null) integrityText.gameObject.SetActive(false);
        // Если игра еще не завершена, переключаем ход
        GameManager.Instance.SwitchTurn();
        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        if (turnText != null) turnText.gameObject.SetActive(true);
        turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
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
    public void ForceSwitchToTacticalMode()
    {
        // Отменяем QTE, если он активен
        if (QTESystem.Instance != null && QTESystem.Instance.IsQTEActive())
        {
            QTESystem.Instance.CancelQTE();
        }
        
        // Сбрасываем таймер и сразу переходим в тактический режим
        StopAllCoroutines(); 
        SwitchToTacticalMode();
    }
    private void HandleTacticalCameraMovement()
    {
        // --- 1. Обработка движения WASD ---

        // Считываем WASD как float-значения (новая система ввода)
        Vector2 moveInput = new Vector2(
        Keyboard.current.dKey.IsActuated() ? 1f : (Keyboard.current.aKey.IsActuated() ? -1f : 0f),
        Keyboard.current.wKey.IsActuated() ? 1f : (Keyboard.current.sKey.IsActuated() ? -1f : 0f)
        ).normalized; 

        // 🚨 ФИКС: Используем полные 3D векторы forward/right камеры.
        // Движение вперед (W/S) теперь будет иметь Y-компонент, который обеспечивает подъем/спуск.
        Vector3 totalMovement = tacticalCamera.transform.forward * moveInput.y * flySpeed * Time.deltaTime
                              + tacticalCamera.transform.right * moveInput.x * flySpeed * Time.deltaTime;

        tacticalCamera.transform.position += totalMovement;


        // --- 2. Обработка вращения (правая кнопка мыши) ---

        // Вращение активируется при удержании правой кнопки мыши
        if (Mouse.current.rightButton.isPressed)
        {
            // 💡 Новая система ввода: скрываем курсор и читаем дельту
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 delta = Mouse.current.delta.ReadValue();

            // Вращение по Y (горизонталь)
            tacticalCamera.transform.Rotate(Vector3.up, delta.x * rotationSpeed, Space.World);

            // Вращение по X (вертикаль, вокруг локальной оси)
            // Мы вращаем сам объект камеры (или его родителя, если tacticalCamera - родитель)
            tacticalCamera.transform.Rotate(Vector3.left, delta.y * rotationSpeed, Space.Self); 
        }
        else if (!isActionMode)
        {
            // Возвращаем курсор в нормальное состояние, если мы не в режиме действия
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        // --- 3. Ограничение (Bounding) ---

        // ... (Ваша логика ограничения по gridBounds, minCameraHeight и maxCameraHeight)
        Vector3 pos = tacticalCamera.transform.position;

        // 🚨 ФИКС: Явное ограничение по X, Z и Y

        // Ограничение по X
        pos.x = Mathf.Clamp(pos.x, minArenaX, maxArenaX); 

        // Ограничение по Z
        pos.z = Mathf.Clamp(pos.z, minArenaZ, maxArenaZ);

        // Ограничение по Y (высота)
        pos.y = Mathf.Clamp(pos.y, minCameraHeight, maxCameraHeight);

        tacticalCamera.transform.position = pos;
    }
    public void OnGameOver()
    {
        StopAllCoroutines(); // Останавливаем таймеры
        isGameEnded = true; // Блокируем Update
        // Гарантируем, что курсор свободен
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    /// <summary>
    /// Возвращает текущий контролируемый юнит (для системы QTE и зон угрозы).
    /// </summary>
    /// <returns>Текущий контролируемый юнит или null, если никого не контролируют</returns>
    public Unit GetCurrentControlledUnit()
    {
        return currentUnit;
    }
    
    /// <summary>
    /// Возвращает тактическую камеру (для способностей, требующих выбора цели)
    /// </summary>
    /// <returns>Тактическая камера</returns>
    public Camera GetTacticalCamera()
    {
        return tacticalCamera;
    }
    
    /// <summary>
    /// Возвращает экшен-камеру (для способностей в экшен-режиме)
    /// </summary>
    /// <returns>Экшен-камера</returns>
    public Camera GetActionCamera()
    {
        return actionCamera;
    }
    
    public bool IsActionMode() => isActionMode;
    
    /// <summary>
    /// Переключает Audio Listener между камерами
    /// </summary>
    /// <param name="activeCamera">Камера, на которую переключаем Audio Listener</param>
    /// <param name="inactiveCamera">Камера, с которой отключаем Audio Listener</param>
    private void SwitchAudioListener(Camera activeCamera, Camera inactiveCamera)
    {
        // Сначала включаем Audio Listener на активной камере (чтобы не было разрыва)
        if (activeCamera != null)
        {
            AudioListener activeListener = activeCamera.GetComponent<AudioListener>();
            if (activeListener == null)
            {
                // Если Audio Listener отсутствует, создаем его
                activeListener = activeCamera.gameObject.AddComponent<AudioListener>();
            }
            activeListener.enabled = true;
        }
        
        // Затем отключаем Audio Listener на неактивной камере
        if (inactiveCamera != null)
        {
            AudioListener inactiveListener = inactiveCamera.GetComponent<AudioListener>();
            if (inactiveListener != null)
            {
                inactiveListener.enabled = false;
            }
        }
    }
    
}