using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Server.TeleportationZone
{
    [RegisterComponent]
    public sealed partial class NukeTeleportationZoneComponent : Component
    {
        public bool WarDeclared = false;

        [DataField("announcement")]
        public bool Announcement = true;

        [DataField("text")]
        public string Text = "Attention! A hostile corporation is trying to move an object to your station... The travel time is {0} seconds. The approximate coordinates of the movement are as follows: X: {1} Y: {2}";

        [DataField("time")]
        public int Time = 12;

        [DataField("sound")]
        public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Announcements/war.ogg");

        [DataField("color")]
        public Color Color = Color.Red;
    }
}
