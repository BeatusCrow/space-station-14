using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.DeadSpace.Disease;

namespace Content.Shared.DeadSpace.Disease.RecoveryActions;

[DataDefinition]
public sealed partial class RecoveryActionLaughterData : BaseRecoveryActionsData
{
    public override string HandlerMethodName => RecoveryActionsHandlerNames.Laughter;
}

