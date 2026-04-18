using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Abilities.Bloodsucker;
using Content.Shared.DeadSpace.Disease;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Disease;

/// <summary>
/// Класс, в котором реализована логика заражения вирусами через кровь.
/// Он отвечает за добавление реагентов-вирусов в кровь заражённых сущностей и за
/// заражение сущностей, у которых в крови уже есть реагенты-вирусы.
/// Это позволяет создавать цепочку заражения от крови к организму и обратно,
/// что может быть использовано для различных механик, таких как распространение болезни.
/// </summary>
public sealed class VirusInfectionSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float TargetViralAmount = 5f;
    public const string ViralSubstanceId = "ViralSubstance";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateInfectedEntities();
        UpdateBloodInfection();
    }

    /// <summary>
    /// Метод, который обрабатывает каждую заражённую сущность и добавляет ей в кровь вирус для его дальнейшей передачи.
    /// </summary>
    private void UpdateInfectedEntities()
    {
        var query = EntityQueryEnumerator<InfectedComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out var infected, out var bloodstream))
        {
            ProcessInfectedEntity(uid, infected, bloodstream);
        }
    }

    /// <summary>
    /// Метод, который обрабатывает конкретную зараженную сущность, добавляя в ее кровь вещество-вирус.
    /// </summary>
    private void ProcessInfectedEntity(EntityUid uid, InfectedComponent infected, BloodstreamComponent bloodstream)
    {
        if (infected.Virus == null || infected.Virus.Count == 0)
            return;

        if (!_solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
            return;

        var currentViralAmount = GetViralSubstanceAmount(bloodSolution);
        if (currentViralAmount >= 5)
            return;

        var defAmount = 5 - currentViralAmount;
        var virusIds = infected.Virus.Select(v => v.VirusId).ToList();

        if (currentViralAmount > 0)
        {
            ReplaceExistingViralSubstance(bloodSolution, currentViralAmount, defAmount, virusIds);
        }
        else
        {
            AddNewViralSubstance(bloodSolution, defAmount, virusIds);
        }
    }

    /// <summary>
    /// Вспомогательный метод, который удаляет старые вирусные
    /// вещества и добавляет новое с обновленным количеством и списком вирусов.
    /// </summary>
    private void ReplaceExistingViralSubstance(Solution bloodSolution, FixedPoint2 currentAmount, FixedPoint2 defAmount, List<string> virusIds)
    {
        RemoveAllViralSubstance(bloodSolution);

        var newAmount = currentAmount + defAmount;
        AddNewViralSubstance(bloodSolution, newAmount, virusIds);
    }

    /// <summary>
    /// Вспомогательный метод для удаления вирусов
    /// </summary>
    /// <param name="bloodSolution"></param>
    private void RemoveAllViralSubstance(Solution bloodSolution)
    {
        var reagentsToRemove = bloodSolution.Contents
            .Where(r => r.Reagent.Prototype == ViralSubstanceId)
            .ToList();

        foreach (var reagent in reagentsToRemove)
        {
            bloodSolution.RemoveReagent(reagent.Reagent, reagent.Quantity);
        }
    }

    /// <summary>
    /// Вспомогательный метод для добавления нового вирусного вещества в кровь,
    /// если его там не было, или после удаления старого.
    /// </summary>
    private void AddNewViralSubstance(Solution bloodSolution, FixedPoint2 amount, List<string> virusIds)
    {
        var virusReagent = new ReagentId(ViralSubstanceId,
            new List<ReagentData> { new VirusReagentData { VirusId = virusIds } });
        bloodSolution.AddReagent(virusReagent, amount);
    }

    /// <summary>
    /// Метод, который обрабатывает каждую сущность с кровеносной системой и добавляет ей вирус в
    /// организм, если он есть в крови, для дальнейшего распространения.
    /// </summary>
    private void UpdateBloodInfection()
    {
        var bloodQuery = EntityQueryEnumerator<BloodstreamComponent>();
        while (bloodQuery.MoveNext(out var entUid, out var bloodstreamComp))
        {
            ProcessBloodInfection(entUid, bloodstreamComp);
        }
    }

    /// <summary>
    /// Метод, который обрабатывает конкретную сущность с кровеносной системой, проверяя наличие
    /// вирусов в крови и заражая её при необходимости.
    /// </summary>
    private void ProcessBloodInfection(EntityUid entUid, BloodstreamComponent bloodstreamComp)
    {
        if (!_solutionContainer.ResolveSolution(entUid, bloodstreamComp.BloodSolutionName,
                ref bloodstreamComp.BloodSolution, out var bloodSolution))
            return;

        foreach (var reagent in bloodSolution.Contents)
        {
            if (reagent.Reagent.Prototype != ViralSubstanceId)
                continue;

            var virusIds = ExtractVirusIdsFromReagent(reagent);
            if (virusIds == null || virusIds.Count == 0)
                continue;

            InfectEntityWithViruses(entUid, virusIds);
        }
    }

    /// <summary>
    /// Вспомогательный метод для извлечения списка идентификаторов вирусов из данных реагента ViralSubstance.
    /// </summary>
    private List<string>? ExtractVirusIdsFromReagent(ReagentQuantity reagent)
    {
        var virusData = reagent.Reagent.Data?
            .OfType<VirusReagentData>()
            .FirstOrDefault();

        return virusData?.VirusId;
    }

    /// <summary>
    /// Метод, который добавляет компонент InfectedComponent с данными о вирусах к сущности, если она ещё не заражена,
    /// или просто обновляет список вирусов, если компонент уже есть. Это позволяет распространять вирусы от крови к организму.
    /// </summary>
    private void InfectEntityWithViruses(EntityUid entUid, List<string> virusIds)
    {
        var infected = EnsureComp<InfectedComponent>(entUid);

        foreach (var virusId in virusIds)
        {
            if (infected.Virus.Exists(v => v.VirusId == virusId))
                continue;

            infected.Virus.Add(new InfectedaVirusData
            {
                VirusId = virusId,
                CurrentStage = 0
            });

            Log.Info($"Entity {ToPrettyString(entUid)} infected with {virusId} via blood");
        }
    }

    /// <summary>
    /// Возвращает текущее количество реагента ViralSubstance в растворе.
    /// </summary>
    private FixedPoint2 GetViralSubstanceAmount(Solution solution)
    {
        FixedPoint2 sum = FixedPoint2.Zero;
        foreach (var reagent in solution.Contents)
        {
            if (reagent.Reagent.Prototype == ViralSubstanceId)
            {
                sum += reagent.Quantity;
            }
        }
        return sum;
    }
}
