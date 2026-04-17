namespace Content.Shared.DeadSpace.Disease.Symptoms;

/// <summary>
/// Базовый класс для симптомов болезни. Содержит общие поля и методы для всех типов симптомов.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[Serializable]
public abstract partial class BaseSymptomData
{
    /// <summary>
    /// Шанс проявления симптома при каждом тике болезни (от 0 до 1).
    /// </summary>
    [DataField("chance")]
    public float Chance { get; protected set; } = 1.0f;

    /// <summary>
    /// Интервал между проверками на проявление симптома в секундах.
    /// Если 0 - проверяется каждый тик системы болезней.
    /// </summary>
    [DataField("interval")]
    public float Interval { get; protected set; } = 1.0f;

    /// <summary>
    /// Сила симптома (может влиять на урон, длительность эффектов и т.д.).
    /// </summary>
    [DataField("severity")]
    public float Severity { get; protected set; } = 1.0f;

    /// <summary>
    /// Имя метода-обработчика для этого симптома.
    /// Должно быть переопределено в наследниках.
    /// </summary>
    public abstract string HandlerMethodName { get; }
}
