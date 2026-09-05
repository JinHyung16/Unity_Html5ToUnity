using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 창을 조종하는 쪽 — <b>화면 전이 · 버튼 → 매니저 · 값 주입</b>.
    ///
    /// <para>
    /// 호출 방향은 한 갈래다: <c>Window.event → BlumgiManagement → BlumgiGameManager</c>.
    /// 창이 매니저를 직접 부르지 않는다 (UIFramework).
    /// </para>
    ///
    /// <para>
    /// ★ <b>원본에 «없는» 화면을 열지 않는다</b> [실측] — 레벨 선택 없음 · 일시정지 없음 · 게임오버 없음.
    /// 실패해도 화면이 안 바뀌고 공만 재장전된다.
    /// </para>
    /// </summary>
    public sealed class BlumgiManagement : BaseManagement
    {
        // ── WELCOME 문구 [실측 원문]. ⚠ 원본 런타임 문자열은 «소문자»이고 화면이 대문자다 —
        //    대문자화는 창이 한다 (의도된 차이 #5). 여기에 대문자를 적으면 그 사실이 사라진다.
        private const string WelcomeText = "welcome";
        private const string CreditLine1 = "blumgi x venturous";
        private const string CreditLine2 = "music by musmus";

        /// <summary>
        /// 튜토리얼 패널이 «관측된» 레벨.
        /// ⚠ <b>노출 «조건»은 미측정이다</b> (원장 재측정 대기) — 규칙을 지어내지 않고
        /// <b>본 것만</b> 적는다. W1L3 이후 캡처에는 없었다 [실측].
        /// </summary>
        private static readonly HashSet<string> TutorialLevels = new HashSet<string> { "W1L1", "W1L2" };

        private BlumgiGameRoot _root;
        private BlumgiLevelPresenter _presenter;
        private BlumgiHoldInput _holdInput;
        private BlumgiConfettiBurst _confetti;

        /// <summary>골인 빛줄기·텔레포트 (패스 ②-h). ⚠ <b>컨페티와 달리 «골대 좌표»가 필요하다</b>.</summary>
        private BlumgiGoalBurst _goalBurst;

        private BlumgiWelcomeWindow _welcome;
        private BlumgiGameWindow _gameWindow;
        private BlumgiClearOverlay _clearOverlay;

        /// <summary>음소거 상태. ⚠ <b>1차는 무음</b>이라 «상태만» 든다 (확정표 B · 의도된 차이 #2).</summary>
        private bool _soundOn = true;

        private BlumgiGameManager Game
        {
            get { return _root == null ? null : _root.Game; }
        }

        /// <summary>초기화 «전»에 부른다 — <see cref="BaseManagement.Initialize"/> 가 이 참조를 곧바로 쓴다.</summary>
        public void Bind(BlumgiGameRoot root,
                         BlumgiLevelPresenter presenter,
                         BlumgiHoldInput holdInput,
                         BlumgiConfettiBurst confetti,
                         BlumgiGoalBurst goalBurst)
        {
            _root = root;
            _presenter = presenter;
            _holdInput = holdInput;
            _confetti = confetti;
            _goalBurst = goalBurst;
        }

        protected override void AddWindows()
        {
            RegisterWindow(BlumgiWelcomeWindow.Key);
            RegisterWindow(BlumgiGameWindow.Key);
            RegisterWindow(BlumgiClearOverlay.Key);
        }

        protected override void OnInitialize()
        {
            // 창을 «미리» 만든다 — 아트 주입과 이벤트 구독이 실물을 요구한다.
            // (만든 직후는 닫힌 상태다 — 여는 것은 화면 전이가 한다.)
            _welcome = GetWindow(BlumgiWelcomeWindow.Key);
            _gameWindow = GetWindow(BlumgiGameWindow.Key);
            _clearOverlay = GetWindow(BlumgiClearOverlay.Key);

            if (_welcome != null)
            {
                _welcome.OnePlayerClicked += HandleOnePlayerClicked;
                _welcome.SoundToggled += HandleSoundToggled;
            }

            if (_gameWindow != null)
            {
                _gameWindow.RetryClicked += HandleRetryClicked;
                _gameWindow.MapClicked += HandleMapClicked;
                _gameWindow.HomeClicked += HandleHomeClicked;
                _gameWindow.SoundToggled += HandleSoundToggled;
            }

            if (_clearOverlay != null)
            {
                _clearOverlay.SetConfetti(_confetti);
                _clearOverlay.SetGoalBurst(_goalBurst);
                _clearOverlay.FadeCovered += HandleFadeCovered;
                _clearOverlay.Finished += HandleClearFinished;
            }

            if (_holdInput != null)
            {
                _holdInput.HoldBegan += HandleHoldBegan;
                _holdInput.HoldEnded += HandleHoldEnded;
                _holdInput.gameObject.SetActive(false);
            }

            if (Game != null)
            {
                Game.LevelLoaded += HandleLevelLoaded;
                Game.LevelCleared += HandleLevelCleared;
            }

            if (_root != null)
                _root.GameFlow.OnScreenChanged += HandleScreenChanged;
        }

        protected override void OnDispose()
        {
            if (_welcome != null)
            {
                _welcome.OnePlayerClicked -= HandleOnePlayerClicked;
                _welcome.SoundToggled -= HandleSoundToggled;
            }

            if (_gameWindow != null)
            {
                _gameWindow.RetryClicked -= HandleRetryClicked;
                _gameWindow.MapClicked -= HandleMapClicked;
                _gameWindow.HomeClicked -= HandleHomeClicked;
                _gameWindow.SoundToggled -= HandleSoundToggled;
            }

            if (_clearOverlay != null)
            {
                _clearOverlay.FadeCovered -= HandleFadeCovered;
                _clearOverlay.Finished -= HandleClearFinished;
            }

            if (_holdInput != null)
            {
                _holdInput.HoldBegan -= HandleHoldBegan;
                _holdInput.HoldEnded -= HandleHoldEnded;
            }

            if (Game != null)
            {
                Game.LevelLoaded -= HandleLevelLoaded;
                Game.LevelCleared -= HandleLevelCleared;
            }

            if (_root != null)
                _root.GameFlow.OnScreenChanged -= HandleScreenChanged;
        }

        // ══════════════════════════════════════════ 아트 주입

        /// <summary>
        /// UI 스프라이트를 어드레서블에서 읽어 창에 물린다.
        ///
        /// <para>
        /// ★ <b>Resources 프리팹이 어드레서블 아트를 직접 물면 빌드에 중복 편입</b>되므로
        /// 프리팹에는 «주소»만 구워져 있다 (ResourceRule) — 여기가 그 주소를 스프라이트로 바꾸는 자리다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>«갈아끼우는» 스프라이트는 바인더로 못 넣는다</b> (바인더는 <c>Image</c> 한 장 = 주소 하나) —
        /// 음소거 2상태 · 마우스 2프레임은 따로 넣는다.
        /// </para>
        /// </summary>
        public async Task LoadUiArtAsync(CancellationToken cancellationToken = default)
        {
            await ApplyBinderAsync(_welcome);
            await ApplyBinderAsync(_gameWindow);
            await ApplyBinderAsync(_clearOverlay);

            if (cancellationToken.IsCancellationRequested)
                return;

            Sprite soundOn = await ArtLoader.LoadSpriteAsync(BlumgiArtAddress.ButtonSoundOnAddress);
            Sprite soundOff = await ArtLoader.LoadSpriteAsync(BlumgiArtAddress.ButtonSoundOffAddress);

            _welcome?.SetSoundSprites(soundOn, soundOff);
            _gameWindow?.SetSoundSprites(soundOn, soundOff);

            Sprite mouseUp = await ArtLoader.LoadSpriteAsync(BlumgiArtAddress.MouseUpAddress);
            Sprite mouseDown = await ArtLoader.LoadSpriteAsync(BlumgiArtAddress.MouseDownAddress);

            _gameWindow?.SetMouseSprites(mouseUp, mouseDown);

            ApplySoundState();
        }

        private static async Task ApplyBinderAsync(BaseWindow window)
        {
            if (window == null)
                return;

            var binder = window.GetComponent<BlumgiUiArtBinder>();

            if (binder == null)
                return;

            string[] addresses = binder.CollectAddresses();
            Sprite[] sprites = await ArtLoader.LoadSpritesAsync(addresses);

            var map = new Dictionary<string, Sprite>(addresses.Length);

            for (int i = 0; i < addresses.Length; i++)
                map[addresses[i]] = sprites[i];

            binder.Apply(map);
        }

        // ══════════════════════════════════════════ 화면 전이

        private void HandleScreenChanged(EBlumgiScreenType previous, EBlumgiScreenType next)
        {
            switch (next)
            {
                case EBlumgiScreenType.Welcome:
                    ShowWelcome();
                    break;

                case EBlumgiScreenType.Game:
                    ShowGame();
                    break;

                case EBlumgiScreenType.WorldSelect:
                    // ⚠ <b>의도된 차이 #6</b> — 월드 선택은 이관 범위 밖이고 ⠿ 버튼도 꺼 뒀다
                    //   (BlumgiPrefabBuilder). 그래서 «정상 경로로는 여기 올 수 없다».
                    //   그래도 남겨 두는 이유는, 누가 나중에 이 화면으로 전이를 넣었을 때
                    //   «빈 화면»으로 넘어가 사람이 갇히는 것을 막기 위해서다.
                    Log.Error("월드 선택은 의도된 차이 #6 (미이관) 인데 전이가 들어왔다 — 부른 자리를 고친다");
                    break;
            }
        }

        private void ShowWelcome()
        {
            CloseClearOverlay();

            _gameWindow?.Close();

            if (_holdInput != null)
                _holdInput.gameObject.SetActive(false);

            if (_welcome == null)
                return;

            _welcome.Open();
            _welcome.SetTexts(WelcomeText, CreditLine1, CreditLine2);

            // 1 PLAYER 카드 아래의 손가락 커서는 «유도 연출»이라 항상 보인다 [실측].
            _welcome.SetHandCursorVisible(true);
            _welcome.SetSoundOn(_soundOn);
        }

        private void ShowGame()
        {
            _welcome?.Close();

            if (_gameWindow == null || Game == null)
                return;

            _gameWindow.Open();
            _gameWindow.SetSoundOn(_soundOn);

            if (_holdInput != null)
                _holdInput.gameObject.SetActive(true);

            // ⚠ 원본은 «저장된 진행 레벨»로 들어간다 — 항상 W1L1 이 아니다 [실측 플로우 diff 1번].
            if (Game.CurrentLevel == null)
                Game.LoadLevel(BlumgiProgress.LoadLevelCode());
            else
                HandleLevelLoaded(Game.CurrentLevel);
        }

        // ══════════════════════════════════════════ 버튼

        private void HandleOnePlayerClicked()
        {
            _root?.GameFlow.ChangeScreen(EBlumgiScreenType.Game);
        }

        /// <summary>↺ — <b>현재 레벨만</b> 되돌린다. 도트 진행은 그대로다 [실측].</summary>
        private void HandleRetryClicked()
        {
            Game?.RestartLevel();
        }

        /// <summary>
        /// ⠿ — 원본은 <b>월드</b> 선택 화면으로 간다 (레벨 선택이 아니다) [실측].
        ///
        /// <para>
        /// ★ <b>의도된 차이 #6 으로 버튼 자체를 숨겼다</b> (PD 판정 2026-08-30 · <c>BlumgiPrefabBuilder</c>) —
        /// 이관 범위가 월드1 이라 갈 곳이 없고, 눌러도 아무 일이 없는 버튼은 «고장»으로 보인다.
        /// 그래서 <b>이 자리는 정상 경로에서 불리지 않는다.</b> 구독을 남겨 두는 것은
        /// 누가 버튼을 다시 켰을 때 «조용히 아무 일도 안 일어나는» 상태가 되지 않게 하기 위해서다.
        /// </para>
        /// </summary>
        private void HandleMapClicked()
        {
            Log.Error("⠿ 는 의도된 차이 #6 으로 숨긴 버튼인데 눌렸다 — 프리팹 빌더가 다시 켰는지 본다");
        }

        /// <summary>🏠 — <b>확인 팝업 없이</b> WELCOME 으로 [실측]. 진행은 유지된다.</summary>
        private void HandleHomeClicked()
        {
            _root?.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);
        }

        /// <summary>
        /// 🔊 — <b>상태 토글만</b> 한다. 화면 전환이 없고 게임도 안 멈춘다 [실측].
        /// ⚠ 실제 오디오는 후속 회차다 (확정표 B · 의도된 차이 #2).
        /// </summary>
        private void HandleSoundToggled(bool on)
        {
            _soundOn = on;
            ApplySoundState();
        }

        private void ApplySoundState()
        {
            _welcome?.SetSoundOn(_soundOn);
            _gameWindow?.SetSoundOn(_soundOn);
        }

        // ══════════════════════════════════════════ 입력

        private void HandleHoldBegan()
        {
            Game?.OnPointerDown();
        }

        private void HandleHoldEnded()
        {
            Game?.OnPointerUp();
        }

        // ══════════════════════════════════════════ 레벨 · 클리어

        private void HandleLevelLoaded(BlumgiLevelRuntime runtime)
        {
            if (_gameWindow == null || Game == null || runtime == null)
                return;

            _gameWindow.SetHud(Game.GetHudSnapshot());
            _gameWindow.SetTutorialVisible(TutorialLevels.Contains(runtime.Level.Code));
        }

        /// <summary>
        /// 골인 — <b>연출을 먼저 띄운다</b>. 다음 레벨은 <see cref="HandleFadeCovered"/> 가 올린다.
        /// 골인과 다음 레벨 표시 사이가 <b>≈ 2 620 ms</b> 다 [실측 05_연출 §2].
        /// </summary>
        private void HandleLevelCleared(string clearedLevelCode)
        {
            if (_clearOverlay == null)
                return;

            _clearOverlay.SetConfetti(_confetti);
            _clearOverlay.SetGoalBurst(_goalBurst);
            _clearOverlay.Open();

            Transform worldFrame = _presenter == null ? null : _presenter.WorldFrame;

            // ⚠ [정정 · 8회차] 컨페티 자리를 «넘기지 않는다» — 캐논 2문이 원본에서 고정 좌표다.
            //   예전에는 골대 좌표를 넘겼는데, 그것은 「골대 부근에서 솟구친다」는 1회차 오독이었다.
            _confetti?.SetWorldFrame(worldFrame);

            // ★★ 반대로 골인 빛줄기·텔레포트는 «골대에 붙는다» [실측 5레벨 5/5 · 패스 ②-h].
            //    레벨마다 좌표를 «표»로 들지 않는다 — 레벨 데이터의 골대 좌표를 그대로 넘긴다.
            if (_goalBurst != null)
            {
                _goalBurst.SetWorldFrame(worldFrame);

                BlumgiLevelRuntime level = Game?.CurrentLevel;

                if (level != null)
                    _goalBurst.SetAnchorWorld(level.Level.GoalRimX, level.Level.GoalRimY);
            }

            _clearOverlay.Play();
        }

        /// <summary>
        /// 흰색이 화면을 덮은 순간 — 원본이 «비쳐 드는» 자리다. 여기서 갈아 끼운다.
        ///
        /// <para>
        /// ★ <b>월드1 을 완주하면(W1L5 클리어) WELCOME 으로 돌아간다</b> — <b>의도된 차이 #7</b>
        /// (PD 판정 2026-08-30). 원본은 W2L1 로 가지만 <b>월드2 는 관측 범위 밖</b>이라 데이터가 없다.
        /// ⚠ 예전처럼 «경고만 남기고 멈추면» 흰 화면이 걷힌 자리에 W1L5 가 그대로 남아
        /// <b>사람이 갇힌다</b>. 없는 화면을 지어내지 않으면서 갇히지도 않는 최소 처리가 «복귀»다.
        /// </para>
        /// </summary>
        private void HandleFadeCovered()
        {
            if (_root == null)
                return;

            if (_root.AdvanceToNextLevel())
                return;

            // ⚠ 되돌아가기 «전에» 마지막 레벨을 다시 올린다. 안 그러면 WELCOME 에서 1 PLAYER 를 눌렀을 때
            //   «이미 골이 들어간» W1L5 가 그대로 살아나 (ShowGame 은 현재 레벨이 있으면 다시 안 올린다)
            //   공을 쏴도 아무 일이 안 일어난다 — 화면만 바뀌고 사람은 여전히 갇힌다.
            Game?.RestartLevel();

            _root.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);
        }

        private void HandleClearFinished()
        {
            CloseClearOverlay();
        }

        private void CloseClearOverlay()
        {
            if (_clearOverlay == null)
                return;

            _clearOverlay.StopAll();
            _clearOverlay.Close();
        }
    }
}
