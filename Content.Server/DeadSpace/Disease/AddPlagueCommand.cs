using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Content.Server.Administration.Commands;
using Content.Shared.Administration;
using Content.Server.Administration;
using Content.Shared.DeadSpace.Disease;
using Content.Shared.DeadSpace.Disease.Treatments;
using Content.Shared.DeadSpace.Disease.Symptoms;

namespace Content.Server.DeadSpace.Disease;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddPlagueCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => "add_plague_command";
    public override string Help => "add_plague_command - i know all about this command :)";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetUidNet) || !_entityManager.TryGetEntity(targetUidNet, out var targetEntity))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entityManager.EntityExists(targetEntity))
        {
            shell.WriteError(Loc.GetString("shell-entity-does-not-exist"));
            return;
        }

        try
        {
            var infectedComponent = _entityManager.EnsureComponent<InfectedComponent>((EntityUid)targetEntity.Value);
            InfectedaVirusData ObjectVirus = new InfectedaVirusData();
            ObjectVirus.VirusId = "TestVirusProto";
            infectedComponent.Virus.Add(ObjectVirus);

            shell.WriteLine($"Componenadded to entity {targetEntity}");
        }
        catch (Exception ex)
        {
            shell.WriteError($"Failed to add component: {ex.Message}");
        }
    }
}
