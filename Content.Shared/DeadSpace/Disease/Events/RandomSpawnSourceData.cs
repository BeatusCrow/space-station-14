using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.Disease.Events;

[DataDefinition]
public sealed partial class RandomSpawnSourceData : BaseVirusSourceData
{
    [DataField("requireAlive")]
    public bool RequireAlive { get; private set; } = true;

    [DataField("requireMind")]
    public bool RequireMind { get; private set; } = true;

    [DataField("targetJob")]
    public string? TargetJob { get; private set; }

    public override string HandlerMethodName => "RandomSpawnSource";
}
