using System.Collections.Generic;

namespace JinHyung.Core
{
    /// <summary>
    /// <b>무작위는 여기 한 곳에서만 꺼낸다.</b>
    ///
    /// <para>
    /// 게임 코드가 <c>UnityEngine.Random</c> 이나 <c>new System.Random()</c> 을 직접 부르면
    /// <b>그 자리를 바깥에서 못 바꾼다</b> — 같은 판을 두 번 만들 수 없고,
    /// 골든 벡터도 결정성 검사도 성립하지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>검증을 위해 게임 코드에 새 문(<c>DebugSetXxx</c>)을 뚫지 않는다.</b>
    /// 대신 <b>이미 있는 이 주입 지점</b>으로 세운다 — 검사는 «실제 경로»를 그대로 지난다.
    /// </para>
    ///
    /// <para>
    /// 원본 HTML5 의 <c>Math.random()</c> 이 몇 군데인지 세고, 그 자리를 전부 이걸로 옮긴다.
    /// </para>
    /// </summary>
    public static class RandomUtil
    {
        private static System.Random _source = new System.Random();

        /// <summary>
        /// 무작위 원천을 갈아 끼운다. <b>검사 하네스가 쓰는 유일한 문</b>이다.
        /// <c>null</c> 을 주면 시드 없는 기본 원천으로 되돌린다.
        /// </summary>
        public static void SetSource(System.Random source)
        {
            _source = source ?? new System.Random();
        }

        /// <summary>시드를 박아 다시 세운다. 같은 시드는 <b>같은 수열</b>을 준다.</summary>
        public static void SetSeed(int seed)
        {
            _source = new System.Random(seed);
        }

        /// <summary><c>[0, maxExclusive)</c> 정수. 원본 <c>Math.floor(random() * n)</c> 자리다.</summary>
        public static int Range(int maxExclusive)
        {
            return _source.Next(0, maxExclusive);
        }

        /// <summary><c>[minInclusive, maxExclusive)</c> 정수.</summary>
        public static int Range(int minInclusive, int maxExclusive)
        {
            return _source.Next(minInclusive, maxExclusive);
        }

        /// <summary><c>[0, 1)</c> 실수. 원본 <c>Math.random()</c> 과 같은 범위다.</summary>
        public static double Value()
        {
            return _source.NextDouble();
        }

        /// <summary>
        /// 목록에서 하나를 고른다. 원본의 <c>arr[floor(random() * arr.length)]</c> 자리다.
        ///
        /// <para>
        /// ⚠ <b>빈 목록이면 터뜨린다.</b> 폴백을 깔면 어서트가 무력화되고
        /// 「왜 아무것도 안 나오나」를 아무도 못 찾는다.
        /// </para>
        /// </summary>
        public static T Pick<T>(IReadOnlyList<T> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                throw new System.InvalidOperationException("빈 목록에서 뽑으려 했다");

            return candidates[Range(candidates.Count)];
        }

        /// <summary>
        /// <b>정해진 값을 순서대로 주는 원천.</b> 검사 하네스가 «원본과 같은 뽑기 순서»를 먹일 때 쓴다.
        /// 다 쓰면 <paramref name="fallback"/> 을 계속 준다.
        /// </summary>
        public sealed class Scripted : System.Random
        {
            private readonly IReadOnlyList<int> _values;
            private readonly int _fallback;
            private int _index;

            public Scripted(IReadOnlyList<int> values, int fallback = 0)
            {
                _values = values;
                _fallback = fallback;
            }

            public override int Next(int minValue, int maxValue)
            {
                if (_values != null && _index < _values.Count)
                    return _values[_index++];

                return _fallback;
            }
        }
    }
}
