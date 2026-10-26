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
        {
            var asset = Resources.Load<Material>("DemoOutlineMaterial");
            if (asset != null) return Instanced(asset, OutlineColor);
            var material = Instanced(baseMaterial, OutlineColor);
            if (!MakeTransparent(material)) material.color = OutlineOpaqueFallback;
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
            drawn += DrawPlayers(_ownedQuery, _ownedMaterial);
            drawn += DrawPlayers(_otherQuery, _otherMaterial);
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
