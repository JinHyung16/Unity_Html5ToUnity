using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★★★ <b>골대의 «골인 전용» 스쿼시·스트레치</b> — 17회차가 닫은 「골대 골인 애니메이션」이다.
    ///
    /// <para>
    /// <b>정체가 달랐다.</b> 2회차가 <c>nFrames = 1</c> 로 적었던 것은 <c>_currentAnimation</c> 하나만
    /// 떴기 때문이고, 실제로는 <c>spr_BasketTop</c>/<c>spr_BasketBottom</c> 이 <b>애니메이션 3개</b>를 갖고
    /// 평상시 <c>Animation 2</c> 에 서 있다가 <b>골인 순간 <c>Animation 3</c> 으로 갈아타 4프레임을
    /// 재생하고 멈춘다</b> [실측 n = 1 골 · rAF 채록].
    /// </para>
    ///
    /// <para>
    /// ★ <b>프레임은 «같은 그림을 눌러 그린 것»이다</b> — 소스 텍스처를 꺼내 보니
    /// 같은 크기끼리 불투명 픽셀 수가 <b>정확히 같다</b>(예: Top <c>Animation 2</c> f0 과
    /// <c>Animation 3</c> f2 가 둘 다 222×45 · 8901 px). 그래서 <b>텍스처 4장을 새로 굽지 않고
    /// «비균일 배율 4단»으로 옮긴다</b> — 기전이 같고, 원본 그림을 뜯지 않는다 (재발방지 #53).
    /// </para>
    ///
    /// <list type="table">
    /// <item><term>Top (기본 212 × 55)</term>
    ///       <description>202×65 → 192×75 → 222×45 → 212×55 · <b>w + h = 267 로 «둘레 보존»</b> · <b>30 fps</b></description></item>
    /// <item><term>Bottom (기본 164 × 140)</term>
    ///       <description>144×164 → 174×130 → 179×125 → 164×140 · <b>25 fps</b></description></item>
    /// </list>
    ///
    /// <para>
    /// ⚠ <b>속도는 «엔진 값»을 쓴다</b> [실측 <c>anims.json</c> — <c>Animation 3</c> speed 30 / 25].
    /// rAF 관측 간격(Top 40.4 / 24.6 / 33.1 ms)이 고르지 않았는데, 그것이 rAF 위상 때문인지
    /// 겹친 트윈 때문인지는 <b>미측정</b>이다 — 값을 관측 쪽으로 맞추지 않았다 (재발방지 #48).
    /// </para>
    ///
    /// <para>
    /// ★ <b>위치는 안 움직인다 — 크기만 바뀐다</b> [실측 중심 x = 825 고정].
    /// 다만 그물(<c>spr_BasketBottom</c>)은 원본 원점이 <c>[0.5, 0.05]</c>(위쪽)이라
    /// <b>위 모서리를 붙잡고 아래로 자란다</b> — 우리 스프라이트는 원점이 중심이므로
    /// <see cref="_anchorFromBottom01"/> 로 그만큼 되민다.
    /// </para>
    /// </summary>
    public sealed class BlumgiHoopGoalAnimation : MonoBehaviour
    {
        /// <summary>프레임 수 [실측 <c>Animation 3</c> 4프레임 · <c>loop = false</c>].</summary>
        public const int FrameCount = 4;

        [Header("대상")]
        [SerializeField] private Transform _target;

        [Header("기본 표시 크기 (원본 world px)")]
        [SerializeField] private float _baseWidthWorld = 212f;

        [SerializeField] private float _baseHeightWorld = 55f;

        /// <summary>프레임별 표시 크기 (원본 world px) — <c>x = 폭 · y = 높이</c> [실측].</summary>
        [SerializeField]
        private Vector2[] _frameSizesWorld =
        {
            new Vector2(202f, 65f),
            new Vector2(192f, 75f),
            new Vector2(222f, 45f),
            new Vector2(212f, 55f),
        };

        /// <summary>프레임 간격 [실측 엔진 <c>speed</c> — Top 30 fps ⇒ 1/30 s].</summary>
        [SerializeField] private float _frameSeconds = 1f / 30f;

        /// <summary>
        /// 크기가 바뀌어도 «붙잡혀 있는» 세로 위치 (0 = 아래변 · 0.5 = 중심 · 1 = 위변).
        /// 원본 원점 <c>org.y</c> 는 «위에서» 재므로 <c>1 − org.y</c> 다 — Top 0.5 · Bottom 0.95.
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _anchorFromBottom01 = 0.5f;

        private Vector3 _baseScale = Vector3.one;
        private Vector3 _basePosition;
        private bool _captured;

        private float _timer = -1f;

        /// <summary>지금 재생 중인 프레임 (−1 = 안 돈다). <b>검사가 이것을 읽는다</b>.</summary>
        public int Frame { get; private set; } = -1;

        /// <summary>지금 표시 크기 (원본 world px). <b>검사가 «둘레 보존»을 여기서 잰다</b>.</summary>
        public Vector2 SizeWorld
        {
            get
            {
                if (Frame < 0 || _frameSizesWorld == null || Frame >= _frameSizesWorld.Length)
                    return new Vector2(_baseWidthWorld, _baseHeightWorld);

                return _frameSizesWorld[Frame];
            }
        }

        /// <summary>
        /// 프레임 «표» 그대로 (원본 world px).
        /// ★ <b>검사가 «둘레 보존»을 여기서 잰다</b> — 재생 중에 훔쳐보면 프레임이 짧아(133 ms)
        /// 표본을 놓친다. <b>표를 직접 채점</b>하는 것이 위상에 안 걸린다 (재발방지 #89 의 정신).
        /// </summary>
        public Vector2[] FrameSizesWorld
        {
            get { return _frameSizesWorld; }
        }

        /// <summary>전체 길이 (초). Top 4/30 = 133 ms · Bottom 4/25 = 160 ms.</summary>
        public float TotalSeconds
        {
            get { return _frameSeconds * (_frameSizesWorld == null ? 0 : _frameSizesWorld.Length); }
        }

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (_captured || _target == null)
                return;

            _baseScale = _target.localScale;
            _basePosition = _target.localPosition;
            _captured = true;
        }

        /// <summary>골인 «그 프레임»에 부른다. 4프레임을 재생하고 기본 크기에서 멈춘다.</summary>
        public void Play()
        {
            Capture();

            _timer = 0f;
            Frame = 0;
            ApplyFrame(0);
        }

        /// <summary>레벨이 갈릴 때. 즉시 기본 크기로 되돌린다.</summary>
        public void ResetPose()
        {
            Capture();

            _timer = -1f;
            Frame = -1;

            if (_target == null)
                return;

            _target.localScale = _baseScale;
            _target.localPosition = _basePosition;
        }

        private void Update()
        {
            if (_timer < 0f || _frameSizesWorld == null || _frameSizesWorld.Length == 0)
                return;

            // ★ 시간축은 unscaled 다 — 클리어 연출로 게임이 멈춰도 이 애니는 돌아야 한다.
            _timer += Time.unscaledDeltaTime;

            int frame = _frameSeconds <= 0f ? _frameSizesWorld.Length - 1
                                            : Mathf.FloorToInt(_timer / _frameSeconds);

            if (frame >= _frameSizesWorld.Length)
            {
                // 마지막 프레임이 «기본 크기»라 그대로 멈추면 된다 [실측 f3 = 212×55 / 164×140].
                ApplyFrame(_frameSizesWorld.Length - 1);
                _timer = -1f;
                Frame = -1;
                return;
            }

            if (frame == Frame)
                return;

            Frame = frame;
            ApplyFrame(frame);
        }

        private void ApplyFrame(int frame)
        {
            if (_target == null || _baseWidthWorld <= 0f || _baseHeightWorld <= 0f)
                return;

            Vector2 size = _frameSizesWorld[Mathf.Clamp(frame, 0, _frameSizesWorld.Length - 1)];

            float sx = size.x / _baseWidthWorld;
            float sy = size.y / _baseHeightWorld;

            _target.localScale = new Vector3(_baseScale.x * sx, _baseScale.y * sy, _baseScale.z);

            // 붙잡힌 자리를 고정한다 — 중심 원점 스프라이트를 원본 원점처럼 쓰기 위한 되밀기.
            float offsetWorld = (_anchorFromBottom01 - 0.5f) * _baseHeightWorld * (1f - sy);

            _target.localPosition = _basePosition + new Vector3(0f, BlumgiUnits.ToUnits(offsetWorld), 0f);
        }

        /// <summary>굽는 도구가 값을 넣는 자리. 상수를 고치러 프리팹을 다시 굽지 않아도 된다.</summary>
        public void Configure(Transform target, float baseWidthWorld, float baseHeightWorld,
                              Vector2[] frameSizesWorld, float frameSeconds, float anchorFromBottom01)
        {
            _target = target;
            _baseWidthWorld = baseWidthWorld;
            _baseHeightWorld = baseHeightWorld;
            _frameSizesWorld = frameSizesWorld;
            _frameSeconds = frameSeconds;
            _anchorFromBottom01 = Mathf.Clamp01(anchorFromBottom01);
            _captured = false;

            Capture();
        }
    }
}
