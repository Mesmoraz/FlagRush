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
    /// <remarks>
    /// The HUD panels cover the left and right of the screen, so the player is framed in the uncovered band
    /// between them (<see cref="DemoHud.UncoveredBandCentre"/>), not at the screen centre: the camera and its
    /// look-at point are both slid along the camera's right axis by <see cref="BandShift"/>, a parallel shift
    /// that keeps the view direction (and the projection) unchanged.
    /// </remarks>
    public class PlayerCameraFollow : MonoBehaviour
    {
        public static readonly Vector3 Offset = new Vector3(0f, 18f, -14f);
        public const float SmoothTime = 0.15f;

        Vector3 _velocity;
        float _shift, _shiftVelocity;
        World _client;
        Camera _camera;

        void Start()
        {
            enabled = DemoConfig.IsPlayerLevel;
            _camera = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (!DemoConfig.IsPlayerLevel)
            {
                enabled = false;
                return;
            }
            var world = ClientWorld();
            var owned = DemoStats.OwnedPlayerEntity;
            if (world == null || owned == Entity.Null) return;
            var em = world.EntityManager;
            if (!em.Exists(owned) || !em.HasComponent<LocalToWorld>(owned)) return;

            Vector3 target = em.GetComponentData<LocalToWorld>(owned).Position;
            float desiredShift = _camera != null ? BandShift(_camera, DemoHud.UncoveredBandCentre, Offset.magnitude) : 0f;
            _shift = Mathf.SmoothDamp(_shift, desiredShift, ref _shiftVelocity, SmoothTime);
            Vector3 right = Quaternion.LookRotation(-Offset) * Vector3.right; // the camera never yaws, so this is fixed
            Vector3 aim = target - right * _shift;
            transform.position = Vector3.SmoothDamp(transform.position, aim + Offset, ref _velocity, SmoothTime);
            transform.LookAt(aim);
        }

        /// <summary>
        /// How far to slide the camera left (along its right axis) so a point <paramref name="depth"/> metres ahead
        /// of it projects to screen x <paramref name="screenX"/> instead of the screen centre.
        /// </summary>
        public static float BandShift(Camera camera, float screenX, float depth)
        {
            float width = camera.pixelWidth;
            if (width <= 0f) return 0f;
            float ndcX = 2f * (screenX - camera.pixelRect.x) / width - 1f;
            float halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth;
            return ndcX * halfHeight * camera.aspect;
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
