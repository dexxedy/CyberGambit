using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Визуальная 3D-кость через RenderTexture в UI (RawImage).
/// Сам создаёт DiceCamera + Light + RenderTexture, и слушает GameManager.OnDiceRolled.
/// Показывается только в тактике (когда CameraManager.IsActionMode()==false).
///
/// Требования к dicePrefab:
/// - Желательно иметь дочерние transforms "Face1".."Face6" с forward, направленным НАРУЖУ из грани.
///   Тогда будет правильный "верх" при показе значения.
/// - Дополнительно: зелёная ось (up) маркера FaceN должна указывать на "верх" цифры/пипсов на этой грани
///   (как ты хочешь читать её сверху вниз). Это используется для финального yaw-выравнивания в top-down.
/// - Если маркеров нет, используется дефолтная ориентация, подходящая для стандартного Unity Cube.
/// </summary>
public class DiceRenderUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private bool hideInActionMode = true;

    [Header("Player roll input")]
    [Tooltip("Клавиша для ручного броска в тактике (только ход человека, пока кубик ещё не брошен).")]
    [SerializeField] private Key rollKey = Key.Space;
    [Tooltip("Разрешить клик по RawImage для броска (нужен Raycast Target на RawImage).")]
    [SerializeField] private bool clickToRoll = true;

    [Header("Dice Prefab")]
    [SerializeField] private GameObject dicePrefab;
    [SerializeField] private float diceScale = 1.0f;

    [Header("Render Setup")]
    [SerializeField] private int textureSize = 512;
    [Tooltip("Фон RenderTexture. Для UI лучше непрозрачный, иначе будет просвечивать текст/цифры под RawImage.")]
    [SerializeField] private Color clearColor = new Color(0, 0, 0, 1);

    [Tooltip("Рекомендуется создать слой в Unity: Tags and Layers -> User Layer (например DiceRender). Тогда DiceCamera/Light не будут влиять на основную сцену.")]
    [SerializeField] private string diceIsolationLayerName = "DiceRender";

    [Header("Camera Look (strict top-down)")]
    [SerializeField] private float orthographicSize = 0.95f;
    [Tooltip("Высота камеры над костью (положительный Y). Для ортографии влияет в основном на near/far комфорт; крупность кадра крути Orthographic Size.")]
    [SerializeField] private float cameraHeight = 6.0f;

    [Header("Digit readability (top-down)")]
    [Tooltip("Если включено — после выравнивания грани кость доворачивается вокруг вертикали, чтобы up маркера FaceN совпал с 'верхом экрана'.")]
    [SerializeField] private bool alignDigitUpright = true;

    [Header("Roll Animation")]
    [SerializeField] private float spinSeconds = 0.9f;
    [SerializeField] private float settleSeconds = 0.25f;
    [SerializeField] private float spinDegreesPerSecond = 720f;

    private RenderTexture rt;
    private Camera diceCam;
    private Light diceLight;
    private Transform rigRoot;
    private Transform diceRoot;
    private Transform diceTransform;
    private Coroutine rollRoutine;
    private CanvasGroup canvasGroup;
    private bool hudForceHidden;

    private void Awake()
    {
        EnsureRig();
    }

    private void OnEnable()
    {
        GameManager.OnDiceRolled += OnDiceRolled;
    }

    private void OnDisable()
    {
        GameManager.OnDiceRolled -= OnDiceRolled;
    }

    private void Update()
    {
        ApplyHudVisibility();
        UpdateClickThroughSettings();
        TryHandlePlayerRollInput();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickToRoll) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver()) return;
        if (GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (GameManager.Instance.IsBotTurn()) return;
        if (GameManager.Instance.HasRolledDiceThisTurn()) return;
        TryRequestPlayerRoll();
    }

    private void TryHandlePlayerRollInput()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[rollKey].wasPressedThisFrame) return;
        TryRequestPlayerRoll();
    }

    /// <summary>Принудительно скрыть/показать 3D-кость в UI (например, экран победы/поражения).</summary>
    public void SetHudVisible(bool visible)
    {
        hudForceHidden = !visible;
        ApplyHudVisibility();
        UpdateClickThroughSettings();
    }

    private void TryRequestPlayerRoll()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsGameOver()) return;
        if (GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (GameManager.Instance.IsPaused()) return;
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode()) return;
        if (GameManager.Instance.IsBotTurn()) return;
        if (GameManager.Instance.HasRolledDiceThisTurn()) return;

        GameManager.Instance.RollDiceForCurrentTurn();
    }

    private void UpdateClickThroughSettings()
    {
        if (targetRawImage == null) return;
        // Включаем raycast только когда клик реально может бросить кубик — чтобы не перекрывать BF-иконки остальное время.
        bool humanNeedsRoll =
            GameManager.Instance != null &&
            !GameManager.Instance.IsGameOver() &&
            !GameManager.Instance.IsArmyDeploymentPhase() &&
            !GameManager.Instance.IsPaused() &&
            !GameManager.Instance.IsBotTurn() &&
            !GameManager.Instance.HasRolledDiceThisTurn();
        bool clickable = clickToRoll && humanNeedsRoll && canvasGroup != null && canvasGroup.alpha > 0.01f;
        targetRawImage.raycastTarget = clickable;
        if (canvasGroup != null)
        {
            // CanvasGroup должен пропускать raycast только когда реально ждём клик по кости.
            canvasGroup.blocksRaycasts = clickable;
            canvasGroup.interactable = canvasGroup.alpha > 0.01f;
        }
    }

    private void OnDestroy()
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
        if (rigRoot != null)
        {
            Destroy(rigRoot.gameObject);
            rigRoot = null;
        }
    }

    private void EnsureRig()
    {
        if (targetRawImage == null) targetRawImage = GetComponent<RawImage>();
        if (targetRawImage == null) return;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (rt == null)
        {
            int size = Mathf.Clamp(textureSize, 128, 2048);
            rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
            rt.name = "DiceRenderTexture";
            rt.Create();
            targetRawImage.texture = rt;
        }

        if (rigRoot == null)
        {
            GameObject go = new GameObject("DiceRenderRig");
            go.hideFlags = HideFlags.DontSave;
            rigRoot = go.transform;
            // ВАЖНО: не держим rig в (0,0,0) — иначе Point light освещает реальный уровень рядом со спавном.
            rigRoot.position = new Vector3(10000f, 10000f, 10000f);

            // Dice root
            GameObject dr = new GameObject("DiceRoot");
            dr.transform.SetParent(rigRoot, false);
            diceRoot = dr.transform;

            // Camera
            GameObject camGo = new GameObject("DiceCamera");
            camGo.transform.SetParent(rigRoot, false);
            diceCam = camGo.AddComponent<Camera>();
            TryAddUrpAdditionalCameraData(camGo);
            diceCam.clearFlags = CameraClearFlags.SolidColor;
            diceCam.backgroundColor = clearColor;
            diceCam.orthographic = true;
            diceCam.orthographicSize = Mathf.Max(0.05f, orthographicSize);
            diceCam.targetTexture = rt;
            diceCam.nearClipPlane = 0.01f;
            diceCam.farClipPlane = 20f;

            // Light
            GameObject lightGo = new GameObject("DiceLight");
            lightGo.transform.SetParent(rigRoot, false);
            diceLight = lightGo.AddComponent<Light>();
            // Directional light освещает всю сцену (даже если камера рендерит в RT) — поэтому используем Point.
            diceLight.type = LightType.Point;
            diceLight.intensity = 2.5f;
            diceLight.range = 8f;
            diceLight.shadows = LightShadows.None;
            diceLight.transform.localPosition = new Vector3(0.35f, 1.35f, -1.2f);
        }

        if (diceTransform == null && dicePrefab != null)
        {
            GameObject inst = Instantiate(dicePrefab, diceRoot);
            inst.name = "DiceInstance";
            diceTransform = inst.transform;
            diceTransform.localScale = Vector3.one * Mathf.Max(0.01f, diceScale);
            ApplyIsolationLayerRecursive(diceTransform);
        }

        ApplyCameraIsolationMask();
        ApplyLightIsolationMask();

        // Default rig layout
        if (diceRoot != null) diceRoot.localPosition = Vector3.zero;
        if (diceCam != null)
        {
            diceCam.orthographic = true;
            diceCam.orthographicSize = Mathf.Max(0.05f, orthographicSize);
            diceCam.transform.localPosition = new Vector3(0f, Mathf.Max(0.5f, cameraHeight), 0f);
            diceCam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // look down -Y in world (rig space)
        }
    }

    private void ApplyHudVisibility()
    {
        if (targetRawImage != null && !targetRawImage.gameObject.activeSelf)
            targetRawImage.gameObject.SetActive(true);

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (hudForceHidden)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        // Если весь тактический HUD выключен (пауза) — прячем и кость вместе с ним.
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        // Фаза расстановки армии — кость ещё недоступна.
        if (GameManager.Instance != null && GameManager.Instance.IsArmyDeploymentPhase())
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (!hideInActionMode)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            return;
        }

        if (CameraManager.Instance == null) return;

        bool tactical = !CameraManager.Instance.IsActionMode();
        canvasGroup.alpha = tactical ? 1f : 0f;
        if (!tactical)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDiceRolled(int value)
    {
        if (targetRawImage == null) return;
        EnsureRig();
        if (diceTransform == null) return;

        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }
        rollRoutine = StartCoroutine(RollRoutine(Mathf.Clamp(value, 1, 6)));
    }

    private IEnumerator RollRoutine(int value)
    {
        // Randomize start
        diceTransform.localRotation = UnityEngine.Random.rotationUniform;

        float spinT = Mathf.Max(0.01f, spinSeconds);
        float settleT = Mathf.Max(0.01f, settleSeconds);
        float t = 0f;

        while (t < spinT)
        {
            t += Time.deltaTime;
            float yaw = spinDegreesPerSecond * Time.deltaTime;
            float pitch = (spinDegreesPerSecond * 0.65f) * Time.deltaTime;
            diceTransform.Rotate(Vector3.up, yaw, Space.Self);
            diceTransform.Rotate(Vector3.right, pitch, Space.Self);
            yield return null;
        }

        Quaternion target = ComputeRotationForValue(value);
        Quaternion start = diceTransform.localRotation;
        float s = 0f;
        while (s < settleT)
        {
            s += Time.deltaTime;
            float k = Mathf.Clamp01(s / settleT);
            diceTransform.localRotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        diceTransform.localRotation = target;
    }

    private Quaternion ComputeRotationForValue(int value)
    {
        // Prefer face markers if present.
        Transform face = diceTransform.Find($"Face{value}");
        if (face != null)
        {
            // Нормаль грани в мировых координатах -> в локальные координаты кости, затем выравниваем на Vector3.up.
            Vector3 nWorld = face.forward;
            if (nWorld.sqrMagnitude < 0.0001f) nWorld = Vector3.up;
            nWorld.Normalize();

            Vector3 nLocal = diceTransform.InverseTransformDirection(nWorld);
            if (nLocal.sqrMagnitude < 0.0001f) nLocal = Vector3.up;
            nLocal.Normalize();

            Quaternion align = Quaternion.FromToRotation(nLocal, Vector3.up);
            if (!alignDigitUpright || diceCam == null)
                return align;

            // "Верх цифры" на грани: локально задаём через зелёную ось маркера (face.up), переведённую в локаль кости ДО align.
            Vector3 upOnFaceLocal = diceTransform.InverseTransformDirection(face.up);
            if (upOnFaceLocal.sqrMagnitude < 0.0001f) upOnFaceLocal = Vector3.forward;
            upOnFaceLocal.Normalize();

            // Убираем компонент вдоль нормали грани (на случай неточного маркера).
            Vector3 upProjected = Vector3.ProjectOnPlane(upOnFaceLocal, nLocal);
            if (upProjected.sqrMagnitude < 0.0001f) upProjected = Vector3.ProjectOnPlane(Vector3.forward, nLocal);
            upProjected.Normalize();

            Vector3 upAfterAlign = (align * upProjected);
            upAfterAlign = Vector3.ProjectOnPlane(upAfterAlign, Vector3.up);
            if (upAfterAlign.sqrMagnitude < 0.0001f) return align;
            upAfterAlign.Normalize();

            // Целевой "верх экрана" для top-down ортокамеры: направление "вверх по экрану" в плоскости XZ рига.
            Vector3 desiredUp = GetTopDownScreenUpWorldOnTablePlane();
            desiredUp = Vector3.ProjectOnPlane(desiredUp, Vector3.up);
            if (desiredUp.sqrMagnitude < 0.0001f) return align;
            desiredUp.Normalize();

            float signed = Mathf.Atan2(
                Vector3.Dot(Vector3.up, Vector3.Cross(upAfterAlign, desiredUp)),
                Vector3.Dot(upAfterAlign, desiredUp)
            );
            Quaternion yaw = Quaternion.AngleAxis(signed * Mathf.Rad2Deg, Vector3.up);
            return yaw * align;
        }

        // Fallback: assume standard cube orientation, where +Y is 1, -Y is 6, +Z is 2, -Z is 5, +X is 3, -X is 4.
        // (You may need to tweak this mapping to match your model's UV/numbering.)
        return value switch
        {
            1 => Quaternion.identity,
            6 => Quaternion.Euler(180f, 0f, 0f),
            2 => Quaternion.Euler(-90f, 0f, 0f),
            5 => Quaternion.Euler(90f, 0f, 0f),
            3 => Quaternion.Euler(0f, 0f, -90f),
            4 => Quaternion.Euler(0f, 0f, 90f),
            _ => Quaternion.identity
        };
    }

    private Vector3 GetTopDownScreenUpWorldOnTablePlane()
    {
        // Камера смотрит вниз, её transform.up на плоскости стола (XZ) задаёт "вверх экрана".
        if (diceCam == null) return Vector3.forward;
        Vector3 camUp = diceCam.transform.up;
        camUp.y = 0f;
        if (camUp.sqrMagnitude < 0.0001f) return Vector3.forward;
        return camUp.normalized;
    }

    private int TryGetIsolationLayerMask()
    {
        if (string.IsNullOrWhiteSpace(diceIsolationLayerName)) return -1;
        int layer = LayerMask.NameToLayer(diceIsolationLayerName);
        if (layer < 0) return -1;
        return 1 << layer;
    }

    private void ApplyIsolationLayerRecursive(Transform root)
    {
        int mask = TryGetIsolationLayerMask();
        if (mask < 0) return;
        int layer = LayerMask.NameToLayer(diceIsolationLayerName);
        if (layer < 0) return;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null) t.gameObject.layer = layer;
        }
    }

    private void ApplyCameraIsolationMask()
    {
        if (diceCam == null) return;
        int mask = TryGetIsolationLayerMask();
        diceCam.cullingMask = mask > 0 ? mask : ~0;
    }

    private void ApplyLightIsolationMask()
    {
        if (diceLight == null) return;
        int mask = TryGetIsolationLayerMask();
        diceLight.cullingMask = mask > 0 ? mask : ~0;
    }

    private static void TryAddUrpAdditionalCameraData(GameObject camGo)
    {
        if (camGo == null) return;

        // URP requires UniversalAdditionalCameraData on cameras; add it if URP is present.
        // Use reflection to avoid hard dependency if project switches pipelines.
        try
        {
            Type t = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime",
                throwOnError: false
            );
            if (t == null) return;
            if (camGo.GetComponent(t) != null) return;
            camGo.AddComponent(t);
        }
        catch
        {
            // ignore
        }
    }
}

