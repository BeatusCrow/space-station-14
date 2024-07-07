using Robust.Shared.Serialization;

namespace Content.Shared.TeleportationZone;

[Serializable, NetSerializable]
public enum TeleportationZoneUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class TeleportationZoneUiState : BoundUserInterfaceState
{
    public bool CanRefreshVol { get; }
    public bool CanStartVol { get; }
    public Dictionary<int, string> Points = new Dictionary<int, string>();

    public TeleportationZoneUiState(bool canRefreshVol, bool canStartVol, Dictionary<int, string> points)
    {
        CanRefreshVol = canRefreshVol;
        CanStartVol = canStartVol;
        Points.Clear();
        foreach(var point in points)
        {
            Points.Add(point.Key, point.Value);
        }
    }
}

[Serializable, NetSerializable]
public sealed class TeleportationZoneRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class TeleportationZoneStartMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class TeleportationZonePointSelectedMessage : BoundUserInterfaceMessage
{
    public int Point { get; }

    public TeleportationZonePointSelectedMessage(int point)
    {
        Point = point;
    }
}
