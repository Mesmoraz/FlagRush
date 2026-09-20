using System;

namespace FlagRush.Domain.Primitives
{
    /// <summary>
    /// Engine-free 3D position. Exists so the domain never references an engine vector type
    /// (Vector3, float3); adapters convert at the boundary.
    /// </summary>
    public readonly struct Point3 : IEquatable<Point3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Point3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Point3 Zero => default;

        public float DistanceSquaredTo(in Point3 other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public bool Equals(Point3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Point3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";

        public static bool operator ==(Point3 a, Point3 b) => a.Equals(b);
        public static bool operator !=(Point3 a, Point3 b) => !a.Equals(b);
    }
}
