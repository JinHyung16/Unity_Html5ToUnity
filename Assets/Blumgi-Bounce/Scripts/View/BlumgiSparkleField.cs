using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 반짝임 <c>FXChling</c> <b>이미터</b>.
    ///
    /// <para>
    /// ★★★ <b>이관하는 것은 «개수»가 아니라 «이미터»다</b> [실측 25회차].
    /// 26회차까지 정본이 적어 둔 「웰컴 ×2 · 인게임 ×1」은 <b>그 캡처 순간의 수</b>였다 —
    /// 실제로는 수명 1 s 짜리가 <c>random(0.5, 1)</c> s 마다 새로 생겨
    /// 동시 존재가 <b>웰컴 1~2 · 인게임 1~4</b> 로 흔들린다 (7,279 / 7,336 프레임 채록).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>아래 값은 유도가 아니라 «이벤트 시트 파라미터 직독»이다</b> [실측 25회차 ·
    /// <c>param.Get(0)</c> 4,000회 재평가]. 생성 블록 8개가 <b>문자 그대로 같은 레시피</b>를 쓴다:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><c>SetSize(1, 1)</c> — 생성 직후 <b>1 × 1</b> world</item>
    /// <item><c>TweenTwoProperties("Size", 1, 64, 64, 0.5, 4, destroy 1, loop 0, ping-pong 1, 1)</c>
    ///       ⇒ <b>0.5 s 에 64 까지 커졌다 되돌아오고 스스로 파괴</b> = <b>수명 1.000 s</b></item>
    /// <item>ease <b>4 = in-out sine</b> [실측 · 21종 잔차 회귀 0.353 vs 차점 quad 1.302]</item>
    /// <item><c>Rotate.SetSpeed = choose(−100, +100)</c> °/s — ★ <b><c>random</c> 이 아니다.</b>
    ///       min/max 만 보면 <c>random(−100, 100)</c> 으로 «보이는데» <b>고유값이 2개뿐</b>이라 갈렸고
    ///       실측 회전율 |평균| <b>100.0</b> 이 확증했다</item>
    /// <item>위치는 <b>생성 시 고정</b> — 수명 내내 |dx| = |dy| = <b>0.000</b> world</item>
    /// <item>틴트 <b>(255,255,255)</b> · 알파 <b>1.0</b> · 각도 초깃값 <b>0</b></item>
    /// </list>
    ///
    /// <para>
    /// ★ <b>「최대 90.2」는 «크기»가 아니었다</b> — 26회차가 잰 것은 <b>회전한 정사각형의 bbox</b>다
    /// (64 × √2 = 90.51). <c>GetWidth()/GetHeight()</c> 를 직접 뜬 실측 최대 폭은 <b>64.000</b> 이다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>생성 영역은 «사각»이다. 원이 아니다</b> — 시트 식이 <c>x = A + random(…)</c> ·
    /// <c>y = B + random(…)</c> 로 <b>축마다 따로</b>라 축정렬 직사각형이 정본이다.
    /// 검산으로 관측 bbox 내접 타원 «밖» 표본이 웰컴 34.9 % · 인게임 67.5 % 나왔다 (원이면 0 % 여야 한다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>무작위는 «주입식»이다.</b> 씨앗을 직렬화 필드로 받고
    /// <see cref="SetSeed"/> 로 갈아 끼운다 — <b>게임 코드에 검증용 문을 새로 뚫지 않는다</b>
    /// (<c>CLAUDE.md</c> 「게임 코드에 검증용 진입점을 두지 않는다」).
    /// 같은 씨앗이면 <b>언제 돌려도 같은 그림</b>이라 렌더 대조가 흔들리지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>좌표·크기는 «이 트랜스폼의 로컬 공간»으로 받는다.</b> 원본 world → 화면 환산은
    /// 레이어마다 배율이 달라(<c>Main</c> 1.6875 · 그 밖 0.84375 · 월드 1/50) <b>굽는 쪽이 한 번만</b> 통과시킨다 —
    /// 이 컴포넌트가 배율을 알면 UI 와 월드 두 벌의 환산이 여기로 새어 든다.
    /// </para>
    /// </summary>
    public sealed class BlumgiSparkleField : MonoBehaviour
    {
        /// <summary>수명 (초) — 저작값 <c>0.5 s × ping-pong</c> [실측 n=234 · 985 ~ 1062 ms].</summary>
        public const float OriginLifeSeconds = 1f;

        /// <summary>생성 간격의 하한 · 상한 (초) — 저작값 <c>Every random(0.5, 1)</c> [실측 평균 725 ~ 754 ms].</summary>
        public const float OriginSpawnMinSeconds = 0.5f;

        public const float OriginSpawnMaxSeconds = 1f;

        /// <summary>회전 속도의 «절댓값» (°/s) — 저작값 <c>choose(−100, +100)</c> [실측 |평균| 100.0].</summary>
        public const float OriginSpinDegreesPerSecond = 100f;

        /// <summary>생성 직후 크기 (원본 world) — <c>SetSize(1, 1)</c>.</summary>
        public const float OriginStartSizeWorld = 1f;

        /// <summary>트윈 목표 크기 (원본 world) — <c>TweenTwoProperties("Size", 1, 64, 64, …)</c>.</summary>
        public const float OriginPeakSizeWorld = 64f;

        [Header("풀 — 동시 존재 실측 웰컴 1~2 · 인게임 1~4")]
        [SerializeField] private Transform[] _items;

        [Header("생성 영역 (이 트랜스폼의 로컬 공간 · 축정렬 사각)")]
        [SerializeField] private Vector2 _areaMin;

        [SerializeField] private Vector2 _areaMax;

        [Header("크기 — localScale 값. 굽는 쪽이 world → 로컬 환산을 이미 통과시켰다")]
        [SerializeField] private float _startScale = 0.01f;

        [SerializeField] private float _peakScale = 1f;

        [Header("저작값 [실측 25회차 · 이벤트 시트 직독]")]
        [SerializeField] private float _lifeSeconds = OriginLifeSeconds;

        [SerializeField] private float _spawnMinSeconds = OriginSpawnMinSeconds;

        [SerializeField] private float _spawnMaxSeconds = OriginSpawnMaxSeconds;

        [SerializeField] private float _spinDegreesPerSecond = OriginSpinDegreesPerSecond;

        /// <summary>
        /// ★ <b>주입식 RNG 의 씨앗.</b> 검증은 이 값을 갈아 끼워 «같은 그림»을 되풀이한다 —
        /// 검사 전용 진입점을 새로 뚫지 않기 위한 자리다.
        /// </summary>
        [SerializeField] private int _seed = 20250831;

        private float[] _age;
        private float[] _spin;
        private float _nextSpawn;
        private uint _state;

        /// <summary>지금 떠 있는 반짝임 수. <b>검사가 「이미터가 도는가」를 이것으로 본다</b>.</summary>
        public int LiveCount
        {
            get
            {
                if (_items == null)
                    return 0;

                int n = 0;

                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i] != null && _items[i].gameObject.activeSelf)
                        n++;
                }

                return n;
            }
        }

        /// <summary>씨앗을 갈아 끼우고 흐름을 처음으로 되돌린다.</summary>
        public void SetSeed(int seed)
        {
            _seed = seed;
            ResetStream();
        }

        /// <summary>난수 흐름과 떠 있는 반짝임을 처음 상태로 되돌린다.</summary>
        public void ResetStream()
        {
            _state = (uint)(_seed == 0 ? 1 : _seed);

            EnsureBuffers();

            if (_items != null)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    _age[i] = -1f;

                    if (_items[i] != null)
                        _items[i].gameObject.SetActive(false);
                }
            }

            _nextSpawn = NextRange(_spawnMinSeconds, _spawnMaxSeconds);
        }

        private void Awake()
        {
            ResetStream();
        }

        private void OnEnable()
        {
            ResetStream();
        }

        private void Update()
        {
            if (_items == null || _items.Length == 0 || _lifeSeconds <= 0f)
                return;

            float dt = Time.unscaledDeltaTime;

            // ── ① 살아 있는 것들을 늙힌다. 위치는 «건드리지 않는다» — 생성 시 고정이다 [실측].
            for (int i = 0; i < _items.Length; i++)
            {
                if (_age[i] < 0f || _items[i] == null)
                    continue;

                _age[i] += dt;

                if (_age[i] >= _lifeSeconds)
                {
                    // destroy-on-complete — 트윈이 왕복을 끝내면 «스스로» 사라진다 [저작값].
                    _age[i] = -1f;
                    _items[i].gameObject.SetActive(false);
                    continue;
                }

                float scale = ScaleAt(_age[i] / _lifeSeconds);

                _items[i].localScale = new Vector3(scale, scale, 1f);
                _items[i].localRotation = Quaternion.Euler(0f, 0f, _spin[i] * _age[i]);
            }

            // ── ② 생성 — `Every random(0.5, 1) s`. 여러 주기가 한 프레임에 몰려도 밀리지 않게 while 로 뺀다.
            _nextSpawn -= dt;

            while (_nextSpawn <= 0f)
            {
                Spawn();
                _nextSpawn += NextRange(_spawnMinSeconds, _spawnMaxSeconds);
            }
        }

        /// <summary>
        /// 크기 곡선 — <b>ping-pong 된 in-out sine</b> [실측 · 저작 ease 4].
        /// <c>s = 2u</c>(u ≤ 0.5) / <c>2(1−u)</c>(u &gt; 0.5) · <c>E(s) = (1 − cos(πs)) / 2</c>.
        /// </summary>
        private float ScaleAt(float u)
        {
            float s = u <= 0.5f ? u * 2f : (1f - u) * 2f;
            float e = (1f - Mathf.Cos(Mathf.PI * Mathf.Clamp01(s))) * 0.5f;

            return Mathf.Lerp(_startScale, _peakScale, e);
        }

        private void Spawn()
        {
            int slot = -1;

            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] != null && _age[i] < 0f)
                {
                    slot = i;
                    break;
                }
            }

            // 풀이 꽉 찼으면 «생성을 거른다». 원본은 무제한이지만 동시 존재 실측 최대가 4 라
            // 풀을 그보다 넉넉히 잡는다 — 여기서 가장 오래된 것을 죽이면 «수명 1 s»가 깨진다.
            if (slot < 0)
                return;

            Transform item = _items[slot];

            item.localPosition = new Vector3(NextRange(_areaMin.x, _areaMax.x),
                                             NextRange(_areaMin.y, _areaMax.y),
                                             0f);

            // ★ `choose(−100, +100)` — 두 값 중 하나다. 연속 난수가 아니다 [실측 고유값 2].
            _spin[slot] = NextBit() ? _spinDegreesPerSecond : -_spinDegreesPerSecond;

            _age[slot] = 0f;

            float scale = ScaleAt(0f);

            item.localScale = new Vector3(scale, scale, 1f);
            item.localRotation = Quaternion.identity;
            item.gameObject.SetActive(true);
        }

        private void EnsureBuffers()
        {
            int n = _items == null ? 0 : _items.Length;

            if (_age == null || _age.Length != n)
            {
                _age = new float[n];
                _spin = new float[n];
            }
        }

        /// <summary>주입식 의사난수 — xorshift32. 씨앗이 같으면 <b>언제나 같은 수열</b>이다.</summary>
        private float NextUnit()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;

            return (_state & 0xFFFFFF) / (float)0x1000000;
        }

        private float NextRange(float min, float max)
        {
            return min + (max - min) * NextUnit();
        }

        private bool NextBit()
        {
            return NextUnit() < 0.5f;
        }
    }
}
