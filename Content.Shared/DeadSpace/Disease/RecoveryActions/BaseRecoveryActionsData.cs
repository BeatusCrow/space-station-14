namespace Content.Shared.DeadSpace.Disease.RecoveryActions;

/// <summary>
/// Базовый класс для действий, которые нужно выполнить во время выздоравления.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[Serializable]
public abstract partial class BaseRecoveryActionsData
{
    /// <summary>
    /// Шанс проявления этого действия.
    /// </summary>
    [DataField("chance")]
    public float Chance { get; protected set; } = 1.0f;

    /// <summary>
    /// Имя метода-обработчика для этого действия.
    /// Должно быть переопределено в наследниках.
    /// </summary>
    public abstract string HandlerMethodName { get; }
}
