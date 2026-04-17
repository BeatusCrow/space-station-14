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

    // Кеш делегатов для вызова методов симптомов
    private readonly Dictionary<string, Action<EntityUid, BaseSymptomData>> _handlerSymptomCache = new();
    private readonly Dictionary<string, Action<EntityUid, BaseRecoveryActionsData>> _handlerActionsCache = new();

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

                    foreach (DiseaseStageData currentStage in proto.Stages)
                    {
                        if (currentStage.Stage != virus.CurrentStage)
                            continue;

                        if (virus.NextStageTick == null)
                            InitStageTime(virus, currentStage.Duration);
                        else
                            CheckCurrentStage(uid, virus, currentStage);


                        ProcessSymptoms(uid, currentStage.Symptoms);
                    }
                }
                //var currentStageData = virus.Stages.FirstOrDefault(s => s.Stage == disease.CurrentStage);
            }
            //// Уменьшаем время стадии
            //disease.StageTimeRemaining -= frameTime;

            //// Проверяем переход на следующую стадию
            //if (disease.StageTimeRemaining <= 0)
            //{
            //    AdvanceToNextStage(uid, disease);
            //}
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

    #region вызов симптомов
    private void ProcessSymptoms(EntityUid uid, List<BaseSymptomData> symptoms)
    {
        foreach (var symptom in symptoms)
        {
            if (_random.Prob(symptom.Chance))
            {
                ProcessSymptom(uid, symptom);
            }
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

    #region симптомы
    // Методы-обработчики симптомов прямо в DiseaseSystem
    private void SymptomSneezing(EntityUid uid, SymptomSneezingData data)
    {
        //Log.Error("SymptomSneezing");
    }
    #endregion

    #region способы лечения
    private void ReagentTreatment(EntityUid uid, ReagentTreatmentData data)
    {
        Log.Error("SymptomSneezing");
    }
    #endregion

    #region действия при выздоравлении
    private void RecoveryActionLaughter(EntityUid uid, RecoveryActionLaughterData data)
    {
        Log.Error($"Yeah {data.Chance}");
    }
    #endregion

    //private void AdvanceToNextStage(EntityUid uid, DiseaseComponent disease)
    //{
    //    var nextStage = disease.Stages.FirstOrDefault(s => s.Stage == disease.CurrentStage + 1);
    //    if (nextStage != null)
    //    {
    //        disease.CurrentStage++;
    //        disease.StageTimeRemaining = nextStage.Duration;
    //    }
    //    else
    //    {
    //        // Болезнь закончилась - можно вылечить
    //        RemComp<DiseaseComponent>(uid);
    //    }
    //}
}
