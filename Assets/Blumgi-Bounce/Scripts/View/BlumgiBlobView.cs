using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 발사대 블롭의 그림. <b>파워 게이지 UI 가 원본에 없다</b> [실측 — 5레벨·10회 이상 홀드에서 관측 0] —
    /// 차징 피드백은 <b>스쿼시 · 표정</b> 둘이다 (05_연출 3-1).
    ///
    /// <para>
    /// ⚠ <b>[정정 · 8회차] 「공이 뜨는 높이」는 세 번째 피드백이 아니었다</b> — 머리 위 공은 «뜨지» 않고
    /// <b>커진다</b>. 자세한 것은 <see cref="BlumgiRestBallView"/> 주석.
    /// </para>
    ///
    /// <para>
    /// ★★ 몸 변형은 <b>프레임 교체가 아니라 연속 스케일</b>이다 —
    /// <c>spr_Slime</c> 은 애니 프레임이 2장뿐인데 관측된 형태가 연속적이었다 [실측].
    /// 8회차가 그 트윈의 <b>목표값·지속·이징을 시트에서 통째로 읽었다</b>:
    /// </para>
    ///
    /// <list type="table">
    /// <item><term>홀드</term><description><c>spr_SlimePhysics</c> <b>87.14 × 50 → 113.28 × 35</b> · <b>1.5 s · easeOutSine</b> (EVENT#45)</description></item>
    /// <item><term>트윈 종료 직후</term><description>한 프레임에 <b>118.04 × 32.27</b> 로 한 단계 더 눌리고 유지 [실측 · 원인 미확정]</description></item>
    /// <item><term>릴리즈 순간</term><description><b>43.57 × 75</b> 로 «즉시» 늘어난다 (EVENT#39)</description></item>
    /// <item><term>릴리즈 복귀</term><description>43.57 × 75 → <b>87.14 × 50</b> · <b>0.25 s · easeOutBack</b> (오버슛 실측 +9.3 %)</description></item>
    /// </list>
    ///
    /// <para>
    /// ⚠ <b>부피 보존이 아니다</b> — 가로 ×1.300 인데 세로는 ×0.700 이다 (곱이 0.91). 예전 구현이
    /// 세로를 가로의 «역수»로 잡았는데, 그것은 <b>측정이 아니라 물리 직관</b>이었다.
    /// </para>
    ///
    /// <para>
    /// 표정(<c>spr_SlimeEyes</c>)은 프레임 3장이고 <b>전환 조건이 전부 실측</b>이다 [8회차 §3-f · 17회차 §2-b]:
    /// 0 평상 · 1 찡그림(<c>forceShoot &gt; 35</c> ⇒ 실 홀드 <b>447.8 ms</b>) ·
    /// 2 발사 표정(릴리즈 프레임부터 <b>340.4 ms</b>).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[해소 · 17회차] 프레임 2 의 «그림»이 닫혔다</b> — 소스 텍스처를 그대로 꺼냈다
    /// (<b>50 × 47</b> · 이 프레임만 시트가 <c>shared-1-sheet2</c> 로 다르다).
    /// 「눈 감음」이 아니라 <b>둥근 눈 2개 + 세로로 길게 벌린 «외치는 입»</b> 이다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[실측 · 17회차] 몸통 <c>spr_Slime</c> 은 프레임이 «안 바뀐다»</b> — rAF 채록 전 구간
    /// <c>frameIdx 0</c> 고정(<c>nFrames 2</c> · <c>speed 0</c>). <b>표정 변화는 «눈» 하나로만 일어나고
    /// 몸통 변형은 프레임이 아니라 크기 트윈</b>이다. 몸통 스프라이트를 갈아끼우면 원본과 다르다.
    /// </para>
    /// </summary>
    public sealed class BlumgiBlobView : MonoBehaviour
    {
        // ── 원본 물리체(spr_SlimePhysics) 크기 [8회차 실측 · 시트 원문]
        private const float IdleWidthWorld = 87.14f;
        private const float IdleHeightWorld = 50f;

        private const float HoldWidthWorld = 113.28f;
        private const float HoldHeightWorld = 35f;

        /// <summary>트윈이 끝난 «직후» 한 프레임에 바뀌어 그대로 유지되는 값 [실측 · 원인 미확정].</summary>
        private const float SettledWidthWorld = 118.04f;

        private const float SettledHeightWorld = 32.27f;

        private const float LaunchWidthWorld = 43.57f;
        private const float LaunchHeightWorld = 75f;

        /// <summary>
        /// 홀드 스쿼시 트윈 지속 [실측 · <c>Tween "slimeSquash" … 1.5 s · ease[3]</c>].
        /// ★★ <b>값을 여기서 «갖지» 않는다</b> — <see cref="BlumgiSquashTween"/> 가 유일한 시계다
        /// (12회차: <b>발사점도 같은 시계로 돈다</b>).
        /// </summary>
        private const float HoldTweenSeconds = BlumgiSquashTween.HoldSecondsFloat;

        /// <summary>발사 복귀 트윈 지속 [실측 · <c>… 0.25 s · ease[9]</c>].</summary>
        private const float ReleaseTweenSeconds = 0.25f;

        /// <summary>
        /// 발사 표정(프레임 2)이 유지되는 시간.
        /// [정정 · 17회차] 8회차의 347 / 349 ms 를 <b>rAF 격자(8.3 ms)에서 다시 재</b> <b>340.4 ms</b> 로 좁혔다.
        /// </summary>
        private const float ShootFaceSeconds = 0.3404f;

        /// <summary>
        /// 찡그림으로 바뀌는 힘 [실측 — 8회차 두 시행 <c>F = 35.21 / 35.22</c> ·
        /// 17회차 rAF 재확증 <c>F = 35.2035</c> (홀드 시작 +447.8 ms)].
        /// 원본 힘은 <c>force = 10 + 55·t</c> 라 <b>실 홀드 447.8 ms</b> 다.
        /// ⚠ 임계의 «형태»가 힘인지 시간인지는 <b>원리적으로 분리 불가</b>(둘이 1차식으로 묶여 있다) —
        /// 이 이벤트가 다루는 것이 force 뿐이라 <b>force 로 적는다</b> `추정(근거: 시트 문맥)`.
        /// </summary>
        private const float AngryForceThreshold = 35f;

        private const float ForceAtRest = 10f;
        private const float ForcePerSecond = 55f;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _body;
        [SerializeField] private SpriteRenderer _eyes;

        [Header("표정 (spr_SlimeEyes)")]
        [SerializeField] private Sprite _eyesIdle;
        [SerializeField] private Sprite _eyesAngry;

        /// <summary>
        /// 프레임 2 «발사 표정» [17회차 실측 소스 50 × 47 — 외치는 입].
        /// ⚠ 비어 있으면 <b>그림만</b> 평상을 유지한다 (상태는 그대로 2 로 답한다).
        /// </summary>
        [SerializeField] private Sprite _eyesShoot;

        private Vector3 _bodyBaseScale = Vector3.one;

        /// <summary>발사 복귀 트윈이 도는 중인가. 음수면 안 돈다.</summary>
        private float _releaseTimer = -1f;

        private float _shootFaceTimer = -1f;
        private float _holdSeconds;

        /// <summary>
        /// 지금 표정 프레임 (0 평상 · 1 찡그림 · 2 발사) — <b>검사가 이것을 읽어 임계를 채점한다</b>.
        /// 그림이 비어 있어도 <b>「지금 몇 번 프레임인가」는 여기서 정확히 답한다</b> (그림과 상태를 분리한다).
        /// </summary>
        public int FaceFrame { get; private set; }

        /// <summary>
        /// 프레임 2 «발사 표정»의 스프라이트 이름 (없으면 빈 문자열).
        /// ★ <b>검사가 「번호만 바뀌고 그림은 그대로」를 이것으로 가른다</b> —
        /// 17회차 전에는 그림이 미관측이라 실제로 그랬다.
        /// </summary>
        public string ShootFaceSpriteName
        {
            get { return _eyesShoot == null ? string.Empty : _eyesShoot.name; }
        }

        /// <summary>
        /// 지금 «화면에 떠 있는» 눈 스프라이트 이름.
        /// ★ <b>검사가 「번호만 바뀌고 그림은 그대로」를 이것으로 가른다</b> —
        /// <see cref="FaceFrame"/> 만 보면 그림이 안 바뀌어도 통과한다 (17회차 전에는 실제로 그랬다).
        /// </summary>
        public string EyesSpriteName
        {
            get { return _eyes == null || _eyes.sprite == null ? string.Empty : _eyes.sprite.name; }
        }

        /// <summary>지금 몸 배율 (1 = 대기 87.14 × 50). <b>검사가 이것을 읽어 트윈을 채점한다</b>.</summary>
        public Vector2 BodyScale
        {
            get
            {
                if (_body == null || _bodyBaseScale.x == 0f || _bodyBaseScale.y == 0f)
                    return Vector2.one;

                Vector3 now = _body.transform.localScale;
                return new Vector2(now.x / _bodyBaseScale.x, now.y / _bodyBaseScale.y);
            }
        }

        private void Awake()
        {
            if (_body != null)
                _bodyBaseScale = _body.transform.localScale;
        }

        /// <summary>
        /// 누르고 있는 «초»를 넣는다.
        /// ⚠ <b>진행도(0~1)가 아니라 초다</b> — 트윈 1.5 s 와 힘 포화 1.633 s 가 다른 시간이라
        /// 진행도로 나누면 8 % 어긋난다 (예전 구현이 그랬다).
        /// </summary>
        public void SetHoldSeconds(float seconds)
        {
            _holdSeconds = seconds < 0f ? 0f : seconds;
            _releaseTimer = -1f;

            float p = (float)BlumgiSquashTween.Progress(_holdSeconds);

            float width;
            float height;

            if (p >= 1f)
            {
                // ★ 트윈이 «끝난 뒤»에는 한 단계 더 눌린 값으로 유지된다 [실측].
                // ⚠ 이 «한 단계 더»가 발사점에도 실리는지는 미측정이다 (12회차 표본이 홀드 0.9 s 까지).
                width = SettledWidthWorld;
                height = SettledHeightWorld;
            }
            else
            {
                // ★ 시계는 하나다 — 머리공·발사점이 «같은 함수»를 쓴다.
                float eased = BlumgiSquashTween.EaseFloat(_holdSeconds);
                width = Mathf.Lerp(IdleWidthWorld, HoldWidthWorld, eased);
                height = Mathf.Lerp(IdleHeightWorld, HoldHeightWorld, eased);
            }

            ApplyBody(width, height);

            float force = ForceAtRest + ForcePerSecond * _holdSeconds;
            SetFace(force > AngryForceThreshold ? 1 : 0);
        }

        /// <summary>
        /// 발사 순간 — <b>즉시 43.57 × 75 로 늘어나고</b> 0.25 s easeOutBack 으로 대기 형태에 돌아온다.
        /// 표정은 프레임 2 로 바뀌고 <b>≈ 348 ms</b> 뒤 평상으로 [실측].
        /// </summary>
        public void PlayRelease()
        {
            _holdSeconds = 0f;
            _releaseTimer = 0f;
            _shootFaceTimer = 0f;

            ApplyBody(LaunchWidthWorld, LaunchHeightWorld);
            SetFace(2);
        }

        /// <summary>레벨이 올라온 직후처럼 «아무 일도 없던» 상태로.</summary>
        public void ResetPose()
        {
            _holdSeconds = 0f;
            _releaseTimer = -1f;
            _shootFaceTimer = -1f;

            ApplyBody(IdleWidthWorld, IdleHeightWorld);
            SetFace(0);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_releaseTimer >= 0f)
            {
                _releaseTimer += dt;

                float p = ReleaseTweenSeconds <= 0f ? 1f : _releaseTimer / ReleaseTweenSeconds;

                if (p >= 1f)
                {
                    _releaseTimer = -1f;
                    ApplyBody(IdleWidthWorld, IdleHeightWorld);
                }
                else
                {
                    // ⚠ OutBack 은 1 을 «넘어갔다» 돌아온다 — Lerp 를 클램프하면 오버슛이 사라진다.
                    float eased = BlumgiEase.OutBack(p);

                    ApplyBody(Mathf.LerpUnclamped(LaunchWidthWorld, IdleWidthWorld, eased),
                              Mathf.LerpUnclamped(LaunchHeightWorld, IdleHeightWorld, eased));
                }
            }

            if (_shootFaceTimer < 0f)
                return;

            _shootFaceTimer += dt;

            if (_shootFaceTimer < ShootFaceSeconds)
                return;

            _shootFaceTimer = -1f;
            SetFace(0);
        }

        /// <summary>몸 크기를 «원본 world px» 로 넣는다 — 텍스처가 이미 대기 비율이라 배율은 나눗셈이다.</summary>
        private void ApplyBody(float widthWorld, float heightWorld)
        {
            if (_body == null)
                return;

            _body.transform.localScale = new Vector3(_bodyBaseScale.x * (widthWorld / IdleWidthWorld),
                                                     _bodyBaseScale.y * (heightWorld / IdleHeightWorld),
                                                     _bodyBaseScale.z);
        }

        /// <summary>
        /// 표정을 «프레임 번호»로 넣는다 — 원본이 <c>SetAnimFrame(n)</c> 이라 번호가 정본이다.
        /// <b>「없는 그림을 지어내는 것」과 「상태를 감추는 것」을 둘 다 피하는 자리다.</b>
        /// </summary>
        private void SetFace(int frame)
        {
            FaceFrame = frame;

            if (_eyes == null)
                return;

            Sprite next = frame == 1 ? _eyesAngry : frame == 2 ? _eyesShoot : _eyesIdle;

            if (next == null)
                next = _eyesIdle;

            if (next != null && _eyes.sprite != next)
                _eyes.sprite = next;
        }
    }
}
