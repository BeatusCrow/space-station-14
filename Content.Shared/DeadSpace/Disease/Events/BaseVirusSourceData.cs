using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.Disease.Events;

[ImplicitDataDefinitionForInheritors]
[Serializable]
public abstract partial class BaseVirusSourceData
{
    /// <summary>
    /// Шанс срабатывания события при каждой проверке (0-1).
    /// </summary>
    [DataField("chance")]
    public float Chance { get; protected set; } = 0.1f;

    /// <summary>
    /// Интервал проверки в секундах.
    /// </summary>
    [DataField("checkInterval")]
    public float CheckInterval { get; protected set; } = 60f;

    /// <summary>
    /// Максимальное количество заражений за одно срабатывание.
    /// </summary>
    [DataField("maxInfections")]
    public int MaxInfections { get; protected set; } = 1;

    /// <summary>
    /// Минимальное время с начала раунда (в секундах).
    /// </summary>
    [DataField("minRoundTime")]
    public float MinRoundTime { get; protected set; } = 0f;

    /// <summary>
    /// Имя метода-обработчика.
    /// </summary>
    public abstract string HandlerMethodName { get; }
}
