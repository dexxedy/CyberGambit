using UnityEngine;

public enum Player { Player1, Player2 }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Player currentPlayer = Player.Player1;

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