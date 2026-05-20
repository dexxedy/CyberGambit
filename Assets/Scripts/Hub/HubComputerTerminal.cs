using UnityEngine;
using UnityEngine.InputSystem;

public class HubComputerTerminal : MonoBehaviour
{
    [SerializeField] private MissionSelectUI missionUI;
    [SerializeField] private float interactRadius = 3f;
    [SerializeField] private bool requireActionMode = true;

    private Transform playerTransform;

    public void Initialize(MissionSelectUI ui)
    {
        missionUI = ui;
    }

    private void Start()
    {
        // Simple search for player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void Update()
    {
        if (missionUI == null) return;
        if (missionUI.IsOpen)
        {
            HubInteractionHintUI.Instance?.HidePrompt();
            return;
        }

        if (requireActionMode && CameraManager.Instance != null && !CameraManager.Instance.IsActionMode())
        {
            HubInteractionHintUI.Instance?.HidePrompt();
            return;
        }

        // Dynamically find player if missing or if CameraManager has it
        if (playerTransform == null || (CameraManager.Instance != null && CameraManager.Instance.GetCurrentControlledUnit() != null))
        {
            playerTransform = CameraManager.Instance?.GetCurrentControlledUnit()?.transform;
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= interactRadius)
            {
                HubInteractionHintUI.Instance?.ShowPrompt();
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    missionUI.Show();
                    HubInteractionHintUI.Instance?.HidePrompt();
                }
                }
                else
                {
                // Only hide if we are the only terminal or if we are certain no one else is showing it.
                // Simple approach: hide if distance > radius.
                HubInteractionHintUI.Instance?.HidePrompt();
                }
                }
                else
                {
                HubInteractionHintUI.Instance?.HidePrompt();
                }
                }

    public void OpenMissionUI()
    {
        if (missionUI != null) missionUI.Show();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}