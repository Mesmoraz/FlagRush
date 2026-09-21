using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// The Web-safe render path: Entities Graphics does not support Web, so entity transforms are pushed
    /// straight into Graphics.RenderMeshInstanced. One draw per material; no GameObjects per entity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class InstancedRenderSystem : SystemBase
    {
        // RenderMeshInstanced requires a field named objectToWorld; LocalToWorld is the same 64 bytes, so reinterpret.
        const int MaxPerCall = 1023; // RenderMeshInstanced's per-call limit

        struct InstanceData
        {
            public Matrix4x4 objectToWorld;
        }

        Mesh _mesh;
        Material _swarmMaterial;
        Material _ghostMaterial;
        EntityQuery _swarmQuery;
        EntityQuery _ghostQuery;

        protected override void OnCreate()
        {
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var baseMaterial = Resources.Load<Material>("DemoMaterial");
            if (baseMaterial == null)
            {
                DemoStats.LastError = "DemoMaterial not found in Resources; using fallback shader";
                baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            _swarmMaterial = new Material(baseMaterial) { color = new Color(0.25f, 0.6f, 1f) };
            _ghostMaterial = new Material(baseMaterial) { color = new Color(1f, 0.45f, 0.15f) };
            _swarmMaterial.enableInstancing = _ghostMaterial.enableInstancing = true;

            _swarmQuery = GetEntityQuery(ComponentType.ReadOnly<SwarmAgent>(), ComponentType.ReadOnly<LocalToWorld>());
            _ghostQuery = GetEntityQuery(ComponentType.ReadOnly<ProbeGhost>(), ComponentType.ReadOnly<LocalToWorld>());
        }

        protected override void OnUpdate()
        {
            DemoStats.DrawCalls = 0;
            DemoStats.DrawnInstances = Draw(_swarmQuery, _swarmMaterial) + Draw(_ghostQuery, _ghostMaterial);
        }

        int Draw(EntityQuery query, Material material)
        {
            var matrices = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            int count = matrices.Length;
            var instances = matrices.Reinterpret<InstanceData>();
            var rp = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f) };
            for (int start = 0; start < count; start += MaxPerCall)
            {
                Graphics.RenderMeshInstanced(rp, _mesh, 0, instances, Mathf.Min(MaxPerCall, count - start), start);
                DemoStats.DrawCalls++;
            }
            matrices.Dispose();
            return count;
        }
    }
}
