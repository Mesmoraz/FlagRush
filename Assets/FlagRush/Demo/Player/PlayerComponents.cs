using Unity.Mathematics;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Per-tick player command: movement stick (XZ plane) and sprint.</summary>
    public struct PlayerInput : IInputComponentData
    {
        public float2 Move;
        public bool Sprint;
    }
}
