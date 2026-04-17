namespace Content.Shared.DeadSpace.Disease.Treatments;

/// <summary>
/// Идентификаторы обработчиков методов лечения.
/// Используются для связи данных симптома (Shared) с обработчиком (Server).
/// Сами обработчики реализованы в Content.Server\DeadSpace\Disease\DiseaseSystem.cs
/// </summary>
public static class TreatmentsHandlerNames
{
    public const string Reagent = "ReagentTreatment";
    public const string Temperature = "TemperatureTreatment";
    // Добавляйте новые методы лечения сюда
}

