using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// WELCOME 화면.
    ///
    /// <para>
    /// ★★ <b>웰컴은 «한 배율»이 아니다</b> [실측 함정] — 원본 <c>Start</c> 레이아웃은 <c>layout._scale = 2</c> 인데
    /// <b><c>Main</c> 레이어만</b> 그 배율을 탄다(<c>scaleRate = 1</c>). <c>UI</c>·<c>FG</c>·<c>BG</c> 는 <c>scaleRate = 0</c> 이라 1배다.
    /// world 좌표를 한 배율로 환산하면 <b>카드가 화면 밖으로 나간다</b>.
    /// ⇒ 그래서 이 창은 <b>UIUX 2-a 의 «정규화 좌표»를 그대로</b> 쓴다 — 배율 계산을 하지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>2 PLAYERS 카드는 1차에서 숨긴다</b> (확정표 C · 의도된 차이 #1) — 자리는 두되 꺼 둔다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 크레딧 2줄은 <b>크기가 서로 다르다</b> — <c>[size=35]</c> / <c>[size=30]</c> [실측].
    /// 한 크기로 굽지 않는다.
    /// </para>
    /// </summary>
    public sealed class BlumgiWelcomeWindow : BaseWindow
    {
        /// <summary>창 식별자. 경로는 <c>Resources</c> 기준이다.</summary>
        public static readonly WindowKey<BlumgiWelcomeWindow> Key =
            new WindowKey<BlumgiWelcomeWindow>("UI/Window/BlumgiWelcomeWindow");

        /// <summary>
        /// ★ <b>배경 장식은 «내용물»과 별개 필드</b>다 (Transfer_Artist 「장식과 내용물」).
        /// 하나로 두면 패스 ③ 의 런타임 주입이 장식을 덮어 버린다.
        /// 값은 <see cref="BlumgiWelcomeBackgroundLayout"/> 실측표대로 빌더가 구워 두었으므로
        /// <b>배선이 아무것도 안 해도 원본과 같다</b> — 색을 갈아야 할 때만 이 자리를 쓴다.
        /// </summary>
        [Header("배경 장식 — BG 5개 (격자 · 야자수 2 · 기둥 2)")]
        [SerializeField] private BlumgiWelcomeBackground _background;

        [Header("카드")]
        [SerializeField] private Button _onePlayerButton;

        /// <summary>확정표 C 로 <b>꺼 둔다</b>. 지우지 않는 이유는 「1차 범위 밖」이지 「없는 화면」이 아니어서다.</summary>
        [SerializeField] private GameObject _twoPlayersCard;

        [Header("문구")]
        [SerializeField] private TextMeshProUGUI _welcomeText;
        [SerializeField] private TextMeshProUGUI _creditLine1;
        [SerializeField] private TextMeshProUGUI _creditLine2;

        [Header("유도")]
        [SerializeField] private GameObject _handCursor;

        [Header("음소거")]
        [SerializeField] private Button _soundButton;
        [SerializeField] private Image _soundIcon;
        [SerializeField] private Sprite _soundOn;
        [SerializeField] private Sprite _soundOff;

        /// <summary>배경 장식 묶음. 패스 ③ 이 <b>색을 갈아야 할 때만</b> 쓴다 — 좌표는 프리팹이 든다.</summary>
        public BlumgiWelcomeBackground Background
        {
            get { return _background; }
        }

        public event Action OnePlayerClicked;
        public event Action<bool> SoundToggled;

        private bool _soundOnState = true;

        private void Awake()
        {
            if (_onePlayerButton != null)
                _onePlayerButton.onClick.AddListener(() => OnePlayerClicked?.Invoke());

            if (_soundButton != null)
                _soundButton.onClick.AddListener(ToggleSound);

            if (_twoPlayersCard != null)
                _twoPlayersCard.SetActive(false);

            ApplySoundIcon();
        }

        /// <summary>
        /// 문구를 넣는다. ⚠ 원본 런타임 문자열은 <b>소문자</b>인데 화면은 전부 대문자다 [실측] —
        /// 「원문 그대로 넣으면 된다」가 <b>여기서는 틀린다</b>. <c>ToUpper()</c> 를 통과시킨다.
        /// </summary>
        public void SetTexts(string welcome, string credit1, string credit2)
        {
            if (_welcomeText != null)
                _welcomeText.text = welcome == null ? string.Empty : welcome.ToUpperInvariant();

            if (_creditLine1 != null)
                _creditLine1.text = credit1 == null ? string.Empty : credit1.ToUpperInvariant();

            if (_creditLine2 != null)
                _creditLine2.text = credit2 == null ? string.Empty : credit2.ToUpperInvariant();
        }

        public void SetHandCursorVisible(bool visible)
        {
            if (_handCursor != null)
                _handCursor.SetActive(visible);
        }

        /// <summary>
        /// 상태에 따라 «갈아끼우는» 스프라이트라 <see cref="BlumgiUiArtBinder"/> 로는 못 넣는다 (그쪽은 <c>Image</c> 한 장 = 주소 하나).
        /// 패스 ③ 이 <c>BlumgiArtAddress.ButtonSoundOn/OffAddress</c> 로 읽어 여기에 넣는다.
        /// </summary>
        public void SetSoundSprites(Sprite on, Sprite off)
        {
            _soundOn = on;
            _soundOff = off;
            ApplySoundIcon();
        }

        public void SetSoundOn(bool on)
        {
            _soundOnState = on;
            ApplySoundIcon();
        }

        private void ToggleSound()
        {
            _soundOnState = _soundOnState == false;
            ApplySoundIcon();
            SoundToggled?.Invoke(_soundOnState);
        }

        private void ApplySoundIcon()
        {
            if (_soundIcon == null)
                return;

            Sprite next = _soundOnState ? _soundOn : _soundOff;

            if (next != null)
                _soundIcon.sprite = next;
        }
    }
}
