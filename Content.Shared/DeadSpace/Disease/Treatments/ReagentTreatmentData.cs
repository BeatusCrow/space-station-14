using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.Treatments;

// Конкретный тип лечения - через реагент
[DataDefinition]
public sealed partial class ReagentTreatmentData : BaseTreatmentData
{
    [DataField("reagent", required: true)]
    public string ReagentId { get; private set; } = string.Empty;

    [DataField("amount", required: true)]
    public int Amount { get; private set; }

    public override string HandlerMethodName => TreatmentsHandlerNames.Reagent;
}
