using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.Treatments;

/// <summary>
/// Базовый класс для способов лечения вирусов.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class BaseTreatmentData
{
    // Общие поля для всех типов лечения
    [DataField("effectiveness")]
    public float Effectiveness { get; private set; } = 1.0f;

    /// <summary>
    /// Имя метода-обработчика для метода лечения.
    /// Должно быть переопределено в наследниках.
    /// </summary>
    public abstract string HandlerMethodName { get; }
}
