using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.Disease.Events;

[DataDefinition]
public sealed partial class HypothermiaSourceData : BaseVirusSourceData
{
    /// <summary>
    /// Пороговая температура для срабатывания.
    /// </summary>
    [DataField("temperatureThreshold")]
    public float TemperatureThreshold { get; private set; } = 310f;

    /// <summary>
    /// Время, которое сущность должна провести при низкой температуре (в секундах).
    /// </summary>
    [DataField("exposureTime")]
    public float ExposureTime { get; private set; } = 30f;

    public override string HandlerMethodName => "HypothermiaSource";
}
