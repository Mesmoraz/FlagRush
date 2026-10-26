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

    /// <summary>The server's authoritative position, sent to the owner too so the HUD can show the prediction error.</summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.All)]
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
