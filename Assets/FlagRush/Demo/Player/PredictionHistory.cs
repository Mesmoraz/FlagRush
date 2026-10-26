using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>
    /// The owned ghost's predicted position per tick, so the client can compare what it predicted for a tick with
    /// what the server says happened on that tick. A fixed ring indexed by <c>tick % capacity</c>; each slot keeps its
    /// tick so a slot overwritten by a newer tick (or never written) is rejected instead of returning stale data.
    /// </summary>
    /// <remarks>Client-only, main thread: a plain managed struct over arrays (not Burst, not a component).</remarks>
    public struct PredictionHistory
    {
        readonly uint[] _ticks;
        readonly float3[] _positions;
        readonly bool[] _written;

        public PredictionHistory(int capacity = 64)
        {
            if (capacity < 1) capacity = 1;
            _ticks = new uint[capacity];
            _positions = new float3[capacity];
            _written = new bool[capacity];
        }

        public void Record(uint tick, float3 position)
        {
            if (_ticks == null) return;
            int slot = (int)(tick % (uint)_ticks.Length);
            _ticks[slot] = tick;
            _positions[slot] = position;
            _written[slot] = true;
        }

        public bool TryGet(uint tick, out float3 position)
        {
            position = default;
            if (_ticks == null) return false;
            int slot = (int)(tick % (uint)_ticks.Length);
            if (!_written[slot] || _ticks[slot] != tick) return false;
            position = _positions[slot];
            return true;
        }

        /// <summary>Forget every recorded tick (owned ghost changed, or the level dropped below 3).</summary>
        public void Clear()
        {
            if (_written != null) System.Array.Clear(_written, 0, _written.Length);
        }
    }
}
