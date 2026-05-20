using System.Collections;

public interface IBotMissionBrain
{
    /// <summary>Возвращает true, если этот brain применим к текущей сцене/миссии.</summary>
    bool CanRun();

    /// <summary>
    /// Выбор ходящего юнита бота до броска кубика. При необходимости вызвать controller.SetPickedBotActingUnitForTurn.
    /// Если юнит не задан, BotController использует SelectBestUnit.
    /// </summary>
    IEnumerator PickActingUnitBeforeDice(BotController controller);

    /// <summary>Выполняет ход бота для миссии. Должен сам завершить ход через controller.EndTurnInternal().</summary>
    IEnumerator ExecuteTurn(BotController controller, float moveBudgetMeters);
}

