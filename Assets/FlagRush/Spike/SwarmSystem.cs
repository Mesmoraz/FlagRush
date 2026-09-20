using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlagRush.Spike
{
    /// <summary>
    /// 150 client-local entities moved by a Burst-compiled parallel job. The job records which thread
    /// indices it ran on and whether its body executed as managed code (a [BurstDiscard] call that Burst
    /// strips out). That is the whole "do Burst jobs multithread on Web" question in one readout.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SwarmSystem : ISystem
    {
        public const int AgentCount = 150;

        NativeArray<int> _threadHits;   // 1 per thread index that executed a chunk
        NativeArray<int> _managedFlag;  // [0] set to 1 only when the body runs without Burst
        float _time;

        public void OnCreate(ref SystemState state)
        {
            _threadHits = new NativeArray<int>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
            _managedFlag = new NativeArray<int>(1, Allocator.Persistent);

            var random = new Random(12345);
            for (int i = 0; i < AgentCount; i++)
            {
                var e = state.EntityManager.CreateEntity(typeof(SwarmAgent), typeof(LocalTransform));
                state.EntityManager.SetComponentData(e, new SwarmAgent
                {
                    Phase = random.NextFloat(0f, math.PI * 2f),
                    Radius = random.NextFloat(8f, 22f),
                    Speed = random.NextFloat(0.2f, 0.9f),
                });
                state.EntityManager.SetComponentData(e, LocalTransform.FromScale(0.5f));
            }
            SpikeStats.SwarmCount = AgentCount;
            SpikeStats.JobWorkerCount = JobsUtility.JobWorkerCount;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_threadHits.IsCreated) _threadHits.Dispose();
            if (_managedFlag.IsCreated) _managedFlag.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            _time += SystemAPI.Time.DeltaTime;
            _managedFlag[0] = 0; // per-frame readout: in the Editor Burst JITs asynchronously, so early frames run managed
            state.Dependency = new SwarmMoveJob
            {
                Time = _time,
                ThreadHits = _threadHits,
                ManagedFlag = _managedFlag,
            }.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            int seen = 0;
            for (int i = 0; i < _threadHits.Length; i++) if (_threadHits[i] != 0) seen++;
            SpikeStats.SwarmJobThreadsSeen = seen;
            SpikeStats.SwarmJobRanManaged = _managedFlag[0] != 0;
            SpikeStats.SwarmFrames++;
        }
    }

    [BurstCompile]
    public partial struct SwarmMoveJob : IJobEntity
    {
        public float Time;
        [NativeDisableParallelForRestriction] public NativeArray<int> ThreadHits;
        [NativeDisableParallelForRestriction] public NativeArray<int> ManagedFlag;
        [NativeSetThreadIndex] int _threadIndex;

        void Execute(in SwarmAgent agent, ref LocalTransform transform)
        {
            float a = agent.Phase + Time * agent.Speed;
            transform.Position = new float3(math.cos(a) * agent.Radius, 0.5f + math.sin(a * 3f) * 0.5f, math.sin(a) * agent.Radius);
            ThreadHits[_threadIndex] = 1;
            MarkManaged(ref ManagedFlag);
        }

        // Burst removes this call entirely, so ManagedFlag only ever gets set by the managed fallback.
        [BurstDiscard]
        static void MarkManaged(ref NativeArray<int> flag) => flag[0] = 1;
    }
}
