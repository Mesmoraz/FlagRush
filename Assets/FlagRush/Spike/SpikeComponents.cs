using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace FlagRush.Spike
{
    /// <summary>Client-local entity moved by a Burst job. Exists to prove jobs run on worker threads.</summary>
    public struct SwarmAgent : IComponentData
    {
        public float Phase;
        public float Radius;
        public float Speed;
    }

    /// <summary>Server-owned replicated state. Exists to prove snapshots cross the IPC transport.</summary>
    [GhostComponent]
    public struct SpikeGhostState : IComponentData
    {
        [GhostField] public uint ServerTick;
        [GhostField(Quantization = 100)] public float3 Position;
        [GhostField] public int Index;
    }

    /// <summary>Per-world singleton pointing at the runtime-created ghost prefab.</summary>
    public struct SpikeGhostPrefab : IComponentData
    {
        public Entity Value;
    }

    /// <summary>Baked from the SubScene. Exists to prove SubScene loading works on Web.</summary>
    public struct SubSceneMarker : IComponentData
    {
        public int Value;
    }
}
