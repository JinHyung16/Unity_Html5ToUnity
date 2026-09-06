using System;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 이 게임의 좌표·속도 자료형.
    ///
    /// <para>
    /// ★ <b><c>UnityEngine.Vector2</c> 를 쓰지 않는다.</b> 그건 <c>float</c> 라
    /// 원본 JS 숫자(<c>double</c>)와 자릿수가 다르다 — 골든 벡터 채점이 자료형 오차에서 흔들린다
    /// (`Client.md` 「수치 계층」).
    /// </para>
    ///
    /// <para>
    /// 좌표계는 <b>원본 world px 1:1</b> 이고 <b>y 는 아래가 +</b> 다 (Pixi 규약).
    /// 뷰(<c>Transform</c>)로 넘길 때만 유니티 좌표로 뒤집는다 —
    /// 그 환산은 <see cref="UndeadUnits"/> 한 곳만 지난다.
    /// </para>
    /// </summary>
    public struct UndeadVec2 : IEquatable<UndeadVec2>
    {
        public double X;
        public double Y;

        public UndeadVec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static UndeadVec2 Zero
        {
            get { return new UndeadVec2(0.0, 0.0); }
        }

        public double SqrMagnitude
        {
            get { return (X * X) + (Y * Y); }
        }

        public double Magnitude
        {
            get { return Math.Sqrt(SqrMagnitude); }
        }

        /// <summary>길이 1 로 만든다. <b>길이가 0 이면 0 벡터를 그대로 준다</b> — NaN 을 만들지 않는다.</summary>
        public UndeadVec2 Normalized
        {
            get
            {
                double m = Magnitude;
                return m <= 0.0 ? Zero : new UndeadVec2(X / m, Y / m);
            }
        }

        public static UndeadVec2 operator +(UndeadVec2 a, UndeadVec2 b)
        {
            return new UndeadVec2(a.X + b.X, a.Y + b.Y);
        }

        public static UndeadVec2 operator -(UndeadVec2 a, UndeadVec2 b)
        {
            return new UndeadVec2(a.X - b.X, a.Y - b.Y);
        }

        public static UndeadVec2 operator *(UndeadVec2 a, double s)
        {
            return new UndeadVec2(a.X * s, a.Y * s);
        }

        public static double Distance(UndeadVec2 a, UndeadVec2 b)
        {
            return (a - b).Magnitude;
        }

        public static double SqrDistance(UndeadVec2 a, UndeadVec2 b)
        {
            return (a - b).SqrMagnitude;
        }

        public bool Equals(UndeadVec2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is UndeadVec2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 2);
        }

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3})";
        }
    }
}
