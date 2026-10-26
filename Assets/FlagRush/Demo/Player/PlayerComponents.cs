using Unity.Entities;
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

    /// <summary>Replicated player state. Predicted on the owning client, interpolated on everyone else.</summary>
    [GhostComponent]
    public struct PlayerGhost : IComponentData
    {
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)] public float3 Position;
        [GhostField(Quantization = 1000)] public float2 Velocity;
        [GhostField] public int BotIndex; // -1 = human
    }

    /// <summary>
    /// The server's authoritative post-separation position, written only on the server and sent only to the ghost's
    /// owner (for the server outline and the misprediction readout). Bots (owner -1) never send it; non-owners
    /// render from <see cref="PlayerGhost.Position"/>.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct ServerPosition : IComponentData
    {
        [GhostField(Quantization = 1000)] public float3 Value;
    }

    /// <summary>Per-world singleton pointing at the runtime-created player ghost prefab.</summary>
    public struct PlayerGhostPrefab : IComponentData
    {
        public Entity Value;
    }
}
