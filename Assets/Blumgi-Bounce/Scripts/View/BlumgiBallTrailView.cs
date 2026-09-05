using JinHyung.Extensions;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 공 뒤의 <b>혜성 꼬리</b>.
    ///
    /// <para>
    /// ★ <b>TrailRenderer 로 «선»을 그리면 원본과 다르다.</b> 원본의 정체는
    /// <c>spr_BallVisu2</c> <b>인스턴스 31개</b>를 과거 위치에 늘어놓은 «고스트 트레일»이다
    /// [런타임 실측 · 05_연출 3-2].
    /// </para>
    ///
    /// <para>
    /// 색은 <b>흰색</b>이고 테마색·공색을 따라가지 않는다 [실측].
    /// </para>
    ///
    /// <para>
    /// ★★ <b>[해소 · 8회차] 알파가 «미측정»에서 «실측»이 됐다 — 그리고 역산이 아니다.</b>
    /// <c>spr_BallVisu2</c> 의 <b>Fade 비헤이비어 상수를 런타임에서 직독</b>했다 [실측 §3-g]:
    /// <c>fadeIn 0</c> · <c>wait 0</c> · <b><c>fadeOut 0.25 s</c></b> · <c>destroy on complete</c> ·
    /// <b><c>maxOpacity 0.15</c></b>. 확정표 6 의 「알파 역산 금지」는 <b>배경 혼합에서 되돌리는 것</b>을
    /// 막는 규칙이라 <b>여기에 걸리지 않는다</b> — 원본이 들고 있는 «수» 자체를 읽은 것이다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>인덱스별 알파 감쇠가 곧 그 Fade 다</b> [검산] — 고스트 <c>i</c> 의 나이는
    /// <c>i × 8.33 ms</c> 이고, 31장 째가 <c>30 × 8.33 = 250 ms</c> = <b>fadeOut 과 같다.</b>
    /// 그래서 머리 <c>0.15</c> → 꼬리 <c>0</c> 의 선형 보간이 원본의 개체별 페이드와 <b>같은 그림</b>을 만든다.
    /// </para>
    /// </summary>
    public sealed class BlumgiBallTrailView : MonoBehaviour
    {
        /// <summary>원본 인스턴스 수 <b>31</b> [런타임 실측].</summary>
        public const int OriginGhostCount = 31;

        [Header("고스트")]
        [SerializeField] private SpriteRenderer[] _ghosts;

        /// <summary>Fade <c>maxOpacity</c> [8회차 실측 · 런타임 <c>_sdkInst</c> 직독 — 역산이 아니다].</summary>
        [Header("알파 [8회차 실측 · Fade maxOpacity 0.15 / fadeOut 0.25 s]")]
        [SerializeField, Range(0f, 1f)] private float _headAlpha = 0.15f;

        [SerializeField, Range(0f, 1f)] private float _tailAlpha = 0f;

        /// <summary>
        /// 잔상을 «몇 초마다» 한 장씩 남기나 — <b>원본 프레임 간격 8.33 ms</b>
        /// [3회차 실측 · rAF 12017표본 중앙값 · 120 Hz].
        ///
        /// <para>
        /// ★★ <b>이것을 안 두면 꼬리 «길이»가 우리 프레임률에 끌려간다.</b> 31장을 «매 프레임» 밀면
        /// 60 fps 에서는 517 ms 어치, 300 fps 에서는 103 ms 어치가 되어 같은 코드가 다른 그림을 낸다
        /// (배치 재생 캡처에서 실제로 꼬리가 공에 뭉쳐 «후광»으로 보였다).
        /// </para>
        ///
        /// <para>
        /// ★ <b>이 값은 지어낸 것이 아니라 세 실측이 서로 맞물린 자리다</b> —
        /// 31장 × 8.33 ms = <b>258 ms</b>, 발사 속력 ≈ 900 px/s 에서 <b>≈ 232 world px</b> 이고
        /// 공 지름 94 px 의 <b>2.5배</b>다. 05_연출 3-2 의 「길이 = 공 지름의 약 2~3배」와 <b>일치한다.</b>
        /// (① 인스턴스 31 [런타임 실측] ② 원본 프레임 8.33 ms [3회차] ③ 꼬리 길이 2~3배 [05_연출])
        /// </para>
        /// </summary>
        [Header("표본 간격 — 원본 프레임 8.33 ms [3회차 실측]")]
        [SerializeField] private float _sampleSeconds = 0.00833f;

        private Vector3[] _history;
        private int _count;
        private float _sampleTimer;

        private void Awake()
        {
            _history = new Vector3[_ghosts == null ? 0 : _ghosts.Length];
            Clear();
        }

        /// <summary>
        /// 비행 중 매 프레임 부른다 — <b>원본 프레임 간격마다 한 장씩만</b> 밀어 넣는다.
        ///
        /// <para>
        /// 우리 프레임이 원본(120 Hz)보다 촘촘하면 <b>건너뛰고</b>, 성기면 프레임마다 한 장이 최선이다.
        /// 어느 쪽이든 <b>꼬리 길이가 프레임률에 안 끌려간다.</b>
        /// </para>
        /// </summary>
        public void Advance(Vector3 worldPosition, float deltaTime)
        {
            if (_sampleSeconds <= 0f)
            {
                Push(worldPosition);
                return;
            }

            _sampleTimer += deltaTime;

            if (_sampleTimer < _sampleSeconds)
                return;

            // ⚠ 0 으로 되돌리지 않고 «빼서» 남긴다 — 그래야 표본 간격이 프레임 경계로 밀리지 않는다.
            //    다만 한 프레임에 여러 장을 «같은 자리»에 밀면 잔상이 뭉치므로 한 장으로 자른다.
            _sampleTimer -= _sampleSeconds;

            if (_sampleTimer > _sampleSeconds)
                _sampleTimer = 0f;

            Push(worldPosition);
        }

        /// <summary>이번 표본의 공 위치를 밀어 넣는다. 가장 오래된 잔상이 밀려 나간다.</summary>
        public void Push(Vector3 worldPosition)
        {
            if (_ghosts.IsNullOrEmpty())
                return;

            for (int i = _history.Length - 1; i > 0; i--)
                _history[i] = _history[i - 1];

            _history[0] = worldPosition;

            if (_count < _history.Length)
                _count++;

            Apply();
        }

        /// <summary>발사 전·골인 후처럼 «빠르게 움직이지 않는» 구간에서는 꼬리가 사라진다 [실측].</summary>
        public void Clear()
        {
            _count = 0;
            _sampleTimer = 0f;

            if (_ghosts.IsNullOrEmpty())
                return;

            for (int i = 0; i < _ghosts.Length; i++)
            {
                if (_ghosts[i] != null)
                    _ghosts[i].gameObject.SetActive(false);
            }
        }

        private void Apply()
        {
            for (int i = 0; i < _ghosts.Length; i++)
            {
                SpriteRenderer ghost = _ghosts[i];

                if (ghost == null)
                    continue;

                bool alive = i < _count;

                if (ghost.gameObject.activeSelf != alive)
                    ghost.gameObject.SetActive(alive);

                if (alive == false)
                    continue;

                ghost.transform.position = _history[i];

                float t = _ghosts.Length <= 1 ? 0f : i / (float)(_ghosts.Length - 1);
                Color c = ghost.color;
                c.a = Mathf.Lerp(_headAlpha, _tailAlpha, t);
                ghost.color = c;
            }
        }
    }
}
