using Content.Shared.DeadSpace.Disease.Treatments;
using Content.Shared.DeadSpace.Disease.Symptoms;

namespace Content.Shared.DeadSpace.Disease;

/// <summary>
/// Класс, содержащий информацию о стадии болезни.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseStageData
{
    /// <summary>
    /// Идентификатор текущей стадии.
    /// </summary>
    [DataField("stage", required: true)]
    public int Stage { get; private set; }

    /// <summary>
    /// Длительность текущей стадии болезни.
    /// </summary>
    [DataField("duration")]
    public int Duration { get; private set; } = 300; // длительность в секундах по умолчанию

    /// <summary>
    /// Шанс на выздоравление в момент смены стадии.
    /// </summary>
    [DataField("recoveryСhance")]
    public float ChanceOfRecovery = 0.2f;

    /// <summary>
    /// Шанс болезни на переход к следующей стадии.
    /// Проверяется после шанса на выздоравление.
    /// </summary>
    [DataField("transitionСhanceUp")]
    public float ChanceOfTransitionUp = 0.8f;

    /// <summary>
    /// Шанс болезни на переход к предыдущей стадии. Проверяется после шанса на переход к следующей стадии.
    /// Если данный шанс не сработает, то болезнь вновь начнет проходить текущий этап.
    /// </summary>
    [DataField("transitionСhanceBack")]
    public float ChanceOfTransitionBack = 0.2f;

    /// <summary>
    /// Список симптомов болезни на текущем этапе.
    /// </summary>
    [DataField("symptoms")]
    public List<BaseSymptomData> Symptoms { get; private set; } = new();

    /// <summary>
    /// Список способов лечения болезни на текущем этапе.
    /// </summary>
    [DataField("treatments")]
    public List<BaseTreatmentData> Treatments { get; private set; } = new();
}
