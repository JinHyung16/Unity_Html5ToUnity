using System;
using System.Text;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 시드 난수 — 원본과 같은 시드에서 같은 수열을 낸다 (결정적 시뮬 · 같은 판 재현의 뿌리).
    ///
    /// <para>
    /// 원본은 seedrandom 기본 알고리즘(ARC4)을 시드 «문자열»로 쓴다 `[소스] Cq3gr8XO.js:this.rng=(0,cu.default)(String(e))`.
    /// 규칙을 읽고 C# 으로 다시 짰다 (코드 복사 아님 · R11). 이관 때 원본 수열 4282개와 대조함 (<c>07_기대값.md</c>).
    /// </para>
    ///
    /// <para>
    /// 순서: ① 시드 문자열 글자로 키 배열을 섞는다 ② 키로 256칸 상태를 섞는다 ③ 256바이트를 버린다
    /// ④ 한 번 뽑을 때 6바이트로 분자를 만들고 2^52 ~ 2^53 사이로 맞춰 [0,1) 소수를 낸다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsRandom
    {
        private const int Width = 256;
        private const int Chunks = 6;
        private const int Mask = Width - 1;

        /// <summary>256^6.</summary>
        private static readonly double StartDenominator = Math.Pow(Width, Chunks);

        /// <summary>2^52 — 소수 정밀도 문턱.</summary>
        private static readonly double Significance = Math.Pow(2, 52);

        /// <summary>2^53.</summary>
        private static readonly double Overflow = Significance * 2;

        /// <summary>36^8 — 8자리 36진수 id 범위 `[소스] POW36_8=36**8`.</summary>
        private static readonly double Pow36To8 = Math.Pow(36, 8);

        private readonly int[] _state = new int[Width];
        private int _i;
        private int _j;

        public FrontWarsRandom(string seed)
        {
            int[] key = MixKey(seed ?? string.Empty);
            InitState(key);
            NextBytes(Width);
        }

        /// <summary>숫자 시드 — 원본은 숫자를 문자열로 바꿔 쓴다 (`String(e)`).</summary>
        public FrontWarsRandom(long seed) : this(seed.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
        }

        /// <summary>[0, 1) 소수.</summary>
        public double Next()
        {
            double n = NextBytes(Chunks);
            double d = StartDenominator;
            double x = 0;

            while (n < Significance)
            {
                n = (n + x) * Width;
                d *= Width;
                x = NextBytes(1);
            }

            while (n >= Overflow)
            {
                n /= 2;
                d /= 2;
                x = (double)((uint)x >> 1);
            }

            return (n + x) / d;
        }

        /// <summary>min 이상 max 미만 정수 `[소스] nextInt(e,t){return Math.floor(this.rng()*(t-e))+e}`.</summary>
        public int NextInt(int min, int max)
        {
            return (int)Math.Floor(Next() * (max - min)) + min;
        }

        /// <summary>min 이상 max 미만 소수 `[소스] nextFloat(e,t)`.</summary>
        public double NextFloat(double min, double max)
        {
            return Next() * (max - min) + min;
        }

        /// <summary>확률 1/k — floor(난수×k) == 0 `[소스] chance(e)`.</summary>
        public bool Chance(double k)
        {
            return Math.Floor(Next() * k) == 0;
        }

        /// <summary>목록에서 하나 `[소스] randElement(e){` — 비어 있으면 멈춘다.</summary>
        public T RandElement<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException("array must not be empty");

            return list[NextInt(0, list.Count)];
        }

        /// <summary>
        /// 섞은 복사본 `[소스] shuffleArray(e){` — 뒤에서부터 k 칸마다 0~k 난수 칸과 바꾼다.
        /// <para>원본 입력은 건드리지 않는다.</para>
        /// </summary>
        public T[] Shuffle<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            var copy = new T[list.Count];

            for (int i = 0; i < copy.Length; i++)
                copy[i] = list[i];

            for (int k = copy.Length - 1; k > 0; k--)
            {
                int n = NextInt(0, k + 1);
                T t = copy[k];
                copy[k] = copy[n];
                copy[n] = t;
            }

            return copy;
        }

        /// <summary>8자리 36진수 id (앞을 0 으로 채움) `[소스] nextID(){`.</summary>
        public string NextId()
        {
            long value = (long)Math.Floor(Next() * Pow36To8);
            return ToBase36(value).PadLeft(8, '0');
        }

        // ── 알고리즘

        /// <summary>
        /// 시드 글자로 키 배열을 만든다. 키 칸은 글자 수만큼(최대 256) 생기고,
        /// 칸마다 (앞 섞임값 ^ 기존 키×19) + 글자 코드 를 256 으로 자른다.
        /// </summary>
        private static int[] MixKey(string seed)
        {
            int length = Math.Min(seed.Length, Width);
            var key = new int[Math.Max(length, 0)];
            var filled = new bool[Width];
            var keyFull = new int[Width];
            int smear = 0;

            for (int i = 0; i < seed.Length; i++)
            {
                int slot = i & Mask;
                int existing = filled[slot] ? keyFull[slot] : 0;
                smear ^= existing * 19;
                keyFull[slot] = Mask & (smear + seed[i]);
                filled[slot] = true;
            }

            for (int i = 0; i < length; i++)
                key[i] = keyFull[i];

            return key;
        }

        private void InitState(int[] key)
        {
            int keyLength = key.Length;

            if (keyLength == 0)
            {
                key = new[] { 0 };
                keyLength = 1;
            }

            for (int a = 0; a < Width; a++)
                _state[a] = a;

            int j = 0;

            for (int a = 0; a < Width; a++)
            {
                int t = _state[a];
                j = Mask & (j + key[a % keyLength] + t);
                _state[a] = _state[j];
                _state[j] = t;
            }

            _i = 0;
            _j = 0;
        }

        /// <summary>count 바이트를 큰 자리부터 이어 붙인 수.</summary>
        private double NextBytes(int count)
        {
            double r = 0;
            int i = _i, j = _j;

            while (count-- > 0)
            {
                i = Mask & (i + 1);
                int t = _state[i];
                j = Mask & (j + t);
                _state[i] = _state[j];
                _state[j] = t;
                r = r * Width + _state[Mask & (_state[i] + _state[j])];
            }

            _i = i;
            _j = j;
            return r;
        }

        private static string ToBase36(long value)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";

            if (value == 0)
                return "0";

            var sb = new StringBuilder();

            while (value > 0)
            {
                sb.Insert(0, digits[(int)(value % 36)]);
                value /= 36;
            }

            return sb.ToString();
        }
    }

    /// <summary>원본 문자열 해시 — 글자마다 h = h×31 + 글자코드 (32비트), 끝에 절댓값 `[소스] function P(e){`.</summary>
    public static class FrontWarsHash
    {
        public static long Of(string text)
        {
            int h = 0;

            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                    h = unchecked((h << 5) - h + text[i]);
            }

            // 원본 Math.abs 는 −2^31 도 2^31 로 돌려준다 — long 으로 받는다
            return Math.Abs((long)h);
        }
    }
}
