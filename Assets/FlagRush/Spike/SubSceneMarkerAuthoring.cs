using Unity.Entities;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>Authoring for the SubScene test: bakes one SubSceneMarker per GameObject.</summary>
    public class SubSceneMarkerAuthoring : MonoBehaviour
    {
        public int Value = 1;

        class Baker : Baker<SubSceneMarkerAuthoring>
        {
            public override void Bake(SubSceneMarkerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SubSceneMarker { Value = authoring.Value });
            }
        }
    }
}
