using Unity.Entities;
using Unity.NetCode;

namespace FlagRush.Demo
{
    /// <summary>Singleton (both worlds) carrying the arena layout so Burst systems never read managed statics.</summary>
    public struct ActiveArena : IComponentData
    {
        public ArenaLayout Layout;
        public bool Match;
    }

    /// <summary>Writes <see cref="ActiveArena"/> each frame from <see cref="DemoConfig.IsMatchLevel"/>.</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ActiveArenaSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton(new ActiveArena { Layout = ArenaLayout.Empty });
        }

        protected override void OnUpdate()
        {
            bool match = DemoConfig.IsMatchLevel;
            SystemAPI.SetSingleton(new ActiveArena { Layout = match ? ArenaLayout.Match : ArenaLayout.Empty, Match = match });
        }
    }
}
