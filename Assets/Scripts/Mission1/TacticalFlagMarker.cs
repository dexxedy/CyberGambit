using TMPro;
using UnityEngine;

/// <summary>
/// Буквенный маркер флага для тактики (A/B/C). Позицию на экране выставляет контроллер.
/// </summary>
public class TacticalFlagMarker : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI letterText;

    public void SetLetter(char letter)
    {
        if (letterText != null)
            letterText.text = letter.ToString();
    }

    public void SetColor(Color color)
    {
        if (letterText != null)
            letterText.color = color;
    }
}

