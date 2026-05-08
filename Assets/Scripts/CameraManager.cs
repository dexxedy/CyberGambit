using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // Для TextMeshProUGUI (таймер)
using System.Collections;
using System.Collections.Generic;
using Mission2;

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
    [SerializeField] private float minCameraHeight = 1f; 
    [SerializeField] private float maxCameraHeight = 60f;
    [Header("Tactical Edge Scroll")]
    [SerializeField] private bool enableEdgeScroll = true;
    [SerializeField] private float edgeScrollBorderPx = 18f;
    [SerializeField] private float edgeScrollSpeedMultiplier = 1.0f;
    [Header("Tactical Rotation (Yaw only)")]
    [SerializeField] private bool enableYawRotation = true;
    [SerializeField] private float yawRotationSpeed = 0.25f;
    [Header("Tactical Rotation (Pitch)")]
    [Tooltip("Если включено — в тактике можно наклонять камеру вверх/вниз (RMB + движение мыши по Y).")]
    [SerializeField] private bool enablePitchRotation = true;
    [SerializeField] private float pitchRotationSpeed = 0.25f;
    [SerializeField] private float minPitchDegrees = -90f;
    [SerializeField] private float maxPitchDegrees = 90f;
    [Header("Tactical Zoom (Mouse Wheel)")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2.0f;

    [Header("Tactical — переключение между своими юнитами")]
    [Tooltip("В тактике, на ходу человека: переносит камеру над следующим своим юнитом (по кругу).")]
    [SerializeField] private bool enableCycleFriendlyUnitsInTactical = true;
    [SerializeField] private Key cycleFriendlyUnitsKey = Key.Tab;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private GameObject turnPanel; // Панель с текстом хода
    
    private Unit currentUnit; // Текущий выбранный юнит
    private bool isActionMode = false; // Флаг режима (false - тактический, true - экшен)
    private float actionTime = 30f; // 30 секунд на ход
    private float remainingTime; // Остаток времени
    private bool isGameEnded = false;
    private bool isTimerPaused = false; // Флаг паузы таймера (для QTE)
    private bool isFollowingBotUnit = false; // Флаг следования камеры за юнитом бота
    private bool isReturningCamera = false; // Флаг возврата камеры на исходную позицию
    private Unit followedBotUnit; // Какой бот-юнит сейчас "в кадре"
    private Vector3 savedTacticalPosition; // Сохраненная позиция тактической камеры
    private Quaternion savedTacticalRotation; // Сохраненная ротация тактической камеры
    private Coroutine followBotUnitCoroutine; // Корутина следования за юнитом бота
    private Coroutine returnCameraCoroutine; // Корутина возврата камеры

    [Header("Army Deployment Camera")]
    [Tooltip("Питч (наклон вниз) камеры в фазе расстановки армии. 90 = строго сверху вниз.")]
    [SerializeField] private float deploymentPitchDegrees = 90f;
    [Tooltip("Питч (наклон вниз) после нажатия «Готов» (переход к обычной тактике).")]
    [SerializeField] private float postDeploymentPitchDegrees = 60f;
    [Tooltip("Высота тактической камеры в фазе расстановки армии.")]
    [SerializeField] private float deploymentCameraHeight = 35f;
    [Tooltip("Высота тактической камеры после «Готов» (переход к обычной тактике).")]
    [SerializeField] private float postDeploymentCameraHeight = 22f;
    [Tooltip("Длительность плавного перехода камеры после «Готов».")]
    [SerializeField] private float deploymentExitTransitionSeconds = 0.65f;

    private Coroutine deploymentCameraCoroutine;
    private bool deploymentCameraLockActive = false;
    private float deploymentLockedYaw;
    private Vector2 deploymentLockedXZ;

    [Header("Tactical Camera Follow")]
    [Tooltip("Если включено — при начале хода камера центрируется над последним юнитом этого игрока.")]
    [SerializeField] private bool centerTacticalCameraOnLastPlayedUnit = true;
    [Tooltip("Мировой Y-offset для центрирования (обычно 0).")]
    [SerializeField] private float centerOnUnitWorldOffsetY = 0f;

    private Unit lastPlayedUnitPlayer1;
    private Unit lastPlayedUnitPlayer2;

    [Header("Bot Turn Presentation")]
    [Tooltip("Если включено — камера НЕ следует за ботом. Если выключено — камера может следовать (кино-показ).")]
    [SerializeField] private bool disableBotFollowCamera = true;
    
    [Tooltip("Если включено — камера мгновенно снэпается к боту (без плавного подлёта).")]
    [SerializeField] private bool snapBotFollowCameraInstantly = true;

    [Tooltip("Если включено — во время кино-показа хода бота показываем 3D-модели: все юниты игрока + всех spotted-врагов.")]
    [SerializeField] private bool showKnownUnitsBodiesDuringBotCinematic = true;

    [Tooltip("Если включено — в PvBot тактическая камера НЕ сбрасывается в фиксированную позицию при смене хода.")]
    [SerializeField] private bool preserveTacticalCameraInPvBot = true;

    private float tacticalYawDegrees;
    private float tacticalPitchDegrees;

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

        // Инициализируем yaw/pitch из текущей ротации камеры.
        if (tacticalCamera != null)
        {
            Vector3 e = tacticalCamera.transform.rotation.eulerAngles;
            tacticalYawDegrees = e.y;
            // Конвертируем в диапазон [-180..180], чтобы кламп работал ожидаемо.
            tacticalPitchDegrees = e.x > 180f ? e.x - 360f : e.x;
            tacticalPitchDegrees = Mathf.Clamp(tacticalPitchDegrees, minPitchDegrees, maxPitchDegrees);
        }
        
        // Инициализируем Audio Listener на тактической камере (начальное состояние)
        SwitchAudioListener(tacticalCamera, actionCamera);

        // Camera shake helper: если скрипт есть в проекте — автоматически добавим его на actionCamera,
        // чтобы VFX могли вызывать CameraShake.Instance без ручной настройки сцены.
        if (actionCamera != null)
        {
            if (actionCamera.GetComponent<CameraShake>() == null)
                actionCamera.gameObject.AddComponent<CameraShake>();
        }

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

        if (TacticalWorldIconsController.Instance != null && TacticalWorldIconsController.Instance.IsConfigured())
            TacticalUnitPresentation.ApplyGlobal(true);
    }

    /// <summary>
    /// В фазе расстановки армии (PvBot) камера смотрит строго вниз (90°),
    /// чтобы игроку было удобно расставлять юнитов по клеткам.
    /// </summary>
    public void EnterArmyDeploymentCamera()
    {
        if (tacticalCamera == null) return;
        if (isActionMode) return;

        if (deploymentCameraCoroutine != null)
        {
            StopCoroutine(deploymentCameraCoroutine);
            deploymentCameraCoroutine = null;
        }

        deploymentCameraLockActive = true;

        Vector3 p = tacticalCamera.transform.position;
        deploymentLockedXZ = new Vector2(p.x, p.z);
        p.y = Mathf.Clamp(deploymentCameraHeight, minCameraHeight, maxCameraHeight);
        p.x = deploymentLockedXZ.x;
        p.z = deploymentLockedXZ.y;
        tacticalCamera.transform.position = p;

        deploymentLockedYaw = tacticalCamera.transform.rotation.eulerAngles.y;
        tacticalCamera.transform.rotation = Quaternion.Euler(deploymentPitchDegrees, deploymentLockedYaw, 0f);

        Debug.Log($"[CameraManager] Deployment camera ON (pitch={deploymentPitchDegrees}, height={deploymentCameraHeight})");
    }

    /// <summary>
    /// После «Готов» камера опускается и переходит на более "игровой" угол (примерно 60° вниз).
    /// </summary>
    public void ExitArmyDeploymentCamera()
    {
        if (tacticalCamera == null) return;
        if (isActionMode) return;

        if (deploymentCameraCoroutine != null)
            StopCoroutine(deploymentCameraCoroutine);
        deploymentCameraCoroutine = StartCoroutine(DeploymentExitTransitionCoroutine());

        Debug.Log($"[CameraManager] Deployment camera OFF -> transition (pitch={postDeploymentPitchDegrees}, height={postDeploymentCameraHeight})");
    }

    private void MaintainDeploymentCameraLock()
    {
        if (tacticalCamera == null) return;

        // lock yaw/pitch/roll completely
        tacticalCamera.transform.rotation = Quaternion.Euler(deploymentPitchDegrees, deploymentLockedYaw, 0f);

        // lock position (XZ + height) for placement readability
        Vector3 p = tacticalCamera.transform.position;
        p.x = deploymentLockedXZ.x;
        p.z = deploymentLockedXZ.y;
        p.y = Mathf.Clamp(deploymentCameraHeight, minCameraHeight, maxCameraHeight);
        tacticalCamera.transform.position = p;
    }

    private IEnumerator DeploymentExitTransitionCoroutine()
    {
        deploymentCameraLockActive = false;

        Vector3 startPos = tacticalCamera.transform.position;
        Quaternion startRot = tacticalCamera.transform.rotation;

        Vector3 endPos = startPos;
        endPos.y = Mathf.Clamp(postDeploymentCameraHeight, minCameraHeight, maxCameraHeight);

        float yaw = startRot.eulerAngles.y;
        Quaternion endRot = Quaternion.Euler(postDeploymentPitchDegrees, yaw, 0f);

        float duration = Mathf.Max(0.01f, deploymentExitTransitionSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = Mathf.SmoothStep(0f, 1f, k);
            tacticalCamera.transform.position = Vector3.Lerp(startPos, endPos, k);
            tacticalCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }

        tacticalCamera.transform.position = endPos;
        tacticalCamera.transform.rotation = endRot;
        deploymentCameraCoroutine = null;
    }

    void Update()
    {
        if (isGameEnded) return;

        if (!isActionMode && deploymentCameraLockActive && GameManager.Instance != null && GameManager.Instance.IsArmyDeploymentPhase())
        {
            MaintainDeploymentCameraLock();
            // Во время расстановки армии блокируем управление тактической камерой полностью.
            // Игрок расставляет юнитов, камера "как в редакторе": строго сверху и без движения/вращения/зума.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

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
            // PvBot spotting: в экшене игрок "разведывает" врагов (LOS/FOV) -> миникарта/показ хода бота.
            if (EnemyIntelTracker.Instance != null && GameManager.Instance != null &&
                GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot && currentUnit != null && actionCamera != null)
            {
                EnemyIntelTracker.Instance.UpdateSpottingFromAction(currentUnit, actionCamera);
            }

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

                // В PvBot игрок всё равно должен иметь возможность двигать тактическую камеру,
                // чтобы наблюдать за траекториями/подсказками хода бота.
                HandleTacticalCameraMovement();
            }
            else
            {
                // Тактика: полёт камеры (WASD). Выбор своего юнита — через UI-иконки (TacticalWorldIconsController).
                HandleTacticalCameraMovement();
                TryCycleTacticalCameraToNextFriendlyUnit();
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

        // Remember last played unit for this player.
        RememberLastPlayedUnit(unit);
        
        // Инициализируем бюджет перемещения в метрах из броска костей
        if (GameManager.Instance != null)
        {
            currentUnit.SetRemainingMoveMeters(GameManager.Instance.GetCurrentTurnMoveBudgetMeters());
        }

        // Подсветка клеток — только если есть шахматная сетка и GridHighlighter в сцене
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

        TacticalUnitPresentation.ApplyGlobal(false);
        
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
        if (GridHighlighter.Instance != null)
        {
            GridHighlighter.Instance.ClearHighlights();
        }
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
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isActionMode = false;

        if (TacticalWorldIconsController.Instance != null && TacticalWorldIconsController.Instance.IsConfigured())
            TacticalUnitPresentation.ApplyGlobal(true);

        // Скрываем панели статистики через ActionModeUI
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.HideStatsPanels();
        }
        
        // Если игра еще не завершена, переключаем ход
        GameManager.Instance.SwitchTurn();
        // В PvBot обычно не сбрасываем камеру, но если ход вернулся игроку — центрируем на последнем сыгранном юните.
        if (preserveTacticalCameraInPvBot && GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot)
        {
            if (!GameManager.Instance.IsBotTurn())
                SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        }
        else
        {
            SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
        }
        if (turnPanel != null) turnPanel.SetActive(true);
        if (turnText != null) turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
    }

    public void TrySwitchToActionModeFromMap(Unit unit)
    {
        if (unit == null || GameManager.Instance == null) return;
        if (GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (isActionMode) return;
        if (unit.owner != GameManager.Instance.currentPlayer) return;
        if (GameManager.Instance.IsBotTurn()) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUnitSelect();

        // Mission2 change: tank is controlled like a normal unit (action/FPS mode).

        if (!GameManager.Instance.HasRolledDiceThisTurn()) return;
        SwitchToActionMode(unit);
    }

    /// <summary>
    /// Сохраняет высоту и поворот тактической камеры, переносит X/Z над указанным юнитом (как при центрировании на последнем ходе).
    /// </summary>
    private void SnapTacticalCameraAboveUnit(Unit u)
    {
        if (u == null || tacticalCamera == null) return;
        if (u.GetHealth() <= 0) return;

        Vector3 p = tacticalCamera.transform.position;
        Vector3 up = Vector3.up * Mathf.Max(0f, centerOnUnitWorldOffsetY);
        p.x = u.transform.position.x + up.x;
        p.z = u.transform.position.z + up.z;
        p.y = Mathf.Clamp(p.y, minCameraHeight, maxCameraHeight);
        p.x = Mathf.Clamp(p.x, minArenaX, maxArenaX);
        p.z = Mathf.Clamp(p.z, minArenaZ, maxArenaZ);
        tacticalCamera.transform.position = p;
    }

    private void SetTacticalCameraPosition(Player player)
    {
        if (centerTacticalCameraOnLastPlayedUnit)
        {
            Unit u = (player == Player.Player1) ? lastPlayedUnitPlayer1 : lastPlayedUnitPlayer2;
            if (u != null && u.GetHealth() > 0 && tacticalCamera != null)
            {
                SnapTacticalCameraAboveUnit(u);

                // Keep current rotation (player can yaw/zoom). If you prefer fixed, uncomment:
                // tacticalCamera.transform.rotation = player == Player.Player1 ? player1TacticalRotation : player2TacticalRotation;
                return;
            }
        }

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

        // Синхронизируем yaw/pitch после любого жёсткого сэта позиции/ротации.
        if (tacticalCamera != null)
        {
            Vector3 e = tacticalCamera.transform.rotation.eulerAngles;
            tacticalYawDegrees = e.y;
            tacticalPitchDegrees = e.x > 180f ? e.x - 360f : e.x;
            tacticalPitchDegrees = Mathf.Clamp(tacticalPitchDegrees, minPitchDegrees, maxPitchDegrees);
        }
    }

    private void RememberLastPlayedUnit(Unit unit)
    {
        if (unit == null) return;
        if (unit.owner == Player.Player1) lastPlayedUnitPlayer1 = unit;
        else lastPlayedUnitPlayer2 = unit;
    }

    /// <summary>
    /// По нажатию клавиши переносит тактическую камеру над следующим своим живым юнитом.
    /// Текущая «опорная» точка — юнит в списке, ближайший к камере по XZ (после ручного пана всё ещё можно перейти дальше по кругу).
    /// </summary>
    private void TryCycleTacticalCameraToNextFriendlyUnit()
    {
        if (!enableCycleFriendlyUnitsInTactical) return;
        if (tacticalCamera == null || GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused()) return;
        if (GameManager.Instance.IsBotTurn()) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current[cycleFriendlyUnitsKey].wasPressedThisFrame) return;

        Player cp = GameManager.Instance.currentPlayer;
        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var mine = new List<Unit>();
        foreach (Unit u in all)
        {
            if (u == null) continue;
            if (u.owner != cp) continue;
            if (u.GetHealth() <= 0) continue;
            mine.Add(u);
        }

        if (mine.Count <= 1) return;

        mine.Sort((a, b) =>
        {
            int cmp = a.currentGridPosition.x.CompareTo(b.currentGridPosition.x);
            if (cmp != 0) return cmp;
            cmp = a.currentGridPosition.y.CompareTo(b.currentGridPosition.y);
            if (cmp != 0) return cmp;
            return string.CompareOrdinal(a.name, b.name);
        });

        Vector3 cam = tacticalCamera.transform.position;
        float camX = cam.x;
        float camZ = cam.z;

        int bestIdx = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < mine.Count; i++)
        {
            Vector3 upos = mine[i].transform.position;
            float dx = upos.x - camX;
            float dz = upos.z - camZ;
            float d = dx * dx + dz * dz;
            if (d < bestDist || (Mathf.Approximately(d, bestDist) && i < bestIdx))
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        int nextIdx = (bestIdx + 1) % mine.Count;
        SnapTacticalCameraAboveUnit(mine[nextIdx]);
    }
    
    /// <summary>
    /// Обновляет камеру и UI для текущего игрока (используется ботом)
    /// </summary>
    public void UpdateCameraForCurrentPlayer()
    {
        if (GameManager.Instance != null)
        {
            // В PvBot камера должна оставаться там, где её оставил игрок.
            if (preserveTacticalCameraInPvBot && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot)
            {
                // Если камера следовала за ботом — обязательно отпускаем её при смене хода.
                if (!GameManager.Instance.IsBotTurn() && isFollowingBotUnit && !isReturningCamera)
                {
                    StartCoroutine(ReturnCameraAndSetPosition());
                    if (turnText != null)
                        turnText.text = $"Ход: {GameManager.Instance.currentPlayer}";
                    return;
                }

                // Но когда ход возвращается игроку, центрируем над последним сыгранным юнитом.
                if (!GameManager.Instance.IsBotTurn())
                    SetTacticalCameraPosition(GameManager.Instance.currentPlayer);
                if (turnText != null)
                    turnText.text = GameManager.Instance.IsBotTurn() ? "Ход: Бот" : $"Ход: {GameManager.Instance.currentPlayer}";
                return;
            }

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
        if (disableBotFollowCamera) return;
        if (botUnit == null) return;
        
        // Сохраняем текущую позицию и ротацию камеры (если еще не сохранена)
        if (!isFollowingBotUnit)
        {
            savedTacticalPosition = tacticalCamera.transform.position;
            savedTacticalRotation = tacticalCamera.transform.rotation;
        }

        followedBotUnit = botUnit;
        
        // 3rd-person: камера сзади юнита бота.
        Vector3 unitPosition = botUnit.transform.position;

        Vector3 back = -botUnit.transform.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
        back.Normalize();

        float height = 2.2f;
        float distance = 4.5f;
        Vector3 cameraPosition = unitPosition + Vector3.up * height + back * distance;
        Vector3 lookAt = unitPosition + Vector3.up * 1.4f;
        Quaternion cameraRotation = Quaternion.LookRotation((lookAt - cameraPosition).normalized);
        
        // На время кино-показа в тактике показываем тела "известных" юнитов (свои + spotted враги).
        if (showKnownUnitsBodiesDuringBotCinematic)
            ApplyKnownBodiesVisibilityForCinematic();
        else
        {
            TacticalUnitPresentation pres = botUnit.GetComponent<TacticalUnitPresentation>();
            if (pres != null)
                pres.SetBodyVisualVisible(true);
        }

        // Мгновенный снэп или плавный подлёт (временно можно включать снэп, чтобы не было дёргано).
        if (snapBotFollowCameraInstantly)
        {
            isFollowingBotUnit = true;
            tacticalCamera.transform.position = cameraPosition;
            tacticalCamera.transform.rotation = cameraRotation;

            // Перезапускаем follow-корутину, если она уже шла.
            if (followBotUnitCoroutine != null)
            {
                StopCoroutine(followBotUnitCoroutine);
                followBotUnitCoroutine = null;
            }
            followBotUnitCoroutine = StartCoroutine(FollowBotUnit(botUnit));
        }
        else
        {
            // Плавно перемещаем камеру к новой позиции
            StartCoroutine(MoveCameraToBotUnit(cameraPosition, cameraRotation, botUnit));
        }
    }

    public bool IsBotFollowCameraEnabled() => !disableBotFollowCamera;
    public bool IsFollowingBotUnit() => isFollowingBotUnit;
    public Unit GetFollowedBotUnit() => followedBotUnit;
    public bool IsKnownBodiesCinematicEnabled() => showKnownUnitsBodiesDuringBotCinematic;

    public bool ShouldHideTacticalIconDuringCinematic(Unit unit)
    {
        if (!isFollowingBotUnit) return false;
        if (!showKnownUnitsBodiesDuringBotCinematic) return unit != null && unit == followedBotUnit;
        if (unit == null) return false;
        if (GameManager.Instance == null || GameManager.Instance.GetGameMode() != GameMode.PlayerVsBot)
            return unit == followedBotUnit;

        // Если показываем тела "известных" юнитов — иконки им не нужны.
        if (unit.owner == Player.Player1) return true;
        if (unit.owner == Player.Player2)
            return EnemyIntelTracker.Instance != null && EnemyIntelTracker.Instance.IsSpotted(unit);

        return unit == followedBotUnit;
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
            Vector3 back = -botUnit.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = Vector3.back;
            back.Normalize();

            Vector3 cameraPosition = unitPosition + Vector3.up * 2.2f + back * 4.5f;
            
            // Плавно перемещаем камеру к новой позиции
            tacticalCamera.transform.position = Vector3.Lerp(
                tacticalCamera.transform.position, 
                cameraPosition, 
                Time.deltaTime * 5f // Скорость следования
            );
            
            // Обновляем ротацию камеры (смотрим на юнит)
            Vector3 lookAt = unitPosition + Vector3.up * 1.4f;
            Vector3 directionToUnit = (lookAt - tacticalCamera.transform.position).normalized;
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
        followedBotUnit = null;

        // Возвращаем показ моделей в тактике к глобальному правилу (обычно скрыты).
        if (TacticalWorldIconsController.Instance != null && TacticalWorldIconsController.Instance.IsConfigured())
            TacticalUnitPresentation.ApplyGlobal(true);
        
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

    private void ApplyKnownBodiesVisibilityForCinematic()
    {
        // PvBot only: тела игрока (Player1) всегда, враги (Player2) только если spotted.
        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        foreach (Unit u in all)
        {
            if (u == null || u.GetHealth() <= 0) continue;
            TacticalUnitPresentation pres = u.GetComponent<TacticalUnitPresentation>();
            if (pres == null) continue;

            bool visible = true;
            if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot)
            {
                if (u.owner == Player.Player2)
                    visible = EnemyIntelTracker.Instance != null && EnemyIntelTracker.Instance.IsSpotted(u);
            }

            pres.SetBodyVisualVisible(visible);
        }
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
        // --- 1) Обработка движения WASD + edge scroll (без вращения камеры) ---

        Vector2 moveInput = new Vector2(
            Keyboard.current.dKey.IsActuated() ? 1f : (Keyboard.current.aKey.IsActuated() ? -1f : 0f),
            Keyboard.current.wKey.IsActuated() ? 1f : (Keyboard.current.sKey.IsActuated() ? -1f : 0f)
        );

        if (enableEdgeScroll && Mouse.current != null)
        {
            Vector2 mp = Mouse.current.position.ReadValue();
            float w = Screen.width;
            float h = Screen.height;
            float b = Mathf.Max(0f, edgeScrollBorderPx);

            float sx = 0f;
            float sy = 0f;
            if (mp.x <= b) sx = -1f;
            else if (mp.x >= w - b) sx = 1f;
            if (mp.y <= b) sy = -1f;
            else if (mp.y >= h - b) sy = 1f;

            moveInput += new Vector2(sx, sy) * Mathf.Max(0f, edgeScrollSpeedMultiplier);
        }

        moveInput = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;

        // Двигаем только по плоскости XZ (Battlefield/RTS), высота регулируется лимитами.
        Vector3 forwardXZ = tacticalCamera.transform.forward;
        forwardXZ.y = 0f;
        forwardXZ = forwardXZ.sqrMagnitude > 0.0001f ? forwardXZ.normalized : Vector3.forward;

        Vector3 rightXZ = tacticalCamera.transform.right;
        rightXZ.y = 0f;
        rightXZ = rightXZ.sqrMagnitude > 0.0001f ? rightXZ.normalized : Vector3.right;

        Vector3 totalMovement = (forwardXZ * moveInput.y + rightXZ * moveInput.x) * flySpeed * Time.deltaTime;
        tacticalCamera.transform.position += totalMovement;

        // --- 2) Вращение (Yaw + Pitch) ---
        if (enableYawRotation && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            // Скрываем курсор и читаем дельту только по X
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 delta = Mouse.current.delta.ReadValue();
            tacticalYawDegrees += delta.x * yawRotationSpeed;
            if (enablePitchRotation)
            {
                // Инверсию можно поменять, если некомфортно.
                tacticalPitchDegrees -= delta.y * pitchRotationSpeed;
                tacticalPitchDegrees = Mathf.Clamp(tacticalPitchDegrees, minPitchDegrees, maxPitchDegrees);
            }

            tacticalCamera.transform.rotation = Quaternion.Euler(tacticalPitchDegrees, tacticalYawDegrees, 0f);
        }
        else if (!isActionMode)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // --- 3) Zoom колёсиком (меняем высоту Y в пределах min/max) ---
        if (enableZoom && Mouse.current != null)
        {
            // В новой системе ввода скролл приходит как Vector2, интересует Y
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 p = tacticalCamera.transform.position;
                // Скролл обычно большой (примерно 120 за щелчок), поэтому нормализуем
                float scrollSteps = scroll / 120f;
                p.y -= scrollSteps * zoomSpeed;
                tacticalCamera.transform.position = p;
            }
        }

        // --- 4) Ограничение (Bounding) ---

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