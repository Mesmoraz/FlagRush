using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Per-player combat state. Team: 0 = Red, 1 = Blue. RespawnTick == 0 means alive.</summary>
    [GhostComponent]
    public struct Combatant : IComponentData
    {
        [GhostField] public byte Team;
        [GhostField] public byte Health;
        [GhostField] public uint LastHitTick;
        [GhostField] public uint RespawnTick;
        [GhostField] public bool Carrying;

        public bool Alive => RespawnTick == 0;
    }

    /// <summary>Server-only combat bookkeeping.</summary>
    public struct CombatantServer : IComponentData
    {
        public uint NextFireTick;
    }

    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct MatchStats : IComponentData
    {
        [GhostField] public short Kills;
        [GhostField] public short Deaths;
        [GhostField] public short Captures;
        [GhostField] public ushort ShotsFired;
    }

    public enum FlagState : byte { Home, Carried, Dropped }

    public enum MatchPhase : byte { Playing, Ended }

    [GhostComponent]
    public struct FlagGhost : IComponentData
    {
        [GhostField] public byte Team;
        [GhostField] public FlagState State;
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)] public float3 Position;
        [GhostField] public int CarrierGhostId;
        [GhostField] public uint DroppedTick;
    }

    [GhostComponent]
    public struct MatchStateGhost : IComponentData
    {
        [GhostField] public short RedScore;
        [GhostField] public short BlueScore;
        [GhostField] public MatchPhase Phase;
        [GhostField] public uint PhaseEndTick;
        [GhostField] public uint RoundStartTick;
    }
}
