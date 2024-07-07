using Content.Shared.TeleportationZone;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.TeleportationZone.UI;

[UsedImplicitly]
public sealed class TeleportationZoneBoundUi : BoundUserInterface
{
    [ViewVariables]
    private TeleportationZoneConsoleWindow? _window;

    public TeleportationZoneBoundUi(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new TeleportationZoneConsoleWindow();
        _window.OpenCentered();

        _window.PointsRefreshButtonPressed += OnPointsRefreshButtonPressed;
        _window.StartLandingButtonPressed += OnStartLandingButtonPressed;
        _window.PointSelected += OnPointSelected;
        _window.OnClose += Close;
    }

    private void OnPointsRefreshButtonPressed()
    {
        SendMessage(new TeleportationZoneRefreshMessage());
    }

    private void OnStartLandingButtonPressed()
    {
        SendMessage(new TeleportationZoneStartMessage());
    }

    private void OnPointSelected(int point)
    {
        SendMessage(new TeleportationZonePointSelectedMessage(point));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not TeleportationZoneUiState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
