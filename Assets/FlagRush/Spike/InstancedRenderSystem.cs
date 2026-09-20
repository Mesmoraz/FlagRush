using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>
    /// The Web-safe render path: Entities Graphics does not support Web, so entity transforms are pushed
    /// straight into Graphics.RenderMeshInstanced. One draw per material; no GameObjects per entity.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class InstancedRenderSystem : SystemBase
    {
        Mesh _mesh;
        Material _swarmMaterial;
        Material _ghostMaterial;
        EntityQuery _swarmQuery;
        EntityQuery _ghostQuery;

        protected override void OnCreate()
        {
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var baseMaterial = Resources.Load<Material>("SpikeMaterial");
            if (baseMaterial == null)
            {
                SpikeStats.LastError = "SpikeMaterial not found in Resources; using fallback shader";
                baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            _swarmMaterial = new Material(baseMaterial) { color = new Color(0.25f, 0.6f, 1f) };
            _ghostMaterial = new Material(baseMaterial) { color = new Color(1f, 0.45f, 0.15f) };
            _swarmMaterial.enableInstancing = _ghostMaterial.enableInstancing = true;

            _swarmQuery = GetEntityQuery(ComponentType.ReadOnly<SwarmAgent>(), ComponentType.ReadOnly<LocalToWorld>());
            _ghostQuery = GetEntityQuery(ComponentType.ReadOnly<SpikeGhostState>(), ComponentType.ReadOnly<LocalToWorld>());
        }

        protected override void OnUpdate()
        {
            Draw(_swarmQuery, _swarmMaterial);
            Draw(_ghostQuery, _ghostMaterial);
        }

        void Draw(EntityQuery query, Material material)
        {
            var matrices = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            if (matrices.Length > 0)
                Graphics.RenderMeshInstanced(new RenderParams(material), _mesh, 0, matrices);
            matrices.Dispose();
        }
    }
}
