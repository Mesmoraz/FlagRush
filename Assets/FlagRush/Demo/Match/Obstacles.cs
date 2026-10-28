using Unity.Mathematics;

namespace FlagRush.Demo
{
    /// <summary>Pure circle/segment vs box queries. No [BurstCompile]; inlined into Burst systems.</summary>
    public static class Obstacles
    {
        const float Epsilon = 1e-6f;

        /// <summary>Pushes the circle out of every box and removes the velocity component pointing into a box.</summary>
        public static void Slide(ref float3 position, ref float2 velocity, float radius, in ArenaLayout layout)
        {
            float2 p = position.xz;
            for (int b = 0; b < layout.Boxes.Length; b++)
            {
                if (!Resolve(layout.Boxes[b], ref p, radius, out float2 n)) continue;
                float into = math.dot(velocity, n);
                if (into < 0f) velocity -= into * n;
            }
            position.x = p.x;
            position.z = p.y;
        }

        /// <summary>Nearest box crossed by segment a-&gt;b (slab test); t in [0,1] along the segment.</summary>
        public static bool SegmentHit(float2 a, float2 b, in ArenaLayout layout, out float t, out int box)
        {
            t = 0f;
            box = -1;
            float best = float.MaxValue;
            float2 d = b - a;
            for (int i = 0; i < layout.Boxes.Length; i++)
            {
                float2 lo = layout.Boxes[i].Center - layout.Boxes[i].HalfExtents;
                float2 hi = layout.Boxes[i].Center + layout.Boxes[i].HalfExtents;
                float t0 = 0f, t1 = 1f;
                bool miss = false;
                for (int axis = 0; axis < 2; axis++)
                {
                    if (math.abs(d[axis]) < Epsilon)
                    {
                        if (a[axis] < lo[axis] || a[axis] > hi[axis]) { miss = true; break; }
                        continue;
                    }
                    float inv = 1f / d[axis];
                    float ta = (lo[axis] - a[axis]) * inv;
                    float tb = (hi[axis] - a[axis]) * inv;
                    if (ta > tb) { float s = ta; ta = tb; tb = s; }
                    t0 = math.max(t0, ta);
                    t1 = math.min(t1, tb);
                    if (t0 > t1) { miss = true; break; }
                }
                if (miss || t0 >= best) continue;
                best = t0;
                box = i;
            }
            if (box < 0) return false;
            t = best;
            return true;
        }

        /// <summary>Returns <paramref name="p"/> moved out of any box a circle of <paramref name="radius"/> would overlap.</summary>
        public static float2 PushOut(float2 p, float radius, in ArenaLayout layout)
        {
            for (int b = 0; b < layout.Boxes.Length; b++)
                Resolve(layout.Boxes[b], ref p, radius, out _);
            return p;
        }

        static bool Resolve(in Box box, ref float2 p, float radius, out float2 normal)
        {
            normal = float2.zero;
            float2 lo = box.Center - box.HalfExtents;
            float2 hi = box.Center + box.HalfExtents;
            float2 closest = math.clamp(p, lo, hi);
            float2 delta = p - closest;
            float dist = math.length(delta);
            if (dist > 0f && dist >= radius) return false;
            if (dist > Epsilon)
            {
                normal = delta / dist;
                p = closest + normal * radius;
                return true;
            }
            // Centre on/inside the box: leave through the nearest face.
            float2 off = p - box.Center;
            float2 pen = box.HalfExtents - math.abs(off);
            if (pen.x <= pen.y)
            {
                float sx = off.x < 0f ? -1f : 1f;
                normal = new float2(sx, 0f);
                p.x = box.Center.x + sx * (box.HalfExtents.x + radius);
            }
            else
            {
                float sz = off.y < 0f ? -1f : 1f;
                normal = new float2(0f, sz);
                p.y = box.Center.y + sz * (box.HalfExtents.y + radius);
            }
            return true;
        }
    }
}
