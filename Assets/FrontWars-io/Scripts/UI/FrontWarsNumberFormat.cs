using System;
using System.Globalization;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 큰 수 줄임 표기 — 골드 · 병력 `[소스] Cq3gr8XO.js:const pu=[ · function mu( · function hu(e){return mu(e/10)}`.
    /// <para>위에서부터 처음 맞는 줄 하나: 값 = floor(n / 나눔) / 배율, 소수 자리 고정. 음수는 0. 그 밖은 floor(n).</para>
    /// </summary>
    public static class FrontWarsNumberFormat
    {
        private readonly struct Rule
        {
            public readonly double Min;
            public readonly double Divisor;
            public readonly double Scale;
            public readonly int Decimals;
            public readonly string Suffix;

            public Rule(double min, double divisor, double scale, int decimals, string suffix)
            {
                Min = min;
                Divisor = divisor;
                Scale = scale;
                Decimals = decimals;
                Suffix = suffix;
            }
        }

        private static readonly Rule[] Rules =
        {
            new Rule(1e9, 1e7, 100, 2, "B"),
            new Rule(1e7, 1e5, 10, 1, "M"),
            new Rule(1e6, 1e4, 100, 2, "M"),
            new Rule(1e5, 1e3, 1, 0, "K"),
            new Rule(1e4, 100, 10, 1, "K"),
            new Rule(1e3, 10, 100, 2, "K")
        };

        /// <summary>병력 표시는 실제 병력 ÷ 10 이다.</summary>
        private const double TroopDisplayDivisor = 10;

        public static string Number(double value)
        {
            double n = Math.Max(0, value);

            foreach (Rule r in Rules)
            {
                if (n < r.Min)
                    continue;

                double shown = Math.Floor(n / r.Divisor) / r.Scale;
                return shown.ToString("F" + r.Decimals, CultureInfo.InvariantCulture) + r.Suffix;
            }

            return Math.Floor(n).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 소수 자리를 maxDecimals 까지만 — 공격 비율 옆 병력 (1.1K · 2.4K) `[실측] probe/ingame_ko3.json` · `[소스] K(ce,1)`.
        /// <para>자리를 줄일 때도 버림이다 (반올림이 아니다).</para>
        /// </summary>
        public static string Number(double value, int maxDecimals)
        {
            double n = Math.Max(0, value);

            foreach (Rule r in Rules)
            {
                if (n < r.Min)
                    continue;

                int decimals = Math.Min(r.Decimals, maxDecimals);
                double extra = Math.Pow(10, r.Decimals - decimals);
                double shown = Math.Floor(n / (r.Divisor * extra)) / (r.Scale / extra);
                return shown.ToString("F" + decimals, CultureInfo.InvariantCulture) + r.Suffix;
            }

            return Math.Floor(n).ToString(CultureInfo.InvariantCulture);
        }

        public static string Troops(double troops)
        {
            return Number(troops / TroopDisplayDivisor);
        }

        public static string Gold(long gold)
        {
            return Number(gold);
        }
    }
}
