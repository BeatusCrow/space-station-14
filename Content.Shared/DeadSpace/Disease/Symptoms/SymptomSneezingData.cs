using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.Symptoms;

[DataDefinition]
public sealed partial class SymptomSneezingData : BaseSymptomData
{
    [DataField("volume")]
    public float Volume { get; private set; } = -5f;

    [DataField("particleCount")]
    public int ParticleCount { get; private set; } = 10;

    [DataField("spreadRange")]
    public float SpreadRange { get; private set; } = 1.5f;

    public override string HandlerMethodName => SymptomHandlerNames.Sneezing;
}

