using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Визуализация хода бота в тактике без движения камеры:
/// - линия траектории (на длину бюджета)
/// - маркер цели
/// - пинг атаки/события
/// </summary>
public class BotTurnTacticalOverlay : MonoBehaviour
{
    public static BotTurnTacticalOverlay Instance;

    [Header("Path Line")]
    [SerializeField] private Color pathColor = new Color(1f, 0.35f, 0.25f, 0.9f);
    [SerializeField] private float pathWidth = 0.08f;
    [SerializeField] private float pathHeightOffset = 0.08f;

    [Header("Target Marker")]
    [SerializeField] private Color targetColor = new Color(1f, 0.55f, 0.2f, 0.95f);
    [SerializeField] private float targetMarkerScale = 0.35f;

    [Header("Ping")]
    [SerializeField] private Color pingColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float pingScale = 0.45f;
    [SerializeField] private float pingLifetime = 0.6f;

    private readonly Dictionary<Unit, LineRenderer> pathByUnit = new Dictionary<Unit, LineRenderer>();
    private readonly Dictionary<Unit, GameObject> targetMarkerByUnit = new Dictionary<Unit, GameObject>();

    private Material lineMat;
    private Material solidMat;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureMaterials()
    {
        if (lineMat == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            lineMat = new Material(s);
        }
        if (solidMat == null)
        {
            Shader s = Shader.Find("Standard");
            solidMat = new Material(s);
            solidMat.EnableKeyword("_EMISSION");
        }
    }

    public void ShowMovePath(Unit unit, IReadOnlyList<Vector3> points, Vector3 targetWorld)
    {
        if (unit == null || points == null || points.Count < 2) return;
        EnsureMaterials();

        LineRenderer lr = GetOrCreateLine(unit);
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            p.y += pathHeightOffset;
            lr.SetPosition(i, p);
        }

        GameObject marker = GetOrCreateTargetMarker(unit);
        marker.transform.position = targetWorld + Vector3.up * pathHeightOffset;
    }

    public void Clear(Unit unit)
    {
        if (unit == null) return;
        if (pathByUnit.TryGetValue(unit, out LineRenderer lr) && lr != null)
            lr.positionCount = 0;
        if (targetMarkerByUnit.TryGetValue(unit, out GameObject m) && m != null)
            m.SetActive(false);
    }

    public void Ping(Vector3 worldPos)
    {
        EnsureMaterials();

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BotTurnPing";
        go.transform.position = worldPos + Vector3.up * pathHeightOffset;
        go.transform.localScale = Vector3.one * pingScale;

        // убираем коллайдер, чтобы не мешал
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null)
        {
            r.sharedMaterial = solidMat;
            r.sharedMaterial.color = pingColor;
            r.sharedMaterial.SetColor("_EmissionColor", pingColor * 1.2f);
        }

        Destroy(go, Mathf.Max(0.05f, pingLifetime));
    }

    private LineRenderer GetOrCreateLine(Unit unit)
    {
        if (pathByUnit.TryGetValue(unit, out LineRenderer lr) && lr != null)
            return lr;

        GameObject go = new GameObject($"BotPath_{unit.name}");
        go.transform.SetParent(transform, false);
        lr = go.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.startWidth = pathWidth;
        lr.endWidth = pathWidth;
        lr.startColor = pathColor;
        lr.endColor = pathColor;
        lr.useWorldSpace = true;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 6;
        lr.positionCount = 0;

        pathByUnit[unit] = lr;
        return lr;
    }

    private GameObject GetOrCreateTargetMarker(Unit unit)
    {
        if (targetMarkerByUnit.TryGetValue(unit, out GameObject m) && m != null)
        {
            m.SetActive(true);
            return m;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"BotTarget_{unit.name}";
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(targetMarkerScale, 0.02f, targetMarkerScale);

        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null)
        {
            EnsureMaterials();
            r.sharedMaterial = solidMat;
            r.sharedMaterial.color = targetColor;
            r.sharedMaterial.SetColor("_EmissionColor", targetColor * 1.1f);
        }

        targetMarkerByUnit[unit] = go;
        return go;
    }
}

