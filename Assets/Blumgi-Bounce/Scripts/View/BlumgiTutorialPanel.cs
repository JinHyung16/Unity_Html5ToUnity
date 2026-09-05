using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 튜토리얼 패널 — <c>HOLD</c> ↔ <c>SHOOT!</c> <b>2컷 교대</b>.
    ///
    /// <para>
    /// ★ <b>한 텍스트의 문자열을 갈아 끼우는 것이 아니다</b> [실측] —
    /// 원본은 <b>판 2장 · 문구 2개 · 마우스 아이콘 3개</b>를 겹쳐 두고 번갈아 보인다 (UIUX 2-b).
    /// 「낱개로 옮기면 반드시 하나가 빠진다」의 그 자리다 — <b>세 벌을 다 옮겼는지 세고 넘어간다.</b>
    /// </para>
    ///
    /// <para>
    /// ★ <b>[정밀화 · 8회차] 한 컷은 «정확히 1.000 s» 다</b> [실측 · 시트 원문
    /// <c>Every(1) → Tuto.SetVisible(toggle)</c>]. 1회차의 「≈ 990 ms ±140」(전환 시각 635 · 1 618 · 2 608 ms)은
    /// <b>같은 값을 격자 오차와 함께 본 것</b>이고, 이제 정확값으로 닫혔다.
    /// 마우스 아이콘도 <b>같은 <c>Every(1)</c></b> 에 물려 있다 [실측].
    /// </para>
    /// </summary>
    public sealed class BlumgiTutorialPanel : MonoBehaviour
    {
        /// <summary>한 컷 지속 [8회차 실측 · 정확히 1.000 s].</summary>
        [SerializeField] private float _cutSeconds = 1f;

        [Header("컷 A — HOLD")]
        [SerializeField] private GameObject _holdCut;

        [Header("컷 B — SHOOT!")]
        [SerializeField] private GameObject _shootCut;

        /// <summary>
        /// 마우스 아이콘 — ★★ <b>세 벌이다</b> [실측 26회차 · 패스 ②-k 정정].
        ///
        /// <para>
        /// 원본 <c>UIMouseIcon2</c> 인스턴스가 <b>3개</b>인 이유가 닫혔다 —
        /// <b>검정 그림자 1(항상) + 흰 컷 2(교대)</b> 다. 흰 두 벌은 프레임만이 아니라
        /// <b>자리도 20 world 다르다</b>(중심 y 364 ↔ 344). 한 이미지의 스프라이트만 갈면
        /// <b>그 20 이 사라지고 그림자도 통째로 빠진다</b> — ②-j 까지가 그 상태였다.
        /// </para>
        /// </summary>
        [Header("마우스 — 검정 그림자 1(고정) + 흰 컷 2(교대) [실측 26회차]")]
        [SerializeField] private Image _mouseShadow;

        /// <summary><c>HOLD</c> 컷과 «같이» 보이는 흰 벌 (원본 f0 · 중심 world y 364).</summary>
        [SerializeField] private Image _mouseHold;

        /// <summary><c>SHOOT!</c> 컷과 «같이» 보이는 흰 벌 (원본 f1 · 중심 world y 344).</summary>
        [SerializeField] private Image _mouseShoot;

        /// <summary>
        /// ★★★ 미니 그림 — <b>두 벌이다</b> [실측 25회차 소스 텍스처 직독 · 패스 ②-l 정정].
        ///
        /// <para>
        /// 원본 <c>Sprite2</c> 는 <b>3프레임짜리 한 오브젝트</b>인데 시트가 <b>컷마다 다른 프레임</b>을 세운다 —
        /// 컷 A 는 <b>f1 · 152 × 154 · 중심 (195, 199)</b>, 컷 B 는 <b>f2 · 230 × 258 · 중심 (255, 147)</b> 이라
        /// <b>그림 · 크기 · 자리가 전부 바뀐다.</b> 한 <c>Image</c> 의 스프라이트만 갈아 끼우면
        /// <b>크기도 자리도 그대로</b>라 컷 B 에서 «안 커진다» — ②-k 까지가 그 상태였다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>f0(138 × 167) 은 안 쓴다</b> — 튜토 채록 전수에서 0회다.
        /// 어디서 쓰이는지는 <b>미측정</b>이라 굽지도 않았다.
        /// </para>
        /// </summary>
        [Header("미니 그림 — 컷마다 «프레임과 크기가 통째로» 바뀐다 [실측 25회차]")]
        [SerializeField] private GameObject _artHold;

        /// <summary><c>SHOOT!</c> 컷의 그림 (원본 f2 · 230 × 258 · 중심 world (255, 147)).</summary>
        [SerializeField] private GameObject _artShoot;

        private float _time;
        private bool _shootShown;

        /// <summary>
        /// 마우스 2프레임(<c>leftClic</c>)을 넣는다 — 패스 ③ 이
        /// <c>BlumgiArtAddress.MouseUp/DownAddress</c> 로 읽어 넣는다.
        ///
        /// <para>
        /// ⚠ <b>자리는 여기서 안 바꾼다.</b> 세 벌이 각자 제 자리에 이미 앉아 있고
        /// 이 메서드는 «어느 그림을 쓰나»만 정한다. 그림자는 <c>HOLD</c> 쪽과 같은 프레임이다
        /// [실측 — 검정 벌의 프레임이 f0 이고 그것이 <c>HOLD</c> 와 같이 보였다].
        /// </para>
        /// </summary>
        public void SetMouseSprites(Sprite up, Sprite down)
        {
            if (_mouseHold != null)
                _mouseHold.sprite = down;

            if (_mouseShoot != null)
                _mouseShoot.sprite = up;

            if (_mouseShadow != null)
                _mouseShadow.sprite = down;

            Apply(_shootShown);
        }

        private void OnEnable()
        {
            _time = 0f;
            Apply(false);
        }

        private void Update()
        {
            if (_cutSeconds <= 0f)
                return;

            _time += Time.unscaledDeltaTime;

            bool shoot = Mathf.Repeat(_time, _cutSeconds * 2f) >= _cutSeconds;

            if (shoot != _shootShown)
                Apply(shoot);
        }

        private void Apply(bool shoot)
        {
            _shootShown = shoot;

            if (_holdCut != null)
                _holdCut.SetActive(shoot == false);

            if (_shootCut != null)
                _shootCut.SetActive(shoot);

            // 흰 두 벌은 «컷»이라 켜고 끈다 — 그림자는 그대로 둔다 [실측 vis].
            if (_mouseHold != null)
                _mouseHold.gameObject.SetActive(shoot == false);

            if (_mouseShoot != null)
                _mouseShoot.gameObject.SetActive(shoot);

            // ★ 미니 그림도 «같은 토글»에 물려 있다 — 문구 · 마우스와 «동시에» 갈아 끼운다 [실측].
            if (_artHold != null)
                _artHold.SetActive(shoot == false);

            if (_artShoot != null)
                _artShoot.SetActive(shoot);
        }
    }
}
