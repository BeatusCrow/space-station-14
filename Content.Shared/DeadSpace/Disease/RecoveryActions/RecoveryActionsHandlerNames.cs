namespace Content.Shared.DeadSpace.Disease.RecoveryActions;

/// <summary>
/// Идентификаторы обработчиков симптомов.
/// Используются для связи данных симптома (Shared) с обработчиком (Server).
/// Сами обработчики реализованы в Content.Server\DeadSpace\Disease\DiseaseSystem.cs
/// </summary>
public static class RecoveryActionsHandlerNames
{
    public const string Laughter = "RecoveryActionLaughter";
    // Добавляйте новые симптомы сюда
}

