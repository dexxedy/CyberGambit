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
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private GameObject turnPanel; // Панель с текстом хода
    
    private Unit currentUnit; // Текущий выбранный юнит
    private bool isActionMode = false; // Флаг режима (false - тактический, true - экшен)
    private float actionTime = 5f; // 5 секунд на ход
    private float remainingTime; // Остаток времени
    private bool isGameEnded = false;
    private bool isTimerPaused = false; // Флаг паузы таймера (для QTE)
    private bool isFollowingBotUnit = false; // Флаг следования камеры за юнитом бота
    private bool isReturningCamera = false; // Флаг возврата камеры на исходную позицию
    private Vector3 savedTacticalPosition; // Сохраненная позиция тактической камеры
    private Quaternion savedTacticalRotation; // Сохраненная ротация тактической камеры
    private Coroutine followBotUnitCoroutine; // Корутина следования за юнитом бота
    private Coroutine returnCameraCoroutine; // Корутина возврата камеры

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

        // Скрываем панели статистики экшен-режима
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.HideStatsPanels();
        }
        
        if (turnPanel != null) turnPanel.SetActive(true);
        // Показываем "Ход: Бот" если игра против бота и сейчас ход Player2
        if (GameManager.Instance != null && GameManager.Instance.IsBotTurn())
        {
            turnText.text = "Ход: Бот";
        }
        else
        {
            turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
        }

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
            // Обновление статистики теперь происходит в ActionModeUI.Update()

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                StopAllCoroutines();
                SwitchToTacticalMode();
            }
        }
        else
        {
            // Проверяем, является ли текущий ход ботом
            if (GameManager.Instance != null && GameManager.Instance.IsBotTurn())
            {
                // Если ход бота, автоматически запускаем его ход (если еще не запущен)
                if (BotController.Instance != null && !BotController.Instance.IsExecutingTurn())
                {
                    BotController.Instance.ExecuteBotTurn();
                }
            }
            else
            {
                // Обычная логика для игрока
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
        
        // Показываем панели статистики через ActionModeUI
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.ShowStatsPanels();
        }
        
        if (turnPanel != null) turnPanel.SetActive(false);
        StartCoroutine(ActionTimer());
    }

    private IEnumerator ActionTimer()
    {
        while (remainingTime > 0)
        {
            // Таймер не уменьшается, если он на паузе (например, во время QTE)
            if (!isTimerPaused)
            {
                remainingTime -= Time.deltaTime;
            }
            yield return null;
        }
        SwitchToTacticalMode();
    }
    
    /// <summary>
    /// Приостанавливает таймер хода (используется во время QTE)
    /// </summary>
    public void PauseTimer()
    {
        isTimerPaused = true;
    }
    
    /// <summary>
    /// Возобновляет таймер хода (используется после завершения QTE)
    /// </summary>
    public void ResumeTimer()
    {
        isTimerPaused = false;
    }
    
    /// <summary>
    /// Проверяет, приостановлен ли таймер
    /// </summary>
    public bool IsTimerPaused() => isTimerPaused;
    
    /// <summary>
    /// Возвращает оставшееся время действия (для ActionModeUI)
    /// </summary>
    public float GetRemainingTime() => remainingTime;

    public void SwitchToTacticalMode()
    {
        // Отменяем QTE, если он активен (например, если закончился таймер хода)
        if (QTESystem.Instance != null && QTESystem.Instance.IsQTEActive())
        {
            QTESystem.Instance.CancelQTE();
        }
        
        // Убеждаемся, что таймер возобновлен (на случай, если QTE был активен)
        isTimerPaused = false;
        
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

        // Скрываем панели статистики через ActionModeUI
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.HideStatsPanels();
        }
        
        // Если игра еще не завершена, переключаем ход
        GameManager.Instance.SwitchTurn();
        SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        if (turnPanel != null) turnPanel.SetActive(true);
        if (turnText != null) turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
    }

    private void SetTacticalCameraPosition(Player player)
    {
        // Если игра против бота, камера всегда остается на стороне Player1
        if (GameManager.Instance != null && GameManager.Instance.IsBotTurn())
        {
            // Не переключаем камеру на сторону бота - оставляем на стороне игрока
            tacticalCamera.transform.position = player1TacticalPosition;
            tacticalCamera.transform.rotation = player1TacticalRotation;
            return;
        }
        
        // Обычная логика для игры против игрока
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
    
    /// <summary>
    /// Обновляет камеру и UI для текущего игрока (используется ботом)
    /// </summary>
    public void UpdateCameraForCurrentPlayer()
    {
        if (GameManager.Instance != null)
        {
            // Если камера следовала за юнитом бота, возвращаем её на исходную позицию
            if (isFollowingBotUnit && !isReturningCamera)
            {
                StartCoroutine(ReturnCameraAndSetPosition());
            }
            else if (!isFollowingBotUnit && !isReturningCamera)
            {
                // Если камера не следует за юнитом, просто устанавливаем позицию
                SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
            }
            
            if (turnText != null)
            {
                // Показываем "Ход: Бот" вместо "Ход: Player2"
                if (GameManager.Instance.IsBotTurn())
                {
                    turnText.text = "Ход: Бот";
                }
                else
                {
                    turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
                }
            }
        }
    }
    
    /// <summary>
    /// Возвращает камеру на исходную позицию и затем устанавливает новую позицию
    /// </summary>
    private IEnumerator ReturnCameraAndSetPosition()
    {
        ReturnTacticalCameraToOriginalPosition();
        
        // Ждем завершения возврата камеры
        while (isReturningCamera)
        {
            yield return null;
        }
        
        // Устанавливаем новую позицию камеры
        if (GameManager.Instance != null)
        {
            SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        }
    }
    
    /// <summary>
    /// Переключает камеру на приближенный вид над юнитом бота для лучшей видимости его хода
    /// </summary>
    /// <param name="botUnit">Юнит бота, за которым должна следовать камера</param>
    public void SwitchToBotUnitView(Unit botUnit)
    {
        if (botUnit == null) return;
        
        // Сохраняем текущую позицию и ротацию камеры (если еще не сохранена)
        if (!isFollowingBotUnit)
        {
            savedTacticalPosition = tacticalCamera.transform.position;
            savedTacticalRotation = tacticalCamera.transform.rotation;
        }
        
        // Вычисляем позицию камеры спереди юнита бота (со стороны игрока)
        Vector3 unitPosition = botUnit.transform.position;
        
        // Определяем направление "спереди" - от юнита бота к стороне игрока (Player1)
        // Используем направление от юнита бота к сохраненной позиции камеры (которая на стороне Player1)
        Vector3 directionToPlayer = (savedTacticalPosition - unitPosition).normalized;
        directionToPlayer.y = 0; // Игнорируем вертикальную составляющую
        directionToPlayer.Normalize();
        
        // Если направление не определено (камера прямо над юнитом), используем направление к центру доски со стороны Player1
        if (directionToPlayer.magnitude < 0.1f)
        {
            // Player1 обычно внизу доски (меньшие Y координаты), направляем камеру вниз
            directionToPlayer = -Vector3.forward; // Направление к Player1
        }
        
        // Позиция камеры: над юнитом, спереди (со стороны игрока)
        Vector3 cameraPosition = unitPosition + Vector3.up * 8f + directionToPlayer * 5f; // Высота 8, отступ спереди 5
        
        // Вычисляем ротацию камеры (смотрим на юнит сверху под углом)
        Vector3 directionToUnit = (unitPosition - cameraPosition).normalized;
        Quaternion cameraRotation = Quaternion.LookRotation(directionToUnit);
        
        // Плавно перемещаем камеру к новой позиции
        StartCoroutine(MoveCameraToBotUnit(cameraPosition, cameraRotation, botUnit));
    }
    
    /// <summary>
    /// Плавно перемещает камеру к позиции над юнитом бота
    /// </summary>
    private IEnumerator MoveCameraToBotUnit(Vector3 targetPosition, Quaternion targetRotation, Unit botUnit)
    {
        isFollowingBotUnit = true;
        Vector3 startPosition = tacticalCamera.transform.position;
        Quaternion startRotation = tacticalCamera.transform.rotation;
        
        float duration = 0.5f; // Время перемещения камеры
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t); // Плавная интерполяция
            
            tacticalCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            tacticalCamera.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            
            yield return null;
        }
        
        // Устанавливаем финальную позицию
        tacticalCamera.transform.position = targetPosition;
        tacticalCamera.transform.rotation = targetRotation;
        
        // Начинаем следовать за юнитом бота
        followBotUnitCoroutine = StartCoroutine(FollowBotUnit(botUnit));
    }
    
    /// <summary>
    /// Следит за юнитом бота, обновляя позицию камеры
    /// </summary>
    private IEnumerator FollowBotUnit(Unit botUnit)
    {
        while (isFollowingBotUnit && botUnit != null && botUnit.GetHealth() > 0)
        {
            // Обновляем позицию камеры спереди юнита бота (со стороны игрока)
            Vector3 unitPosition = botUnit.transform.position;
            
            // Определяем направление "спереди" - от юнита бота к стороне игрока
            Vector3 directionToPlayer = (savedTacticalPosition - unitPosition).normalized;
            directionToPlayer.y = 0; // Игнорируем вертикальную составляющую
            directionToPlayer.Normalize();
            
            // Если направление не определено, используем направление к Player1
            if (directionToPlayer.magnitude < 0.1f)
            {
                directionToPlayer = -Vector3.forward; // Направление к Player1
            }
            
            // Позиция камеры: над юнитом, спереди (со стороны игрока)
            Vector3 cameraPosition = unitPosition + Vector3.up * 8f + directionToPlayer * 5f;
            
            // Плавно перемещаем камеру к новой позиции
            tacticalCamera.transform.position = Vector3.Lerp(
                tacticalCamera.transform.position, 
                cameraPosition, 
                Time.deltaTime * 5f // Скорость следования
            );
            
            // Обновляем ротацию камеры (смотрим на юнит)
            Vector3 directionToUnit = (unitPosition - tacticalCamera.transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToUnit);
            tacticalCamera.transform.rotation = Quaternion.Lerp(
                tacticalCamera.transform.rotation,
                targetRotation,
                Time.deltaTime * 5f
            );
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Возвращает тактическую камеру на исходную позицию (публичный метод для использования из BotController)
    /// </summary>
    public void ReturnTacticalCameraToOriginalPosition()
    {
        if (!isFollowingBotUnit) return;
        
        isFollowingBotUnit = false;
        
        // Останавливаем корутину следования, если она активна
        if (followBotUnitCoroutine != null)
        {
            StopCoroutine(followBotUnitCoroutine);
            followBotUnitCoroutine = null;
        }
        
        // Останавливаем предыдущую корутину возврата, если она активна
        if (returnCameraCoroutine != null)
        {
            StopCoroutine(returnCameraCoroutine);
        }
        
        // Плавно возвращаем камеру на исходную позицию
        returnCameraCoroutine = StartCoroutine(ReturnCameraToOriginalPosition());
    }
    
    /// <summary>
    /// Плавно возвращает камеру на исходную позицию
    /// </summary>
    private IEnumerator ReturnCameraToOriginalPosition()
    {
        isReturningCamera = true;
        Vector3 startPosition = tacticalCamera.transform.position;
        Quaternion startRotation = tacticalCamera.transform.rotation;
        
        float duration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            tacticalCamera.transform.position = Vector3.Lerp(startPosition, savedTacticalPosition, t);
            tacticalCamera.transform.rotation = Quaternion.Lerp(startRotation, savedTacticalRotation, t);
            
            yield return null;
        }
        
        // Устанавливаем финальную позицию
        tacticalCamera.transform.position = savedTacticalPosition;
        tacticalCamera.transform.rotation = savedTacticalRotation;
        
        isReturningCamera = false;
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