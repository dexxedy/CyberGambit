using UnityEngine;

namespace Mission2
{
    /// <summary>
    /// Сфера-защита вокруг <see cref="DestructibleObjective"/>. Пока активна — урон цели не проходит.
    /// Вешай на дочерний объект с коллайдером (или включи Auto Create).
    /// </summary>
    public class Mission2ObjectiveShield : MonoBehaviour
    {
        [Tooltip("Если true — создаёт дочернюю сферу с коллайдером при старте.")]
        [SerializeField] private bool autoCreateSphere = true;
        [SerializeField] private float sphereRadius = 2.5f;
        [SerializeField] private bool startProtected = true;
        [SerializeField] private Color shieldColor = new Color(0.2f, 0.7f, 1f, 0.25f);

        [Header("Optional manual refs")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Collider blockingCollider;

        private bool protectionActive;
        private Renderer[] shieldRenderers;

        public bool IsProtectionActive => protectionActive;

        private void Awake()
        {
            if (autoCreateSphere && visualRoot == null)
                CreateShieldVisual();

            if (visualRoot == null)
                visualRoot = gameObject;

            if (blockingCollider == null)
                blockingCollider = GetComponent<Collider>() ?? visualRoot.GetComponent<Collider>();

            shieldRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            SetProtectionActive(startProtected);
        }

        private void CreateShieldVisual()
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ProtectionSphere";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            float d = Mathf.Max(0.1f, sphereRadius * 2f);
            sphere.transform.localScale = new Vector3(d, d, d);

            var col = sphere.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                var mat = sh != null ? new Material(sh) : renderer.sharedMaterial;
                if (mat != null && sh != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", shieldColor);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", shieldColor);
                    renderer.material = mat;
                }
            }

            visualRoot = sphere;
            blockingCollider = sphere.GetComponent<Collider>();
        }

        public void SetProtectionActive(bool active)
        {
            protectionActive = active;
            if (blockingCollider != null)
                blockingCollider.enabled = active;
            if (visualRoot != null)
                visualRoot.SetActive(active);
            else
            {
                foreach (var r in shieldRenderers)
                {
                    if (r != null) r.enabled = active;
                }
            }
        }
    }
}
