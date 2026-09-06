namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 지형·오브젝트 노이즈의 <b>시드 문자열과 주파수</b> [소스 직독 · 회차 9·11].
    /// 지형 뷰(타일)와 시뮬(나무·모닥불 자리·통행)이 <b>같은 값</b>을 읽어야 «그림과 판정»이 안 갈린다 — 그래서 한 곳이다.
    ///
    /// <para>⚠ 값 자체가 원본의 것이라 튜너블이 아니다. 시드 «난수원»만 우리 것이다 (의도된 차이 #6).</para>
    /// </summary>
    public static class UndeadTerrainSeeds
    {
        public const string Detail = "xadsnh53";
        public const string Grass = "rwboygvbw38y";
        public const string Road = "road_seed_4821";
        public const string MediumObject = "medium_obj_seed_123";
        public const string HealingObject = "healing_obj_seed_789";

        public const double DetailFrequency = 1.0;
        public const double GrassFrequency = 0.05;
        public const double RoadFrequency = 0.01;
        public const double ObjectFrequency = 0.1;

        /// <summary>타일 한 칸 (world px) [실측 · 소스 <c>vh = 24</c>].</summary>
        public const int TilePixels = 24;
    }

    /// <summary>
    /// 문자열 시드 난수 — 원본 <c>El(seed)</c> 그대로다.
    ///
    /// <para>
    /// ★★ <b>원본은 Alea 0.9 + Mash 0.9</b> 다 [소스 직독 — <c>index.poki-*.js</c> 의 <c>El=t(Fl)</c>].
    /// 예전에는 「난수원만 우리 것」이라 적고 xorshift32 를 썼는데, 그러면 <b>나무·모닥불이
    /// 전부 다른 자리에 선다</b> — 원본은 3회 독립 실행에서 소수점까지 같은 자리에 놓였다 [실측].
    /// 「지형 시드가 다르다」는 의도된 차이가 아니라 <b>안 옮긴 것</b>이었다.
    /// </para>
    ///
    /// <para>⚠ JS 의 <c>|0</c>·<c>&gt;&gt;&gt;0</c> 의미(32비트 절단)를 그대로 지켜야 한 판 뒤에 갈리지 않는다.</para>
    /// </summary>
    public struct UndeadSeededRandom
    {
        private double _s0;
        private double _s1;
        private double _s2;
        private int _c;

        public UndeadSeededRandom(string seed)
        {
            var mash = new UndeadMash();

            _s0 = mash.Next(" ");
            _s1 = mash.Next(" ");
            _s2 = mash.Next(" ");
            _c = 1;

            _s0 -= mash.Next(seed);

            if (_s0 < 0.0)
                _s0 += 1.0;

            _s1 -= mash.Next(seed);

            if (_s1 < 0.0)
                _s1 += 1.0;

            _s2 -= mash.Next(seed);

            if (_s2 < 0.0)
                _s2 += 1.0;
        }

        /// <summary>0 ≤ v &lt; 1.</summary>
        public double Next()
        {
            // [소스] t = 2091639*s0 + c*2^-32 ;  s0=s1, s1=s2, s2 = t - (c = t|0)
            double t = 2091639.0 * _s0 + _c * 2.3283064365386963e-10;

            _s0 = _s1;
            _s1 = _s2;
            _c = (int)t;          // JS 의 t|0 — 부호 방향 절단
            _s2 = t - _c;

            return _s2;
        }
    }

    /// <summary>
    /// 문자열 → [0,1) 해시. 원본 <b>Mash 0.9</b> 그대로다 [소스 직독].
    /// <para>⚠ <b>호출 사이에 상태가 남는다</b> — 같은 시드라도 «몇 번째 호출인가»가 값을 바꾼다.</para>
    /// </summary>
    public struct UndeadMash
    {
        private double _n;
        private bool _started;

        /// <summary>[소스] n = 0xefc8249d.</summary>
        public double Next(string data)
        {
            if (_started == false)
            {
                _n = 4022871197.0;
                _started = true;
            }

            for (int i = 0; i < data.Length; i++)
            {
                _n += data[i];

                double h = 0.02519603282416938 * _n;

                _n = ToUint32(h);
                h -= _n;
                h *= _n;
                _n = ToUint32(h);
                h -= _n;
                _n += h * 4294967296.0;
            }

            return ToUint32(_n) * 2.3283064365386963e-10;
        }

        /// <summary>JS <c>x &gt;&gt;&gt; 0</c> — 0 방향 절단 후 2^32 로 감싼다.</summary>
        private static double ToUint32(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            double truncated = System.Math.Truncate(value);
            double wrapped = truncated % 4294967296.0;

            return wrapped < 0.0 ? wrapped + 4294967296.0 : wrapped;
        }
    }
}
