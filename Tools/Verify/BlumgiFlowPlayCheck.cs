using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 패스 ③ <b>배선·플로우 검사</b> — <b>재생 모드에서 실제 포인터 이벤트</b>를 흘린다.
    ///
    /// <para>
    /// ★★ <b>편집 모드로는 «하나도» 못 잰다.</b> 캔버스가 한 번도 렌더되지 않으면
    /// 모든 <c>Graphic</c> 의 depth 가 −1 로 남고 <c>GraphicRaycaster</c> 가 전부 건너뛴다 —
    /// 「맞는 것이 하나도 없다」가 게임 버그처럼 보인다. 그래서 <c>PLAYMODE=1</c> + 그래픽 장치다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>대조군을 같이 쏜다</b> (재발방지 #13) — 「사람이 눌러서 되는 것」으로 알려진 HUD 아이콘 버튼을
    /// 같은 방법으로 때려 본다. <b>대조군까지 안 맞으면 게임이 아니라 검사 환경이 문제</b>다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 검사용 문을 뚫지 않았다</b> — 입력은 <c>EventSystem</c> 을 지나고,
    /// 홀드는 <c>BlumgiShotSimulation.HoldSeconds</c> 가 실제로 차오를 때까지 «기다린다».
    /// </para>
    ///
    /// <para>실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.BlumgiFlowPlayCheck.RunAll</c></para>
    /// </summary>
    public static class BlumgiFlowPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiFlowPlayCheck.Armed";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

        public const string ScenePath = "Assets/Blumgi-Bounce/Scenes/BlumgiBounce.unity";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);

            // ⚠ 골든 채점기와 다르다 — 저쪽은 «전용 로컬 물리 씬»을 만들지만
            //   이 검사의 대상이 «씬 그 자체»라 반드시 이 씬을 열어야 한다.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arm()
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(ArmedKey, false) == false)
                return;

            SessionState.SetBool(ArmedKey, false);

            var go = new GameObject("BlumgiFlowProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiFlowProbe>();
#endif
        }
    }

    /// <summary>실제 검사기. 화면을 «켜 놓고» 사람처럼 누른다.</summary>
    public sealed class BlumgiFlowProbe : MonoBehaviour
    {
        private const string ReportPath = "Temp/BlumgiFlowReport.txt";

        /// <summary>
        /// ★★ <b>레벨별 클리어 홀드</b> [실측 Design — 포인터 (0.5, 0.5)].
        ///
        /// <para>
        /// ⚠ <b>패스 ②-c 인계 — 이 검사가 «저장된 진행도»에 따라 점수가 갈렸다</b>(63/64 vs 66/66).
        /// W1L1 의 600ms 하나만 들고 있어서, 진행도가 남아 다른 레벨로 들어가면 그 레벨을 못 깨고
        /// <b>없는 결함이 실패로 잡혔다.</b> 그래서 ① 검사 «시작 상태를 스스로 만들고»(<see cref="ResetProgress"/>)
        /// ② 그래도 어느 레벨에 서든 깰 수 있게 <b>레벨별 홀드를 전부</b> 든다.
        /// </para>
        /// </summary>
        private static double ClearHoldSecondsFor(string levelCode)
        {
            switch (levelCode)
            {
                case "W1L1": return 0.600;
                case "W1L2": return 0.800;
                case "W1L3": return 0.600;
                case "W1L4": return 0.250;
                case "W1L5": return 0.250;
                default: return 0.600;
            }
        }

        /// <summary>발사가 안 되는 홀드 [실측 — 실홀드 118 ms 이하는 위로 뜨지 않는다].</summary>
        private const double DropHoldSeconds = 0.050;

        private const float StepTimeoutSeconds = 40f;

        private readonly StringBuilder _report = new StringBuilder(1 << 14);
        private readonly List<string> _failLines = new List<string>();

        private int _pass;
        private int _fail;

        private BlumgiGameInitialize _init;
        private BlumgiGameRoot _root;
        private BlumgiHoldInput _hold;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;

            IEnumerator body = Body();

            while (true)
            {
                bool moved;

                try
                {
                    moved = body.MoveNext();
                }
                catch (Exception e)
                {
                    Line($"  ❌ 검사 중 예외: {e}");
                    break;
                }

                if (moved == false)
                    break;

                yield return body.Current;
            }

            Line(string.Empty);
            Line("========================================");
            Line($"═══ 플로우 diff: {_pass}/{_pass + _fail} 통과");

            if (_failLines.Count > 0)
            {
                Line(string.Empty);
                Line("-- 못 맞춘 항목 --");

                for (int i = 0; i < _failLines.Count; i++)
                    Line("  " + _failLines[i]);
            }

            WriteReport();
            Debug.Log(_report.ToString());

            yield return null;

            Finish(_fail == 0);
        }

        private IEnumerator Body()
        {
            // ══════════════════════════════ 0. ★ 검사가 «시작 상태를 스스로 만든다»
            ResetProgress();

            // ══════════════════════════════ 0. 초기화 · 씬 확정값
            _init = FindFirstObjectByType<BlumgiGameInitialize>();
            Check("0-a 씬에 BlumgiGameInitialize 가 있다", _init != null, "씬이 안 구워졌다");

            if (_init == null)
                yield break;

            float waited = 0f;

            while (_init.IsReady == false && waited < StepTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check("0-b 초기화가 끝났다 (데이터·프리팹·창)", _init.IsReady, $"{waited:F1}s 안에 안 끝났다");

            if (_init.IsReady == false)
                yield break;

            _root = BlumgiGameRoot.Instance;

            ScoreSceneSetup();
            ScoreDataGate();

            // ══════════════════════════════ 1. 첫 화면 = WELCOME
            Section("1. 화면 전이 — 첫 화면");

            Check("1-a 첫 화면이 WELCOME 이다",
                  _root.GameFlow.Current == EBlumgiScreenType.Welcome,
                  _root.GameFlow.Current.ToString());

            var welcome = Window<BlumgiWelcomeWindow>(BlumgiWelcomeWindow.Key);
            var gameWindow = Window<BlumgiGameWindow>(BlumgiGameWindow.Key);

            Check("1-b WELCOME 창이 열려 있다", welcome != null && welcome.IsOpen(), "");
            Check("1-c 인게임 창이 «안» 열려 있다", gameWindow == null || gameWindow.IsOpen() == false, "");

            // 의도된 차이 #1 — 2 PLAYERS 카드는 숨긴다.
            Transform twoCard = FindDeep(welcome == null ? null : welcome.transform, "TwoPlayersCard");
            Check("1-d 2 PLAYERS 카드가 꺼져 있다 (의도된 차이 #1)",
                  twoCard != null && twoCard.gameObject.activeInHierarchy == false,
                  twoCard == null ? "카드를 못 찾았다" : "켜져 있다");

            // 의도된 차이 #4 — 워터마크는 자리조차 없다.
            Check("1-e 워터마크가 없다 (의도된 차이 #4)",
                  FindDeep(welcome == null ? null : welcome.transform, "LogoBlumgi") == null,
                  "워터마크 오브젝트가 있다");

            ScoreWelcomeBackground(welcome);

            // ══════════════════════════════ 2. 1 PLAYER → 인게임
            Section("2. WELCOME → 1 PLAYER");

            Transform oneCard = FindDeep(welcome == null ? null : welcome.transform, "OnePlayerCard");
            Check("2-a 1 PLAYER 카드를 찾았다", oneCard != null, "");

            if (oneCard != null)
            {
                Check("2-b 1 PLAYER 카드가 레이캐스트에 «자기 자신»으로 잡힌다  ← 대조군과 같은 방법",
                      RaycastHits(oneCard), "RaycastAll 첫 결과가 이 카드가 아니다");

                ClickAt(oneCard);
            }

            yield return null;
            yield return null;

            Check("2-c 화면이 인게임으로 갔다",
                  _root.GameFlow.Current == EBlumgiScreenType.Game,
                  _root.GameFlow.Current.ToString());

            gameWindow = Window<BlumgiGameWindow>(BlumgiGameWindow.Key);

            Check("2-d 인게임 창이 열렸다", gameWindow != null && gameWindow.IsOpen(), "");
            Check("2-e WELCOME 창이 닫혔다", welcome != null && welcome.IsOpen() == false, "");

            BlumgiGameManager game = _root.Game;
            Check("2-f 레벨이 올라갔다", game.CurrentLevel != null, "");

            if (game.CurrentLevel == null)
                yield break;

            string entryLevel = game.CurrentLevel.Level.Code;
            Line($"    진입 레벨 = {entryLevel} (저장된 진행 = {BlumgiProgress.LoadLevelCode()})");

            // ⚠ 진행도를 지우고 시작하므로 여기서는 «첫 레벨»이 나와야 한다.
            //   「저장된 진행으로 들어간다」는 거동은 §7-d/7-e 가 «클리어 뒤에» 실제로 검사한다 —
            //   그쪽이 훨씬 강한 검사이고, 이쪽은 시작 상태가 결정적인지를 본다.
            Check("2-g 진행도를 지운 뒤에는 «첫 레벨»로 들어간다 (시작 상태가 결정적이다)",
                  entryLevel == BlumgiProgress.LoadLevelCode() && entryLevel == BlumgiGameManager.FirstLevelCode,
                  $"{entryLevel} (저장 {BlumgiProgress.LoadLevelCode()})");

            ScoreLevelBuild(game);
            ScoreHud(gameWindow, game);
            ScoreButtons(gameWindow);

            // ══════════════════════════════ 3. 홀드 → 릴리즈 (실패 샷)
            Section("3. 입력 — 홀드 → 릴리즈  (⚠ 조준은 «없다» · 파워 1축)");

            _hold = FindFirstObjectByType<BlumgiHoldInput>(FindObjectsInactive.Include);
            Check("3-a 홀드 판이 있다", _hold != null, "");

            if (_hold == null)
                yield break;

            Check("3-b 홀드 판이 화면 중앙에서 «자기 자신»으로 잡힌다",
                  RaycastHitsScreenPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), _hold.transform),
                  "중앙 레이캐스트 첫 결과가 홀드 판이 아니다");

            // ── 발사가 «안 되는» 홀드 — 원본은 118 ms 이하면 위로 안 뜬다 [실측].
            yield return Shoot(game, DropHoldSeconds);

            // ★ 6회차 실측 — 임계 미만은 «임펄스 없이 놓이는» 것이 아니라 **발사가 아예 안 걸린다**.
            //   그래서 상태가 Ready 로 남고 |v0| 가 0 이다 (공은 대기 자리에서 계속 떨어진다).
            Check("3-c 짧은 홀드는 발사되지 않는다 (실측 임계 미만)",
                  game.Shot.State == EBlumgiShotState.Ready &&
                  game.Shot.LaunchVelocity.Magnitude < 1e-6,
                  $"state={game.Shot.State} |v0|={game.Shot.LaunchVelocity.Magnitude:F1}");

            Check("3-d 실패해도 «화면이 안 바뀐다» (게임오버 화면 없음)",
                  _root.GameFlow.Current == EBlumgiScreenType.Game, "");

            Check("3-e 레벨이 그대로다 (실패 패널티 없음)",
                  game.CurrentLevel.Level.Code == entryLevel, game.CurrentLevel.Level.Code);

            // ── 재장전을 기다린다 — 공이 화면 밖으로 나가고 되돌아온다.
            yield return WaitUntilReady(game, 12f);

            // ══════════════════════════════ 4. ↺ 재시작
            Section("4. ↺ 재시작 — 현재 레벨만");

            Transform retry = FindDeep(gameWindow.transform, "RetryButton");
            Check("4-a ↺ 버튼을 찾았다", retry != null, "");

            if (retry != null)
            {
                Check("4-b ↺ 가 레이캐스트에 «자기 자신»으로 잡힌다  ← 대조군",
                      RaycastHits(retry), "RaycastAll 첫 결과가 ↺ 가 아니다");

                ClickAt(retry);
                yield return null;

                Check("4-c 같은 레벨이 다시 올라갔다 (화면을 안 떠난다)",
                      game.CurrentLevel != null && game.CurrentLevel.Level.Code == entryLevel &&
                      _root.GameFlow.Current == EBlumgiScreenType.Game,
                      "");
            }

            // ══════════════════════════════ 5. 🔊 음소거 — 상태 토글만
            Section("5. 🔊 음소거 — 상태 토글만 (의도된 차이 #2)");

            Transform sound = FindDeep(gameWindow.transform, "SoundButton");
            string levelBeforeSound = game.CurrentLevel.Level.Code;

            if (sound != null)
            {
                Sprite before = IconSprite(sound);
                ClickAt(sound);
                yield return null;

                Check("5-a 아이콘이 갈렸다", IconSprite(sound) != before,
                      "스프라이트가 그대로다 (SetSoundSprites 주입 실패?)");

                Check("5-b 화면 전환이 없다",
                      _root.GameFlow.Current == EBlumgiScreenType.Game &&
                      game.CurrentLevel.Level.Code == levelBeforeSound, "");

                ClickAt(sound);
                yield return null;

                Check("5-c 다시 누르면 원복된다", IconSprite(sound) == before, "");
            }
            else
            {
                Check("5-a 🔊 버튼을 찾았다", false, "");
            }

            // ══════════════════════════════ 6. 클리어 → 자동으로 다음 레벨
            Section("6. 클리어 — 입력 없이 다음 레벨 (연출 ≈ 2 620 ms 뒤)");

            yield return WaitUntilReady(game, 12f);

            bool cleared = false;
            int attempts = 0;

            // ★ 서 있는 레벨의 홀드를 쓴다 — 「진행도가 남으면 못 깬다」가 ②-c 의 인계 결함이었다.
            double clearHold = ClearHoldSecondsFor(game.CurrentLevel.Level.Code);
            Line($"    클리어 홀드 = {clearHold * 1000:F0} ms ({game.CurrentLevel.Level.Code} 실측값)");

            while (cleared == false && attempts < 3)
            {
                attempts++;
                yield return Shoot(game, clearHold);

                Check($"6-a({attempts}) 홀드 {clearHold * 1000:F0} ms 가 «실제로» 발사됐다",
                      game.Shot.LaunchVelocity.Magnitude > 1.0,
                      $"|v0| = {game.Shot.LaunchVelocity.Magnitude:F1}");

                float t = 0f;

                while (t < 20f)
                {
                    if (game.Shot.State == EBlumgiShotState.Scored)
                    {
                        cleared = true;
                        break;
                    }

                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (cleared == false)
                    yield return WaitUntilReady(game, 12f);
            }

            Check("6-b 한 판이 «실제로» 골인했다", cleared, $"{attempts}회 시도해도 골인 없음");

            if (cleared)
            {
                var overlay = Window<BlumgiClearOverlay>(BlumgiClearOverlay.Key);
                Check("6-c 클리어 연출 창이 열렸다", overlay != null && overlay.IsOpen(), "");

                float t = 0f;

                while (t < 12f && game.CurrentLevel.Level.Code == entryLevel)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                Check("6-d 입력 «없이» 다음 레벨로 갔다",
                      game.CurrentLevel.Level.Code != entryLevel,
                      $"{t:F1}s 뒤에도 {game.CurrentLevel.Level.Code}");

                Line($"    골인 → 다음 레벨 표시까지 {t:F2}s (원본 실측 ≈ 2.62 s)");

                Check("6-e 전이가 «연출 뒤»에 일어난다 (즉시 컷이 아니다)",
                      t > 1.5f, $"{t:F2}s 만에 갈렸다 — 연출을 건너뛰었다");
            }

            // ══════════════════════════════ 7. 🏠 홈 → WELCOME · 진행 유지
            Section("7. 🏠 홈 — 확인 팝업 없이 WELCOME · 진행 유지");

            string progressLevel = game.CurrentLevel.Level.Code;
            Transform home = FindDeep(gameWindow.transform, "HomeButton");

            if (home != null)
            {
                ClickAt(home);
                yield return null;

                Check("7-a WELCOME 으로 돌아왔다",
                      _root.GameFlow.Current == EBlumgiScreenType.Welcome,
                      _root.GameFlow.Current.ToString());

                Check("7-b 인게임 창이 닫혔다", gameWindow.IsOpen() == false, "");
                Check("7-c 확인 팝업이 «안» 떴다", CountOpenPopups() == 0, "팝업이 떠 있다");

                Check("7-d 진행이 유지된다 (저장 = 방금 레벨)",
                      BlumgiProgress.LoadLevelCode() == progressLevel,
                      $"{BlumgiProgress.LoadLevelCode()} vs {progressLevel}");

                oneCard = FindDeep(welcome.transform, "OnePlayerCard");
                ClickAt(oneCard);
                yield return null;
                yield return null;

                Check("7-e 다시 1 PLAYER 를 누르면 «이어서» 시작한다",
                      game.CurrentLevel != null && game.CurrentLevel.Level.Code == progressLevel,
                      game.CurrentLevel == null ? "레벨 없음" : game.CurrentLevel.Level.Code);
            }
            else
            {
                Check("7-a 🏠 버튼을 찾았다", false, "");
            }

            // ══════════════════════════════ 8. ⠿ 월드 선택 — 의도된 차이 #6 으로 «꺼 둔다»
            //
            // ★ 패스 ③ 에서 이 자리가 플로우 diff 의 «유일한 실패»였다 — 원본은 월드 선택 화면으로 가는데
            //   우리에겐 그 창이 없었기 때문이다. PD 가 **의도된 차이 #6** 으로 닫았다:
            //   이관 범위가 월드1 뿐이라 «갈 곳이 없어» 버튼을 꺼 둔다.
            //   ⚠ 등재된 의도된 차이를 검사기가 계속 실패로 세면 다음 회차가 «없는 결함»을 쫓는다 —
            //     그래서 기대값을 옮긴다. 다만 «왜 꺼졌는지»를 문구에 남겨 「빠뜨린 것」으로 오해되지 않게 한다.
            Section("8. ⠿ 월드 선택 — 의도된 차이 #6 (월드1 만 이관해 «갈 곳이 없다» → 꺼 둔다)");

            gameWindow = Window<BlumgiGameWindow>(BlumgiGameWindow.Key);

            // ⚠ FindDeep 은 «비활성 자식도» 찾는다 — 그래서 「있다」로는 꺼진 것을 못 가린다.
            Transform map = FindDeep(gameWindow.transform, "MapButton");

            Check("8-a ⠿ 버튼이 «꺼져 있다» (의도된 차이 #6 — 프리팹에서 비활성으로 굽는다)",
                  map != null && map.gameObject.activeInHierarchy == false,
                  map == null ? "버튼 자체가 없다" : $"activeInHierarchy={map.gameObject.activeInHierarchy}");

            if (map != null)
            {
                // 꺼진 오브젝트는 레이캐스트에 안 잡힌다 — «눌러도 아무 일이 없다»가 이 차이의 실체다.
                Check("8-b ⠿ 는 눌리지 않는다 (레이캐스트에 안 잡힌다)",
                      RaycastHits(map) == false,
                      "꺼져 있어야 하는데 레이캐스트에 잡힌다");

                ClickAt(map);
                yield return null;

                Check("8-c 그래도 게임이 «갇히지» 않는다 (빈 화면으로 전이하지 않는다)",
                      _root.GameFlow.Current == EBlumgiScreenType.Game && gameWindow.IsOpen(),
                      "빈 화면으로 갔다");
            }
        }

        /// <summary>
        /// ★★ <b>검사가 자기 시작 상태를 만든다</b> — 패스 ②-c 인계.
        ///
        /// <para>
        /// 앞 회차는 저장된 진행도가 남아 있으면 <b>같은 코드가 63/64 로도 66/66 으로도</b> 나왔다.
        /// 사람이 슬롯을 지우고 다시 재는 것으로 넘겼는데, 그건 <b>절차가 아니라 «운»</b>이다 —
        /// 다음 회차가 «없는 결함»을 쫓게 된다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>게임 코드에 문을 뚫지 않았다.</b> <see cref="BlumgiProgress.PrefKey"/> 는 이미 공개된
        /// 저장 키라 <b>검사기 쪽에서 지우기만</b> 한다 (게임이 쓰는 그 경로 그대로다).
        /// </para>
        /// </summary>
        private void ResetProgress()
        {
            string before = BlumgiProgress.LoadLevelCode();

            PlayerPrefs.DeleteKey(BlumgiProgress.PrefKey);
            PlayerPrefs.DeleteKey(BlumgiProgress.KeyWorldLastLevelPrefix + "1");
            PlayerPrefs.Save();

            Line($"  [시작 상태] 저장 진행도를 지웠다 — 직전 {before} → 지금 {BlumgiProgress.LoadLevelCode()}");
            Line("             ⚠ 이 한 줄이 없으면 같은 코드가 회차마다 다른 점수를 낸다 (②-c 인계).");
        }

        // ══════════════════════════════ 채점 조각

        private void ScoreSceneSetup()
        {
            Section("0. 씬 확정값 — 확정 E 개정 · 확정 3 · 3-b · 3-c");

            Camera gameCam = null;
            Camera uiCam = null;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].name == "GameCamera")
                    gameCam = cameras[i];
                else if (cameras[i].name == "UICamera")
                    uiCam = cameras[i];
            }

            Check("0-c 게임 카메라가 직교다", gameCam != null && gameCam.orthographic, "");

            Check("0-d 직교 크기 12.8 (1280 px ÷ 2 ÷ PPU 50)",
                  gameCam != null && Mathf.Abs(gameCam.orthographicSize - 12.8f) < 1e-3f,
                  gameCam == null ? "카메라 없음" : gameCam.orthographicSize.ToString("F4"));

            Vector3 expected = BlumgiUnits.ToPosition(BlumgiBackgroundLayout.VanishingCenterX,
                                                      BlumgiBackgroundLayout.VanishingCenterY);

            Check("0-e 카메라 중심 = 레이아웃 스크롤 (640, 639.5)",
                  gameCam != null &&
                  Mathf.Abs(gameCam.transform.position.x - expected.x) < 1e-3f &&
                  Mathf.Abs(gameCam.transform.position.y - expected.y) < 1e-3f,
                  gameCam == null ? "" : gameCam.transform.position.ToString("F3"));

            Check("0-f 전용 UI 카메라가 있고 «UI 만» 그린다",
                  uiCam != null && uiCam.cullingMask == (1 << 5),
                  uiCam == null ? "UI 카메라 없음" : uiCam.cullingMask.ToString());

            Check("0-g 게임 카메라는 UI 를 «안» 그린다",
                  gameCam != null && (gameCam.cullingMask & (1 << 5)) == 0, "");

            var canvas = FindFirstObjectByType<Canvas>();
            var scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();

            Check("0-h 캔버스가 ScreenSpace-Camera 다 (확정 3-c)",
                  canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera, "");

            Check("0-i 캔버스 참조 해상도 1920×1080 (확정 3)",
                  scaler != null && Mathf.Approximately(scaler.referenceResolution.x, 1920f)
                                 && Mathf.Approximately(scaler.referenceResolution.y, 1080f),
                  scaler == null ? "" : scaler.referenceResolution.ToString());

            Check("0-j Match = Height (확정 3-b — Expand 가 아니다)",
                  scaler != null && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                                 && Mathf.Approximately(scaler.matchWidthOrHeight, 1f),
                  scaler == null ? "" : $"{scaler.screenMatchMode} match={scaler.matchWidthOrHeight}");

            Check("0-k EventSystem 이 있다 (없으면 버튼이 통째로 죽는다)",
                  EventSystem.current != null, "");
        }

        /// <summary>데이터가 «실제로» 읽혔나 — 등록을 빠뜨리면 JSON 이 있어도 조용히 빈다.</summary>
        private void ScoreDataGate()
        {
            Section("0-L. 데이터 게이트 — BlumgiContainerRegister.RegisterAll() 이 실제로 불렸나");

            var levels = GameRoot.Instance.BlumgiLevelDataContainer;
            var blocks = GameRoot.Instance.BlumgiBlockDataContainer;
            var config = GameRoot.Instance.BlumgiConfigDataContainer;

            Check("0-L1 레벨 5행", levels != null && levels.AllValues.Count == 5,
                  levels == null ? "컨테이너 없음" : levels.AllValues.Count.ToString());

            Check("0-L2 블록 462행", blocks != null && blocks.Count == 462,
                  blocks == null ? "컨테이너 없음" : blocks.Count.ToString());

            Check("0-L3 설정 1행", config != null && config.Config != null, "");
        }

        private void ScoreLevelBuild(BlumgiGameManager game)
        {
            Section("2-h. 레벨 구성 — 블록 · 좌캡 · 배경");

            int dataRows = game.CurrentLevel.BlockCenters.Count;

            Check("2-h1 블록 실체 수 = 데이터 행 수",
                  game.PhysicsWorld.BlockCount == dataRows,
                  $"{game.PhysicsWorld.BlockCount} vs {dataRows}");

            var presenter = FindFirstObjectByType<BlumgiLevelPresenter>();

            Check("2-h2 레벨 프레젠터가 붙어 있다", presenter != null, "");

            if (presenter != null && game.CurrentLevel.Level.Code == "W1L1")
            {
                // 5회차 실측 표와 교차 검산 — 기전(겹침)과 파생 규칙이 어긋나면 무언가 틀린 것이다.
                Check("2-h3 W1L1 좌캡 22칸 [5회차 픽셀 검산 34/34]",
                      presenter.VisibleCapCount == 22,
                      presenter.VisibleCapCount.ToString());
            }

            Transform bgRoot = FindDeepInScene("BackgroundRoot");

            Check("2-h4 배경 부모가 원근 1/3 을 들고 있다 [5회차 zElevation −200]",
                  bgRoot != null && Mathf.Abs(bgRoot.localScale.x - 1f / 3f) < 1e-4f,
                  bgRoot == null ? "BackgroundRoot 없음" : bgRoot.localScale.ToString("F4"));

            Transform palms = bgRoot == null ? null : FindDeep(bgRoot, "Palms");

            Check("2-h5 야자수 5그루가 «원점이 아닌» 실측 좌표에 있다",
                  palms != null && palms.childCount == 5 &&
                  palms.GetChild(0).localPosition.sqrMagnitude > 1e-6f,
                  palms == null ? "Palms 없음" : $"{palms.childCount}그루");

            var hoop = FindFirstObjectByType<BlumgiHoopView>();
            Check("2-h6 골대가 세워졌다", hoop != null, "");

            var blob = FindFirstObjectByType<BlumgiBlobView>();
            Check("2-h7 발사대(블롭)가 세워졌다", blob != null, "");
        }

        private void ScoreHud(BlumgiGameWindow window, BlumgiGameManager game)
        {
            Section("2-i. HUD — WORLD 표기 · 레벨 도트");

            Transform worldText = FindDeep(window.transform, "WorldText");
            var tmp = worldText == null ? null : worldText.GetComponent<TextMeshProUGUI>();

            BlumgiHudSnapshot snapshot = game.GetHudSnapshot();

            Check("2-i1 WORLD 표기가 «실제로» 들어갔다 (SetData 실호출)",
                  tmp != null && tmp.text == snapshot.WorldText.ToUpperInvariant(),
                  tmp == null ? "텍스트 없음" : $"'{tmp.text}' vs '{snapshot.WorldText.ToUpperInvariant()}'");

            Check("2-i2 화면 표기가 «전부 대문자»다 (의도된 차이 #5)",
                  tmp != null && tmp.text == tmp.text.ToUpperInvariant(), tmp == null ? "" : tmp.text);

            Transform dots = FindDeep(window.transform, "LevelDots");
            Transform fill = dots == null ? null : FindDeep(dots, "Fill");
            Transform empty = dots == null ? null : FindDeep(dots, "Empty");

            float scale = 1080f / 1280f;
            float cell = BlumgiLevelDotBar.CellWorldWidth * scale;

            // ★ [정정 2026-08-30] 폭을 «캔버스 px 로 환산해서» 잰다 — 바 뿌리에 스케일이 걸려 있기 때문이다.
            //   sizeDelta 만 보면 «어느 자로 쟀는지»가 안 보여, 자가 바뀌어도 검사가 그대로 통과한다.
            float barScale = dots == null ? 1f : dots.localScale.x;
            float emptyPx = empty == null ? 0f : ((RectTransform)empty).sizeDelta.x * barScale;
            float fillPx = fill == null ? 0f : ((RectTransform)fill).sizeDelta.x * barScale;

            Check("2-i3 도트 바가 «5칸»이다 [실측] — 화면 폭 기준",
                  empty != null && Mathf.Abs(emptyPx - cell * 5f) < 0.5f,
                  empty == null ? "" : $"{emptyPx:F2} px (기대 {cell * 5f:F2})");

            Check("2-i4 채움 폭이 현재 레벨까지다 (SetActive 가 아니라 «폭»이다)",
                  fill != null && Mathf.Abs(fillPx - cell * snapshot.LevelStep) < 0.5f,
                  fill == null ? "" : $"{fillPx:F2} px (기대 {cell * snapshot.LevelStep:F2})");

            // ★★ 여기가 ②-d 의 «도트가 5개로 안 보인다» 를 «수치로» 잡는 자리다.
            //
            //   Tiled 는 «바 폭»이 아니라 «타일 폭»으로 칸을 그린다. 두 자가 어긋나면
            //   폭 검사(2-i3·2-i4)는 통과하는데 화면에는 4칸 + 잘린 조각이 나온다 —
            //   실제로 그랬다. 그래서 «그려지는 칸 수»를 따로 잰다.
            ScoreDotTilePitch(empty, cell);
        }

        /// <summary>
        /// 도트 «한 칸이 화면에서 몇 px 인가» 를 스프라이트·PPU 에서 되계산해 칸 폭과 대조한다.
        /// Tiled 타일 폭 = 스프라이트 rect px ÷ 스프라이트 PPU × 캔버스 refPPU. 굽는 strip 은 <b>2칸</b>이다.
        /// </summary>
        private void ScoreDotTilePitch(Transform empty, float cell)
        {
            var image = empty == null ? null : empty.GetComponent<Image>();
            Sprite sprite = image == null ? null : image.sprite;
            Canvas canvas = empty == null ? null : empty.GetComponentInParent<Canvas>();

            if (sprite == null || canvas == null)
            {
                Check("2-i5 도트 «반복»이 살아 있다 (런타임이 Simple 로 덮지 않았다)", false,
                      sprite == null ? "스프라이트가 안 물렸다" : "캔버스 없음");
                Check("2-i6 도트 «타일 피치»가 한 칸 폭과 같다", false, "위와 같음");
                return;
            }

            // ★★ 여기가 «진짜 원인»을 잡는 자리다 — 빌더는 Tiled 로 구웠는데
            //   아트 바인더가 «Sliced 아니면 Simple» 로 덮어써서 반복이 사라졌었다.
            //   프리팹 파일에는 Tiled 가 저장돼 있어 «파일을 봐도» 안 보인다 — 재생 중에 재야 잡힌다.
            Check("2-i5 도트 «반복(Tiled)»이 재생 중에도 살아 있다 (아트 바인더가 Simple 로 안 덮었다)",
                  image.type == Image.Type.Tiled, image.type.ToString());

            const int CellsPerTile = 2;   // BlumgiSpriteBuilder.BuildDotStrip — 58×2 = 116 로 굽는다

            float refPpu = canvas.rootCanvas.referencePixelsPerUnit;
            float tilePx = sprite.rect.width / sprite.pixelsPerUnit * refPpu;
            float pitchPx = tilePx / CellsPerTile * empty.lossyScale.x / canvas.rootCanvas.transform.localScale.x;

            Check("2-i6 도트 «타일 피치»가 한 칸 폭과 같다 (Tiled 는 바 폭이 아니라 타일로 그린다)",
                  Mathf.Abs(pitchPx - cell) < 0.5f,
                  $"피치 {pitchPx:F2} px vs 칸 {cell:F2} px");
        }

        private void ScoreButtons(BlumgiGameWindow window)
        {
            Section("2-j. 우상단 버튼 — 1P 는 «4개» [실측] · ⠿ 는 의도된 차이 #6 으로 꺼져 있다");

            // ★ ⠿(MapButton)은 «자리에는 있고 꺼져 있다» — 원본과 배치가 같아야 나머지 3개의 위치가 맞는다.
            //   그래서 «위치·존재» 검사에는 넣고, «눌린다» 검사에서는 뺀다 (꺼진 것을 누르라고 재면 그게 오답이다).
            string[] names = { "RetryButton", "SoundButton", "MapButton", "HomeButton" };
            const string DisabledByIntendedDiff = "MapButton";
            int found = 0;

            for (int i = 0; i < names.Length; i++)
            {
                Transform button = FindDeep(window.transform, names[i]);

                if (button == null)
                {
                    _failLines.Add($"버튼이 없다: {names[i]}");
                    continue;
                }

                found++;

                if (names[i] == DisabledByIntendedDiff)
                {
                    Check($"2-j{i + 1} {names[i]} 은 «꺼져» 있다 (의도된 차이 #6 — 월드1 만 이관)",
                          button.gameObject.activeInHierarchy == false,
                          $"activeInHierarchy={button.gameObject.activeInHierarchy}");
                    continue;
                }

                Check($"2-j{i + 1} {names[i]} 이 레이캐스트에 «자기 자신»으로 잡힌다",
                      RaycastHits(button), "RaycastAll 첫 결과가 다르다");
            }

            Check("2-j0 버튼 4개가 다 «있다» (⠿ 는 꺼진 채로 자리를 지킨다 — 나머지 3개의 위치 근거다)",
                  found == 4, found.ToString());
        }

        // ══════════════════════════════ 조작

        /// <summary>
        /// 홀드 → 릴리즈. <b>실제 포인터 이벤트</b>로 흘리고, 홀드 길이는
        /// <c>HoldSeconds</c> 가 «차오를 때까지» 기다린다 — 프레임 수로 세면 시간 감각이 무너진다.
        /// </summary>
        private IEnumerator Shoot(BlumgiGameManager game, double holdSeconds)
        {
            yield return WaitUntilReady(game, 12f);

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GameObject target = FirstRaycast(center);

            if (target == null)
            {
                _failLines.Add("홀드: 화면 중앙 레이캐스트가 비었다 (검사 환경 문제일 수 있다)");
                yield break;
            }

            var data = new PointerEventData(EventSystem.current) { position = center, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);

            float guard = 0f;

            while (game.Shot.HoldSeconds < holdSeconds && guard < 10f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }

            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            yield return null;
        }

        /// <summary>
        /// 다음 발사를 «걸 수 있는» 상태가 될 때까지 기다린다.
        ///
        /// <para>
        /// ★★ <b>[정정 · 8회차] <c>Ready</c> «만» 기다리면 영영 안 온다.</b> 원본에는 자동 재장전이 없어서
        /// (미스 후 6.5 초 무입력 반례 [실측]) 빗나간 판은 <c>Missed</c> 로 <b>머문다</b> —
        /// 되돌리는 것은 <b>다음 누름</b>이다 (<c>BlumgiGameManager.OnPointerDown</c>).
        /// 그래서 <c>Missed</c> 도 «누를 수 있는 상태»로 센다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 예전에는 매니저의 「바닥선 + 0.5 s」 타이머가 <c>Missed → Ready</c> 를 만들어 줘서
        /// 이 함수가 돌아갔다. <b>그 타이머가 원본에 없어서 걷어냈다</b> — 검사도 같이 고친다.
        /// </para>
        /// </summary>
        private IEnumerator WaitUntilReady(BlumgiGameManager game, float timeoutSeconds)
        {
            float t = 0f;

            while (t < timeoutSeconds)
            {
                if (game.Shot != null &&
                    (game.Shot.State == EBlumgiShotState.Ready ||
                     game.Shot.State == EBlumgiShotState.Missed))
                    yield break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static void ClickAt(Transform target)
        {
            if (target == null)
                return;

            Vector2 point = ScreenPointOf(target);
            GameObject hit = FirstRaycast(point);

            if (hit == null)
                return;

            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };

            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
        }

        private static bool RaycastHits(Transform target)
        {
            return RaycastHitsScreenPoint(ScreenPointOf(target), target);
        }

        private static bool RaycastHitsScreenPoint(Vector2 point, Transform target)
        {
            GameObject hit = FirstRaycast(point);

            if (hit == null || target == null)
                return false;

            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        private static GameObject FirstRaycast(Vector2 point)
        {
            if (EventSystem.current == null)
                return null;

            var data = new PointerEventData(EventSystem.current) { position = point };
            var results = new List<RaycastResult>();

            EventSystem.current.RaycastAll(data, results);
            return results.Count == 0 ? null : results[0].gameObject;
        }

        private static Vector2 ScreenPointOf(Transform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas == null ? null : canvas.worldCamera;

            return RectTransformUtility.WorldToScreenPoint(camera, target.position);
        }

        private static Sprite IconSprite(Transform button)
        {
            var image = button.GetComponentInChildren<Image>(true);
            return image == null ? null : image.sprite;
        }

        private static int CountOpenPopups()
        {
            BaseWindow[] windows = FindObjectsByType<BaseWindow>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None);
            int count = 0;

            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i].WindowType == EWindowType.Popup && windows[i].IsOpen())
                    count++;
            }

            return count;
        }

        // ══════════════════════════════ 1-f. ★★★ WELCOME 배경 장식 (BG 5개)

        /// <summary>
        /// ★★★ <b>WELCOME 의 <c>BG</c> 장식 5개</b> — 격자(op 0.5) · 야자수 잎 2 · 기둥 2.
        ///
        /// <para>
        /// ⚠ <b>정답지는 «구현이 읽는 표»가 아니라 문서다.</b> 아래 기대값은
        /// <c>04_UIUX규칙.md §2-a</c> 의 <b>「1920×1080 중심 / 정규화 중심」 열</b>을 그대로 옮긴 «리터럴»이다 —
        /// <see cref="BlumgiWelcomeBackgroundLayout"/> 를 읽으면 <b>같은 표를 두 번 읽는 것</b>이라
        /// 빌더가 틀려도 통과한다 (자기 자신을 채점하는 꼴).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「돌았다」가 아니라 «보였다»로 닫는다</b> (재발방지 #60) — 이 검사는 수치만 본다.
        /// 그림은 <c>BlumgiRenderPlayCheck</c> 의 <c>S00_welcome_ui.png</c> 를 원본 캡처와 <b>눈으로</b> 대조한다.
        /// </para>
        /// </summary>
        private void ScoreWelcomeBackground(BlumgiWelcomeWindow welcome)
        {
            Section("1-f. WELCOME 배경 장식 — BG 5개 (04 §2-a 실측표)");

            Transform decorRoot = FindDeep(welcome == null ? null : welcome.transform, "BgDecor");

            Check("1-f1 배경 장식 묶음(BgDecor)이 있다", decorRoot != null,
                  "웰컴이 단색 판 + 그라디언트뿐이다 — BG 장식이 통째로 빠졌다");

            if (decorRoot == null)
                return;

            Check("1-f2 장식이 «5개»다 (인게임 11개와 다르다)", decorRoot.childCount == 5,
                  decorRoot.childCount.ToString());

            // 원본 zIndex 0·1·2·3·4 오름차순 = UI 형제 순서. 잎 → 기둥 짝이 그루마다 반복된다.
            string[] order = { "Grid", "PalmLeafA", "PalmTrunkA", "PalmLeafB", "PalmTrunkB" };
            bool orderOk = decorRoot.childCount == order.Length;
            var actual = new StringBuilder();

            for (int i = 0; i < decorRoot.childCount; i++)
            {
                actual.Append(decorRoot.GetChild(i).name).Append(' ');

                if (i < order.Length && decorRoot.GetChild(i).name != order[i])
                    orderOk = false;
            }

            Check("1-f3 그리는 순서 = 격자 → 잎A → 기둥A → 잎B → 기둥B (원본 zi 0~4)",
                  orderOk, actual.ToString());

            // 장식은 «내용물 아래»에 그려져야 한다 — 형제 순서가 곧 순서다.
            Transform windowRoot = welcome == null ? null : welcome.transform;
            Transform safe = FindDeep(windowRoot, "SafeArea");

            Check("1-f4 장식이 내용물(SafeArea)보다 «아래»에 그려진다",
                  safe != null && decorRoot.parent == safe.parent &&
                  decorRoot.GetSiblingIndex() < safe.GetSiblingIndex(),
                  safe == null ? "SafeArea 를 못 찾았다" : $"decor={decorRoot.GetSiblingIndex()} safe={safe.GetSiblingIndex()}");

            var canvas = welcome == null ? null : welcome.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas == null ? null : canvas.rootCanvas.GetComponent<RectTransform>();

            Check("1-f5 캔버스를 찾았다 (좌표 판정의 자)", canvasRect != null, "");

            if (canvasRect == null)
                return;

            // ── 04 §2-a 「1920×1080 중심」 · 「정규화 중심 × (1920,1080)」 [문서 리터럴]
            //    잎 크기 366 × 197 · 기둥 폭 63 도 같은 표에서 왔다.
            CheckDecorCenter(decorRoot, canvasRect, "1-f6", "PalmLeafA", 1367.2f, 596.9f);
            CheckDecorCenter(decorRoot, canvasRect, "1-f7", "PalmLeafB", 611.5f, 879.6f);
            CheckDecorCenter(decorRoot, canvasRect, "1-f8", "PalmTrunkA", 1361.1f, 2294.1f);
            CheckDecorCenter(decorRoot, canvasRect, "1-f9", "PalmTrunkB", 605.6f, 2576.7f);

            CheckDecorSize(decorRoot, canvasRect, "1-f10", "PalmLeafA", 366.1f, 196.6f, 2f);
            CheckDecorSize(decorRoot, canvasRect, "1-f11", "PalmLeafB", 366.1f, 196.6f, 2f);

            // 기둥 — 폭 0.0329 × 1920 = 63.2 · 높이 3.078 × 1080 = 3324.6 [04 §2-a 정규화 열]
            CheckDecorSize(decorRoot, canvasRect, "1-f12", "PalmTrunkA", 63.2f, 3324.6f, 3f);
            CheckDecorSize(decorRoot, canvasRect, "1-f13", "PalmTrunkB", 63.2f, 3324.6f, 3f);

            // ── 틴트 [실측 (58,177,228) · 4개 전부 같다]
            var palmTint = new Color32(58, 177, 228, 255);
            bool tintOk = true;
            var tintDetail = new StringBuilder();

            for (int i = 1; i < order.Length; i++)
            {
                Image image = DecorImage(decorRoot, order[i]);
                Color32 c = image == null ? new Color32(0, 0, 0, 0) : (Color32)image.color;
                tintDetail.Append(order[i]).Append('=').Append(c.ToString()).Append(' ');

                if (image == null || c.r != palmTint.r || c.g != palmTint.g || c.b != palmTint.b || c.a != 255)
                    tintOk = false;
            }

            Check("1-f14 야자수 4개 틴트가 (58,177,228) 다 — 순백 마스크에 런타임 착색 (#94)",
                  tintOk, tintDetail.ToString());

            Image grid = DecorImage(decorRoot, "Grid");

            Check("1-f15 격자가 «흰색 · 불투명도 0.5» 다 (인게임은 테마색 op 1 — 다르다)",
                  grid != null && Mathf.Approximately(grid.color.r, 1f)
                               && Mathf.Approximately(grid.color.g, 1f)
                               && Mathf.Approximately(grid.color.b, 1f)
                               && Mathf.Abs(grid.color.a - 0.5f) < 1e-3f,
                  grid == null ? "격자를 못 찾았다" : grid.color.ToString("F3"));

            Check("1-f16 격자가 «반복»으로 그려진다 (한 장 늘리기가 아니다)",
                  grid != null && grid.type == Image.Type.Tiled,
                  grid == null ? "" : grid.type.ToString());

            // ── 격자 칸 피치 — 150 world [실측 캡처] × (1080/1280) = 126.5625 캔버스 px @1080
            float pitch = GridCellPitchPixels(grid, canvas);
            float expectedPitch = 150f * (canvasRect.rect.height / 1280f);

            Check("1-f17 격자 한 칸이 150 world 다 (인게임 232.5 가 «아니다»)",
                  pitch > 0f && Mathf.Abs(pitch - expectedPitch) < 1f,
                  $"{pitch:F3} px (기대 {expectedPitch:F3})");

            // ── 격자 윗변 = world −2111 [실측] · 화면 전체를 덮는다
            if (grid != null)
            {
                Rect g = CanvasRectOf(grid.rectTransform, canvasRect);
                float scale = canvasRect.rect.height / 1280f;
                float expectedTop = -2111f * scale;   // 캔버스 «위»에서 잰 거리 (음수 = 화면 위로 나간다)
                float top = canvasRect.rect.yMax - g.yMax;

                Check("1-f18 격자 윗변이 world −2111 자리다",
                      Mathf.Abs(top - expectedTop) < 2f, $"{top:F2} px (기대 {expectedTop:F2})");

                Check("1-f19 격자가 화면을 완전히 덮는다",
                      g.xMin <= canvasRect.rect.xMin && g.xMax >= canvasRect.rect.xMax &&
                      g.yMin <= canvasRect.rect.yMin && g.yMax >= canvasRect.rect.yMax,
                      g.ToString());
            }

            // ── 장식은 «입력을 막지 않는다» — 카드 위를 덮는 큰 판이라 이게 뚫리면 1 PLAYER 가 죽는다.
            bool raycastOff = true;

            for (int i = 0; i < order.Length; i++)
            {
                Image image = DecorImage(decorRoot, order[i]);

                if (image != null && image.raycastTarget)
                    raycastOff = false;
            }

            Check("1-f20 장식이 레이캐스트를 «안» 막는다", raycastOff, "raycastTarget 이 켜진 장식이 있다");
        }

        private static Image DecorImage(Transform decorRoot, string name)
        {
            Transform found = FindDeep(decorRoot, name);
            return found == null ? null : found.GetComponent<Image>();
        }

        /// <summary>루트 캔버스 로컬 좌표계에서 본 사각형 (배율·앵커를 전부 먹은 «진짜» 자리).</summary>
        private static Rect CanvasRectOf(RectTransform target, RectTransform canvasRect)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector3 min = canvasRect.InverseTransformPoint(corners[0]);
            Vector3 max = canvasRect.InverseTransformPoint(corners[2]);

            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                                   Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>
        /// 기대값은 <b>1920×1080 기준</b>으로 적고, 실제 캔버스 높이에 맞춰 늘린다.
        /// ⚠ <b>가로는 «캔버스 중심에서 잰 오프셋»으로 본다</b> — 원본이 세로 고정 · 가로 확장이라
        /// 화면 비가 달라져도 그 오프셋이 불변이기 때문이다.
        /// </summary>
        private void CheckDecorCenter(Transform decorRoot, RectTransform canvasRect,
                                      string id, string name, float x1920, float y1080)
        {
            Transform found = FindDeep(decorRoot, name);

            if (found == null)
            {
                Check($"{id} {name} 중심 (1920×1080 기준 {x1920:F1}, {y1080:F1})", false, "오브젝트가 없다");
                return;
            }

            float k = canvasRect.rect.height / 1080f;
            Rect r = CanvasRectOf((RectTransform)found, canvasRect);

            float offsetX = r.center.x - canvasRect.rect.center.x;
            float fromTop = canvasRect.rect.yMax - r.center.y;

            float expectedOffsetX = (x1920 - 960f) * k;
            float expectedFromTop = y1080 * k;

            bool ok = Mathf.Abs(offsetX - expectedOffsetX) < 2f * k
                   && Mathf.Abs(fromTop - expectedFromTop) < 2f * k;

            Check($"{id} {name} 중심 (1920×1080 기준 {x1920:F1}, {y1080:F1})", ok,
                  $"중심오프셋 {offsetX:F2}/{fromTop:F2} (기대 {expectedOffsetX:F2}/{expectedFromTop:F2})");
        }

        private void CheckDecorSize(Transform decorRoot, RectTransform canvasRect,
                                    string id, string name, float w1920, float h1080, float tolerance)
        {
            Transform found = FindDeep(decorRoot, name);

            if (found == null)
            {
                Check($"{id} {name} 크기 (1920×1080 기준 {w1920:F1} × {h1080:F1})", false, "오브젝트가 없다");
                return;
            }

            float k = canvasRect.rect.height / 1080f;
            Rect r = CanvasRectOf((RectTransform)found, canvasRect);

            bool ok = Mathf.Abs(r.width - w1920 * k) < tolerance * k
                   && Mathf.Abs(r.height - h1080 * k) < tolerance * k;

            Check($"{id} {name} 크기 (1920×1080 기준 {w1920:F1} × {h1080:F1})", ok,
                  $"{r.width:F2} × {r.height:F2} (기대 {w1920 * k:F2} × {h1080 * k:F2})");
        }

        /// <summary>
        /// 격자 한 «칸»의 화면 피치. 타일 한 장(<c>grid_tile</c> 100 px)이 <b>2칸</b>이라 반으로 나눈다.
        /// 타일 크기는 <b>「스프라이트 px ÷ 스프라이트 PPU × 캔버스 refPPU」</b> 로 정해진다.
        /// </summary>
        private static float GridCellPitchPixels(Image grid, Canvas canvas)
        {
            if (grid == null || grid.sprite == null || canvas == null)
                return -1f;

            float tileLocal = grid.sprite.rect.height
                              / (grid.sprite.pixelsPerUnit / canvas.rootCanvas.referencePixelsPerUnit);

            return tileLocal * 0.5f * grid.rectTransform.lossyScale.y
                   / canvas.rootCanvas.GetComponent<RectTransform>().lossyScale.y;
        }

        private static T Window<T>(WindowKey<T> key) where T : BaseWindow
        {
            return WindowManagement.Instance.FindCreated(key) as T;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindDeepInScene(string name)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                    return all[i];
            }

            return null;
        }

        // ══════════════════════════════ 보고

        private void Section(string title)
        {
            Line(string.Empty);
            Line("── " + title);
        }

        private void Check(string title, bool ok, string detail)
        {
            if (ok)
            {
                _pass++;
                Line($"  ✅ {title}");
                return;
            }

            _fail++;
            Line($"  ❌ {title}   {detail}");
            _failLines.Add($"{title}   {detail}");
        }

        private void Line(string text)
        {
            _report.AppendLine(text);
        }

        private void WriteReport()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, _report.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"검사 전문을 못 썼다: {e.Message}");
            }
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            // ★ 다리가 시킨 실행이면 «에디터를 끄지 않는다» — 사람이 쓰던 창이 사라진다.
            if (SessionState.GetBool(BlumgiFlowPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }
}
