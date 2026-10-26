using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Level 3+: the main camera follows the owned player ghost from above and behind (≈ 50° down, never yaws,
    /// so camera-relative input means W = screen up). Below Level 3 the behaviour disables itself and the
    /// scene's fixed camera pose stays. With no owned ghost (connecting, reconnecting) the camera holds still.
    /// </summary>
    public class PlayerCameraFollow : MonoBehaviour
    {
        public static readonly Vector3 Offset = new Vector3(0f, 18f, -14f);
        public const float SmoothTime = 0.15f;

        Vector3 _velocity;
        World _client;

        void Start() => enabled = DemoConfig.IsPlayerLevel;

        void LateUpdate()
        {
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            if (world == null || owned == Entity.Null) return;
            var em = world.EntityManager;
            if (!em.Exists(owned) || !em.HasComponent<LocalToWorld>(owned)) return;

            Vector3 target = em.GetComponentData<LocalToWorld>(owned).Position;
            transform.position = Vector3.SmoothDamp(transform.position, target + Offset, ref _velocity, SmoothTime);
            transform.LookAt(target);
        }

        World ClientWorld()
        {
            if (_client != null && _client.IsCreated) return _client;
            _client = null;
            foreach (var world in World.All)
                if (world.IsClient() && !world.IsThinClient()) return _client = world;
            return null;
        }
    }
}
