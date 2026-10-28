using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlagRush.Demo
{
    /// <summary>
    /// The Web-safe render path: Entities Graphics does not support Web, so entity transforms are pushed
    /// straight into Graphics.RenderMeshInstanced. One draw per material; no GameObjects per entity.
    /// </summary>
    /// <remarks>
    /// Level 3 adds three player draws: the owned player (green), everyone else (grey) and, while prediction is on,
    /// a translucent green outline at the owned player's raw server position. Player matrices are built from
    /// <see cref="LocalTransform"/> (the rendered position: predicted for the owner, interpolated for the rest)
    /// with the player scale, so they do not depend on the transform system's update order.
    /// Below Level 3 there are no player ghosts, so these draws add nothing.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class InstancedRenderSystem : SystemBase
    {
        // RenderMeshInstanced requires a field named objectToWorld; LocalToWorld is the same 64 bytes, so reinterpret.
        const int MaxPerCall = 1023; // RenderMeshInstanced's per-call limit

        public static readonly Vector3 PlayerScale = new Vector3(1f, 1.6f, 1f);
        public const float PlayerYOffset = 0.8f;
        public static readonly Color OwnedColor = new Color(0.3f, 1f, 0.4f);
        public static readonly Color OtherColor = new Color(0.78f, 0.8f, 0.82f);
        public static readonly Color OutlineColor = new Color(0.3f, 1f, 0.4f, 0.35f);
        public static readonly Color TeamRed = new Color(0.9f, 0.3f, 0.3f);
        public static readonly Color TeamBlue = new Color(0.3f, 0.5f, 1f);
        public static readonly Color BoxColor = new Color(0.22f, 0.23f, 0.26f);
        public static readonly Color TracerColor = new Color(1f, 0.92f, 0.2f);
        public const float TracerSeconds = 0.05f;
        static readonly Vector3 RingScale = new Vector3(1.8f, 0.05f, 1.8f);
        static readonly Vector3 DiscScale = new Vector3(6f, 0.05f, 6f);
        static readonly Vector3 FlagScale = new Vector3(0.3f, 2f, 0.3f);
        static readonly Color OutlineOpaqueFallback = new Color(0.15f, 0.5f, 0.2f);

        struct InstanceData
        {
            public Matrix4x4 objectToWorld;
        }

        Mesh _mesh;
        Material _swarmMaterial;
        Material _ghostMaterial;
        Material _ownedMaterial;
        Material _otherMaterial;
        Material _outlineMaterial;
        EntityQuery _swarmQuery;
        EntityQuery _ghostQuery;
        EntityQuery _ownedQuery;
        EntityQuery _otherQuery;

        // Level 4 (match) draws
        Material _redMaterial, _blueMaterial, _whiteMaterial, _boxMaterial, _redDiscMaterial, _blueDiscMaterial, _tracerMaterial;
        EntityQuery _matchOwnedQuery;
        EntityQuery _matchOtherQuery;
        EntityQuery _flagQuery;
        bool _tracerGateArmed;
        uint _lastTracerTick;
        float _tracerUntil;
        InstanceData _tracer;

        protected override void OnCreate()
        {
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var baseMaterial = Resources.Load<Material>("DemoMaterial");
            if (baseMaterial == null)
            {
                DemoStats.LastError = "DemoMaterial not found in Resources; using fallback shader";
                baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            _swarmMaterial = Instanced(baseMaterial, new Color(0.25f, 0.6f, 1f));
            _ghostMaterial = Instanced(baseMaterial, new Color(1f, 0.45f, 0.15f));
            _ownedMaterial = Instanced(baseMaterial, OwnedColor);
            _otherMaterial = Instanced(baseMaterial, OtherColor);
            _outlineMaterial = CreateOutlineMaterial(baseMaterial);
            _redMaterial = Instanced(baseMaterial, TeamRed);
            _blueMaterial = Instanced(baseMaterial, TeamBlue);
            _whiteMaterial = Instanced(baseMaterial, Color.white);
            _boxMaterial = Instanced(baseMaterial, BoxColor);
            _tracerMaterial = Instanced(baseMaterial, TracerColor);
            _redDiscMaterial = CreateTranslucentMaterial(baseMaterial, new Color(TeamRed.r, TeamRed.g, TeamRed.b, 0.35f), TeamRed * 0.5f);
            _blueDiscMaterial = CreateTranslucentMaterial(baseMaterial, new Color(TeamBlue.r, TeamBlue.g, TeamBlue.b, 0.35f), TeamBlue * 0.5f);

            _swarmQuery = GetEntityQuery(ComponentType.ReadOnly<SwarmAgent>(), ComponentType.ReadOnly<LocalToWorld>());
            _ghostQuery = GetEntityQuery(ComponentType.ReadOnly<ProbeGhost>(), ComponentType.ReadOnly<LocalToWorld>());
            // GhostOwnerIsLocal is enableable: All = enabled only, Disabled = present but disabled (bots, other humans).
            _ownedQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ServerPosition>(), ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _otherQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>() },
                Disabled = new[] { ComponentType.ReadOnly<GhostOwnerIsLocal>() },
            });
            _matchOwnedQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Combatant>(), ComponentType.ReadOnly<PlayerInput>(), ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _matchOtherQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<Combatant>() },
                Disabled = new[] { ComponentType.ReadOnly<GhostOwnerIsLocal>() },
            });
            _flagQuery = GetEntityQuery(ComponentType.ReadOnly<FlagGhost>());
        }

        static Material Instanced(Material source, Color color)
        {
            return new Material(source) { color = color, enableInstancing = true };
        }

        /// <summary>
        /// The outline is green at 35 % alpha. Prefers the transparent asset DemoOutlineMaterial (created by
        /// DemoSceneSetup, so the transparent shader variant survives build stripping); otherwise switches a copy of
        /// the base material to URP's transparent surface type; if the shader has no transparent mode, a dimmer opaque green.
        /// </summary>
        static Material CreateOutlineMaterial(Material baseMaterial)
            => CreateTranslucentMaterial(baseMaterial, OutlineColor, OutlineOpaqueFallback);

        static Material CreateTranslucentMaterial(Material baseMaterial, Color color, Color opaqueFallback)
        {
            var asset = Resources.Load<Material>("DemoOutlineMaterial");
            if (asset != null) return Instanced(asset, color);
            var material = Instanced(baseMaterial, color);
            if (!MakeTransparent(material)) material.color = opaqueFallback;
            return material;
        }

        /// <summary>URP Unlit/Lit transparent setup: what the material inspector does for Surface Type = Transparent.</summary>
        public static bool MakeTransparent(Material material)
        {
            if (!material.HasProperty("_Surface")) return false;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f); // alpha blend
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            return true;
        }

        protected override void OnUpdate()
        {
            DemoStats.DrawCalls = 0;
            int drawn = Draw(_swarmQuery, _swarmMaterial) + Draw(_ghostQuery, _ghostMaterial);
            if (DemoConfig.IsMatchLevel)
            {
                drawn += DrawMatch();
            }
            else
            {
                drawn += DrawPlayers(_ownedQuery, _ownedMaterial);
                drawn += DrawPlayers(_otherQuery, _otherMaterial);
            }
            if (Sandbox.PredictionEnabled) drawn += DrawOutline();
            DemoStats.DrawnInstances = drawn;
        }

        int Draw(EntityQuery query, Material material)
        {
            var matrices = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            int count = matrices.Length;
            Submit(matrices.Reinterpret<InstanceData>(), material);
            matrices.Dispose();
            return count;
        }

        int DrawPlayers(EntityQuery query, Material material)
        {
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var instances = new NativeArray<InstanceData>(transforms.Length, Allocator.Temp);
            for (int i = 0; i < transforms.Length; i++)
                instances[i] = PlayerInstance(transforms[i].Position);
            Submit(instances, material);
            int count = instances.Length;
            transforms.Dispose();
            instances.Dispose();
            return count;
        }

        /// <summary>
        /// Level 4: team-coloured players (white while hit-flashing, none while dead), the owned player's white ring,
        /// boxes, base discs, flags and the owned player's tracer. One draw per material.
        /// </summary>
        int DrawMatch()
        {
            uint ownedTick = SystemAPI.TryGetSingleton<NetworkTime>(out var time) && time.ServerTick.IsValid
                ? time.ServerTick.TickIndexForValidTick : 0u;
            var layout = SystemAPI.TryGetSingleton<ActiveArena>(out var arena) && arena.Match ? arena.Layout : ArenaLayout.Empty;

            var red = new NativeList<InstanceData>(16, Allocator.Temp);
            var blue = new NativeList<InstanceData>(16, Allocator.Temp);
            var white = new NativeList<InstanceData>(16, Allocator.Temp);
            var boxes = new NativeList<InstanceData>(16, Allocator.Temp);
            var redDisc = new NativeList<InstanceData>(1, Allocator.Temp);
            var blueDisc = new NativeList<InstanceData>(1, Allocator.Temp);
            var tracer = new NativeList<InstanceData>(1, Allocator.Temp);

            using (var transforms = _matchOwnedQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            using (var combatants = _matchOwnedQuery.ToComponentDataArray<Combatant>(Allocator.Temp))
            using (var inputs = _matchOwnedQuery.ToComponentDataArray<PlayerInput>(Allocator.Temp))
            {
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (!combatants[i].Alive) continue;
                    float3 pos = transforms[i].Position;
                    AddPlayer(combatants[i], pos, ownedTick, red, blue, white);
                    white.Add(Instance(new Vector3(pos.x, 0.02f, pos.z), RingScale));
                    UpdateTracer(inputs[i], pos, ownedTick, layout);
                }
            }
            if (UnityEngine.Time.realtimeSinceStartup < _tracerUntil) tracer.Add(_tracer);

            uint interpTick = DemoStats.ClientInterpolationTick;
            using (var transforms = _matchOtherQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            using (var combatants = _matchOtherQuery.ToComponentDataArray<Combatant>(Allocator.Temp))
            {
                for (int i = 0; i < transforms.Length; i++)
                    if (combatants[i].Alive) AddPlayer(combatants[i], transforms[i].Position, interpTick, red, blue, white);
            }

            using (var flags = _flagQuery.ToComponentDataArray<FlagGhost>(Allocator.Temp))
            {
                for (int i = 0; i < flags.Length; i++)
                {
                    var p = flags[i].Position;
                    var list = flags[i].Team == 0 ? red : blue;
                    list.Add(Instance(new Vector3(p.x, p.y + 1f, p.z), FlagScale));
                }
            }

            for (int i = 0; i < layout.Boxes.Length; i++)
            {
                var b = layout.Boxes[i];
                boxes.Add(Instance(new Vector3(b.Center.x, 1f, b.Center.y), new Vector3(b.HalfExtents.x * 2f, 2f, b.HalfExtents.y * 2f)));
            }
            redDisc.Add(Instance(new Vector3(layout.RedBase.x, 0.025f, layout.RedBase.y), DiscScale));
            blueDisc.Add(Instance(new Vector3(layout.BlueBase.x, 0.025f, layout.BlueBase.y), DiscScale));

            int count = 0;
            count += Flush(boxes, _boxMaterial);
            count += Flush(redDisc, _redDiscMaterial);
            count += Flush(blueDisc, _blueDiscMaterial);
            count += Flush(red, _redMaterial);
            count += Flush(blue, _blueMaterial);
            count += Flush(white, _whiteMaterial);
            count += Flush(tracer, _tracerMaterial);
            return count;
        }

        static void AddPlayer(in Combatant c, float3 pos, uint now, NativeList<InstanceData> red, NativeList<InstanceData> blue, NativeList<InstanceData> white)
        {
            var instance = PlayerInstance(pos);
            if (MatchHudText.IsHitFlash(now, c.LastHitTick)) white.Add(instance);
            else if (c.Team == 0) red.Add(instance);
            else blue.Add(instance);
        }

        /// <summary>Starts a tracer when the owned player fires and the local gate (15 client ticks) allows it.</summary>
        void UpdateTracer(in PlayerInput input, float3 pos, uint tick, in ArenaLayout layout)
        {
            if (!input.Fire || math.lengthsq(input.Aim) < 1e-6f) return;
            if (_tracerGateArmed && tick - _lastTracerTick < MatchTuning.FireIntervalTicks) return;
            _tracerGateArmed = true;
            _lastTracerTick = tick;

            float2 dir = math.normalize(input.Aim);
            float2 a = pos.xz;
            float2 b = a + dir * MatchTuning.ShotRange;
            float length = MatchTuning.ShotRange;
            if (Obstacles.SegmentHit(a, b, layout, out float t, out _)) length *= t;
            var d3 = new Vector3(dir.x, 0f, dir.y);
            var mid = new Vector3(a.x, 1f, a.y) + d3 * (length * 0.5f);
            _tracer = new InstanceData { objectToWorld = Matrix4x4.TRS(mid, Quaternion.LookRotation(d3), new Vector3(0.08f, 0.08f, length)) };
            _tracerUntil = UnityEngine.Time.realtimeSinceStartup + TracerSeconds;
        }

        static InstanceData Instance(Vector3 position, Vector3 scale)
            => new InstanceData { objectToWorld = Matrix4x4.TRS(position, Quaternion.identity, scale) };

        int Flush(NativeList<InstanceData> list, Material material)
        {
            int count = list.Length;
            if (count > 0) Submit(list.AsArray(), material);
            list.Dispose();
            return count;
        }

        int DrawOutline()
        {
            var positions = _ownedQuery.ToComponentDataArray<ServerPosition>(Allocator.Temp);
            var instances = new NativeArray<InstanceData>(positions.Length, Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
                instances[i] = PlayerInstance(positions[i].Value);
            Submit(instances, _outlineMaterial);
            int count = instances.Length;
            positions.Dispose();
            instances.Dispose();
            return count;
        }

        static InstanceData PlayerInstance(float3 position)
        {
            var p = new Vector3(position.x, position.y + PlayerYOffset, position.z);
            return new InstanceData { objectToWorld = Matrix4x4.TRS(p, Quaternion.identity, PlayerScale) };
        }

        void Submit(NativeArray<InstanceData> instances, Material material)
        {
            int count = instances.Length;
            var rp = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f) };
            for (int start = 0; start < count; start += MaxPerCall)
            {
                Graphics.RenderMeshInstanced(rp, _mesh, 0, instances, Mathf.Min(MaxPerCall, count - start), start);
                DemoStats.DrawCalls++;
            }
        }
    }
}
