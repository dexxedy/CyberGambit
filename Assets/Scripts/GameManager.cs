using UnityEngine;

public enum Player { Player1, Player2 } // Enum для игроков

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // Singleton для доступа из других скриптов
    public Player currentPlayer = Player.Player1; // Начинаем с твоего хода (Player1)

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SwitchTurn()
    {
        currentPlayer = (currentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        Debug.Log($"Ход перешёл к {currentPlayer}");
    }
}