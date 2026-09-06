using System;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 지형 «절차 생성» — <b>2D Simplex 노이즈</b> [소스 직독 · 회차 9].
    ///
    /// <para>
    /// ★★ 원본 지형은 <b>저작한 맵 데이터가 아니라 «시드 + 노이즈»</b>다. 소스에 시드 문자열이
    /// 그대로 있었고(<c>xadsnh53</c> · <c>road_seed_4821</c> …), 노이즈는 <b>표준 2D Simplex</b>였다 —
    /// 기울기 12개 · <c>F2 = (√3−1)/2</c> · <c>G2 = (3−√3)/6</c> · 마지막에 <c>×70</c>.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>시드 PRNG 도 원본 그대로다 — Alea 0.9</b> [소스 직독 · <see cref="UndeadSeededRandom"/>].
    /// 예전 회차는 「난수원만 우리 것」이라 적고 넘어갔는데, 그러면 <b>지형과 그 위의 나무·모닥불이
    /// 통째로 다른 자리</b>에 선다. 원본은 3회 독립 실행에서 <b>소수점까지 같은 자리</b>였다 [실측] —
    /// 결정적인 것을 「시드가 다르다」로 등재한 것이 잘못이었다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 그래도 <b>결정적</b>이어야 한다 — 같은 칸은 언제 다시 그려도 같은 타일이어야 하고,
    /// 그게 <c>UndeadRenderCheck</c> 의 「같은 칸 = 같은 타일」 항목이다.
    /// </para>
    /// </summary>
    public sealed class UndeadTerrainNoise
    {
        private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0);
        private static readonly double G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;

        /// <summary>기울기 12개 [소스 — 표준 Simplex 의 grad3 를 2D 로 쓴 것].</summary>
        private static readonly double[] GradX =
        {
            1.0, -1.0, 1.0, -1.0, 1.0, -1.0, 1.0, -1.0, 0.0, 0.0, 0.0, 0.0,
        };

        private static readonly double[] GradY =
        {
            1.0, 1.0, -1.0, -1.0, 0.0, 0.0, 0.0, 0.0, 1.0, -1.0, 1.0, -1.0,
        };

        private readonly int[] _perm = new int[512];
        private readonly double[] _gx = new double[512];
        private readonly double[] _gy = new double[512];

        /// <summary>
        /// 시드 문자열로 순열 테이블을 섞는다 [소스와 «같은 절차» — Fisher–Yates].
        /// <para>⚠ 난수원만 우리 것이다(위 주석). 시드가 같으면 <b>언제나 같은 지형</b>이 나온다.</para>
        /// </summary>
        public UndeadTerrainNoise(string seed)
        {
            // [소스] buildPermutationTable(alea(seed)) — 255회 Fisher–Yates
            var rng = new UndeadSeededRandom(seed);

            for (int i = 0; i < 256; i++)
                _perm[i] = i;

            for (int i = 0; i < 255; i++)
            {
                int j = i + (int)(rng.Next() * (256 - i));

                if (j >= 256)
                    j = 255;

                (_perm[i], _perm[j]) = (_perm[j], _perm[i]);
            }

            for (int i = 256; i < 512; i++)
                _perm[i] = _perm[i - 256];

            for (int i = 0; i < 512; i++)
            {
                int g = _perm[i] % 12;
                _gx[i] = GradX[g];
                _gy[i] = GradY[g];
            }
        }

        /// <summary>−1 ~ +1. 원본과 «같은 식»이다.</summary>
        public double Sample(double x, double y)
        {
            double s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);
            double t = (i + j) * G2;
            double x0 = x - (i - t);
            double y0 = y - (j - t);

            int i1 = x0 > y0 ? 1 : 0;
            int j1 = x0 > y0 ? 0 : 1;

            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2;
            double y2 = y0 - 1.0 + 2.0 * G2;

            int ii = i & 255;
            int jj = j & 255;

            double n0 = Corner(x0, y0, ii + _perm[jj]);
            double n1 = Corner(x1, y1, ii + i1 + _perm[jj + j1]);
            double n2 = Corner(x2, y2, ii + 1 + _perm[jj + 1]);

            return 70.0 * (n0 + n1 + n2);
        }

        private double Corner(double x, double y, int index)
        {
            double t = 0.5 - x * x - y * y;

            if (t < 0.0)
                return 0.0;

            int wrapped = index & 511;
            t *= t;
            return t * t * (_gx[wrapped] * x + _gy[wrapped] * y);
        }

        private static int FastFloor(double value)
        {
            int i = (int)value;
            return value < i ? i - 1 : i;
        }

    }
}
