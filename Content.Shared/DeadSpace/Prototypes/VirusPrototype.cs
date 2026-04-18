using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.Disease;
using Content.Shared.DeadSpace.Disease.RecoveryActions;
using Content.Shared.DeadSpace.Disease.Events;

namespace Content.Shared.DeadSpace.Prototypes;

/// <summary>  
/// Прототип для различных вирусов. 
/// </summary>  
[Prototype]
public sealed partial class VirusPrototype : IPrototype
{
    /// <summary>
    /// Уникальный идентификатор вируса.
    /// </summary>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// Имя вируса.
    /// На данный момент не используется, но может быть полезно для отображения в интерфейсе или логах.
    /// </summary>
    [DataField]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Тип распространения вируса.
    /// Определяет, как вирус может передаваться между организмами (воздушно-капельный, контактный, через укусы и т.д.).
    /// </summary>
    [DataField]
    public TypeOfDistribution Distribution { get; set; }

    /// <summary>
    /// Время жизни вируса в воздухе или на поверхности, измеряемое в секундах. 0 - не умирает.
    /// </summary>
    [DataField("ttl")]
    public int TTL = 0;

    /// <summary>
    /// Размер вируса, который может влиять на его способность проникать в организм и вызывать инфекцию.
    /// Соответственно маски будут обладать останавливающим эффектом, если размер вируса будет больше, чем размер маски.
    /// TODO: Скорее всего, если вирус будет меньше, чем размер "дырок" маски, то будет задействоваться формула для вычисления
    /// вероятности заражения, которая будет учитывать размер вируса и размер маски.
    /// </summary>
    [DataField]
    public float Size = 0.1f;

    /// <summary>
    /// Время в секундах между распадом облака вируса... То есть через условные 5 секунд вирус
    /// распространится на соседние клетки, образуя равномерное распределение.
    /// </summary>
    [DataField]
    public float TimeBetweenDisintegration = 1f;

    /// <summary>
    /// Количество вирусов, которое может быть сгенерировано пассивно (от зараженного организма) за единицу времени (секунду).
    /// </summary>
    [DataField]
    public float PassiveGeneration = 0.1f;

    /// <summary>
    /// Количество вирусов, которое должно находится в одном тайле, чтобы он начал распадаться и распространяться на соседние тайлы.
    /// TODO: Это поле в данный момент под сильным вопросом :)
    /// </summary>
    [DataField]
    public float MothsForDecay = 0.5f;

    /// <summary>
    /// Стадии болезни, которые определяют симптомы и лечение на каждом этапе развития инфекции.
    /// </summary>
    [DataField("stages")]
    public List<DiseaseStageData> Stages { get; private set; } = new();

    /// <summary>
    /// Действия, которые нужно выполнить при выздоравлении сущности.
    /// </summary>
    [DataField("recoveryActions")]
    public List<BaseRecoveryActionsData> RecoveryActions { get; private set; } = new();

    /// <summary>
    /// События, которые могут вызвать появление вируса в раунде.
    /// </summary>
    [DataField("sourceEvents")]
    public List<BaseVirusSourceData> SourceEvents { get; private set; } = new();
}

[Flags]
public enum TypeOfDistribution : byte
{
    None = 0,
    Air = 1 << 0,
    Surface = 1 << 1,
    Bite = 1 << 2,
}
