using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Фаза расстановки армии (PvBot, Player1): бюджет очков, зона на сетке, карточки → перетаскивание на поле.
///
/// Подключение к миссии (например mission1):
/// 1) Режим игры PvBot (GameManager / меню).
/// 2) Добавь пустой GameObject, повесь ArmyDeploymentController.
/// 3) Заполни offers (префабы из Assets/Prefabs, costPoints, maxCopies = -1 без лимита).
/// 4) Выставь deploymentZoneMin/Max в координатах сетки ChessGrid (сторона игрока).
/// 5) Отключи или удали предрасставленных юнитов Player1 в иерархии (например объект Player1Units),
///    иначе они займут клетки и пересекутся с расстановкой.
/// 6) Оставь Create Minimal UI If Missing = true для автопанели или назначь свой deploymentRoot вручную.
/// </summary>
public class ArmyDeploymentController : MonoBehaviour
{
    [Header("Бюджет и зона (координаты сетки, включительно)")]
    [SerializeField] private int armyPointBudget = 12;
    [SerializeField] private Vector2Int deploymentZoneMin = new Vector2Int(0, 0);
    [SerializeField] private Vector2Int deploymentZoneMax = new Vector2Int(7, 2);

    [Header("Доступные типы (карточки)")]
    [SerializeField] private List<DeploymentOfferEntry> offers = new List<DeploymentOfferEntry>();

    [Header("UI")]
    [Tooltip("Если null и включено Create Minimal UI If Missing — создаётся Canvas с панелью.")]
    [SerializeField] private GameObject deploymentRoot;
    [SerializeField] private DeploymentCardUI cardPrefab;
    [Tooltip("Родитель карточек (Horizontal Layout). Если null — создаётся вместе с минимальным UI.")]
    [SerializeField] private Transform cardHandContainer;
    [Tooltip("Зона «вернуть юнита» (нижняя панель). Drop с поля сюда отменяет перетаскивание.")]
    [SerializeField] private RectTransform returnDropZone;
    [SerializeField] private TMP_Text budgetText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmHintText;

    [SerializeField] private bool createMinimalUiIfMissing = true;

    [Header("Правила готовности")]
    [SerializeField] private bool requireAtLeastOneUnit = true;
    [SerializeField] private bool requireExactlyOneKing = true;

    [Header("Визуал зоны размещения")]
    [SerializeField] private bool showDeploymentZoneOverlay = true;
    [SerializeField] private Color deploymentZoneColor = new Color(0.2f, 0.85f, 0.45f, 0.38f);
    [Tooltip("Смещение над плоскостью ChessGrid, чтобы не мерцало с полом.")]
    [SerializeField] private float deploymentZoneYOffset = 0.04f;

    private int pointsRemaining;
    private int[] spawnedPerOffer = Array.Empty<int>();
    private readonly Dictionary<Unit, DeploymentRecord> deployed = new Dictionary<Unit, DeploymentRecord>();

    /// <summary> Дочерние плейны подсветки клеток; уничтожаются при «Готов». </summary>
    private GameObject deploymentZoneVisualRoot;

    private Material deploymentZoneSharedMaterial;

    private struct DeploymentRecord
    {
        public int OfferIndex;
        public int Cost;
    }

    void Awake()
    {
        spawnedPerOffer = new int[offers != null ? offers.Count : 0];
        if (createMinimalUiIfMissing && deploymentRoot == null && offers != null && offers.Count > 0)
            BuildMinimalDeploymentUi();
    }

    /// <summary>
    /// Ждём GameManager: при загрузке сцены порядок скриптов не гарантирован — раньше Instance мог быть null и фаза не включалась.
    /// </summary>
    private IEnumerator Start()
    {
        const float maxWaitSeconds = 20f;
        float waited = 0f;
        while (GameManager.Instance == null && waited < maxWaitSeconds)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[ArmyDeploymentController] GameManager.Instance не найден — фаза расстановки не активирована. Проверь, что в сцене есть GameManager.");
            enabled = false;
            yield break;
        }

        if (GameManager.Instance.GetGameMode() != GameMode.PlayerVsBot)
        {
            Debug.LogWarning("[ArmyDeploymentController] Режим не PlayerVsBot — расстановка отключена. В меню выбери игру против бота или на GameManager выставь Test Game Mode = PlayerVsBot при запуске сцены напрямую.");
            if (deploymentRoot != null)
                deploymentRoot.SetActive(false);
            enabled = false;
            yield break;
        }

        if (offers == null || offers.Count == 0)
        {
            Debug.LogError("[ArmyDeploymentController] Заполни offers (префабы юнитов в списке).");
            enabled = false;
            yield break;
        }

        if (deploymentRoot == null || cardHandContainer == null)
        {
            Debug.LogError("[ArmyDeploymentController] Нет UI расстановки (deploymentRoot / cardHandContainer). Включи Create Minimal UI If Missing или назначь ссылки.");
            enabled = false;
            yield break;
        }

        pointsRemaining = Mathf.Max(0, armyPointBudget);
        GameManager.Instance.BeginArmyDeploymentPhase();
        Debug.Log("[ArmyDeploymentController] Фаза расстановки армии активна — кость недоступна до кнопки «Готов».");

        EnsureEventSystemExists();
        deploymentRoot.SetActive(true);

        BuildCards();
        RefreshBudgetLabel();
        UpdateConfirmButtonState();

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        BuildDeploymentZoneVisual();
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        DestroyDeploymentZoneVisual();
    }

    private void BuildCards()
    {
        if (cardHandContainer == null || offers == null) return;

        for (int i = 0; i < cardHandContainer.childCount; i++)
            Destroy(cardHandContainer.GetChild(i).gameObject);

        for (int i = 0; i < offers.Count; i++)
        {
            DeploymentOfferEntry e = offers[i];
            if (e.unitPrefab == null) continue;

            DeploymentCardUI card;
            if (cardPrefab != null)
            {
                card = Instantiate(cardPrefab, cardHandContainer);
            }
            else
            {
                GameObject go = CreateRuntimeDeploymentCardObject();
                card = go.GetComponent<DeploymentCardUI>();
            }

            card.Setup(this, i, e);
        }
    }

    private GameObject CreateRuntimeDeploymentCardObject()
    {
        GameObject root = new GameObject("DeploymentCard");
        root.transform.SetParent(cardHandContainer, false);
        RectTransform rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 140);

        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);
        bg.raycastTarget = true;

        TMP_Text titleTmp = CreateTmpText(root.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -6), new Vector2(-10, 26), 13f, TextAlignmentOptions.Top);
        TMP_Text costTmp = CreateTmpText(root.transform, "Cost", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-8, 8), new Vector2(44, 28), 16f, TextAlignmentOptions.BottomRight);

        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(root.transform, false);
        RectTransform irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(72, 72);
        irt.anchoredPosition = new Vector2(0, -6);
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;

        var card = root.AddComponent<DeploymentCardUI>();
        card.SetRuntimeRefs(iconImg, titleTmp, costTmp);
        return root;
    }

    private static TMP_Text CreateTmpText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    public RectTransform GetReturnDropZone() => returnDropZone;

    public int GetPointsRemaining() => pointsRemaining;

    public bool TryGetOffer(int index, out DeploymentOfferEntry entry)
    {
        entry = null;
        if (offers == null || index < 0 || index >= offers.Count) return false;
        entry = offers[index];
        return entry != null && entry.unitPrefab != null;
    }

    public bool IsCellInDeploymentZone(Vector2Int cell)
    {
        return cell.x >= deploymentZoneMin.x && cell.x <= deploymentZoneMax.x &&
               cell.y >= deploymentZoneMin.y && cell.y <= deploymentZoneMax.y;
    }

    public bool TryPlaceOfferAtGridCell(int offerIndex, Vector2Int cell)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsArmyDeploymentPhase()) return false;
        if (!TryGetOffer(offerIndex, out DeploymentOfferEntry offer)) return false;
        if (ChessGrid.Instance == null || CameraManager.Instance == null) return false;

        int cost = Mathf.Max(0, offer.costPoints);
        if (cost > pointsRemaining) return false;

        int maxCopies = offer.maxCopies;
        if (maxCopies >= 0 && spawnedPerOffer != null && offerIndex >= 0 && offerIndex < spawnedPerOffer.Length &&
            spawnedPerOffer[offerIndex] >= maxCopies)
            return false;

        if (!ChessGrid.Instance.IsValidCoord(cell.x, cell.y)) return false;
        if (!IsCellInDeploymentZone(cell)) return false;
        if (IsGridCellOccupied(cell)) return false;

        Vector3 world = ChessGrid.Instance.GridToWorldPosition(cell.x, cell.y);
        GameObject inst = Instantiate(offer.unitPrefab, world, Quaternion.identity);
        Unit unit = inst.GetComponent<Unit>();
        if (unit == null)
        {
            Destroy(inst);
            return false;
        }

        unit.owner = Player.Player1;
        unit.currentGridPosition = cell;
        unit.SnapToGrid();

        pointsRemaining -= cost;
        if (spawnedPerOffer != null && offerIndex >= 0 && offerIndex < spawnedPerOffer.Length)
            spawnedPerOffer[offerIndex]++;

        var marker = inst.AddComponent<DeploymentPlacedUnitMarker>();
        marker.Initialize(this);

        deployed[unit] = new DeploymentRecord { OfferIndex = offerIndex, Cost = cost };

        RefreshBudgetLabel();
        UpdateConfirmButtonState();
        return true;
    }

    public void RemoveDeployedUnit(Unit unit)
    {
        if (unit == null || GameManager.Instance == null || !GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (!deployed.TryGetValue(unit, out DeploymentRecord rec)) return;

        pointsRemaining += rec.Cost;
        if (spawnedPerOffer != null && rec.OfferIndex >= 0 && rec.OfferIndex < spawnedPerOffer.Length)
            spawnedPerOffer[rec.OfferIndex] = Mathf.Max(0, spawnedPerOffer[rec.OfferIndex] - 1);

        deployed.Remove(unit);
        Destroy(unit.gameObject);

        RefreshBudgetLabel();
        UpdateConfirmButtonState();
    }

    private bool IsGridCellOccupied(Vector2Int cell)
    {
        Unit[] all = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (var u in all)
        {
            if (u == null || u.GetHealth() <= 0) continue;
            if (ChessGrid.Instance == null) continue;
            Vector2Int gp = ChessGrid.Instance.WorldToGridCoords(u.transform.position);
            if (gp == cell) return true;
        }
        return false;
    }

    /// <summary> Луч из тактической камеры в мировую точку на плоскости поля → клетка сетки. </summary>
    public static bool TryScreenPointToGridCell(Vector2 screenPosition, out Vector2Int cell)
    {
        cell = default;
        if (ChessGrid.Instance == null || CameraManager.Instance == null) return false;
        Camera cam = CameraManager.Instance.GetTacticalCamera();
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        float planeY = ChessGrid.Instance.GridOrigin.y;
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return false;
        float t = (planeY - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;

        Vector3 hit = ray.origin + ray.direction * t;
        cell = ChessGrid.Instance.WorldToGridCoords(hit);
        return ChessGrid.Instance.IsValidCoord(cell.x, cell.y);
    }

    private void RefreshBudgetLabel()
    {
        if (budgetText != null)
            budgetText.text = $"Очки: {pointsRemaining} / {armyPointBudget}";
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmButton == null) return;
        bool ok = ValidateDeploymentRequirements(out string hint);
        confirmButton.interactable = ok;
        if (confirmHintText != null)
            confirmHintText.text = hint;
    }

    private bool ValidateDeploymentRequirements(out string hint)
    {
        hint = "";

        Unit[] all = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        int p1 = 0;
        int kings = 0;
        foreach (var u in all)
        {
            if (u == null || u.GetHealth() <= 0) continue;
            if (u.owner != Player.Player1) continue;
            p1++;
            if (u.chessType == ChessUnitType.King)
                kings++;
        }

        if (requireAtLeastOneUnit && p1 < 1)
        {
            hint = "Поставьте хотя бы одного юнита.";
            return false;
        }

        if (requireExactlyOneKing && kings != 1)
        {
            hint = kings == 0 ? "Нужен ровно один король." : "Допускается только один король.";
            return false;
        }

        hint = "Нажмите, чтобы начать бой.";
        return true;
    }

    private void OnConfirmClicked()
    {
        if (GameManager.Instance == null) return;
        if (!ValidateDeploymentRequirements(out _)) return;

        DestroyDeploymentZoneVisual();

        if (deploymentRoot != null)
            deploymentRoot.SetActive(false);

        foreach (Unit u in deployed.Keys)
        {
            if (u == null) continue;
            if (u.TryGetComponent(out DeploymentPlacedUnitMarker m))
                m.enabled = false;
        }

        GameManager.Instance.CompleteArmyDeploymentPhase();
    }

    private void BuildDeploymentZoneVisual()
    {
        if (!showDeploymentZoneOverlay || ChessGrid.Instance == null)
            return;

        DestroyDeploymentZoneVisual();

        deploymentZoneVisualRoot = new GameObject("DeploymentZoneVisual");
        deploymentZoneVisualRoot.transform.SetParent(transform, false);

        float cell = ChessGrid.Instance.cellSize;
        float y = ChessGrid.Instance.GridOrigin.y + deploymentZoneYOffset;
        // Стандартный Plane Unity — 10×10 в локальных единицах, лежит в XZ, нормаль вверх.
        float scaleXZ = (cell / 10f) * 0.96f;

        deploymentZoneSharedMaterial = CreateDeploymentZoneTileMaterial(deploymentZoneColor);
        Material mat = deploymentZoneSharedMaterial;

        for (int gx = deploymentZoneMin.x; gx <= deploymentZoneMax.x; gx++)
        {
            for (int gy = deploymentZoneMin.y; gy <= deploymentZoneMax.y; gy++)
            {
                if (!ChessGrid.Instance.IsValidCoord(gx, gy))
                    continue;

                Vector3 center = ChessGrid.Instance.GridToWorldPosition(gx, gy);
                center.y = y;

                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Plane);
                tile.name = $"DeployZone_{gx}_{gy}";
                tile.transform.SetParent(deploymentZoneVisualRoot.transform, false);
                tile.transform.position = center;
                tile.transform.localScale = new Vector3(scaleXZ, 1f, scaleXZ);

                Collider col = tile.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);

                MeshRenderer mr = tile.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.sharedMaterial = mat;
            }
        }
    }

    private static Material CreateDeploymentZoneTileMaterial(Color tint)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null)
            sh = Shader.Find("Unlit/Color");
        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        Material mat = new Material(sh);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);
        else if (mat.HasProperty("_Color"))
            mat.color = tint;
        mat.renderQueue = 3000;

        // Прозрачность для URP Unlit
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        return mat;
    }

    private void DestroyDeploymentZoneVisual()
    {
        if (deploymentZoneVisualRoot != null)
        {
            Destroy(deploymentZoneVisualRoot);
            deploymentZoneVisualRoot = null;
        }

        if (deploymentZoneSharedMaterial != null)
        {
            Destroy(deploymentZoneSharedMaterial);
            deploymentZoneSharedMaterial = null;
        }
    }

    private void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>Создаёт Overlay Canvas с нижней полосой для карточек, текстом бюджета и кнопкой «Готов».</summary>
    private void BuildMinimalDeploymentUi()
    {
        GameObject canvasGo = new GameObject("ArmyDeploymentCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasGo.AddComponent<GraphicRaycaster>();

        deploymentRoot = canvasGo;

        // Сначала подложка «зоны возврата», затем рука с карточками — в uGUI последний sibling рисуется сверху
        // и получает raycast. Иначе ReturnDropZone перекрывала карточки и drag не начинался.
        {
            GameObject ret = new GameObject("ReturnDropZone");
            ret.transform.SetParent(canvasGo.transform, false);
            RectTransform rr = ret.AddComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.04f, 0.02f);
            rr.anchorMax = new Vector2(0.96f, 0.30f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            Image rim = ret.AddComponent<Image>();
            rim.color = new Color(0.2f, 0.25f, 0.35f, 0.25f);
            // Логика «отпустил в зоне отмены» — RectangleContainsScreenPoint в DeploymentCardUI; raycast не нужен.
            rim.raycastTarget = false;
            returnDropZone = rr;
        }

        {
            GameObject hand = new GameObject("CardHand");
            hand.transform.SetParent(canvasGo.transform, false);
            RectTransform handRt = hand.AddComponent<RectTransform>();
            handRt.anchorMin = new Vector2(0.04f, 0.02f);
            handRt.anchorMax = new Vector2(0.96f, 0.26f);
            handRt.offsetMin = Vector2.zero;
            handRt.offsetMax = Vector2.zero;
            var hlg = hand.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            cardHandContainer = hand.transform;
        }

        {
            GameObject budgetGo = new GameObject("BudgetText");
            budgetGo.transform.SetParent(canvasGo.transform, false);
            RectTransform br = budgetGo.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(0.04f, 0.72f);
            br.anchorMax = new Vector2(0.5f, 0.96f);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;
            budgetText = budgetGo.AddComponent<TextMeshProUGUI>();
            budgetText.fontSize = 22;
            budgetText.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                budgetText.font = TMP_Settings.defaultFontAsset;
        }

        {
            GameObject hintGo = new GameObject("ConfirmHint");
            hintGo.transform.SetParent(canvasGo.transform, false);
            RectTransform hr = hintGo.AddComponent<RectTransform>();
            hr.anchorMin = new Vector2(0.35f, 0.30f);
            hr.anchorMax = new Vector2(0.65f, 0.38f);
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = Vector2.zero;
            confirmHintText = hintGo.AddComponent<TextMeshProUGUI>();
            confirmHintText.fontSize = 15;
            confirmHintText.alignment = TextAlignmentOptions.Center;
            confirmHintText.color = new Color(1f, 0.85f, 0.6f);
            if (TMP_Settings.defaultFontAsset != null)
                confirmHintText.font = TMP_Settings.defaultFontAsset;
        }

        {
            GameObject btnGo = new GameObject("ConfirmDeploymentButton");
            btnGo.transform.SetParent(canvasGo.transform, false);
            RectTransform brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.38f, 0.18f);
            brt.anchorMax = new Vector2(0.62f, 0.26f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            Image img = btnGo.AddComponent<Image>();
            img.color = new Color(0.15f, 0.45f, 0.22f, 1f);
            confirmButton = btnGo.AddComponent<Button>();

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            RectTransform lr = labelGo.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            TMP_Text lbl = labelGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "Готов — начать бой";
            lbl.fontSize = 18;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                lbl.font = TMP_Settings.defaultFontAsset;
        }
    }
}

[System.Serializable]
public class DeploymentOfferEntry
{
    public GameObject unitPrefab;
    [Min(0)] public int costPoints = 1;
    [Tooltip("-1 = без лимита копий за партию")]
    public int maxCopies = -1;
}
