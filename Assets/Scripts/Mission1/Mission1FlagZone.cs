using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mission1FlagZone : MonoBehaviour
{
    public enum FlagOwner
    {
        None,
        Player1,
        Player2
    }

    [Header("Debug / Setup")]
    [SerializeField] private int flagIndex = 0;

    // Keep-logic: если в зоне никого нет, владелец не сбрасывается.
    [SerializeField] private FlagOwner currentOwner = FlagOwner.None;

    [Header("Ground ring (optional)")]
    [Tooltip("MeshRenderer кольца на полу (дочерний Plane/Quad). Пусто — без подсветки.")]
    [SerializeField] private Renderer groundRingRenderer;
    [SerializeField] private Color neutralColor = new Color(1f, 0.2f, 0.2f, 0.4f);
    [SerializeField] private Color player1Color = new Color(0.2f, 0.6f, 1f, 0.45f);
    [SerializeField] private Color player2Color = new Color(1f, 0.25f, 0.25f, 0.45f);

    private readonly HashSet<Unit> unitsInZone = new HashSet<Unit>();
    private MaterialPropertyBlock groundRingMpb;
    private FlagOwner lastVisualOwner = (FlagOwner)(-1);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public int FlagIndex => flagIndex;
    public FlagOwner CurrentOwner => currentOwner;

    public void Initialize(int index)
    {
        flagIndex = index;
        // Начальное состояние - "никто не владеет", пока игроки не зайдут в зону.
        currentOwner = FlagOwner.None;
        lastVisualOwner = (FlagOwner)(-1);
        ApplyGroundRingColor();
    }

    private void Start()
    {
        ApplyGroundRingColor();
    }

    private void Awake()
    {
        // На случай, если инспектором не выставили collider как trigger.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit == null) return;
        if (unit.GetHealth() <= 0) return;

        unitsInZone.Add(unit);
        RecalculateOwner();
    }

    private void OnTriggerExit(Collider other)
    {
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit == null) return;

        unitsInZone.Remove(unit);
        RecalculateOwner();
    }

    private void RecalculateOwner()
    {
        // Если юнит умер, но не успел триггернуться на выходе - убираем его при пересчёте.
        unitsInZone.RemoveWhere(u => u == null || u.GetHealth() <= 0);

        bool hasPlayer1Unit = unitsInZone.Any(u => u.owner == Player.Player1);
        bool hasPlayer2Unit = unitsInZone.Any(u => u.owner == Player.Player2);

        if (hasPlayer1Unit)
        {
            currentOwner = FlagOwner.Player1;
        }
        else if (hasPlayer2Unit)
        {
            currentOwner = FlagOwner.Player2;
        }
        // else: keep currentOwner (keep-логика)

        ApplyGroundRingColor();
    }

    private void ApplyGroundRingColor()
    {
        if (groundRingRenderer == null || lastVisualOwner == currentOwner)
            return;

        lastVisualOwner = currentOwner;
        groundRingMpb ??= new MaterialPropertyBlock();
        groundRingRenderer.GetPropertyBlock(groundRingMpb);

        Color c = OwnerToRingColor(currentOwner);
        Material mat = groundRingRenderer.sharedMaterial;
        if (mat != null && mat.HasProperty(BaseColorId))
            groundRingMpb.SetColor(BaseColorId, c);
        else
            groundRingMpb.SetColor(ColorId, c);

        groundRingRenderer.SetPropertyBlock(groundRingMpb);
    }

    private Color OwnerToRingColor(FlagOwner owner)
    {
        switch (owner)
        {
            case FlagOwner.Player1: return player1Color;
            case FlagOwner.Player2: return player2Color;
            default: return neutralColor;
        }
    }

    public bool IsOwnedBy(Player player)
    {
        return (player == Player.Player1 && currentOwner == FlagOwner.Player1) ||
               (player == Player.Player2 && currentOwner == FlagOwner.Player2);
    }

    public Player? GetOwnerPlayerOrNull()
    {
        switch (currentOwner)
        {
            case FlagOwner.Player1: return Player.Player1;
            case FlagOwner.Player2: return Player.Player2;
            default: return null;
        }
    }
}

