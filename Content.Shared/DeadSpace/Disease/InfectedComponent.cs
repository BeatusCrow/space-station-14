using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.Prototypes;

namespace Content.Shared.DeadSpace.Disease;

/// <summary>
/// Компонент зараженной сущности.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InfectedComponent : Component
{
    /// <summary>
    /// Поле, отображающее способ распространения вируса.
    /// </summary>
    [DataField]
    public List<InfectedaVirusData> Virus = new List<InfectedaVirusData>();
}

/// <summary>
/// Информация о конкретной болезни в организме сущности.
/// </summary>
[DataDefinition]
public sealed partial class InfectedaVirusData
{
    /// <summary>
    /// Прототип вируса, которым сущность заразилась.
    /// </summary>
    [DataField]
    public string VirusId;

    /// <summary>
    /// Способ распространения данного вируса.
    /// </summary>
    [DataField]
    public TypeOfDistribution DistributionWay = TypeOfDistribution.Surface;

    /// <summary>
    /// Текущая стадия болезни.
    /// </summary>
    [DataField]
    public int CurrentStage = 0;

    /// <summary>
    /// Максимальная стадия болезни.
    /// </summary>
    [DataField]
    public int MaxStage;

    /// <summary>
    /// Время когда должна произойти смена стадии.
    /// </summary>
    [DataField]
    public TimeSpan? NextStageTick = null;
}
