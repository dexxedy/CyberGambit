using TMPro;
using UnityEngine;

namespace Mission2
{
    /// <summary>
    /// Подпись на тактической «карте» (Screen Space) для цели или терминала Mission 2.
    /// </summary>
    public class TacticalMission2Marker : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;

        public void SetLabel(string text)
        {
            if (labelText != null)
                labelText.text = text ?? string.Empty;
        }

        public void SetColor(Color color)
        {
            if (labelText != null)
                labelText.color = color;
        }
    }
}
