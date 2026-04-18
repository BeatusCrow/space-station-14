using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;
using Content.Shared.DeadSpace.Prototypes;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Shared.DeadSpace.Disease;

[Serializable, NetSerializable]
public sealed partial class VirusReagentData : ReagentData
{
    [DataField]
    public List<string> VirusId = new List<string>();

    [DataField]
    public TypeOfDistribution DistributionWay = TypeOfDistribution.Surface;

    [DataField]
    public int CurrentStage = 0;

    [DataField]
    public int MaxStage;

    [DataField]
    public TimeSpan? NextStageTick = null;

    public override bool Equals(ReagentData? other)
    {
        if (other is not VirusReagentData o)
            return false;

        return VirusId == o.VirusId &&
               DistributionWay == o.DistributionWay &&
               CurrentStage == o.CurrentStage &&
               MaxStage == o.MaxStage &&
               NextStageTick == o.NextStageTick;
    }

    public override ReagentData Clone()
    {
        return new VirusReagentData
        {
            VirusId = this.VirusId,
            DistributionWay = this.DistributionWay,
            CurrentStage = this.CurrentStage,
            MaxStage = this.MaxStage,
            NextStageTick = this.NextStageTick
        };
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(VirusId, DistributionWay, CurrentStage, MaxStage, NextStageTick);
    }
}
