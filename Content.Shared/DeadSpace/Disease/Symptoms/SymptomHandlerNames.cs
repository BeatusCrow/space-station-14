namespace Content.Shared.DeadSpace.Disease.Symptoms;

/// <summary>
/// Идентификаторы обработчиков симптомов.
/// Используются для связи данных симптома (Shared) с обработчиком (Server).
/// Сами обработчики реализованы в Content.Server\DeadSpace\Disease\DiseaseSystem.cs
/// </summary>
public static class SymptomHandlerNames
{
    public const string Sneezing = "SymptomSneezing";
    public const string Coughing = "SymptomCoughing";
    public const string Damage = "SymptomDamage";
    public const string Fever = "SymptomFever";
    public const string Nausea = "SymptomNausea";
    // Добавляйте новые симптомы сюда
}

