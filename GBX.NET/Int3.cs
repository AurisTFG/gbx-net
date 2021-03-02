﻿using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace GBX.NET
{
    public struct Int3
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        [IgnoreDataMember]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Int3 XZ => new Int3(X, 0, Z);

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        public override bool Equals(object obj) => obj is Int3 a && a == this;

        public static bool operator ==(Int3 a, Int3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Int3 a, Int3 b) => !(a.X == b.X && a.Y == b.Y && a.Z == b.Z);

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Int3 operator +(Int3 a, Int2 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z);
        public static Int3 operator +(Int3 a, int b) => new Int3(a.X + b, a.Y + b, a.Z + b);
        public static Vec3 operator +(Int3 a, float b) => new Vec3(a.X + b, a.Y + b, a.Z + b);

        public static Int3 operator +(Int2 a, Int3 b) => b + a;
        public static Int3 operator +(int a, Int3 b) => b + a;
        public static Vec3 operator +(float a, Int3 b) => b + a;

        public static Int3 operator -(Int3 a, Int3 b) => new Int3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Int3 operator -(Int3 a, Int2 b) => new Int3(a.X - b.X, a.Y - b.Y, a.Z);
        public static Int3 operator -(Int3 a, int b) => new Int3(a.X - b, a.Y - b, a.Z - b);
        public static Vec3 operator -(Int3 a, float b) => new Vec3(a.X - b, a.Y - b, a.Z - b);

        public static Int3 operator *(Int3 a, Int3 b) => new Int3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        public static Int3 operator *(Int3 a, Int2 b) => new Int3(a.X * b.X, a.Y * b.Y, a.Z);
        public static Int3 operator *(Int3 a, int b) => new Int3(a.X * b, a.Y * b, a.Z * b);
        public static Vec3 operator *(Int3 a, float b) => new Vec3(a.X * b, a.Y * b, a.Z * b);

        public static Int3 operator *(Int2 a, Int3 b) => b * a;
        public static Int3 operator *(int a, Int3 b) => b * a;
        public static Vec3 operator *(float a, Int3 b) => b * a;

        public static Int3 operator ^(Int3 a, Int3 b) => new Int3(a.X ^ b.X, a.Y ^ b.Y, a.Z ^ b.Z);
        public static Int3 operator ^(Int3 a, Int2 b) => new Int3(a.X ^ b.X, a.Y ^ b.Y, a.Z);
        public static Int3 operator ^(Int3 a, int b) => new Int3(a.X ^ b, a.Y ^ b, a.Z ^ b);

        public static Int3 operator ^(Int2 a, Int3 b) => b ^ a;
        public static Int3 operator ^(int a, Int3 b) => b ^ a;

        public static implicit operator Int3((int X, int Y, int Z) v) => new Int3(v.X, v.Y, v.Z);
        public static implicit operator (int X, int Y, int Z)(Int3 v) => (v.X, v.Y, v.Z);

        public static explicit operator Int3(Vec3 a) => new Int3(Convert.ToInt32(a.X), Convert.ToInt32(a.Y), Convert.ToInt32(a.Z));
        public static explicit operator Int3(Byte3 a) => new Int3(a.X, a.Y, a.Z);
        public static explicit operator Int3(Int2 a) => new Int3(a.X, 0, a.Y);
        public static explicit operator Int3(int[] a) => a == null ? new Int3() : a.Length >= 3 ? new Int3(a[0], a[1], a[2]) : throw new Exception();
        public static explicit operator Int3(Direction a)
        {
            switch (a)
            {
                case Direction.North:
                    return (0, 0, 1);
                case Direction.East:
                    return (-1, 0, 0);
                case Direction.South:
                    return (0, 0, -1);
                case Direction.West:
                    return (1, 0, 0);
                default:
                    return (0, 0, 0);
            }
        }
    }
}
