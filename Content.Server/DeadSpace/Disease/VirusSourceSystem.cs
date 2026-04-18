using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server.Temperature.Systems;
using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.Disease;
using Content.Shared.DeadSpace.Disease.Events;
using Content.Shared.DeadSpace.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Temperature.Components;

namespace Content.Server.DeadSpace.Disease
{
    public sealed class VirusSourceSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly DiseaseSystem _diseaseSystem = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;

        // Кеш делегатов для обработчиков событий
        private readonly Dictionary<string, Func<BaseVirusSourceData, string, Dictionary<EntityUid, TimeSpan>, List<EntityUid>>> _handlerCache = new();

        // Трекеры экспозиции: ключ = "VirusId_EventType"
        private readonly Dictionary<string, Dictionary<EntityUid, TimeSpan>> _exposureTrackers = new();

        // Время последней проверки
        private readonly Dictionary<string, float> _lastCheckTime = new();

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var currentTime = (float)_timing.CurTime.TotalSeconds;

            foreach (var prototype in _prototype.EnumeratePrototypes<VirusPrototype>())
            {
                if (prototype.SourceEvents == null || prototype.SourceEvents.Count == 0)
                    continue;

                foreach (var sourceEvent in prototype.SourceEvents)
                {
                    var eventKey = $"{prototype.ID}_{sourceEvent.GetType().Name}";

                    // Проверяем интервал
                    if (_lastCheckTime.TryGetValue(eventKey, out var lastCheck))
                    {
                        if (currentTime - lastCheck < sourceEvent.CheckInterval)
                            continue;
                    }

                    // Проверяем минимальное время раунда
                    if (currentTime < sourceEvent.MinRoundTime)
                        continue;

                    _lastCheckTime[eventKey] = currentTime;

                    // Получаем или создаём трекер экспозиции
                    if (!_exposureTrackers.TryGetValue(eventKey, out var tracker))
                    {
                        tracker = new Dictionary<EntityUid, TimeSpan>();
                        _exposureTrackers[eventKey] = tracker;
                    }

                    // Вызываем обработчик
                    var infected = ProcessSourceEvent(sourceEvent, prototype.ID, tracker);

                    if (infected.Count > 0)
                    {
                        Log.Info($"Virus {prototype.ID} spawned via {sourceEvent.GetType().Name}. Infected: {string.Join(", ", infected)}");
                    }
                }
            }
        }

        private List<EntityUid> ProcessSourceEvent(BaseVirusSourceData sourceEvent, string virusId, Dictionary<EntityUid, TimeSpan> tracker)
        {
            var handlerName = sourceEvent.HandlerMethodName;

            if (!_handlerCache.TryGetValue(handlerName, out var handler))
            {
                handler = CreateHandler(handlerName, sourceEvent.GetType());
                if (handler != null)
                    _handlerCache[handlerName] = handler;
                else
                    return new List<EntityUid>();
            }

            return handler(sourceEvent, virusId, tracker);
        }

        private Func<BaseVirusSourceData, string, Dictionary<EntityUid, TimeSpan>, List<EntityUid>>? CreateHandler(string methodName, Type dataType)
        {
            var method = GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { dataType, typeof(string), typeof(Dictionary<EntityUid, TimeSpan>) },
                null);

            if (method == null)
            {
                Logger.Error($"Handler method '{methodName}' not found in VirusSourceSystem for type {dataType.Name}");
                return null;
            }

            return (data, virusId, tracker) =>
            {
                var result = method.Invoke(this, new object[] { data, virusId, tracker });
                return result as List<EntityUid> ?? new List<EntityUid>();
            };
        }

        #region Обработчики событий

        /// <summary>
        /// Обработчик переохлаждения.
        /// </summary>
        private List<EntityUid> HypothermiaSource(
            HypothermiaSourceData data,
            string virusId,
            Dictionary<EntityUid, TimeSpan> exposureTracker)
        {
            var infected = new List<EntityUid>();

            var query = EntityQueryEnumerator<BodyComponent, MobStateComponent, TemperatureComponent>();

            while (query.MoveNext(out var uid, out _, out var mobState, out var temperature))
            {
                if (!_mobState.IsAlive(uid, mobState))
                    continue;

                var currentTemp = temperature.CurrentTemperature;

                if (currentTemp < data.TemperatureThreshold)
                {
                    if (!exposureTracker.ContainsKey(uid))
                        exposureTracker[uid] = TimeSpan.Zero;

                    exposureTracker[uid] += _timing.FrameTime;

                    var requiredExposure = TimeSpan.FromSeconds(data.ExposureTime);
                    if (exposureTracker[uid] >= requiredExposure)
                    {
                        if (_random.Prob(data.Chance))
                        {
                            if (_diseaseSystem.TryAddVirus(uid, virusId))
                            {
                                infected.Add(uid);
                                if (infected.Count >= data.MaxInfections)
                                    break;
                            }
                        }
                        exposureTracker[uid] = TimeSpan.Zero;
                    }
                }
                else
                {
                    exposureTracker[uid] = TimeSpan.Zero;
                }
            }

            return infected;
        }

        /// <summary>
        /// Обработчик появления вируса у мёртвых существ.
        /// </summary>
        //private List<EntityUid> DeadBodySource(
        //    DeadBodySourceData data,
        //    string virusId,
        //    Dictionary<EntityUid, float> exposureTracker)
        //{
        //    var infected = new List<EntityUid>();
        //    var mobStateSystem = System<MobStateSystem>();

        //    var query = EntityQueryEnumerator<BodyComponent, MobStateComponent, TransformComponent>();

        //    while (query.MoveNext(out var uid, out _, out var mobState, out var transform))
        //    {
        //        if (mobStateSystem.IsAlive(uid, mobState))
        //            continue;

        //        if (_diseaseSystem.HasVirus(uid, virusId))
        //            continue;

        //        if (data.RequireCorpse && !HasComp<CorpseComponent>(uid))
        //            continue;

        //        if (_random.Prob(data.Chance))
        //        {
        //            if (_diseaseSystem.TryAddVirus(uid, virusId))
        //            {
        //                infected.Add(uid);
        //                if (infected.Count >= data.MaxInfections)
        //                    break;
        //            }
        //        }
        //    }

        //    return infected;
        //}

        #endregion
    }
}
