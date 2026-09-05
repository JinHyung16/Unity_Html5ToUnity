using System;

namespace JinHyung.BlumgiBounce
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
    /// 좌표계는 <b>원본 world px 1:1</b> 이고 <b>y 는 아래가 +</b> 다.
    /// 뷰(<c>Transform</c>)로 넘길 때만 유니티 좌표로 뒤집는다 — 그 환산은 패스 ③ 의 몫이다.
    /// </para>
    /// </summary>
    public struct BlumgiVec2 : IEquatable<BlumgiVec2>
    {
        public double X;
        public double Y;

        public BlumgiVec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static BlumgiVec2 Zero
        {
            get { return new BlumgiVec2(0.0, 0.0); }
        }

        public double SqrMagnitude
        {
            get { return (X * X) + (Y * Y); }
        }

        public double Magnitude
        {
            get { return Math.Sqrt(SqrMagnitude); }
        }

        public BlumgiVec2 Normalized
        {
            get
            {
                double m = Magnitude;

                if (m <= 0.0)
                    return Zero;

                return new BlumgiVec2(X / m, Y / m);
            }
        }

        public static BlumgiVec2 operator +(BlumgiVec2 a, BlumgiVec2 b)
        {
            return new BlumgiVec2(a.X + b.X, a.Y + b.Y);
        }

        public static BlumgiVec2 operator -(BlumgiVec2 a, BlumgiVec2 b)
        {
            return new BlumgiVec2(a.X - b.X, a.Y - b.Y);
        }

        public static BlumgiVec2 operator *(BlumgiVec2 a, double s)
        {
            return new BlumgiVec2(a.X * s, a.Y * s);
        }

        /// <summary>
        /// 「위쪽이 + 인 각도(도)」를 world 방향 단위벡터로 바꾼다.
        ///
        /// <para>
        /// 원본 실측표(`07_기대값.md` §5-0)의 각도가 이 규약이다 — world y 가 아래로 +이므로
        /// <c>y = −sin θ</c> 다. 검산: W1L4 130.2° · |v0| 415 → (−268.5, −317.0),
        /// 실측 (−269, −317) 과 일치한다.
        /// </para>
        /// </summary>
        public static BlumgiVec2 FromAngleUpPositive(double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new BlumgiVec2(Math.Cos(rad), -Math.Sin(rad));
        }

        /// <summary>world 방향 벡터를 「위쪽이 + 인 각도(도)」로 되돌린다. 채점 대조용이다.</summary>
        public static double ToAngleUpPositive(BlumgiVec2 v)
        {
            return Math.Atan2(-v.Y, v.X) * 180.0 / Math.PI;
        }

        public bool Equals(BlumgiVec2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is BlumgiVec2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() << 2);
        }

        public override string ToString()
        {
            return $"({X:0.###}, {Y:0.###})";
        }
    }
}
