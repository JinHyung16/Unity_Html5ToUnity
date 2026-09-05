using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 인게임 HUD. <b>화면 고정</b>이고 레벨이 바뀌어도 그대로다
    /// (원본 <c>UI</c> 레이어는 <c>scaleRate = 0</c> 이라 레이아웃 스케일을 안 탄다 [실측]).
    ///
    /// <para>
    /// ★ <b>앵커가 두 갈래다</b> [UIUX 1-e 실측] —
    /// 버튼 4개는 <b>오른쪽 끝 기준</b>(−60 / −160 / −260 / −360 world · 간격 100 · 각 100×100),
    /// <c>WORLD n</c> 과 도트는 <b>가운데 기준</b>이다.
    /// 고정 x 로 구우면 1920 이 아닌 화면에서 전부 어긋난다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>워터마크(<c>LogoBlumgi</c>)는 이관하지 않는다</b> — 타사 상표라 우리 빌드에 넣으면 출처 사칭이다
    /// (의도된 차이 #4). 자리도 만들지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>사운드는 1차 무음</b>(확정표 B) — 버튼은 <b>상태 토글만</b> 한다. 실제 오디오는 후속 회차다 (의도된 차이 #2).
    /// </para>
    /// </summary>
    public sealed class BlumgiGameWindow : BaseWindow
    {
        /// <summary>창 식별자. 경로는 <c>Resources</c> 기준이다 (확정표 10-c — UI 프리팹만 Resources).</summary>
        public static readonly WindowKey<BlumgiGameWindow> Key =
            new WindowKey<BlumgiGameWindow>("UI/Window/BlumgiGameWindow");

        [Header("표기")]
        [SerializeField] private TextMeshProUGUI _worldText;
        [SerializeField] private BlumgiLevelDotBar _dotBar;

        [Header("우상단 버튼 — 오른쪽 끝 앵커")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _soundButton;
        [SerializeField] private Button _mapButton;
        [SerializeField] private Button _homeButton;

        [Header("사운드 아이콘 2상태 (Sound 애니 2프레임)")]
        [SerializeField] private Image _soundIcon;
        [SerializeField] private Sprite _soundOn;
        [SerializeField] private Sprite _soundOff;

        [Header("튜토리얼 패널 — 레이어가 UI 가 아니라 FXBottom 이다 (월드와 같이 스케일된다)")]
        [SerializeField] private BlumgiTutorialPanel _tutorial;

        /// <summary>↺ 재시작 — 원본은 <b>현재 레벨만</b> 리셋하고 진행도는 건드리지 않는다 [실측].</summary>
        public event Action RetryClicked;

        /// <summary>⠿ — <b>월드</b> 선택 화면이다 (레벨 선택이 아니다) [실측].</summary>
        public event Action MapClicked;

        public event Action HomeClicked;

        /// <summary>음소거 토글. 인자는 «켜짐(소리 남)» 여부다.</summary>
        public event Action<bool> SoundToggled;

        private bool _soundOnState = true;

        private void Awake()
        {
            if (_retryButton != null)
                _retryButton.onClick.AddListener(() => RetryClicked?.Invoke());

            if (_mapButton != null)
                _mapButton.onClick.AddListener(() => MapClicked?.Invoke());

            if (_homeButton != null)
                _homeButton.onClick.AddListener(() => HomeClicked?.Invoke());

            if (_soundButton != null)
                _soundButton.onClick.AddListener(ToggleSound);

            ApplySoundIcon();
        }

        /// <summary>
        /// HUD 표기값을 넣는다. 계산은 여기서 하지 않는다 — <c>BlumgiGameManager.GetHudSnapshot()</c> 이 만든 것을 찍는다.
        /// ⚠ 원본은 <b>전부 대문자</b>로 그린다 (의도된 차이 #5) — 대체 서체가 올캡이 아니라 여기서 <c>ToUpper()</c> 를 통과시킨다.
        /// </summary>
        public void SetHud(BlumgiHudSnapshot snapshot)
        {
            if (_worldText != null)
                _worldText.text = snapshot.WorldText == null
                    ? string.Empty
                    : snapshot.WorldText.ToUpperInvariant();

            if (_dotBar != null)
                _dotBar.SetStep(snapshot.LevelStep, snapshot.LevelStepCount);
        }

        /// <summary>튜토리얼 패널은 W1L1 에서만 봤다 — 표시 조건은 배선(패스 ③)이 정한다.</summary>
        public void SetTutorialVisible(bool visible)
        {
            if (_tutorial != null)
                _tutorial.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 튜토 마우스 2프레임(<c>leftClic</c>)을 넘긴다. <b>갈아끼우는 스프라이트</b>라
        /// <see cref="BlumgiUiArtBinder"/> 로는 못 넣는다 (바인더는 <c>Image</c> 한 장 = 주소 하나).
        /// 패널이 창의 <c>[SerializeField]</c> 자식이라 <b>창을 통해</b> 전달한다 — 배선이 자식을 직접 뒤지지 않게.
        /// </summary>
        public void SetMouseSprites(Sprite up, Sprite down)
        {
            if (_tutorial != null)
                _tutorial.SetMouseSprites(up, down);
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
