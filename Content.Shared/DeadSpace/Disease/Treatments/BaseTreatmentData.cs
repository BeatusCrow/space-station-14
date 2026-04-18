using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.Treatments;

/// <summary>
/// Базовый класс для способов лечения вирусов.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class BaseTreatmentData
{
    [DataField("id")]
    public string? Id { get; protected set; }

    // Общие поля для всех типов лечения
    [DataField("effectiveness")]
    public float Effectiveness { get; private set; } = 1.0f;

    /// <summary>
    /// Необходимая длительность НЕПРЕРЫВНОГО выполнения условия (в секундах).
    /// Если 0 — лечение срабатывает мгновенно при первой проверке.
    /// </summary>
    [DataField("requiredDuration")]
    public float RequiredDuration { get; protected set; } = 0f;

    /// <summary>
    /// Интервал проверки условия (в секундах).
    /// </summary>
    [DataField("checkInterval")]
    public float CheckInterval { get; protected set; } = 1.0f;

    /// <summary>
    /// Тип воздействия лечения на болезнь.
    /// </summary>
    [DataField("strength")]
    public TreatmentStrength Strength { get; protected set; } = TreatmentStrength.RegressOneStage;

    /// <summary>
    /// Количество стадий для отката (используется при Strength = RegressMultipleStages).
    /// </summary>
    [DataField("stagesToRegress")]
    public int StagesToRegress { get; protected set; } = 1;

    /// <summary>
    /// Множитель замедления прогрессии (используется при Strength = SlowProgression).
    /// Например, 2.0 = стадия длится в 2 раза дольше.
    /// </summary>
    [DataField("slowMultiplier")]
    public float SlowMultiplier { get; protected set; } = 2.0f;

    /// <summary>
    /// Длительность паузы прогрессии в секундах (используется при Strength = PauseProgression).
    /// </summary>
    [DataField("pauseDuration")]
    public float PauseDuration { get; protected set; } = 30f;

    [DataField("cooldown")]
    public float Cooldown { get; protected set; } = 1f;

    /// <summary>
    /// Имя метода-обработчика для метода лечения.
    /// Должно быть переопределено в наследниках.
    /// </summary>
    public abstract string HandlerMethodName { get; }
}
