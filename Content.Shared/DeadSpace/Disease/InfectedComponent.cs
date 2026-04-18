using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.DeadSpace.Prototypes;
using Content.Shared.DeadSpace.Disease.Symptoms;
using Robust.Shared.Timing;
using Content.Shared.DeadSpace.Disease.Treatments;

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

    /// <summary>
    /// Добавлен ли вирус в кровь.
    /// </summary>
    [DataField]
    public bool VirusAddedToBlood = false;

    /// <summary>
    /// Список проявляющихся симптомов и время, когда они должны быть проверены в следующий раз.
    /// </summary>
    [DataField]
    public Dictionary<int, TimeSpan> SymptomCooldowns { get; private set; } = new();

    /// <summary>
    /// Прогресс лечения для каждого типа лечения.
    /// Ключ: хэш лечения, Значение: накопленное время успешных проверок.
    /// </summary>
    [DataField]
    public Dictionary<int, float> TreatmentProgress { get; private set; } = new();

    /// <summary>
    /// Время последней проверки для каждого лечения.
    /// </summary>
    [DataField]
    public Dictionary<int, TimeSpan> TreatmentLastCheck { get; private set; } = new();

    /// <summary>
    /// Был ли прогресс лечения прерван (условие не выполнилось).
    /// </summary>
    [DataField]
    public Dictionary<int, bool> TreatmentInterrupted { get; private set; } = new();

    /// <summary>
    /// Перерыв между мгновенными способами лечения, чтобы предотвратить спам и ошибки.
    /// </summary>
    [DataField]
    public Dictionary<int, TimeSpan> TreatmentCooldowns { get; private set; } = new();

    /// <summary>
    /// Инициализирует таймер для симптома при первом появлении.
    /// </summary>
    public void InitSymptomTimer(BaseSymptomData symptom, IGameTiming timing, int symptomIndex)
    {
        if (!SymptomCooldowns.ContainsKey(symptomIndex))
        {
            SymptomCooldowns[symptomIndex] = timing.CurTime + TimeSpan.FromSeconds(symptom.Interval);
        }
    }

    /// <summary>
    /// Проверяет, готов ли симптом к срабатыванию.
    /// </summary>
    public bool IsSymptomReady(BaseSymptomData symptom, IGameTiming timing, int symptomIndex)
    {
        if (!SymptomCooldowns.TryGetValue(symptomIndex, out var nextTime))
        {
            // Первый запуск
            SymptomCooldowns[symptomIndex] = timing.CurTime + TimeSpan.FromSeconds(symptom.Interval);
            return true;
        }

        if (timing.CurTime >= nextTime)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Сбрасывает таймер симптома после срабатывания.
    /// </summary>
    public void ResetSymptomTimer(BaseSymptomData symptom, IGameTiming timing, int symptomIndex)
    {
        SymptomCooldowns[symptomIndex] = timing.CurTime + TimeSpan.FromSeconds(symptom.Interval);
    }

    /// <summary>
    /// Генерирует уникальный индекс для симптома в рамках вируса.
    /// </summary>
    public static int GetSymptomIndex(VirusPrototype proto, DiseaseStageData stage, BaseSymptomData symptom)
    {
        // Комбинируем хеши для уникальности
        return HashCode.Combine(proto.ID, stage.Stage, symptom.GetType().Name);
    }

    /// <summary>
    /// Генерирует уникальный индекс для лечения.
    /// </summary>
    public static int GetTreatmentIndex(VirusPrototype proto, DiseaseStageData stage, BaseTreatmentData treatment)
    {
        return HashCode.Combine(proto.ID, stage.Stage, treatment.Id);
    }

    /// <summary>
    /// Обновляет прогресс лечения.
    /// </summary>
    public void UpdateTreatmentProgress(int treatmentIndex, BaseTreatmentData treatment, IGameTiming timing, bool conditionMet)
    {
        var currentTime = timing.CurTime;

        // Инициализация при первом вызове
        if (!TreatmentLastCheck.ContainsKey(treatmentIndex))
        {
            TreatmentLastCheck[treatmentIndex] = currentTime;
            TreatmentProgress[treatmentIndex] = 0f;
            TreatmentInterrupted[treatmentIndex] = false;
        }

        var lastCheck = TreatmentLastCheck[treatmentIndex];
        var timeSinceLastCheck = (float)(currentTime - lastCheck).TotalSeconds;

        // Проверяем интервал
        if (timeSinceLastCheck < treatment.CheckInterval)
            return;

        TreatmentLastCheck[treatmentIndex] = currentTime;

        if (conditionMet && !TreatmentInterrupted[treatmentIndex])
        {
            // Условие выполнено — накапливаем прогресс
            TreatmentProgress[treatmentIndex] += timeSinceLastCheck;

            // Логируем прогресс (опционально)
            var progress = TreatmentProgress[treatmentIndex];
            var required = treatment.RequiredDuration;
            if (required > 0)
            {
                Logger.Debug($"Прогресс лечения: {progress:F1}/{required:F1} сек ({progress / required * 100:F0}%)");
            }
        }
        else if (!conditionMet)
        {
            // Условие нарушено — сбрасываем прогресс
            if (TreatmentProgress[treatmentIndex] > 0)
            {
                Logger.Debug($"Прогресс лечения сброшен (условие не выполнено)");
            }
            TreatmentProgress[treatmentIndex] = 0f;
            TreatmentInterrupted[treatmentIndex] = true;
        }
        else if (conditionMet && TreatmentInterrupted[treatmentIndex])
        {
            // Условие снова выполнено, но прогресс был прерван — начинаем заново
            Logger.Debug($"Прогресс лечения начат заново");
            TreatmentProgress[treatmentIndex] = timeSinceLastCheck;
            TreatmentInterrupted[treatmentIndex] = false;
        }
    }

    /// <summary>
    /// Проверяет, завершено ли лечение.
    /// </summary>
    public bool IsTreatmentComplete(int treatmentIndex, BaseTreatmentData treatment)
    {
        if (treatment.RequiredDuration <= 0)
            return true; // Мгновенное лечение

        return TreatmentProgress.GetValueOrDefault(treatmentIndex) >= treatment.RequiredDuration;
    }

    /// <summary>
    /// Сбрасывает прогресс лечения после успешного применения.
    /// </summary>
    public void ResetTreatmentProgress(int treatmentIndex)
    {
        TreatmentProgress.Remove(treatmentIndex);
        TreatmentLastCheck.Remove(treatmentIndex);
        TreatmentInterrupted.Remove(treatmentIndex);
    }
}

/// <summary>
/// Определяет силу воздействия лечения на болезнь.
/// </summary>
public enum TreatmentStrength : byte
{
    /// <summary>
    /// Откатывает болезнь на одну стадию назад.
    /// </summary>
    RegressOneStage = 0,

    /// <summary>
    /// Откатывает болезнь на указанное количество стадий.
    /// </summary>
    RegressMultipleStages = 1,

    /// <summary>
    /// Полностью излечивает болезнь.
    /// </summary>
    Cure = 2,

    /// <summary>
    /// Замедляет прогрессию болезни (увеличивает время до следующей стадии).
    /// </summary>
    SlowProgression = 3,

    /// <summary>
    /// Останавливает прогрессию на определённое время.
    /// </summary>
    PauseProgression = 4
}
