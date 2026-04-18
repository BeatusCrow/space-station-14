using Content.Server.Chat.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Temperature.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;
using Content.Shared.DeadSpace.Prototypes;
using Content.Shared.DeadSpace.Disease;
using Content.Shared.DeadSpace.Disease.Symptoms;
using Content.Shared.DeadSpace.Disease.Treatments;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Reflection;
using Content.Shared.DeadSpace.Disease.RecoveryActions;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Server.DeadSpace.Disease;

/// <summary>
/// Основа системы вирусов. Отвечает за обновление состояния болезни, обработку симптомов и переход между стадиями.
/// А также за распространение болезни между сущностями (если это предусмотрено механикой).
/// </summary>
public sealed class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    // Кеш делегатов для вызова методов симптомов
    private readonly Dictionary<string, Action<EntityUid, BaseSymptomData>> _handlerSymptomCache = new();
    private readonly Dictionary<string, Action<EntityUid, BaseRecoveryActionsData>> _handlerActionsCache = new();
    private readonly Dictionary<string, Func<EntityUid, BaseTreatmentData, bool>> _handlerTreatmentCache = new();

    private List<string> _fingerlessGloves = new List<string>() // беспалые перчатки не должны защищать от заражения
    {
        "ClothingHandsGlovesFingerlessInsulated",
        "ClothingHandsGlovesFingerless",
        "ClothingHandsGlovesMercFingerless"
    };

    public override void Initialize()
    {
        // когда зараженный берет предметы в руки
        SubscribeLocalEvent<InfectedComponent, DidEquipHandEvent>(OnGotEquippedHand);

        //SubscribeLocalEvent<InfectedPlagueComponent, ComponentInit>(OnComponentInit);
        //// если кто-то бьет другого зараженным объектом.
        //SubscribeLocalEvent<InfectedItemPlagueComponent, MeleeHitEvent>(OnMeleeHit);
        //// взаимодействие с зараженным предметом
        //SubscribeLocalEvent<InfectedItemPlagueComponent, GettingInteractedWithAttemptEvent>(OnInteractionAttemptEvent);
        //// снятие зараженного предмета
        //SubscribeLocalEvent<InfectedItemPlagueComponent, GotUnequippedEvent>(OnGotUnequipped);
        //// надевание зараженного предмета
        //SubscribeLocalEvent<InfectedItemPlagueComponent, GotEquippedEvent>(OnGotEquipped);
        //// когда трогают зараженного
        //SubscribeLocalEvent<InfectedPlagueComponent, GettingInteractedWithAttemptEvent>(OnInteractionWithInfectedHumanEvent);
        //// когда зараженный берет предметы в руки
        //SubscribeLocalEvent<InfectedPlagueComponent, DidEquipHandEvent>(OnGotEquippedHand);
        //// когда зараженный трогает что-либо или кого-либо
        //SubscribeLocalEvent<InfectedPlagueComponent, UserInteractHandEvent>(OnUserInteractHandEvent);
        //SubscribeLocalEvent<UserInteractHandEvent>(OnUserWithoutInfectionInteractHandEvent); // на случай, если игрок не заражен, но инфицированны перчатки.
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<InfectedComponent>();

        while (query.MoveNext(out var uid, out var disease))
        {
            var virusesCopy = disease.Virus.ToList();

            foreach (InfectedaVirusData virus in virusesCopy)
            {
                if (_prototype.TryIndex<VirusPrototype>(virus.VirusId, out var proto))
                {
                    virus.MaxStage = proto.Stages.Count - 1;

                    var currentStage = proto.Stages.FirstOrDefault(s => s.Stage == virus.CurrentStage);
                    if (currentStage == null)
                        continue;

                    if (virus.NextStageTick == null)
                    {
                        InitStageTime(virus, currentStage.Duration);
                    }
                    else
                    {
                        CheckCurrentStage(uid, virus, currentStage);
                    }

                    // Обработка лечения
                    ProcessTreatments(uid, virus, proto, currentStage);

                    // Обработка симптомов с учётом таймеров
                    ProcessSymptoms(uid, currentStage.Symptoms, virus, proto, currentStage);
                }
            }
        }
    }

    /// <summary>
    /// Метод, которой устанавливает время следующей смены стадии болезни.
    /// </summary>
    /// <param name="virus"> Текущая болезнь </param>
    /// <param name="time"> Время текущей стадии </param>
    private void InitStageTime(InfectedaVirusData virus, int time)
    {
        virus.NextStageTick = _gameTiming.CurTime + TimeSpan.FromSeconds(time);
        Log.Debug($"[{_gameTiming.CurTime}] Установлено время текущей стадии ({virus.CurrentStage}) на {virus.NextStageTick}"); // Debug
    }

    /// <summary>
    /// Метод, который проверяет, не истекло ли время текущей стадии.
    /// Если время истикло, то проверяем ряд условий:
    ///   * выздоровление сущности;
    ///   * переход болезни на следующий этап;
    ///   * переход болезни на предыдущий этап;
    /// В случае, если никакое событие не произошло, просто перезапускаем таймер текущего этапа.
    /// </summary>
    /// <param name="virus"> Текущая болезнь </param>
    /// <param name="currentStage"> Текущая стадия болезни </param>
    private void CheckCurrentStage(EntityUid uid, InfectedaVirusData virus, DiseaseStageData currentStage)
    {
        if (_gameTiming.CurTime > virus.NextStageTick)
        {
            Log.Debug($"Начинаю ряд проверок. Вирус: {virus.VirusId} Текущая стадия: {virus.CurrentStage} Максимальная стадия: {virus.MaxStage}");
            // Шанс выздороветь
            if (_random.Prob(currentStage.ChanceOfRecovery))
            {
                Log.Debug($"Выздоровел");
                TryDoRecoveryAction(uid, virus.VirusId);
                TryDeleteVirus(uid, virus);
                return;
            }

            // Шанс болезни перейти на следующий этап
            if (_random.Prob(currentStage.ChanceOfTransitionUp))
            {
                Log.Debug($"Переходит на следующий этап");
                if (virus.CurrentStage + 1 <= virus.MaxStage)
                {
                    virus.CurrentStage++;
                    InitStageTime(virus, GetStageTime(virus.MaxStage, virus.VirusId));
                    Log.Debug($"Этап увеличился до {virus.CurrentStage}");
                    return;
                }
                else
                {
                    InitStageTime(virus, currentStage.Duration); // просто перезапускаем текущую стадию
                    return;
                }
            }

            // Шанс болезни перейти на предыдущий этап
            if (_random.Prob(currentStage.ChanceOfTransitionBack))
            {
                Log.Debug($"Переходит на предыдузий этап");
                if (virus.CurrentStage - 1 >= 0)
                {
                    virus.CurrentStage--;
                    InitStageTime(virus, GetStageTime(0, virus.VirusId));
                    Log.Debug($"Этап уменьшился до {virus.CurrentStage}");
                    return;
                }
                else
                {
                    InitStageTime(virus, currentStage.Duration); // просто перезапускаем текущую стадию
                    return;
                }
            }

            Log.Debug($"Обновление счетчика");
            InitStageTime(virus, currentStage.Duration); // Если ничего не произошло, то просто вновь начинаем этап.
        }
    }

    /// <summary>
    /// Вспомогательный метод для получения времени длительность определенного этапа.
    /// </summary>
    /// <param name="numberOfStage"> Номер этапа, время которого необходимо получить </param>
    /// <param name="virusId"> Идентификатор болезни, этапы которой рассматриваются </param>
    /// <returns></returns>
    private int GetStageTime(int numberOfStage, string virusId)
    {
        if (_prototype.TryIndex<VirusPrototype>(virusId, out var proto))
        {
            if (numberOfStage > proto.Stages.Count - 1)
                return 0;

            return proto.Stages[numberOfStage].Duration;
        }

        return 0;
    }

    /// <summary>
    /// Вспомогательный метод, который удаляет вирус из организма.
    /// </summary>
    /// <param name="uid"> Сущность из организма, которой нужно удалить вирус </param>
    /// <param name="virus"> Вирус, который удаляют </param>
    private void TryDeleteVirus(EntityUid uid, InfectedaVirusData virus)
    {
        if (TryComp<InfectedComponent>(uid, out var ifectedComponent))
        {
            ifectedComponent.Virus.Remove(virus);

            if (ifectedComponent.Virus.Count == 0)
                RemComp<InfectedComponent>(uid);
        }
    }

    /// <summary>
    /// Вспомогательный метод, который реализует действия при выздоравлении сущности.
    /// </summary>
    private void TryDoRecoveryAction(EntityUid uid, string virusId)
    {
        if (_prototype.TryIndex<VirusPrototype>(virusId, out var proto))
        {
            ProcessRecoveryActions(uid, proto.RecoveryActions);
        }
    }

    private void OnGotEquippedHand(EntityUid uid, InfectedComponent component, DidEquipHandEvent args)
    {
        foreach (InfectedaVirusData virus in component.Virus)
        {
            if ((virus.DistributionWay & TypeOfDistribution.Surface) != 0)
                Log.Error($"Зараженный {uid} взял предмет в руки: {args.Equipped}");
        }
    }

    #region вспомогательные методы для событий

    public bool TryAddVirus(EntityUid uid, string virusId)
    {
        if (!_prototype.TryIndex<VirusPrototype>(virusId, out var proto))
            return false;

        var infected = EnsureComp<InfectedComponent>(uid);

        if (infected.Virus.Any(v => v.VirusId == virusId))
            return false;

        var virusData = new InfectedaVirusData
        {
            VirusId = virusId,
            CurrentStage = 0,
            DistributionWay = TypeOfDistribution.Surface
        };

        var firstStage = proto.Stages.FirstOrDefault(s => s.Stage == 0);
        if (firstStage != null)
        {
            virusData.NextStageTick = _gameTiming.CurTime + TimeSpan.FromSeconds(firstStage.Duration);
        }

        infected.Virus.Add(virusData);

        Log.Info($"Entity {ToPrettyString(uid)} infected with {virusId}");
        return true;
    }

    public bool HasVirus(EntityUid uid, string virusId)
    {
        return TryComp(uid, out InfectedComponent? infected) &&
               infected.Virus.Any(v => v.VirusId == virusId);
    }

    public IEnumerable<EntityUid> GetAllInfectedEntities(string virusId)
    {
        var query = EntityQueryEnumerator<InfectedComponent>();
        while (query.MoveNext(out var uid, out var infected))
        {
            if (infected.Virus.Any(v => v.VirusId == virusId))
                yield return uid;
        }
    }

    #endregion

    #region вызов симптомов
    /// <summary>
    /// Метод, который перебирает все симптомы данного этапа болезни и проверяет, не пора ли их применить.
    /// </summary>
    /// <param name="uid"> Сущность, к которой применится симптом </param>
    /// <param name="symptoms"> Список симптомов данной стадии </param>
    /// <param name="virus"> Информация о конкретной болезни в организме сущности </param>
    /// <param name="proto"> Прототип вируса </param>
    /// <param name="currentStage"> Класс, содержащий информацию о текущей стадии болезни </param>
    private void ProcessSymptoms(
        EntityUid uid,
        List<BaseSymptomData> symptoms,
        InfectedaVirusData virus,
        VirusPrototype proto,
        DiseaseStageData currentStage)
    {
        foreach (var symptom in symptoms)
        {
            int symptomIndex = InfectedaVirusData.GetSymptomIndex(proto, currentStage, symptom);

            if (!virus.IsSymptomReady(symptom, _gameTiming, symptomIndex))
                continue;

            if (!_random.Prob(symptom.Chance))
            {
                virus.ResetSymptomTimer(symptom, _gameTiming, symptomIndex);
                continue;
            }

            ProcessSymptom(uid, symptom);
            virus.ResetSymptomTimer(symptom, _gameTiming, symptomIndex);
        }
    }

    private void ProcessSymptom(EntityUid uid, BaseSymptomData symptomData)
    {
        if (symptomData == null)
            return;

        var handlerName = symptomData.HandlerMethodName;

        if (!_handlerSymptomCache.TryGetValue(handlerName, out var handler))
        {
            handler = CreateSymptomHandler(handlerName, symptomData.GetType());
            if (handler != null)
                _handlerSymptomCache[handlerName] = handler;
            else
                return;
        }

        handler(uid, symptomData);
    }

    private Action<EntityUid, BaseSymptomData>? CreateSymptomHandler(string methodName, Type dataType)
    {
        // Ищем метод в этом же классе (DiseaseSystem)
        var method = GetType().GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(EntityUid), dataType },
            null);

        if (method == null)
        {
            Logger.Error($"Method '{methodName}' not found in DiseaseSystem");
            return null;
        }

        return (uid, data) => method.Invoke(this, new object[] { uid, data });
    }
    #endregion

    #region вызов действий
    private void ProcessRecoveryActions(EntityUid uid, List<BaseRecoveryActionsData> actions)
    {
        foreach (var action in actions)
        {
            if (_random.Prob(action.Chance))
            {
                ProcessRecoveryAction(uid, action);
            }
        }
    }

    private void ProcessRecoveryAction(EntityUid uid, BaseRecoveryActionsData action)
    {
        if (action == null)
            return;

        var handlerName = action.HandlerMethodName;

        if (!_handlerActionsCache.TryGetValue(handlerName, out var handler))
        {
            handler = CreateRecoveryActionHandler(handlerName, action.GetType());
            if (handler != null)
                _handlerActionsCache[handlerName] = handler;
            else
                return;
        }

        handler(uid, action);
    }

    private Action<EntityUid, BaseRecoveryActionsData>? CreateRecoveryActionHandler(string methodName, Type dataType)
    {
        var method = GetType().GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(EntityUid), dataType },
            null);

        if (method == null)
        {
            Logger.Error($"Method '{methodName}' not found in DiseaseSystem");
            return null;
        }

        return (uid, data) => method.Invoke(this, new object[] { uid, data });
    }
    #endregion

    #region вызов способов лечения
    private void ProcessTreatments(
        EntityUid uid,
        InfectedaVirusData virus,
        VirusPrototype proto,
        DiseaseStageData currentStage)
    {
        foreach (var treatment in currentStage.Treatments)
        {
            int treatmentIndex = InfectedaVirusData.GetTreatmentIndex(proto, currentStage, treatment);

            bool conditionMet = ProcessTreatment(uid, treatment);

            virus.UpdateTreatmentProgress(treatmentIndex, treatment, _gameTiming, conditionMet);

            // то есть тут обработка мгновенного лечения
            if (treatment.RequiredDuration <= 0)
            {
                if (!IsTreatmentCooldownReady(virus, treatmentIndex, treatment))
                    continue;

                if (conditionMet && _random.Prob(treatment.Effectiveness))
                {
                    ApplyTreatmentEffect(uid, virus, proto, treatment);
                    SetTreatmentCooldown(virus, treatmentIndex, treatment);
                }
                continue;
            }

            // а тут того, что требует непрерывного выполнения условия в течение определенного времени
            if (virus.IsTreatmentComplete(treatmentIndex, treatment))
            {
                if (!IsTreatmentCooldownReady(virus, treatmentIndex, treatment))
                    continue;

                if (_random.Prob(treatment.Effectiveness))
                {
                    ApplyTreatmentEffect(uid, virus, proto, treatment);
                }
                virus.ResetTreatmentProgress(treatmentIndex);
            }
        }
    }

    private bool IsTreatmentCooldownReady(InfectedaVirusData virus, int treatmentIndex, BaseTreatmentData treatment)
    {
        if (treatment.Cooldown <= 0)
            return true;

        if (!virus.TreatmentCooldowns.TryGetValue(treatmentIndex, out var nextTime))
            return true;

        return _gameTiming.CurTime >= nextTime;
    }

    private void SetTreatmentCooldown(InfectedaVirusData virus, int treatmentIndex, BaseTreatmentData treatment)
    {
        if (treatment.Cooldown > 0)
        {
            virus.TreatmentCooldowns[treatmentIndex] = _gameTiming.CurTime + TimeSpan.FromSeconds(treatment.Cooldown);
        }
    }

    /// <summary>
    /// Обрабатывает лечение и возвращает true, если условие лечения выполнено.
    /// </summary>
    private bool ProcessTreatment(EntityUid uid, BaseTreatmentData treatmentData)
    {
        if (treatmentData == null)
            return false;

        var handlerName = treatmentData.HandlerMethodName;

        if (!_handlerTreatmentCache.TryGetValue(handlerName, out var handler))
        {
            handler = CreateTreatmentHandler(handlerName, treatmentData.GetType());
            if (handler != null)
                _handlerTreatmentCache[handlerName] = handler;
            else
                return false;
        }

        return handler(uid, treatmentData);
    }

    private Func<EntityUid, BaseTreatmentData, bool>? CreateTreatmentHandler(string methodName, Type dataType)
    {
        var method = GetType().GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(EntityUid), dataType },
            null);

        if (method == null)
        {
            Logger.Error($"Method '{methodName}' not found in DiseaseSystem");
            return null;
        }

        // Проверяем, что метод возвращает bool
        if (method.ReturnType != typeof(bool))
        {
            Logger.Error($"Method '{methodName}' must return bool");
            return null;
        }

        return (uid, data) => (bool)method.Invoke(this, new object[] { uid, data })!;
    }

    /// <summary>
    /// Применяет эффект лечения в зависимости от Strength.
    /// </summary>
    private void ApplyTreatmentEffect(EntityUid uid, InfectedaVirusData virus, VirusPrototype proto, BaseTreatmentData treatment)
    {
        switch (treatment.Strength)
        {
            case TreatmentStrength.RegressOneStage:
                RegressStages(uid, virus, proto, 1);
                break;

            case TreatmentStrength.RegressMultipleStages:
                RegressStages(uid, virus, proto, treatment.StagesToRegress);
                break;

            case TreatmentStrength.Cure:
                TryDoRecoveryAction(uid, virus.VirusId);
                TryDeleteVirus(uid, virus);
                break;

            case TreatmentStrength.SlowProgression:
                ApplySlowProgression(uid, virus, treatment);
                break;

            case TreatmentStrength.PauseProgression:
                ApplyPauseProgression(uid, virus, treatment);
                break;

            default:
                Logger.Warning($"Unknown treatment strength: {treatment.Strength}");
                break;
        }
    }

    /// <summary>
    /// Откатывает болезнь на указанное количество стадий.
    /// </summary>
    private void RegressStages(EntityUid uid, InfectedaVirusData virus, VirusPrototype proto, int stages)
    {
        int newStage = Math.Max(0, virus.CurrentStage - stages);

        if (newStage == virus.CurrentStage)
        {
            Log.Debug($"Болезнь уже на минимальной стадии, откат невозможен"); // Debug
            Log.Debug($"Выздоровел"); // Debug
            TryDoRecoveryAction(uid, virus.VirusId);
            TryDeleteVirus(uid, virus);
            return;
        }

        int oldStage = virus.CurrentStage;
        virus.CurrentStage = newStage;

        var newStageData = proto.Stages.FirstOrDefault(s => s.Stage == newStage);
        if (newStageData != null)
        {
            virus.NextStageTick = _gameTiming.CurTime + TimeSpan.FromSeconds(newStageData.Duration);
        }

        Log.Debug($"Болезнь {virus.VirusId} откатилась с {oldStage} до {virus.CurrentStage} стадии у {ToPrettyString(uid)}"); // Debug
    }

    /// <summary>
    /// Замедляет прогрессию болезни.
    /// </summary>
    private void ApplySlowProgression(EntityUid uid, InfectedaVirusData virus, BaseTreatmentData treatment)
    {
        if (treatment.RequiredDuration <= 0)
        {
            Logger.Warning($"SlowProgression with RequiredDuration=0 is not allowed for {ToPrettyString(uid)}");
            return;
        }

        if (virus.NextStageTick == null)
            return;

        var currentTime = _gameTiming.CurTime;
        var remainingTime = virus.NextStageTick.Value - currentTime;

        // Увеличиваем оставшееся время
        var newRemainingTime = remainingTime * treatment.SlowMultiplier;
        virus.NextStageTick = currentTime + newRemainingTime;

        Logger.Info($"Прогрессия болезни {virus.VirusId} замедлена в {treatment.SlowMultiplier}x раз у {ToPrettyString(uid)}");
    }

    /// <summary>
    /// Ставит прогрессию на паузу.
    /// </summary>
    private void ApplyPauseProgression(EntityUid uid, InfectedaVirusData virus, BaseTreatmentData treatment)
    {
        if (treatment.RequiredDuration <= 0)
        {
            Logger.Warning($"ApplyPauseProgression with RequiredDuration=0 is not allowed for {ToPrettyString(uid)}");
            return;
        }

        if (virus.NextStageTick == null)
            return;

        // Добавляем время паузы к времени следующей стадии
        virus.NextStageTick = virus.NextStageTick.Value + TimeSpan.FromSeconds(treatment.PauseDuration);

        Logger.Info($"Прогрессия болезни {virus.VirusId} приостановлена на {treatment.PauseDuration} сек у {ToPrettyString(uid)}");
    }
    #endregion

    #region симптомы
    // Методы-обработчики симптомов прямо в DiseaseSystem
    private void SymptomSneezing(EntityUid uid, SymptomSneezingData data)
    {
        _chat.TryEmoteWithChat(uid, "Sneeze", ChatTransmitRange.Normal);
    }
    #endregion

    #region способы лечения
    /// В данном разделе реализуются методы-обработчики способов лечения.
    /// Каждый метод должен принимать EntityUid и конкретный класс данных лечения,
    /// а возвращать bool, который указывает, было ли лечение успешным (т.е. выполнены ли условия для лечения).

    /// <summary>
    /// Способ лечения через реагент. Проверяет, есть ли в организме сущности достаточное количество определенного реагента.
    /// </summary>
    private bool ReagentTreatment(EntityUid uid, ReagentTreatmentData data)
    {
        var requiredAmountFixed = FixedPoint2.New(data.Amount);
        var totalAmount = _solutionContainerSystem.GetTotalPrototypeQuantity(uid, data.ReagentId);

        var hasEnough = totalAmount >= requiredAmountFixed;

        return hasEnough;
    }

    /// <summary>
    /// Способ лечения через температуру. Проверяет, находится ли текущая температура сущности в заданном диапазоне.
    /// </summary>
    private bool TemperatureTreatment(EntityUid uid, TemperatureTreatmentData data)
    {
        if (!TryComp<TemperatureComponent>(uid, out var temp))
        {
            Log.Debug($"Сущность {ToPrettyString(uid)} не имеет TemperatureComponent");
            return false;
        }

        var inRange = temp.CurrentTemperature >= data.MinTemperature &&
                      temp.CurrentTemperature <= data.MaxTemperature;

        return inRange;
    }
    #endregion

    #region действия при выздоравлении
    private void RecoveryActionLaughter(EntityUid uid, RecoveryActionLaughterData data)
    {
        Log.Error($"Yeah {data.Chance}");
    }
    #endregion
}
