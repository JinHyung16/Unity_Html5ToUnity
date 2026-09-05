using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// <b>블롭 머리 위의 공</b> — <c>spr_BallVisu</c> [6회차 실측 · 04 §2-d-3].
    ///
    /// <para>
    /// ★★ <b>이것은 물리체가 아니다.</b> 원본은 미발사 공(<c>SPR_BallPhysics</c>)을 경기장에 두지 않고
    /// <c>(0, 10000)</c> 에서 자유낙하시킨다 — 머리 위에 «보이는» 공은 <b>별개 오브젝트</b>
    /// (<c>spr_BallVisu</c> · 94 × 93 world)다. 패스 ①-d 가 물리 공을 원본대로 경기장 밖으로 내리면서
    /// <b>머리 위가 비었고</b>, 그 자리를 채우는 것이 이 뷰다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>여기에 콜라이더·강체를 붙이지 마라.</b> 붙이는 순간 「발사하지 않은 공이 굴러다니다 골이 된다」는
    /// 패스 ①-c 의 위양성 2건이 되살아난다 — 그것을 없애려고 ①-d 가 물리 공을 내린 것이다.
    /// </para>
    ///
    /// <para>
    /// 자리는 <b>레벨 데이터</b>가 갖고 있다 — <c>BallRestX/Y</c> → <c>BlumgiLevelRuntime.BallRestPosition</c>
    /// [★ 14회차-b 5레벨 실측: (200.106,476.804) · (259.239,817.581) · (1050.106,224.804) ·
    /// (985.906,285.847) · (187.047,261.077) — 6회차의 0.5 반올림 값을 갈아 끼웠다].
    /// ⚠ <b>발사대 기준 «오프셋»으로 파생시키지 않는다</b> — 5레벨의 오프셋이
    /// (+50.11,−94.96) ~ (−64.09,−83.91) 로 흩어져 있고 04 §6 이 <b>「레벨 데이터로 쓰면 안 된다」</b>고 못 박았다.
    /// 14회차-b 가 5레벨을 나란히 재니 <b>길이는 104.98~107.76 으로 같은데 방향이 조준각을 따라간다</b> —
    /// 그래도 <b>−2.6°~+4.0° 가 남아</b> 상수로도 함수로도 안 접힌다. <b>절대 좌표만 실측이다.</b>
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>[정정 · 8회차] 홀드 중에 이 공은 «뜨지» 않는다 — «커진다».</b>
    /// 1회차가 「살짝 위·오른쪽으로 뜬다」로 적었고 패스 ②-c 가 그 서술대로
    /// <c>_holdRiseWorld</c>·<c>_holdShiftWorld</c> 라는 <b>변위 파라미터</b>를 뒀는데,
    /// 8회차 런타임 채록이 그것을 뒤집었다 [실측]:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>블롭 물리체 중심 기준 오프셋 <b>(+50.11, −94.96) → (+50.42, −92.59)</b> — 홀드 내내 <b>2.4 px 안에서 불변</b></item>
    /// <item>바뀌는 것은 <b>크기 하나</b> — <b>94 × 93 → 112.8 × 111.6</b> (+20.0 %) · <b>easeOutSine · 1.5 s</b></item>
    /// <item>공의 <b>위쪽 모서리</b>는 y 430.30 → 430.87 로 <b>0.57 px 만</b> 움직인다 ⇒ <b>피벗이 «위쪽 가운데»</b></item>
    /// </list>
    ///
    /// <para>
    /// ⇒ <b>「커지는 것」을 «이동»으로 읽은 것이 1회차 오류의 정체다.</b> 그래서 변위 파라미터를 걷어내고
    /// <b>위 모서리를 고정한 스케일 트윈</b>으로 바꿨다. 스프라이트 피벗 자체는 <b>안 건드린다</b> —
    /// 같은 <c>ball</c> 텍스처를 물리 공이 함께 쓰기 때문이다. 대신 <b>자식 렌더러를 스케일하고
    /// 그만큼 내려</b> 위 모서리를 붙잡는다 (결과가 피벗 (0.5, 1.0) 과 같다).
    /// </para>
    ///
    /// <para>
    /// 거동은 <b>발사에 숨김 → «다음 누름»에 다시 보임</b>이다 [8회차 정정 —
    /// 예전 서술의 「바닥선 통과 + 0.5 s 자동 복귀」는 원본에 <b>없다</b>].
    /// 켜고 끄는 판단은 <see cref="BlumgiLevelPresenter"/> 가 한다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlumgiRestBallView : MonoBehaviour
    {
        /// <summary>어드레서블 주소 = 프리팹 파일명 (<c>BlumgiAddressableSetup</c> 규약).</summary>
        public const string PrefabName = "BlumgiRestBall";

        /// <summary>대기 표시 크기 (원본 world px) [8회차 실측 · <c>SetSize(94, 93)</c> 원문].</summary>
        public const float RestWidthWorld = 94f;

        public const float RestHeightWorld = 93f;

        /// <summary>홀드 포화 표시 크기 [8회차 실측 · Tween <c>"sizeBall"</c> 목표값].</summary>
        public const float HoldWidthWorld = 112.8f;

        public const float HoldHeightWorld = 111.6f;

        /// <summary>
        /// 크기 트윈의 지속 [8회차 실측 · <c>eControls</c> EVENT#45 <c>Tween "sizeBall" … 1.5 s</c>].
        ///
        /// <para>
        /// ★★ <b>값을 여기서 «갖지» 않는다 — <see cref="BlumgiSquashTween"/> 를 읽는다.</b>
        /// 12회차가 <b>발사점도 같은 시계로 돈다</b>는 것을 실측했다. 세 곳(블롭·머리공·발사점)이
        /// 각자 1.5 를 들고 있으면 한 곳만 고쳐졌을 때 <b>서로 다른 시계로 돈다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>힘 포화(1.633 s)와 «다른 시간»이다.</b> 홀드 진행도(0~1)로 나눠 쓰면
        /// 8 % 어긋난다 — 그래서 이 뷰는 <b>초</b>를 받는다.
        /// </para>
        /// </summary>
        public const float HoldTweenSeconds = BlumgiSquashTween.HoldSecondsFloat;

        [Header("그림")]
        [SerializeField] private SpriteRenderer _renderer;

        private Vector3 _restPosition;
        private Vector3 _rendererBaseScale = Vector3.one;
        private Vector3 _rendererBasePosition;
        private float _holdSeconds;

        /// <summary>지금 그려지고 있나. <b>재생 검사가 이것을 본다</b>.</summary>
        public bool IsShown
        {
            get { return _renderer != null && _renderer.enabled; }
        }

        /// <summary>이 레벨에서 공이 얹혀 있는 자리 (유니티 월드 좌표).</summary>
        public Vector3 RestPosition
        {
            get { return _restPosition; }
        }

        /// <summary>지금 크기 배율 (1 = 대기 94×93). <b>검사가 이것을 읽어 트윈을 채점한다</b>.</summary>
        public Vector2 SizeScale
        {
            get
            {
                if (_renderer == null || _rendererBaseScale.x == 0f || _rendererBaseScale.y == 0f)
                    return Vector2.one;

                Vector3 now = _renderer.transform.localScale;
                return new Vector2(now.x / _rendererBaseScale.x, now.y / _rendererBaseScale.y);
            }
        }

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<SpriteRenderer>(true);

            if (_renderer != null)
            {
                _rendererBaseScale = _renderer.transform.localScale;
                _rendererBasePosition = _renderer.transform.localPosition;
            }

            _restPosition = transform.position;
            ApplySize();
        }

        /// <summary>레벨이 올라올 때 자리를 잡는다. 좌표는 <c>BallRestX/Y</c> 를 환산한 값이다.</summary>
        public void SetRestPosition(Vector3 worldPosition)
        {
            _restPosition = worldPosition;
            transform.position = _restPosition;
        }

        /// <summary>
        /// <b>발사(릴리즈)</b> — 즉시 숨는다 [8회차 실측 · EVENT#38 <c>BallVisu.SetVisible(0)</c>].
        /// 지연도 페이드도 축소 트윈도 <b>없다</b>.
        /// </summary>
        public void HideOnLaunch()
        {
            SetShown(false);
        }

        /// <summary>
        /// <b>다음 누름</b> — 즉시 보이고 크기가 <b>94 × 93 으로 리셋</b>된다
        /// [8회차 실측 · EVENT#39 <c>SetSize(94, 93)</c> → EVENT#44 <c>SetVisible(1)</c>].
        /// <b>이것이 재장전의 전부다 — 1단이고 지연이 0 이다.</b>
        /// </summary>
        public void ShowOnPress()
        {
            _holdSeconds = 0f;
            ApplySize();
            SetShown(true);
        }

        /// <summary>
        /// 켜고 끈다.
        /// ⚠ <c>GameObject</c> 를 끄지 않는다 — 끄면 <c>Awake</c> 가 도로 안 돌아 자리 계산이 갈린다.
        /// </summary>
        public void SetShown(bool shown)
        {
            if (_renderer != null)
                _renderer.enabled = shown;
        }

        /// <summary>
        /// 누르고 있는 «초»를 넣는다 — 크기 트윈이 이것으로 돈다.
        /// ⚠ <b>진행도(0~1)가 아니라 초다</b> — 트윈 1.5 s 와 힘 포화 1.633 s 가 다른 시간이기 때문이다.
        /// </summary>
        public void SetHoldSeconds(float seconds)
        {
            _holdSeconds = seconds < 0f ? 0f : seconds;
            ApplySize();
        }

        /// <summary>
        /// 크기를 실제로 얹는다 — <b>위쪽 모서리를 붙잡고</b> 아래로 자란다.
        ///
        /// <para>
        /// 스케일이 <c>s</c> 일 때 중심 기준 스케일은 위 모서리를 <c>(s−1)·46.5 world</c> 만큼 올린다.
        /// 그만큼 <b>도로 내려</b> 실측(「위 모서리 0.57 px 만 움직인다」)과 맞춘다.
        /// </para>
        /// </summary>
        private void ApplySize()
        {
            if (_renderer == null)
                return;

            // ★ 시계는 하나다 — 발사점(BlumgiLevelRuntime.LaunchPositionAt)이 «같은 함수»를 쓴다.
            float eased = BlumgiSquashTween.EaseFloat(_holdSeconds);

            float sx = Mathf.Lerp(1f, HoldWidthWorld / RestWidthWorld, eased);
            float sy = Mathf.Lerp(1f, HoldHeightWorld / RestHeightWorld, eased);

            Transform sprite = _renderer.transform;

            sprite.localScale = new Vector3(_rendererBaseScale.x * sx,
                                            _rendererBaseScale.y * sy,
                                            _rendererBaseScale.z);

            // 위 모서리 고정 — 자란 만큼(세로 절반) 내린다. 유니티는 위가 +y 라 «빼는» 것이 아래다.
            float grownHalf = (sy - 1f) * RestHeightWorld * 0.5f;

            sprite.localPosition = _rendererBasePosition
                                   - new Vector3(0f, BlumgiUnits.ToUnits(grownHalf), 0f);
        }
    }
}
