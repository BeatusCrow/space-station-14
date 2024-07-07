using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Server.TeleportationZone
{
    [RegisterComponent]
    public sealed partial class TeleportationZoneConsoleComponent : Component
    {
        public float top_border = 0f;
        public float bottom_border = 0f;
        public float left_border = 0f;
        public float right_border = 0f;

        public int LandingPointId = 0;
    }
}
