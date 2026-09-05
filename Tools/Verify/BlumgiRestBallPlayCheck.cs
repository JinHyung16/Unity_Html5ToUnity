using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 패스 ②-d <b>머리 위 공 · 발사 연출 검사</b>.
    ///
    /// <para>
    /// ★★ <b>편집 모드로는 못 잰다.</b> 이 뷰는 <c>BlumgiGameInitialize.Awake</c> 가 어드레서블에서
    /// 프리팹을 읽어 세우고, <c>BlumgiLevelPresenter</c> 가 <b>샷 상태 «전이»를 따라</b> 켜고 끈다 —
    /// 둘 다 재생 모드에서만 돈다. 그래서 <c>PLAYMODE=1</c> 이다.
    /// </para>
    ///
    /// <para>
    /// ★★★ <b>②-d 가 더한 것 — «자동 복귀가 없다»를 기계로 확인한다.</b>
    /// 8회차가 원본에서 <b>미스 후 6.5 초 무입력에도 공이 안 돌아온다</b>는 반례를 잡았다 [실측].
    /// 이 검사는 그 반례를 <b>축약 재현</b>한다 — 미스 뒤 <b>2 초를 무입력</b>으로 두고
    /// ① 머리공이 계속 안 보이는가 ② 샷 상태가 <c>Missed</c> 로 머무는가를 본다.
    /// <b>「없어야 하는 것이 없다」는 이렇게만 잰다</b> — 있는 것만 세면 영영 안 걸린다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>홀드 크기 곡선은 «원본 실측 8점»과 대조한다</b> (8회차 §2-b) — 우리 공식이 아니라
    /// <b>원본에서 잰 표</b>가 정답지다. 표는 <see cref="HoldCurve"/>.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 검사용 문을 뚫지 않았다.</b> 화면 전이는 <c>GameFlow.ChangeScreen</c>
    /// (1 PLAYER 카드가 부르는 그 함수), 홀드는 <c>BlumgiGameManager.OnPointerDown/Up</c>
    /// (<c>BlumgiManagement</c> 가 부르는 그 함수)을 그대로 지난다.
    /// </para>
    ///
    /// <para>
    /// 실행: <c>CLOSE_EDITOR=1 PLAYMODE=1 GRAPHICS=1
    /// Tools/unity-batch.sh JinHyung.EditorTools.BlumgiRestBallPlayCheck.RunAll</c>
    /// </para>
    /// </summary>
    public static class BlumgiRestBallPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiRestBallPlayCheck.Armed";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

        public const string ScenePath = "Assets/Blumgi-Bounce/Scenes/BlumgiBounce.unity";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);
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

            var go = new GameObject("BlumgiRestBallProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiRestBallProbe>();
#endif
        }
    }

    /// <summary>실제 검사기.</summary>
    public sealed class BlumgiRestBallProbe : MonoBehaviour
    {
        private const string ReportPath = "Temp/BlumgiRestBallReport.txt";

        // ★ 상태마다 «갈리는 시점»에 찍는다 — 두 상태의 그림이 같아 보이는 순간에 찍으면 판정이 안 된다
        //   (패스 ②-c 가 여기서 한 번 실패했다: 발사 위치와 대기 자리가 사실상 같은 레벨이었다).
        private const string ShotPathReady = "Temp/BlumgiShot_1_ready.png";
        private const string ShotPathHold = "Temp/BlumgiShot_2_hold.png";
        private const string ShotPathFlying = "Temp/BlumgiShot_3_flying.png";
        private const string ShotPathMissed = "Temp/BlumgiShot_4_missed_noreload.png";
        private const string ShotPathReloaded = "Temp/BlumgiShot_5_reloaded.png";
        private const string ShotPathCleared = "Temp/BlumgiShot_6_cleared.png";

        /// <summary>
        /// ★ 골 <b>+0.2 s</b> — <c>FXwinLight</c> 2개와 <c>FXteleport</c> 가 <b>동시에 살아 있는 유일한 창</b>이다.
        /// 「돌았다」가 아니라 <b>「보였다」</b>로 닫기 위한 그림이다 (<c>재발방지 #60</c>) —
        /// <c>_6_cleared</c> 는 t ≈ 0.65 s 라 <b>텔레포트도 1번 빛줄기도 이미 죽어 있다</b>.
        /// </summary>
        private const string ShotPathGoalFx = "Temp/BlumgiShot_7_goalfx.png";

        /// <summary>
        /// ★ 골 <b>+0.15 s</b> — 텔레포트가 <b>십자로 벌어진 프레임</b>이고 빛줄기는 «좁아진» 중이다.
        /// 시작 그림만 남기면 <b>「멈춰 있는 것」과 구별이 안 된다</b> (<c>재발방지 #60</c> — 갈리는 시점을 골라라).
        /// </summary>
        private const string ShotPathGoalFxMid = "Temp/BlumgiShot_8_goalfx_mid.png";

        /// <summary>W1L1 «미클리어» 홀드 [골든 실측 — 2000 ms 는 캡 구간이라 전부 미클리어].</summary>
        private const double MissHoldSeconds = 2.0;

        /// <summary>W1L1 «클리어» 홀드 [골든 실측 — 3회차 RUN A 실홀드 817 ms → 3/3 CLEAR].</summary>
        private const double ClearHoldSeconds = 0.817;

        /// <summary>미스 뒤 «무입력»으로 버티는 시간. 원본 반례(6.5 s)의 축약이다.</summary>
        private const float NoInputSeconds = 2f;

        private const float StepTimeoutSeconds = 40f;

        /// <summary>좌표 허용오차(원본 world px). 부동소수 왕복만 흡수한다.</summary>
        private const double PositionTolerance = 0.05;

        /// <summary>크기 허용오차(원본 world px). 8회차 표 자체가 bbox 실측이라 이 폭을 준다.</summary>
        private const double SizeTolerance = 0.6;

        /// <summary>
        /// ★ <b>정답지</b> — 원본 홀드 중 <c>spr_BallVisu</c> 폭 [8회차 §2-b 실측 8점].
        /// <c>(홀드 ms, 폭 world px)</c>. <b>우리 공식이 아니라 원본에서 «잰» 값이다.</b>
        /// </summary>
        private static readonly (double HoldMs, double WidthWorld)[] HoldCurve =
        {
            (90, 95.54), (290, 99.54), (490, 103.19), (690, 106.35),
            (890, 108.96), (1090, 110.97), (1290, 112.28), (1491, 112.80),
        };

        private readonly StringBuilder _report = new StringBuilder(1 << 13);

        private int _pass;
        private int _fail;

        private static readonly string[] ShotPaths =
        {
            ShotPathReady, ShotPathHold, ShotPathFlying,
            ShotPathMissed, ShotPathReloaded, ShotPathCleared, ShotPathGoalFx, ShotPathGoalFxMid,
        };

        /// <summary>
        /// ★★ 골인 FX 채록 한 줄 — <c>BlumgiGoalBurst</c> 의 상태를 <b>매 프레임</b> 담는다.
        ///
        /// <para>
        /// ⚠ <b>「끝나고 나서」 재면 못 잰다.</b> 빛줄기는 497 ms · 텔레포트는 300 ms 에 사라지고,
        /// 사라진 뒤에는 <b>남는 흔적이 없다</b>(원본과 같다) — 그렇다고 게임 코드에 「마지막 사망 시각」
        /// 같은 검증용 문을 뚫으면 안 된다. 그래서 <b>재는 쪽이 시계열을 든다</b>.
        /// </para>
        /// </summary>
        private struct GoalBurstSample
        {
            public float T;
            public int Live;
            public float W0;
            public float H0;
            public float A0;
            public double Bottom0;
            public float W1;
            public float H1;
            public float A1;
            public double Bottom1;
            public bool TeleportLive;
            public int TeleportFrame;
            public float TeleportDisplay;
            public double TeleportWorldY;
        }

        private readonly List<GoalBurstSample> _burst = new List<GoalBurstSample>(256);

        private void Start()
        {
            // ⚠ 지난 회차 그림이 남아 있으면 «이번에 안 찍힌 것»을 못 알아본다 —
            //   그리고 홀드 캡처는 「아직 없으면 찍는다」로 한 장만 남기므로 반드시 지우고 시작한다.
            for (int i = 0; i < ShotPaths.Length; i++)
            {
                try
                {
                    if (File.Exists(ShotPaths[i]))
                        File.Delete(ShotPaths[i]);

                    string ui = UiPathOf(ShotPaths[i]);

                    if (File.Exists(ui))
                        File.Delete(ui);
                }
                catch (Exception e)
                {
                    Log.Warning($"지난 캡처를 못 지웠다: {ShotPaths[i]} — {e.Message}");
                }
            }

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
                    _fail++;
                    break;
                }

                if (moved == false)
                    break;

                yield return body.Current;
            }

            Line(string.Empty);
            Line("========================================");
            Line($"═══ 머리 위 공 · 연출: {_pass}/{_pass + _fail} 통과");

            WriteReport();
            Debug.Log(_report.ToString());

            yield return null;

            Finish(_fail == 0);
        }

        private IEnumerator Body()
        {
            var init = FindFirstObjectByType<BlumgiGameInitialize>();
            Check("0-a 씬에 BlumgiGameInitialize 가 있다", init != null, "씬이 안 구워졌다");

            if (init == null)
                yield break;

            float waited = 0f;

            while (init.IsReady == false && waited < StepTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check("0-b 초기화가 끝났다", init.IsReady, $"{waited:F1}s 안에 안 끝났다");

            if (init.IsReady == false)
                yield break;

            // ── 1. 실물이 세워졌나
            var restBall = FindFirstObjectByType<BlumgiRestBallView>(FindObjectsInactive.Include);
            Check("1-a 머리 위 공(BlumgiRestBallView)이 씬에 있다", restBall != null,
                  "프리팹이 안 읽혔거나 배선이 없다");

            if (restBall == null)
                yield break;

            // ★★ 이것이 이 검사의 핵심이다 — 물리가 붙으면 ①-c 위양성(굴러다니다 골)이 되살아난다.
            Check("1-b 강체가 «없다» (물리와 무관한 그림이다)",
                  restBall.GetComponentInChildren<Rigidbody2D>(true) == null, "Rigidbody2D 가 붙어 있다");
            Check("1-c 콜라이더가 «없다»",
                  restBall.GetComponentInChildren<Collider2D>(true) == null, "Collider2D 가 붙어 있다");

            var renderer = restBall.GetComponentInChildren<SpriteRenderer>(true);
            Check("1-d 스프라이트가 물려 있다", renderer != null && renderer.sprite != null,
                  renderer == null ? "SpriteRenderer 가 없다" : "sprite 가 비었다");

            // ── 2. 인게임 진입 — 1 PLAYER 카드가 부르는 «그» 함수를 지난다
            //    ★★ 그 «전»에 시작 상태를 스스로 만든다 (재발방지 #61 · ②-c 인계).
            //       이 검사의 홀드 두 개(2000 미스 · 817 클리어)는 **W1L1 골든값**이라,
            //       저장된 진행도가 남아 다른 레벨로 들어가면 «없는 결함»이 잡히거나
            //       «있는 결함»이 우연히 통과한다. 실제로 직전 실행이 W1L2 로 들어갔다.
            ResetProgress();

            BlumgiGameRoot root = BlumgiGameRoot.Instance;
            root.GameFlow.ChangeScreen(EBlumgiScreenType.Game);

            // ── 2-f. 줌 펀치 — «첫 프레임부터» 떠야 한다. 8회차 정정: 0.8 에서 시작하는 탄성이다.
            yield return SampleZoomPunch();

            BlumgiGameManager game = root.Game;
            Check("2-a 레벨이 올라갔다", game.CurrentLevel != null, "");

            if (game.CurrentLevel == null)
                yield break;

            BlumgiLevelRuntime level = game.CurrentLevel;
            string code = level.Level.Code;

            Check($"2-a2 진행도를 지운 뒤 «첫 레벨»로 들어간다 (홀드 골든값이 W1L1 것이다) — {code}",
                  code == BlumgiGameManager.FirstLevelCode, code);

            Check($"2-b [{code}] 발사 전 머리 위 공이 «보인다»",
                  restBall.IsShown, "안 보인다");

            // 자리 — 데이터 BallRestX/Y 를 되읽어 대조한다 (「그렸다」가 아니라 «맞다»로 닫는다).
            BlumgiUnits.ToWorld(restBall.transform.position, out double wx, out double wy);

            CheckNear($"2-c [{code}] 자리 x = BallRestX", wx, level.BallRestPosition.X);
            CheckNear($"2-c [{code}] 자리 y = BallRestY", wy, level.BallRestPosition.Y);

            // ★ 물리 공은 «경기장 밖»에 그대로 있어야 한다 — 이 패스가 ①-d 를 되돌리지 않았다는 증거다.
            BlumgiVec2 ball = game.Shot.BallPosition;
            Check($"2-d 물리 공은 여전히 경기장 밖이다 (y {ball.Y:F0} > 1279.5)",
                  ball.Y > level.Config.ReloadTriggerY, $"y={ball.Y:F1}");

            Check($"2-e 대기 크기가 «94 × 93» 이다 (배율 1) — {restBall.SizeScale.x:F3}",
                  Mathf.Abs(restBall.SizeScale.x - 1f) < 0.01f, restBall.SizeScale.ToString());

            // 펀치가 잦아든 뒤에 찍는다 — 대기 그림을 흔들리지 않는 상태로 남긴다.
            yield return WaitSeconds(2.2f);
            yield return CaptureAndWait(ShotPathReady);

            var blob = FindFirstObjectByType<BlumgiBlobView>(FindObjectsInactive.Include);
            Check("2-g 블롭 뷰가 있다", blob != null, "");

            var hoop = FindFirstObjectByType<BlumgiHoopView>(FindObjectsInactive.Include);
            Check("2-h 골 화살표가 «보인다» (골 전)", hoop != null && hoop.IsArrowVisible, "");

            // ══════════════════════════════ 3. 미스 샷 — 홀드 곡선 · 발사 · 자동 복귀 없음
            game.OnPointerDown();
            yield return null;

            Check("3-a 누른 «그 프레임»에 머리 위 공이 보인다", restBall.IsShown, "안 보인다");
            Check($"3-b 누른 순간 크기가 94 × 93 으로 리셋됐다 — 배율 {restBall.SizeScale.x:F3}",
                  Mathf.Abs(restBall.SizeScale.x - 1f) < 0.02f, restBall.SizeScale.ToString());

            var samples = new List<(double HoldMs, double WidthWorld)>(512);
            double topEdgeMin = double.MaxValue;
            double topEdgeMax = double.MinValue;
            bool sawAngryFace = false;
            double angryAtMs = -1.0;

            waited = 0f;

            while (game.Shot.HoldSeconds < MissHoldSeconds && waited < StepTimeoutSeconds)
            {
                double holdMs = game.Shot.HoldSeconds * 1000.0;
                double width = restBall.SizeScale.x * BlumgiRestBallView.RestWidthWorld;

                samples.Add((holdMs, width));

                // 위 모서리 = 중심 − 높이/2 (원본은 아래가 +y 라 «작은 y» 가 위다).
                BlumgiUnits.ToWorld(restBall.transform.position, out double _, out double cy);
                double height = restBall.SizeScale.y * BlumgiRestBallView.RestHeightWorld;

                // ⚠ 중심은 transform 이 아니라 «그려지는 것»의 중심이다 — 렌더러 자식을 읽는다.
                if (renderer != null)
                {
                    BlumgiUnits.ToWorld(renderer.transform.position, out double _, out double ry);
                    cy = ry;
                }

                double top = cy - height * 0.5;

                if (holdMs > 30.0)
                {
                    topEdgeMin = Math.Min(topEdgeMin, top);
                    topEdgeMax = Math.Max(topEdgeMax, top);
                }

                if (sawAngryFace == false && blob != null && blob.FaceFrame == 1)
                {
                    sawAngryFace = true;
                    angryAtMs = holdMs;
                }

                if (holdMs >= 740.0 && holdMs <= 780.0 && File.Exists(ShotPathHold) == false)
                    yield return CaptureAndWait(ShotPathHold);

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check($"3-c 홀드가 실제로 {MissHoldSeconds * 1000:F0} ms 까지 차올랐다",
                  game.Shot.HoldSeconds >= MissHoldSeconds, $"{game.Shot.HoldSeconds * 1000:F0} ms");

            ScoreHoldCurve(samples);

            Check($"3-d 홀드 내내 «위 모서리»가 붙잡혀 있다 (편차 {topEdgeMax - topEdgeMin:F2} px ≤ 2)",
                  topEdgeMax - topEdgeMin <= 2.0,
                  $"{topEdgeMin:F2} ~ {topEdgeMax:F2} — 스케일 피벗이 위쪽 가운데가 아니다");

            Check($"3-e 표정이 «force > 35 ⇒ ≈455 ms» 에 찡그림으로 바뀐다 (실측 {angryAtMs:F0} ms)",
                  sawAngryFace && Math.Abs(angryAtMs - 455.0) <= 60.0,
                  sawAngryFace ? $"{angryAtMs:F0} ms" : "안 바뀌었다");

            if (blob != null)
            {
                // 트윈(1.5 s)이 끝난 뒤에는 «한 단계 더» 눌린 값으로 유지된다 [실측 118.04 × 32.27].
                double bw = blob.BodyScale.x * 87.14;
                double bh = blob.BodyScale.y * 50.0;

                CheckNearSize("3-f 블롭 트윈 종료 뒤 가로", bw, 118.04);
                CheckNearSize("3-f 블롭 트윈 종료 뒤 세로", bh, 32.27);
            }

            game.OnPointerUp();
            yield return null;

            Check("3-g 발사됐다 (Flying)", game.Shot.State == EBlumgiShotState.Flying,
                  game.Shot.State.ToString());
            Check("3-h 발사와 «동시에» 머리 위 공이 숨는다", restBall.IsShown == false, "아직 보인다");
            // ★★ 17회차가 프레임 2 의 «그림»을 소스 텍스처(50×47)로 닫았다 —
            //    예전에는 번호만 2 로 바뀌고 그림은 평상 그대로였다. 그림까지 바뀌는지 «따로» 본다.
            Check($"3-i2 ★ 발사 순간 눈 «그림»이 발사 표정으로 바뀐다 — '{(blob == null ? string.Empty : blob.EyesSpriteName)}'",
                  blob != null && blob.EyesSpriteName == "blob_eyes_shout",
                  blob == null ? "블롭 없음" : $"'{blob.EyesSpriteName}' (기대 blob_eyes_shout)");

            Check("3-i 발사 순간 표정이 프레임 2 다", blob != null && blob.FaceFrame == 2,
                  blob == null ? "블롭 없음" : blob.FaceFrame.ToString());

            // ⚠ 발사 «직후»에 찍으면 물리 공이 아직 발사대에 겹쳐 있어 «머리 위 공이 남은 것»처럼 보인다.
            //    그림으로 갈리게 하려면 공이 «떠난 뒤»에 찍어야 한다.
            float flew = 0f;

            while (flew < 0.45f && game.Shot.State == EBlumgiShotState.Flying)
            {
                flew += Time.unscaledDeltaTime;
                yield return null;
            }

            Check("3-j 공이 발사대를 떠난 뒤에도 머리 위가 «비어 있다»", restBall.IsShown == false, "다시 보인다");

            yield return CaptureAndWait(ShotPathFlying);

            // ── 트레일 — 원본은 «고스트 31장»이다. 실제로 켜지는지 «세어» 본다.
            var trail = FindFirstObjectByType<BlumgiBallTrailView>(FindObjectsInactive.Include);
            int liveGhosts = CountActiveChildren(trail == null ? null : trail.transform);

            Check($"3-k 비행 중 트레일 고스트가 켜져 있다 ({liveGhosts}/{BlumgiBallTrailView.OriginGhostCount})",
                  liveGhosts > 0, trail == null ? "트레일 뷰가 없다" : "한 장도 안 켜졌다");

            // ══════════════════════════════ 4. ★ 자동 복귀가 «없다»
            waited = 0f;

            while (game.Shot.State != EBlumgiShotState.Missed && waited < StepTimeoutSeconds)
            {
                if (game.Shot.State == EBlumgiShotState.Scored)
                    break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check($"4-a 홀드 {MissHoldSeconds * 1000:F0} ms 는 «미스»다 (골든: 캡 구간 전부 미클리어)",
                  game.Shot.State == EBlumgiShotState.Missed, game.Shot.State.ToString());

            if (game.Shot.State == EBlumgiShotState.Missed)
            {
                // ★★ 무입력으로 버틴다 — 원본 반례(6.5 s)의 축약.
                float idle = 0f;
                bool cameBack = false;

                while (idle < NoInputSeconds)
                {
                    if (restBall.IsShown)
                        cameBack = true;

                    idle += Time.unscaledDeltaTime;
                    yield return null;
                }

                Check($"4-b ★ 무입력 {NoInputSeconds:F1} s 동안 머리 위 공이 «안 돌아온다» (자동 복귀 없음)",
                      cameBack == false, "다시 나타났다 — 자동 복귀 타이머가 살아 있다");

                Check("4-c ★ 무입력 동안 샷 상태가 Missed 로 «머문다»",
                      game.Shot.State == EBlumgiShotState.Missed, game.Shot.State.ToString());

                yield return CaptureAndWait(ShotPathMissed);
            }

            // ── 다음 «누름» — 그 프레임에 되살아난다
            game.OnPointerDown();
            yield return null;

            Check("4-d ★ 다음 누름 «그 프레임»에 머리 위 공이 되살아난다", restBall.IsShown, "안 보인다");
            Check($"4-e 되살아난 크기가 94 × 93 이다 — 배율 {restBall.SizeScale.x:F3}",
                  Mathf.Abs(restBall.SizeScale.x - 1f) < 0.02f, restBall.SizeScale.ToString());

            int leftoverGhosts = CountActiveChildren(trail == null ? null : trail.transform);

            Check($"4-f 재장전 뒤 꼬리가 «사라졌다» (남은 고스트 {leftoverGhosts})",
                  leftoverGhosts == 0, "잔상이 화면에 박혀 있다");

            BlumgiUnits.ToWorld(restBall.transform.position, out double rx, out double ry2);
            CheckNear("4-g 재장전 뒤에도 자리가 BallRestX", rx, game.CurrentLevel.BallRestPosition.X);
            CheckNear("4-g 재장전 뒤에도 자리가 BallRestY", ry2, game.CurrentLevel.BallRestPosition.Y);

            yield return CaptureAndWait(ShotPathReloaded);

            // ══════════════════════════════ 5. 클리어 샷 — 골 연출
            waited = 0f;

            while (game.Shot.HoldSeconds < ClearHoldSeconds && waited < StepTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            game.OnPointerUp();
            yield return null;

            waited = 0f;

            var director = FindFirstObjectByType<BlumgiCameraDirector>(FindObjectsInactive.Include);

            while (game.Shot.State != EBlumgiShotState.Scored && waited < StepTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check($"5-a 홀드 {ClearHoldSeconds * 1000:F0} ms 가 «클리어»다 (골든 3/3)",
                  game.Shot.State == EBlumgiShotState.Scored, game.Shot.State.ToString());

            if (game.Shot.State != EBlumgiShotState.Scored)
                yield break;

            // ══════════════════════════════ ★ 6. 골 «그 순간»의 연출 — 17회차 rAF 실측 반영분
            //    프레임 순서를 못 박기 위해 골 직후 몇 프레임을 «훑어» 잰다
            //    (프레임 위상에 값이 섞이는 것을 피한다 — 재발방지 #89 의 정신).
            bool shakeSeen = false;
            bool shakeBeforeZoom = false;
            float goalDrop = 0f;
            int hoopGoalFrameSeen = -1;
            var hoopSizes = new List<Vector2>(8);

            // ★★ 패스 ②-h — 골인 FX(FXwinLight ×2 · FXteleport)를 «이 프레임부터» 채록한다.
            var goalBurst = FindFirstObjectByType<BlumgiGoalBurst>(FindObjectsInactive.Include);
            _burst.Clear();
            SampleGoalBurst(goalBurst);

            for (int i = 0; i < 12; i++)
            {
                SampleGoalBurst(goalBurst);

                if (director != null)
                {
                    if (director.IsShaking)
                        shakeSeen = true;

                    if (director.IsZoomPunching == false && director.IsShaking)
                        shakeBeforeZoom = true;

                    goalDrop = Mathf.Max(goalDrop, -director.ZoomDeltaThisFrame);
                }

                if (hoop != null && hoop.RimGoalFrame >= 0)
                {
                    hoopGoalFrameSeen = Mathf.Max(hoopGoalFrameSeen, hoop.RimGoalFrame);

                    if (hoopSizes.Contains(hoop.RimSizeWorld) == false)
                        hoopSizes.Add(hoop.RimSizeWorld);
                }

                yield return null;
            }

            Check("5-b 골 프레임에 화살표가 «사라진다»",
                  hoop != null && hoop.IsArrowVisible == false,
                  hoop == null ? "골대 없음" : "아직 보인다");

            Check($"6-a ★ 골에 «셰이크»가 돈다 (실측 398.6 ms · Shake(20, 0.4))", shakeSeen,
                  "골인에 카메라가 안 흔들린다");

            Check("6-b ★ 셰이크가 «줌보다 먼저» 시작한다 (실측 정확히 한 프레임 앞선다)",
                  shakeBeforeZoom, "줌과 같은 프레임이거나 뒤에 시작한다");

            Check($"6-c ★ 골 줌이 «한 프레임 만에» 점프한다 (최대 낙차 {goalDrop:F3} · 실측 1.00 → 0.81)",
                  goalDrop >= 0.15f, $"낙차 {goalDrop:F3} — 골에 줌 펀치가 안 돌거나 트윈으로 내려간다");

            Check($"6-d ★ 골대가 «골인 전용» 애니(Animation 3)로 갈아탄다 — 관측 프레임 {hoopGoalFrameSeen}",
                  hoopGoalFrameSeen >= 0, "골대가 그대로다 (17회차 실측: 4프레임 스쿼시)");

            // ★ 「둘레 보존」 — Top 은 w + h 가 «4프레임 전부 267» 이다 [실측].
            //   ⚠ 재생 중 표본으로 재면 애니가 133 ms 라 «프레임을 놓친다» (실제로 1/1 만 잡혔다).
            //     프레임 «표»를 직접 채점한다 — 위상에 안 걸린다.
            Vector2[] rimFrames = hoop == null ? new Vector2[0] : hoop.RimGoalFrameSizes;
            int perimeterOk = 0;

            for (int i = 0; i < rimFrames.Length; i++)
            {
                if (Mathf.Abs(rimFrames[i].x + rimFrames[i].y - 267f) <= 0.5f)
                    perimeterOk++;
            }

            Check($"6-e ★ 골대 스쿼시가 «둘레 보존»이다 (w + h = 267) — {perimeterOk}/{rimFrames.Length} 프레임",
                  rimFrames.Length == BlumgiHoopGoalAnimation.FrameCount
                  && perimeterOk == rimFrames.Length,
                  string.Join(" · ", Array.ConvertAll(rimFrames, v => $"{v.x:F0}×{v.y:F0}")));

            Check($"6-e2 ★ 골인 애니가 «4프레임»이고 마지막이 기본 크기다 (212×55)",
                  rimFrames.Length == 4
                  && Mathf.Abs(rimFrames[3].x - 212f) < 0.5f && Mathf.Abs(rimFrames[3].y - 55f) < 0.5f,
                  rimFrames.Length == 0 ? "표가 비어 있다" : $"{rimFrames[rimFrames.Length - 1]}");

            // 재생 중에 실제로 크기가 «변했다»는 것은 6-d 가 든다. 여기서는 표를 본다.
            if (hoopSizes.Count > 0)
                Line($"    (재생 중 잡힌 프레임 {hoopSizes.Count}개: " +
                     string.Join(" · ", hoopSizes.ConvertAll(v => $"{v.x:F0}×{v.y:F0}")) + ")");


            // ★★ 「보였다」로 닫는 그림 두 장 (재발방지 #60 — 시작 · 진행 중).
            //
            // ⚠⚠ <b>캡처 «전»에도 채록한다.</b> 캡처는 한 프레임이 통째로 길어서 그 프레임 동안
            //    텔레포트 애니(6f @ 20 fps = 50 ms/프레임)가 «한 칸 이상» 넘어간다 —
            //    캡처 뒤에만 채록하면 그 칸의 표본이 통째로 빈다. 실제로 ②-j 에서
            //    8-k·8-l 이 «실행마다» 5/6 ↔ 6/6 으로 갈렸다. 허용치를 낮추는 대신
            //    <b>표본이 비는 자리를 없앤다</b> (재발방지 #48).
            SampleGoalBurst(goalBurst);
            yield return CaptureAndWait(ShotPathGoalFx);
            SampleGoalBurst(goalBurst);

            yield return WaitBurstUntil(goalBurst, 0.15f);
            SampleGoalBurst(goalBurst);
            yield return CaptureAndWait(ShotPathGoalFxMid);
            SampleGoalBurst(goalBurst);

            // 컨페티는 골 +200 ms 다 [실측 · 시트 Wait 0.2]. 넉넉히 지나고 «100개인지» 센다.
            yield return WaitSecondsSampling(0.45f, goalBurst);

            var confetti = FindFirstObjectByType<BlumgiConfettiBurst>(FindObjectsInactive.Include);

            Check($"5-c 컨페티가 «100개» 떴다 (실측 FXconfettis 100) — {(confetti == null ? 0 : confetti.LivePieceCount)}개",
                  confetti != null && confetti.LivePieceCount == BlumgiConfettiBurst.OriginPieceCount,
                  confetti == null ? "컨페티 없음" : confetti.LivePieceCount.ToString());

            // ── YES! — 화면(UI)에 «실제로» 떠 있나. 그림만 보면 「안 찍힌 것」과 구별이 안 된다.
            var rainbow = FindFirstObjectByType<BlumgiRainbowText>(FindObjectsInactive.Include);

            Check("5-c2 `YES!` 가 화면에 «떠 있다»",
                  rainbow != null && rainbow.gameObject.activeInHierarchy, "안 떠 있다");

            yield return CaptureAndWait(ShotPathCleared);

            // ★ 색상환은 «1 초에 한 바퀴»다 [8회차 실측] — 0.30 s 를 재서 hue 가 그만큼 도는지 본다.
            if (rainbow != null && rainbow.gameObject.activeInHierarchy)
            {
                float hueBefore = rainbow.Hue01;
                yield return WaitSecondsSampling(0.3f, goalBurst);
                float advanced = Mathf.Repeat(rainbow.Hue01 - hueBefore, 1f);

                // ★ [정정 · 17회차] 주기는 «1.0077 s» 다 (파라미터 0 → 1 되감김 2회의 간격 실측).
                const float CycleSeconds = 1.0077f;
                float expected = 0.30f / CycleSeconds;

                Check($"5-c3 `YES!` 색상환이 «{CycleSeconds:F4} s 에 한 바퀴» 돈다 " +
                      $"(0.30 s 에 {advanced:F3} 바퀴 · 기대 {expected:F3})",
                      Mathf.Abs(advanced - expected) <= 0.05f, $"{advanced:F3}");

                // ★★ 17회차가 닫은 「채움인가 외곽선인가」 — «외곽선»이다.
                Color fillBefore = rainbow.FillColor;
                Color outlineBefore = rainbow.OutlineColor;
                yield return WaitSecondsSampling(0.25f, goalBurst);

                float fillDelta = Mathf.Abs(rainbow.FillColor.r - fillBefore.r)
                                  + Mathf.Abs(rainbow.FillColor.g - fillBefore.g)
                                  + Mathf.Abs(rainbow.FillColor.b - fillBefore.b);

                float outlineDelta = Mathf.Abs(rainbow.OutlineColor.r - outlineBefore.r)
                                     + Mathf.Abs(rainbow.OutlineColor.g - outlineBefore.g)
                                     + Mathf.Abs(rainbow.OutlineColor.b - outlineBefore.b);

                Check($"6-g ★ `YES!` 채움이 «안 돈다» (흰색 고정 · 변화량 {fillDelta:F3})",
                      fillDelta <= 0.01f && rainbow.FillColor.r > 0.99f
                      && rainbow.FillColor.g > 0.99f && rainbow.FillColor.b > 0.99f,
                      $"채움이 돈다 — 17회차 실측은 «흰 채우기»다 ({rainbow.FillColor})");

                Check($"6-h ★ `YES!` 는 «외곽선»이 돈다 (0.25 s 변화량 {outlineDelta:F3})",
                      outlineDelta >= 0.10f, "외곽선 색이 안 변한다");
            }

            // ★ 「소멸이 없다」 — 1.2 s 를 더 둬도 조각 수가 안 준다 (지우는 것은 전환뿐이다).
            int before = confetti == null ? 0 : confetti.LivePieceCount;
            yield return WaitSeconds(0.9f);
            int after = confetti == null ? 0 : confetti.LivePieceCount;

            Check($"5-d 컨페티가 «스스로 사라지지 않는다» ({before} → {after})",
                  before > 0 && after == before, "수명 타이머가 살아 있다");

            // ══════════════════════════════ ★★ 8. 패스 ②-h — 새로 만든 골인 FX
            CheckGoalBurst(level, goalBurst);

            // ══════════════════════════════ ★ 7. 17회차가 닫은 «리소스·배경·저장» 반영분
            yield return CheckResourcesAndStorage(level);
        }

        /// <summary>
        /// ★★★ <b>패스 ②-h — <c>FXwinLight</c> ×2 · <c>FXteleport</c></b>.
        ///
        /// <para>
        /// 이 둘은 <b>«우리에게 없던» 오브젝트</b>다 — 23회차가 훅으로 실측해 놓았는데
        /// 이관본에는 만들어져 있지 않았다. 그래서 여기 항목은 전부 <b>«새로 만든 것이 실제로 도는가»</b>다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>골 이후 시각은 «전환 2 216.9 ms» 안에서만 잴 수 있다</b> — 채록 창이 그래서 ≈1.2 s 다.
        /// </para>
        /// </summary>
        private void CheckGoalBurst(BlumgiLevelRuntime level, BlumgiGoalBurst burst)
        {
            if (burst == null || _burst.Count == 0)
            {
                Check("8-a 골인 FX(BlumgiGoalBurst)가 씬에 있다", false,
                      burst == null ? "오브젝트가 없다 — 프리팹을 굽고 배선했는가" : "채록이 비었다");
                return;
            }

            Check("8-a 골인 FX(BlumgiGoalBurst)가 씬에 있다", true, string.Empty);

            // 표본 간격 — 시각 판정의 «자연 허용 오차»다. 프레임보다 촘촘히 요구할 수 없다.
            float maxGap = 0f;

            for (int i = 1; i < _burst.Count; i++)
                maxGap = Mathf.Max(maxGap, _burst[i].T - _burst[i - 1].T);

            // ⚠ <b>프레임보다 촘촘히 요구할 수 없다</b> — 사건은 «표본 간격 안»에서만 관측된다.
            //   그렇다고 여유를 곱해 늘리지 않는다(그러면 «두 배 늦어도» 통과한다) — 딱 한 프레임이다.
            float tolerance = Mathf.Max(0.05f, maxGap);
            Line($"    (골인 FX 채록 {_burst.Count}표본 · 최대 간격 {maxGap * 1000f:F1} ms " +
                 $"· 허용 ±{tolerance * 1000f:F0} ms · 창 {_burst[_burst.Count - 1].T:F3} s)");

            GoalBurstSample first = _burst[0];

            // ── 8-b 골 «그 프레임»에 빛줄기 1개 + 텔레포트가 «같이» 뜬다 [실측 둘 다 0 ms].
            Check($"8-b ★ 골 «그 프레임»에 빛줄기 1개 · 텔레포트가 같이 뜬다 " +
                  $"(live {first.Live} · 텔레포트 {(first.TeleportLive ? "산다" : "없다")} · f{first.TeleportFrame})",
                  first.Live == 1 && first.TeleportLive && first.TeleportFrame == 0 && first.T <= tolerance,
                  $"t {first.T * 1000f:F1} ms · live {first.Live} · tele {first.TeleportLive}");

            // ── 8-c 시작 크기 50 × 1550 [실측].
            //    ⚠ 「첫 표본이 정확히 50」을 요구하면 «프레임 위상»에 걸린다 — 트윈이 이미 한 프레임 돌았을 수 있다.
            //      그래서 «관측 최대»로 본다: 시작이 50/1550 이고 «줄기만 한다»는 것이 이 항목의 주장이다.
            float widthMax = 0f;
            float heightMax = 0f;
            float widthMin = float.MaxValue;
            float heightMin = float.MaxValue;
            bool widthMonotone = true;
            float previousWidth = float.MaxValue;

            for (int i = 0; i < _burst.Count; i++)
            {
                GoalBurstSample s = _burst[i];

                if (s.Live < 1 || s.W0 <= 0f)
                    continue;

                widthMax = Mathf.Max(widthMax, s.W0);
                heightMax = Mathf.Max(heightMax, s.H0);
                widthMin = Mathf.Min(widthMin, s.W0);
                heightMin = Mathf.Min(heightMin, s.H0);

                if (s.W0 > previousWidth + 0.01f)
                    widthMonotone = false;

                previousWidth = s.W0;
            }

            Check($"8-c ★ 빛줄기 «시작» 크기가 50 × 1550 이다 (관측 최대 {widthMax:F2} × {heightMax:F1})",
                  widthMax <= 50.05f && widthMax >= 48.5f
                  && heightMax <= 1550.2f && heightMax >= 1535f,
                  $"{widthMax:F2} × {heightMax:F1}");

            // ── 8-d 아래 모서리가 «골대 y» 에 못 박혀 있다 [실측 61행 전부 977.00].
            double bottomMin = double.MaxValue;
            double bottomMax = double.MinValue;
            float alphaMin = float.MaxValue;
            float alphaMax = float.MinValue;

            for (int i = 0; i < _burst.Count; i++)
            {
                GoalBurstSample s = _burst[i];

                if (s.Live >= 1 && double.IsNaN(s.Bottom0) == false)
                {
                    bottomMin = Math.Min(bottomMin, s.Bottom0);
                    bottomMax = Math.Max(bottomMax, s.Bottom0);
                    alphaMin = Mathf.Min(alphaMin, s.A0);
                    alphaMax = Mathf.Max(alphaMax, s.A0);
                }

                if (s.Live >= 2 && double.IsNaN(s.Bottom1) == false)
                {
                    bottomMin = Math.Min(bottomMin, s.Bottom1);
                    bottomMax = Math.Max(bottomMax, s.Bottom1);
                    alphaMin = Mathf.Min(alphaMin, s.A1);
                    alphaMax = Mathf.Max(alphaMax, s.A1);
                }
            }

            double rimY = level == null ? double.NaN : level.Level.GoalRimY;

            Check($"8-d ★ 빛줄기 «아래 모서리»가 골대 y({rimY:F0})에 못 박혀 있다 " +
                  $"— {bottomMin:F2} ~ {bottomMax:F2}",
                  bottomMax - bottomMin <= 0.5 && Math.Abs(bottomMin - rimY) <= 0.5,
                  $"{bottomMin:F2} ~ {bottomMax:F2} (골대 {rimY:F2})");

            // ── 8-e 알파 «0.300 고정» [실측 61행 변동 0]. 사라지는 것은 «크기»다.
            Check($"8-e ★ 빛줄기 알파가 «0.300 고정»이다 (관측 {alphaMin:F4} ~ {alphaMax:F4})",
                  alphaMax - alphaMin <= 0.002f && Mathf.Abs(alphaMin - 0.300f) <= 0.002f,
                  $"{alphaMin:F4} ~ {alphaMax:F4}");

            // ── 8-f 2번째 빛줄기가 «+200 ms» 에 뜬다 [실측 5레벨 199.8 ~ 208.7].
            float secondAt = FirstTimeWhen(s => s.Live >= 2);

            Check($"8-f ★ 2번째 빛줄기가 «골 +204 ms» 에 뜬다 (컨페티와 같은 이벤트 행) — {secondAt * 1000f:F1} ms",
                  secondAt >= 0f && Mathf.Abs(secondAt - 0.2042f) <= tolerance,
                  secondAt < 0f ? "2개가 «안 됐다»" : $"{secondAt * 1000f:F1} ms");

            // ── 8-g 트윈 «끝» — 폭 0 · 높이 1000 [실측 마지막 표본 0.066 × 1000.73].
            //    ★★ 이 항목이 「Tween size 0 → 1000」(방향 반대) 오독을 막는 자리다 —
            //       폭이 «커지면» widthMonotone 이 깨지고, 높이가 1000 «아래»로 가면 하한에 걸린다.
            Check($"8-g ★ 빛줄기가 «닫힌다» — 폭 50 → 0 · 높이 1550 → 1000 " +
                  $"(관측 최소 {widthMin:F2} × {heightMin:F1} · 폭 단조감소 {widthMonotone})",
                  widthMonotone
                  && widthMin <= 8f
                  && heightMin >= 999.9f && heightMin <= 1080f,
                  $"최소 {widthMin:F2} × {heightMin:F1} · 단조 {widthMonotone}");

            // ── 8-h · 8-i 소멸 시각 [실측 1번 499.3~509.1 ms · 2번 691.7~700.5 ms].
            float firstDeath = FirstTimeWhen(s => s.Live == 0 && s.T > 0.1f);
            float twoToOne = FirstTimeWhen(s => s.Live == 1 && s.T > 0.3f);

            Check($"8-h ★ 1번 빛줄기가 «골 +497 ms» 에 사라진다 — {twoToOne * 1000f:F1} ms",
                  twoToOne >= 0f && Mathf.Abs(twoToOne - 0.4974f) <= tolerance,
                  twoToOne < 0f ? "2 → 1 로 안 줄었다" : $"{twoToOne * 1000f:F1} ms");

            Check($"8-i ★ 2번 빛줄기가 «골 +702 ms» 에 사라진다 (200 ms 늦게 떴다) — {firstDeath * 1000f:F1} ms",
                  firstDeath >= 0f && Mathf.Abs(firstDeath - (0.2042f + 0.4974f)) <= tolerance,
                  firstDeath < 0f ? "빛줄기가 «안 사라진다»" : $"{firstDeath * 1000f:F1} ms");

            // ── 8-j 텔레포트 소멸 [실측 307.4 ~ 309.3 ms = 6프레임 / 20 fps].
            float teleportDeath = FirstTimeWhen(s => s.TeleportLive == false && s.T > 0.05f);

            Check($"8-j ★ 텔레포트가 «골 +300 ms» 에 사라진다 (6f @ 20 fps) — {teleportDeath * 1000f:F1} ms",
                  teleportDeath >= 0f && Mathf.Abs(teleportDeath - 0.300f) <= tolerance,
                  teleportDeath < 0f ? "텔레포트가 «안 사라진다»" : $"{teleportDeath * 1000f:F1} ms");

            // ══════════════════════════════════════════════════════════════════
            // 8-k · 8-k2 · 8-l — 텔레포트 6프레임
            //
            // ★★ <b>「표본을 놓쳐도 잡히게」 만든 자리다</b> (재발방지 #48 · #95).
            //
            //   ②-j 에서 이 세 줄이 «실행마다» 갈렸다(1차 5/6 → 재실행 6/6). 원인은 구현이 아니라
            //   <b>재생 프레임 지터</b>다 — 애니는 50 ms/칸인데 캡처 프레임 하나가 그보다 길어
            //   그 칸의 표본이 통째로 빈다. <b>허용치를 낮추면 진짜 결함까지 통과</b>하므로 안 낮췄다.
            //   대신 둘을 넣었다:
            //     ① 캡처 «전»에도 채록해 빈 자리를 없앤다 (위 골 캡처 구간)
            //     ② 그래도 한 칸이 비면 <b>«시간 사상»으로 메운다</b> — 프레임은 «시각의 함수»
            //        (frame = ⌊t × 20⌋) 라서, 그 칸의 «앞뒤» 표본이 둘 다 사상을 지키면
            //        그 사이에 그 칸을 지났다는 것이 «수»로 닫힌다.
            //   ⚠ 메우기는 <b>사상 위반이 0건일 때만</b> 허용한다 — 사상이 깨지면(8-k2 실패)
            //     그건 지터가 아니라 «구현이 시간축을 안 따른다»는 뜻이고 메워선 안 된다.
            // ══════════════════════════════════════════════════════════════════

            const float TeleportFps = 20f;
            const float TeleportFramePeriod = 1f / TeleportFps;

            var frames = new List<int>(BlumgiGoalBurst.TeleportFrameCount);
            int frameMapViolations = 0;
            int liveSamples = 0;
            float liveGapMax = 0f;
            float livePrevT = -1f;

            for (int i = 0; i < _burst.Count; i++)
            {
                if (_burst[i].TeleportLive == false)
                    continue;

                liveSamples++;

                if (livePrevT >= 0f)
                    liveGapMax = Mathf.Max(liveGapMax, _burst[i].T - livePrevT);

                livePrevT = _burst[i].T;

                if (frames.Contains(_burst[i].TeleportFrame) == false)
                    frames.Add(_burst[i].TeleportFrame);

                int expected = Mathf.Clamp(Mathf.FloorToInt(_burst[i].T * TeleportFps),
                                           0, BlumgiGoalBurst.TeleportFrameCount - 1);

                if (_burst[i].TeleportFrame != expected)
                    frameMapViolations++;
            }

            frames.Sort();

            // ── 8-k2 프레임이 «시각의 함수»다. 이건 표본 수와 무관해서 «안 흔들린다».
            Check($"8-k2 ★ 텔레포트 프레임이 «시각의 함수»다 (frame = ⌊t × {TeleportFps:F0}⌋) " +
                  $"— 위반 {frameMapViolations}/{liveSamples} 표본",
                  liveSamples > 0 && frameMapViolations == 0,
                  liveSamples == 0 ? "살아 있는 표본이 0" : $"위반 {frameMapViolations}건");

            // ── 8-k 6프레임을 «전부» 지난다 [실측 f0~f5]. 직접 관측 + 시간 사상 메우기.
            int directCount = frames.Count;
            var bridged = new List<int>(BlumgiGoalBurst.TeleportFrameCount);

            if (frameMapViolations == 0)
            {
                for (int k = 0; k < BlumgiGoalBurst.TeleportFrameCount; k++)
                {
                    if (frames.Contains(k))
                        continue;

                    float windowStart = k * TeleportFramePeriod;
                    float windowEnd = (k + 1) * TeleportFramePeriod;

                    bool before = false;
                    bool after = false;

                    for (int i = 0; i < _burst.Count; i++)
                    {
                        // 앞: «살아 있고» 그 칸이 시작하기 전에 잰 표본
                        if (_burst[i].TeleportLive && _burst[i].T < windowStart)
                            before = true;

                        // 뒤: 그 칸이 «끝난 뒤»의 표본. 죽어 있어도 된다 —
                        //     죽었다는 것 자체가 「그 시각을 지났다」는 증거다.
                        if (_burst[i].T >= windowEnd)
                            after = true;
                    }

                    // k = 0 은 «앞» 표본이 원리적으로 없다 (t < 0 이 없다).
                    if ((before || k == 0) && after)
                    {
                        frames.Add(k);
                        bridged.Add(k);
                    }
                }

                frames.Sort();
                bridged.Sort();
            }

            Line($"    (텔레포트 채록 {liveSamples}표본 · 살아 있는 구간 최대 간격 {liveGapMax * 1000f:F1} ms " +
                 $"· 칸 {TeleportFramePeriod * 1000f:F0} ms · 직접 관측 {directCount}종" +
                 (bridged.Count == 0
                      ? ")"
                      : $" · 시간 사상으로 메움 {string.Join(",", bridged.ConvertAll(f => f.ToString()))})"));

            Check($"8-k ★ 텔레포트가 «6프레임»을 전부 지난다 — {frames.Count}/{BlumgiGoalBurst.TeleportFrameCount} " +
                  $"({string.Join(",", frames.ConvertAll(f => f.ToString()))}" +
                  (bridged.Count == 0 ? "" : $" · 그중 {bridged.Count}칸은 시간 사상") + ")",
                  frames.Count == BlumgiGoalBurst.TeleportFrameCount && frameMapViolations == 0,
                  $"관측 {directCount}종 · 메움 {bridged.Count}종");

            // ── 8-l 표시 크기 [실측 f0 만 256 · f1~f4 는 512. «크기 트윈»이 아니라 소스 크기 차이다].
            //    ⚠ 「f1 의 표본」을 고집하면 그 칸을 놓쳤을 때 «구현이 아니라 자»가 실패한다 —
            //      f1~f4 는 실측이 «전부 512» 이므로 <b>그중 «관측된» 칸을 쓴다</b>.
            //      그리고 «관측된 모든 칸»을 검사해 한 칸이라도 다르면 잡는다 (약해지지 않는다).
            //    ⚠ <b>f5 는 «굽는 그림이 없다»</b> — 원본 6번째 프레임이 완전 투명이라 안 구웠다
            //      (`06 B-2 #16-b`). 그래서 f5 의 표시 크기는 <b>0 이 정상</b>이고 512 무리에 넣으면 안 된다.
            const int TeleportEmptyFrame = BlumgiGoalBurst.TeleportFrameCount - 1;   // 5

            float display0 = -1f;
            float displayBig = -1f;
            int displayBigFrame = -1;
            int bigMismatch = 0;
            int bigChecked = 0;
            int emptyChecked = 0;
            int emptyMismatch = 0;

            for (int i = 0; i < _burst.Count; i++)
            {
                if (_burst[i].TeleportLive == false)
                    continue;

                int f = _burst[i].TeleportFrame;

                if (f == 0)
                {
                    if (display0 < 0f)
                        display0 = _burst[i].TeleportDisplay;

                    continue;
                }

                if (f >= TeleportEmptyFrame)
                {
                    emptyChecked++;

                    if (_burst[i].TeleportDisplay > 0.5f)
                        emptyMismatch++;

                    continue;
                }

                bigChecked++;

                if (displayBig < 0f)
                {
                    displayBig = _burst[i].TeleportDisplay;
                    displayBigFrame = f;
                }

                if (Mathf.Abs(_burst[i].TeleportDisplay - 512f) > 1f)
                    bigMismatch++;
            }

            Check($"8-l ★ 텔레포트 표시 크기가 f0 256 · f1~f4 512 · f5 «없음» 이다 (imageScale 2) " +
                  $"— f0 {display0:F0} · f{displayBigFrame} {displayBig:F0} " +
                  $"(512 아닌 표본 {bigMismatch}/{bigChecked} · f5 에 그림이 뜬 표본 {emptyMismatch}/{emptyChecked})",
                  display0 >= 0f && Mathf.Abs(display0 - 256f) <= 1f
                  && bigChecked > 0 && bigMismatch == 0
                  && emptyMismatch == 0,
                  display0 < 0f
                      ? "f0 를 한 번도 못 봤다"
                      : $"{display0:F0} / {displayBig:F0} · 어긋난 표본 {bigMismatch} · f5 오염 {emptyMismatch}");

            // ── 8-m 텔레포트 중심이 «골대 − 70» 이다 [실측 5레벨 5/5 · 오차 0].
            double teleportY = double.NaN;

            for (int i = 0; i < _burst.Count; i++)
            {
                if (_burst[i].TeleportLive && double.IsNaN(_burst[i].TeleportWorldY) == false)
                {
                    teleportY = _burst[i].TeleportWorldY;
                    break;
                }
            }

            Check($"8-m ★ 텔레포트 중심이 «골대 y − 70»({rimY - 70:F0})이다 — {teleportY:F2}",
                  double.IsNaN(teleportY) == false && Math.Abs(teleportY - (rimY - 70.0)) <= 0.5,
                  $"{teleportY:F2}");

            // ── 8-n 레이어 — 원본은 둘 다 «FXBottom» 이라 «블록보다 아래»다 [실측 layers index 1 < 2].
            SpriteRenderer[] burstRenderers = burst.GetComponentsInChildren<SpriteRenderer>(true);
            var block = FindFirstObjectByType<BlumgiBlockView>(FindObjectsInactive.Include);
            SpriteRenderer blockRenderer = block == null
                ? null
                : block.GetComponentInChildren<SpriteRenderer>(true);

            int burstMax = int.MinValue;

            for (int i = 0; i < burstRenderers.Length; i++)
                burstMax = Mathf.Max(burstMax, burstRenderers[i].sortingOrder);

            Check($"8-n ★ 골인 FX 가 «FXBottom» 이라 블록보다 «아래»에 그려진다 " +
                  $"(FX {burstMax} < 블록 {(blockRenderer == null ? 0 : blockRenderer.sortingOrder)})",
                  burstRenderers.Length > 0 && blockRenderer != null
                  && burstMax < blockRenderer.sortingOrder,
                  $"FX {burstMax} · 블록 {(blockRenderer == null ? -999 : blockRenderer.sortingOrder)}");

            // ── 8-o 「보였다」로 닫는 그림이 실제로 남았나 (재발방지 #60).
            Check($"8-o ★ 골인 FX 캡처가 «파일로» 남았다 — {ShotPathGoalFx}",
                  File.Exists(ShotPathGoalFx), "캡처가 안 만들어졌다");

            Check($"8-p ★ «진행 중»(골 +150 ms) 캡처도 남았다 — {ShotPathGoalFxMid}",
                  File.Exists(ShotPathGoalFxMid), "캡처가 안 만들어졌다");
        }

        /// <summary>채록에서 <b>조건이 «처음» 참이 되는 시각</b>. 없으면 −1.</summary>
        private float FirstTimeWhen(Func<GoalBurstSample, bool> predicate)
        {
            for (int i = 0; i < _burst.Count; i++)
            {
                if (predicate(_burst[i]))
                    return _burst[i].T;
            }

            return -1f;
        }

        /// <summary>
        /// ★ 17회차 실측이 닫은 <b>리소스 · 배경 착색 · 저장 스키마</b>를 기계로 확인한다.
        /// <b>「굽혔다」가 아니라 「실제로 그 프레임을 물고 있다」로 닫는다.</b>
        /// </summary>
        private IEnumerator CheckResourcesAndStorage(BlumgiLevelRuntime level)
        {
            // ── ① 블록 스킨 «5프레임» · 레벨↔프레임 표
            var block = FindFirstObjectByType<BlumgiBlockView>(FindObjectsInactive.Include);

            int expectedFrame = BlumgiLevelPresenter.BlockSkinFrameOf(level.Level.LevelNo);

            Check($"7-a ★ 블록이 «스킨 프레임»을 고른다 — L{level.Level.LevelNo} → {expectedFrame} " +
                  $"(실측 L1→0 L2→3 L3→1 L4→2 L5→4)",
                  block != null && block.SkinFrame == expectedFrame,
                  block == null ? "블록 뷰 없음" : $"프레임 {block.SkinFrame}");

            Check($"7-b ★ 그 프레임의 «그림»을 실제로 물고 있다 — '{(block == null ? string.Empty : block.SkinSpriteName)}'",
                  block != null && block.SkinSpriteName == $"block_skin_{expectedFrame}",
                  block == null ? "블록 뷰 없음" : block.SkinSpriteName);

            // ── ② 레벨↔프레임 표가 5레벨 전부 «서로 다른» 프레임을 준다 (462 인스턴스 예외 0건의 뜻)
            var used = new HashSet<int>();

            for (int levelNo = 1; levelNo <= 5; levelNo++)
                used.Add(BlumgiLevelPresenter.BlockSkinFrameOf(levelNo));

            Check($"7-c ★ 5레벨이 «서로 다른» 프레임을 쓴다 ({used.Count}/5)", used.Count == 5,
                  "표가 중복이다 — 레벨이 같은 그림으로 나온다");

            // ── ③ 배경 착색 기전 — 야자수는 «순백 마스크 + 런타임 틴트»이고 5레벨 전부 같은 색이다
            string palmHex = level.Config.PalmColorHex;

            Check($"7-d ★ 야자수 색이 5레벨 «전부 동일»한 설정값이다 — #{palmHex} (실측 (128,229,255))",
                  string.Equals(palmHex, "80E5FF", StringComparison.OrdinalIgnoreCase),
                  $"#{palmHex} — 레벨 팔레트가 아니다 [실측 25 인스턴스]");

            // ── ④ 저장 스키마 — «월드별 도달 최고 레벨» 숫자 하나 + LastLayout
            BlumgiProgress.SaveLevelCode("W1L3");

            Check("7-e ★ 저장이 «레이아웃 이름»을 그대로 든다 (실측 LastLayout \"W1L1\")",
                  BlumgiProgress.LoadLevelCode() == "W1L3", BlumgiProgress.LoadLevelCode());

            Check($"7-f ★ 진행도가 «월드별 도달 최고 레벨» 숫자 하나다 — WorldLastLevel_1 = {BlumgiProgress.LoadWorldLastLevel(1)}",
                  BlumgiProgress.LoadWorldLastLevel(1) == 3, BlumgiProgress.LoadWorldLastLevel(1).ToString());

            // ★ 「최고」라서 되돌아가도 «안 줄어든다» — 원본이 그렇다.
            BlumgiProgress.SaveLevelCode("W1L2");

            Check($"7-g ★ 되돌아가도 최고 레벨이 «안 줄어든다» — {BlumgiProgress.LoadWorldLastLevel(1)}",
                  BlumgiProgress.LoadWorldLastLevel(1) == 3, BlumgiProgress.LoadWorldLastLevel(1).ToString());

            ResetProgress();

            yield break;
        }

        /// <summary>
        /// 홀드 크기 곡선을 <b>원본 실측 8점</b>과 대조한다 [8회차 §2-b].
        /// 각 정답점에 «가장 가까운 홀드 ms» 표본을 골라 폭을 비교한다.
        /// </summary>
        private void ScoreHoldCurve(List<(double HoldMs, double WidthWorld)> samples)
        {
            if (samples.Count == 0)
            {
                Check("3-c2 홀드 곡선 표본이 있다", false, "0 표본");
                return;
            }

            int ok = 0;

            for (int i = 0; i < HoldCurve.Length; i++)
            {
                double targetMs = HoldCurve[i].HoldMs;
                double expected = HoldCurve[i].WidthWorld;

                int best = 0;
                double bestGap = double.MaxValue;

                for (int s = 0; s < samples.Count; s++)
                {
                    double gap = Math.Abs(samples[s].HoldMs - targetMs);

                    if (gap >= bestGap)
                        continue;

                    bestGap = gap;
                    best = s;
                }

                double actual = samples[best].WidthWorld;
                bool near = Math.Abs(actual - expected) <= SizeTolerance;

                if (near)
                    ok++;

                Line(near
                         ? $"     · h{targetMs:F0} 기대 {expected:F2} / 실측 {actual:F2} (표본 {samples[best].HoldMs:F0} ms) ✅"
                         : $"     · h{targetMs:F0} 기대 {expected:F2} / 실측 {actual:F2} (표본 {samples[best].HoldMs:F0} ms) ❌");
            }

            Check($"3-c2 홀드 크기 곡선이 원본 실측 8점과 맞는다 ({ok}/{HoldCurve.Length} · 허용 ±{SizeTolerance} px)",
                  ok == HoldCurve.Length, $"{ok}/{HoldCurve.Length}");
        }

        /// <summary>
        /// 줌 펀치를 시계열로 떠서 <b>탄성인지</b>를 본다 —
        /// 「0.8 에서 시작한다」와 「1 을 넘어갔다 온다」 둘 다 있어야 <c>easeOutElastic</c> 이다.
        /// 단조 이징이면 두 번째가 «절대» 안 나온다.
        /// </summary>
        private IEnumerator SampleZoomPunch()
        {
            var director = FindFirstObjectByType<BlumgiCameraDirector>(FindObjectsInactive.Include);

            if (director == null)
            {
                Check("2-f 카메라 디렉터가 있다", false, "없다");
                yield break;
            }

            float first = director.ZoomValue;
            float max = first;
            float min = first;
            float drop = 0f;
            float t = 0f;

            while (t < 1.2f)
            {
                max = Mathf.Max(max, director.ZoomValue);
                min = Mathf.Min(min, director.ZoomValue);
                drop = Mathf.Max(drop, -director.ZoomDeltaThisFrame);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // ★★ [정정 · 17회차] 예전 주석이 「0.8 그 값은 원리적으로 못 읽는다」였는데,
            //   흔들림의 정체는 «우리 검사»가 아니라 «원본이 불연속»이라는 사실이었다 —
            //   원본도 골 프레임엔 1.00 이고 «그 다음 프레임»에 0.81 로 뚝 떨어진다 [실측 rAF].
            //   ⇒ 시작을 절대값으로 읽지 않고 «한 프레임 낙차»로 잰다. 허용치는 안 낮췄다 (#48).
            Check($"2-f1 줌 펀치가 «1 보다 뚜렷이 아래»에서 시작한다 (관측 최소 {min:F3} · 트윈 시작값 0.8)",
                  min <= 0.93f, $"최소 {min:F3} — 확대된 채 시작하지 않는다");

            Check($"2-f2 줌 펀치가 1 을 «넘어갔다» 온다 = 탄성 (관측 최대 {max:F3} · 실측 1.075)",
                  max >= 1.03f, $"최대 {max:F3} — 단조 이징으로 보인다");

            Check($"2-f3 ★ 줌이 «한 프레임 만에» 내려간다 = 트윈이 아니다 (최대 낙차 {drop:F3} · 실측 1.00 → 0.81)",
                  drop >= 0.15f, $"한 프레임 최대 낙차 {drop:F3} — 부드럽게 내려가면 타격감이 사라진다");
        }

        /// <summary>
        /// <b>검사가 자기 시작 상태를 만든다</b> (재발방지 #61).
        /// ⚠ <b>게임 코드에 문을 뚫지 않았다</b> — <c>BlumgiProgress.PrefKey</c> 는 이미 공개된 상수다.
        /// </summary>
        private void ResetProgress()
        {
            string before = BlumgiProgress.LoadLevelCode();

            PlayerPrefs.DeleteKey(BlumgiProgress.PrefKey);
            PlayerPrefs.DeleteKey(BlumgiProgress.KeyWorldLastLevelPrefix + "1");
            PlayerPrefs.Save();

            Line($"  [시작 상태] 저장 진행도를 지웠다 — 직전 {before} → 지금 {BlumgiProgress.LoadLevelCode()}");
        }

        private static int CountActiveChildren(Transform parent)
        {
            if (parent == null)
                return 0;

            int n = 0;

            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).gameObject.activeSelf)
                    n++;
            }

            return n;
        }

        /// <summary>
        /// ⚠ <b>먼저 «넘기고» 그 다음에 «더한다».</b> 반대로 하면 <b>부르기 «전»에 이미 흘러간 프레임의
        /// <c>deltaTime</c> 이 대기 예산에 먼저 들어가</b> 실제로는 그만큼 «덜» 기다린다.
        /// 캡처처럼 <b>한 프레임이 통째로 긴 작업</b> 뒤에 부르면 오차가 수십 ms 로 커진다 —
        /// 실제로 UI 캡처를 전 해상도로 올리자 <c>5-c3</c>(색상환 0.30 s) 가 0.234 로 «측정 오차 때문에» 떨어졌다.
        /// 재는 도구가 자기 대기 시간을 잘못 세면 <b>구현이 아니라 자를 채점하게 된다</b>.
        /// </summary>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;

            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
        }

        /// <summary>
        /// 기다리면서 <b>골인 FX 를 채록</b>한다. ⚠ <b>기다리는 시간을 늘리지 않는다</b> —
        /// 골 +2 216.9 ms 에 레벨이 «전환»되므로, 재려고 시간을 더 쓰면 <b>전환 뒤를 재게 된다</b>.
        /// </summary>
        private IEnumerator WaitSecondsSampling(float seconds, BlumgiGoalBurst burst)
        {
            float t = 0f;

            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                SampleGoalBurst(burst);
            }
        }

        /// <summary>골 기준 <paramref name="seconds"/> 가 될 때까지 채록하며 기다린다.</summary>
        private IEnumerator WaitBurstUntil(BlumgiGoalBurst burst, float seconds)
        {
            float guard = 0f;

            while (burst != null && burst.ElapsedSeconds >= 0f
                   && burst.ElapsedSeconds < seconds && guard < 2f)
            {
                yield return null;
                guard += Time.unscaledDeltaTime;
                SampleGoalBurst(burst);
            }
        }

        private void SampleGoalBurst(BlumgiGoalBurst burst)
        {
            if (burst == null || burst.ElapsedSeconds < 0f)
                return;

            _burst.Add(new GoalBurstSample
            {
                T = burst.ElapsedSeconds,
                Live = burst.LiveWinLightCount,
                W0 = burst.WinLightWidthWorld(0),
                H0 = burst.WinLightHeightWorld(0),
                A0 = burst.WinLightAlpha(0),
                Bottom0 = burst.WinLightBottomWorldY(0),
                W1 = burst.WinLightWidthWorld(1),
                H1 = burst.WinLightHeightWorld(1),
                A1 = burst.WinLightAlpha(1),
                Bottom1 = burst.WinLightBottomWorldY(1),
                TeleportLive = burst.IsTeleportLive,
                TeleportFrame = burst.TeleportFrame,
                TeleportDisplay = burst.TeleportDisplayWorld,
                TeleportWorldY = burst.TeleportCenterWorldY,
            });
        }

        /// <summary>
        /// 그림을 «실제로» 남긴다.
        ///
        /// <para>
        /// ⚠ <c>ScreenCapture.CaptureScreenshot</c> 은 <b>배치모드에서 파일을 안 만든다</b> [실측 —
        /// 30프레임 기다려도 안 생겼다]. 표시할 백버퍼가 없기 때문이다.
        /// 그래서 <b>카메라를 RenderTexture 에 직접 그려</b> PNG 로 쓴다 — 배치에서도 된다.
        /// </para>
        ///
        /// <para>
        /// ★★ <b>②-d 가 고친 것 — UI 를 «따로» 겹쳐 그린다.</b> ②-c 의 캡처는 게임 카메라만 그렸고,
        /// 그 결과 <b>HUD 도 클리어 오버레이(<c>YES!</c>·플래시)도 한 장도 안 찍혔다</b>
        /// (실제로 ②-d 첫 실행의 골 캡처에 컨페티만 있고 <c>YES!</c> 가 없었다).
        /// URP 는 <c>Camera.Render()</c> 를 «단일 카메라» 경로로 처리해 <b>스택(Overlay)을 안 따라간다</b> —
        /// 그래서 UI 카메라를 <b>이 순간만 <c>Base</c> 로 돌려</b> 같은 RT 에 한 번 더 그린 뒤 되돌린다.
        /// <b>「보이는데 안 찍힌 것」과 「원래 없는 것」은 그림으로는 구별이 안 된다</b> — 그래서 고쳤다.
        /// </para>
        ///
        /// <para>
        /// ★ 이것이 「그렸다」가 아니라 <b>«보인다»</b> 로 닫는 유일한 길이다 (재발방지 #60).
        /// </para>
        /// </summary>
        private IEnumerator CaptureAndWait(string path)
        {
            // ⚠ <b><c>WaitForEndOfFrame</c> 을 쓰지 마라</b> — 배치모드에서는 «영영 안 깨어난다»
            //    [실측: 420초 시간 제한에 걸려 강제 종료됐다]. 프레임을 한 번 넘기는 것으로 족하다.
            yield return null;

            // ⚠ 씬 카메라는 MainCamera 태그가 «없다» — Camera.main 은 null 이다 [실측].
            //    이름으로 집는다 (BlumgiSceneBuilder 가 굽는 이름).
            Camera camera = null;
            Camera[] cameras = Camera.allCameras;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].name != "GameCamera")
                    continue;

                camera = cameras[i];
                break;
            }

            if (camera == null && cameras.Length > 0)
                camera = cameras[0];

            if (camera == null)
            {
                Line($"  · 캡처 건너뜀 (게임 카메라를 못 찾았다): {path}");
                yield break;
            }

            const int Width = 960;
            const int Height = 540;

            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousCameraTarget = camera.targetTexture;

            try
            {
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;

                var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                readback.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, readback.EncodeToPNG());

                UnityEngine.Object.DestroyImmediate(readback);
            }
            catch (Exception e)
            {
                Line($"  · 캡처 실패: {path} — {e.Message}");
            }
            finally
            {
                camera.targetTexture = previousCameraTarget;
                RenderTexture.active = previousActive;

                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }

            var info = new FileInfo(path);
            Line(info.Exists ? $"  📷 {path} ({info.Length} bytes)" : $"  · 캡처 파일이 안 생겼다: {path}");

            // ★ UI 는 «따로 한 장» 남긴다 — 아래 주석 참조. 겹쳐 그리면 월드가 통째로 지워진다 [실측].
            yield return CaptureUiAndWait(UiPathOf(path), cameras);
        }

        private static string UiPathOf(string path)
        {
            return path.Substring(0, path.Length - 4) + "_ui.png";
        }

        /// <summary>
        /// UI 를 <b>별도 한 장</b>으로 남긴다.
        ///
        /// <para>
        /// ★★ <b>왜 겹쳐 그리지 않나</b> — UI 카메라는 URP <c>Overlay</c> 라 <c>Camera.Render()</c> 로는
        /// 안 그려진다(URP 는 단일 카메라 경로에서 스택을 안 따라간다). <c>Base</c> 로 돌려 그리면 그려지지만
        /// <b>URP 가 색까지 지워 월드가 통째로 사라진다</b> [실측 — 골 캡처가 배경색 한 장이 됐다].
        /// 그래서 <b>월드 한 장 · UI 한 장</b>으로 나눈다. 둘을 나란히 보면 화면이 재구성된다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이 장치가 없으면 「UI 가 안 보이는 것」과 「UI 가 없는 것」을 구별할 수 없다</b> —
        /// 실제로 이 회차에 <c>YES!</c> 가 <b>패스 ② 이후 한 번도 안 떠 있었다</b>는 결함이
        /// 여기서 드러났다.
        /// </para>
        /// </summary>
        private IEnumerator CaptureUiAndWait(string path, Camera[] cameras)
        {
            Camera ui = null;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].name == "UICamera")
                    ui = cameras[i];
            }

            if (ui == null)
                yield break;

            // ★ UI 는 «전 해상도»로 남긴다 — HUD 의 레벨 도트 한 칸이 49 px 라
            //   반해상도(960)로 찍으면 24 px 가 되어 «5칸인지 4칸인지»를 눈으로 못 가른다.
            //   실제로 ②-d 가 「도트가 5개로 안 보인다」를 여기서 놓칠 뻔했다.
            const int Width = 1920;
            const int Height = 1080;

            var data = ui.GetComponent<UniversalAdditionalCameraData>();
            CameraRenderType previousType = data == null ? CameraRenderType.Overlay : data.renderType;
            RenderTexture previousTarget = ui.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;

            try
            {
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

                if (data != null)
                    data.renderType = CameraRenderType.Base;

                ui.targetTexture = target;
                ui.Render();

                RenderTexture.active = target;

                var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                readback.Apply();

                File.WriteAllBytes(path, readback.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(readback);
            }
            catch (Exception e)
            {
                Line($"  · UI 캡처 실패: {path} — {e.Message}");
            }
            finally
            {
                ui.targetTexture = previousTarget;

                if (data != null)
                    data.renderType = previousType;

                RenderTexture.active = previousActive;

                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }

            var info = new FileInfo(path);

            if (info.Exists)
                Line($"  📷 {path} ({info.Length} bytes · UI 층)");
        }

        private void CheckNear(string title, double actual, double expected)
        {
            Check($"{title} = {expected:F2}", Math.Abs(actual - expected) <= PositionTolerance,
                  $"실측 {actual:F2}");
        }

        private void CheckNearSize(string title, double actual, double expected)
        {
            Check($"{title} = {expected:F2}", Math.Abs(actual - expected) <= SizeTolerance,
                  $"실측 {actual:F2}");
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
            if (SessionState.GetBool(BlumgiRestBallPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }
}
