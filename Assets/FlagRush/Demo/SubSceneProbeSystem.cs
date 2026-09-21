using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Counts entities baked from the SubScene in each world. Zero on the client after load = SubScene failed on this platform.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SubSceneProbeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            int count = 0;
            foreach (var _ in SystemAPI.Query<RefRO<SubSceneMarker>>()) count++;
            if (state.World.IsServer()) DemoStats.SubSceneEntitiesServer = count;
            else DemoStats.SubSceneEntitiesClient = count;
        }
    }
}
