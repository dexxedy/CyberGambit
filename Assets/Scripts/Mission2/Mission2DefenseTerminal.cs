using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mission2
{
    /// <summary>
    /// Терминал Mission 2: не-танк игрока подходит, жмёт E — снимает защитные сферы с целей.
    /// Состояние на дочернем <see cref="SpriteRenderer"/> в мире: два спрайта (активна / снята защита).
    /// </summary>
    public class Mission2DefenseTerminal : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float interactRadiusMeters = 2.5f;
        [SerializeField] private Key interactKey = Key.E;
        [Tooltip("Только юниты этого игрока могут взломать.")]
        [SerializeField] private Player allowedPlayer = Player.Player1;

        [Header("Shields to disable")]
        [SerializeField] private List<Mission2ObjectiveShield> shields = new List<Mission2ObjectiveShield>();
        [SerializeField] private bool autoFindShieldsInScene;

        [Header("Display (дочерний SpriteRenderer в мире, вариант C)")]
        [Tooltip("Экран терминала: дочерний объект с SpriteRenderer. Пусто — первый SpriteRenderer среди детей.")]
        [SerializeField] private SpriteRenderer statusSpriteRenderer;
        [SerializeField] private bool autoFindFirstChildSpriteRenderer = true;
        [SerializeField] private Sprite spriteProtectionActive;
        [SerializeField] private Sprite spriteProtectionDeactivated;

        [Header("Quest UI")]
        [SerializeField] private int hackTerminalQuestIndex = 0;

        [Header("Tactical map marker")]
        [Tooltip("Подпись на тактической карте (например T, TER).")]
        [SerializeField] private string tacticalMapLabel = "T";

        private bool hacked;

        /// <summary>Терминал уже взломан — маркер на карте можно подсветить иначе.</summary>
        public bool IsHacked => hacked;

        public string TacticalMapLabel =>
            string.IsNullOrWhiteSpace(tacticalMapLabel) ? "T" : tacticalMapLabel.Trim();

        private void Awake()
        {
            if (statusSpriteRenderer == null && autoFindFirstChildSpriteRenderer)
                statusSpriteRenderer = FindFirstChildSpriteRenderer();
        }

        private SpriteRenderer FindFirstChildSpriteRenderer()
        {
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent(out SpriteRenderer sr))
                    return sr;
                sr = child.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null)
                    return sr;
            }

            return GetComponentInChildren<SpriteRenderer>(true);
        }

        private void Start()
        {
            if (autoFindShieldsInScene)
            {
                Mission2ObjectiveShield[] found = FindObjectsByType<Mission2ObjectiveShield>(FindObjectsInactive.Exclude);
                foreach (Mission2ObjectiveShield s in found)
                {
                    if (s != null && !shields.Contains(s))
                        shields.Add(s);
                }
            }

            RefreshDisplay();
        }

        private void Update()
        {
            if (hacked) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
            if (CameraManager.Instance == null || !CameraManager.Instance.IsActionMode()) return;

            Unit unit = CameraManager.Instance.GetCurrentControlledUnit();
            if (unit == null || !unit.IsControlled()) return;
            if (unit.owner != allowedPlayer) return;
            if (unit.IsTankUnit) return;

            float dist = Vector3.Distance(unit.transform.position, transform.position);
            if (dist > interactRadiusMeters) return;

            if (Keyboard.current == null) return;
            if (!Keyboard.current[interactKey].wasPressedThisFrame) return;

            HackTerminal();
        }

        private void HackTerminal()
        {
            hacked = true;
            foreach (Mission2ObjectiveShield s in shields)
            {
                if (s != null)
                    s.SetProtectionActive(false);
            }

            RefreshDisplay();
            MissionQuestUI.Instance?.Complete(hackTerminalQuestIndex);
        }

        private void RefreshDisplay()
        {
            if (statusSpriteRenderer == null) return;

            Sprite next = hacked ? spriteProtectionDeactivated : spriteProtectionActive;
            if (next != null)
                statusSpriteRenderer.sprite = next;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactRadiusMeters);
        }
#endif
    }
}
