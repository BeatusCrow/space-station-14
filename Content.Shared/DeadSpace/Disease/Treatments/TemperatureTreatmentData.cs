using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.Treatments;

// Другой тип лечения - через температуру
[DataDefinition]
public sealed partial class TemperatureTreatmentData : BaseTreatmentData
{
    [DataField("minTemperature")]
    public float MinTemperature { get; private set; } = 310f; // нормальная температура тела

    [DataField("maxTemperature")]
    public float MaxTemperature { get; private set; } = 313f;

    public override string HandlerMethodName => TreatmentsHandlerNames.Temperature;
}
