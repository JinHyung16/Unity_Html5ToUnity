using System;
using System.Collections;
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
    /// 22회차 신설 — <b>렌더 A/B 캡처기</b>.
    ///
    /// <para>
    /// ★★ <b>이 도구는 «찍기»만 한다. 채점은 하지 않는다.</b> 원본 캡처(<c>Html_Screenshot/</c>)와
    /// 나란히 놓을 우리 쪽 그림을 <b>같은 상태 · 같은 크기</b>로 남기는 것이 전부다.
    /// 픽셀 대조 숫자는 바깥의 파이썬(Pillow)이 낸다 — <b>「재는 도구」라 이관이 끝나면 통째로 지운다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>합격 기준 3 은 «픽셀 동일»이 아니다</b> — 원장 확정표가
    /// 「아트 재제작이라 픽셀 동일이 아닌 «퀄리티 동일» — 단 배치 좌표·비율·색상값은 수치로 대조」라고
    /// 못 박았다. 그래서 이 캡처로 내는 숫자는 <b>합격/불합격이 아니라 «읽는 숫자»</b>다.
    /// 픽셀 일치율이 낮다고 결함이 아니고, 높다고 통과도 아니다 — <b>배치·비율·색</b>이 판정이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>트윈·연출 중에 찍지 않는다</b> (20회차 사고 — 색 트윈이 «화면 픽셀에만» 걸린다).
    /// 레벨 진입 줌 펀치와 색 트윈이 전부 끝나도록 <see cref="SettleSeconds"/> 를 기다린 뒤에 찍는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>편집 모드로는 못 찍는다</b> — 캔버스가 한 번도 렌더되지 않아 UI 가 통째로 빈다.
    /// 그래서 <c>PLAYMODE=1 GRAPHICS=1</c> 이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 검사용 문을 뚫지 않았다</b> — 레벨 이동은 <c>BlumgiGameManager.LoadLevel</c>
    /// (진행이 부르는 그 함수), 화면 전이는 <c>GameFlow.ChangeScreen</c> (1 PLAYER 카드가 부르는 그 함수)다.
    /// </para>
    ///
    /// <para>
    /// 실행: <c>PLAYMODE=1 GRAPHICS=1
    /// Tools/unity-batch.sh JinHyung.EditorTools.BlumgiRenderPlayCheck.RunAll</c>
    /// </para>
    /// </summary>
    public static class BlumgiRenderPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiRenderPlayCheck.Armed";

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

            var go = new GameObject("BlumgiRenderProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiRenderProbe>();
#endif
        }
    }

    /// <summary>실제 캡처기.</summary>
    public sealed class BlumgiRenderProbe : MonoBehaviour
    {
        private const string ReportPath = "Temp/BlumgiRenderReport.txt";
        private const string OutDir = "Temp/BlumgiRender";

        /// <summary>
        /// ★ 원본 캡처가 <b>836 × 470</b>(비 1.7787)이고 우리는 1920 × 1080(비 1.7778)이다.
        /// <b>더 큰 쪽으로 찍어 바깥에서 줄인다</b> — 반대로 하면 축소 손실이 두 번 들어간다.
        /// </summary>
        private const int Width = 1920;

        private const int Height = 1080;

        /// <summary>
        /// ★★ <b>정착 대기</b>. 진입 줌 펀치(0.8 → 1 탄성)와 색 트윈이 끝나야 한다.
        /// <b>이 값을 줄이면 「연출 중 그림」을 원본 «정지 화면»과 대조하게 된다</b> (20회차 사고).
        /// </summary>
        private const float SettleSeconds = 3.0f;

        private const float StepTimeoutSeconds = 40f;

        /// <summary>레벨 전수 — 관측 범위가 월드1 이다 (확정표 11-b).</summary>
        private static readonly string[] Levels = { "W1L1", "W1L2", "W1L3", "W1L4", "W1L5" };

        private readonly StringBuilder _report = new StringBuilder(1 << 12);

        private int _shots;
        private int _failed;

        private void Start()
        {
            try
            {
                if (Directory.Exists(OutDir))
                    Directory.Delete(OutDir, true);
            }
            catch (Exception e)
            {
                Log.Warning($"지난 캡처 폴더를 못 지웠다 — {e.Message}");
            }

            Directory.CreateDirectory(OutDir);
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
                    Line($"  ❌ 캡처 중 예외: {e}");
                    _failed++;
                    break;
                }

                if (moved == false)
                    break;

                yield return body.Current;
            }

            Line(string.Empty);
            Line("========================================");
            Line($"═══ 렌더 캡처: {_shots}장 · 실패 {_failed}건 · 폴더 {OutDir}");
            Line("   ⚠ 이 도구는 «채점하지 않는다». 픽셀 숫자는 바깥의 대조 스크립트가 낸다.");
            Line("   ⚠ 합격 기준 3 은 «픽셀 동일»이 아니라 «퀄리티 동일 + 배치·비율·색 수치 대조»다.");

            WriteReport();
            Debug.Log(_report.ToString());

            yield return null;

            Finish(_failed == 0);
        }

        private IEnumerator Body()
        {
            var init = FindFirstObjectByType<BlumgiGameInitialize>();

            if (init == null)
            {
                Line("  ❌ 씬에 BlumgiGameInitialize 가 없다 — 씬이 안 구워졌다");
                _failed++;
                yield break;
            }

            float waited = 0f;

            while (init.IsReady == false && waited < StepTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (init.IsReady == false)
            {
                Line($"  ❌ 초기화가 {waited:F1}s 안에 안 끝났다");
                _failed++;
                yield break;
            }

            BlumgiGameRoot root = BlumgiGameRoot.Instance;

            // ── 0. 주요 화면 — 모드 선택(WELCOME)
            //    ⚠ 진행도가 남아 있으면 카드 구성이 달라진다. 검사가 자기 시작 상태를 만든다 (재발방지 #61).
            ResetProgress();
            root.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);

            yield return WaitSeconds(SettleSeconds);
            yield return Capture("S00_welcome");

            // ── 1. 레벨 전수 — 진입 «정지 화면»
            root.GameFlow.ChangeScreen(EBlumgiScreenType.Game);

            BlumgiGameManager game = root.Game;

            for (int i = 0; i < Levels.Length; i++)
            {
                string code = Levels[i];

                // ★ 게임이 진행할 때 부르는 «그» 함수다 — 검사용 문이 아니다.
                game.LoadLevel(code);

                // ⚠ 트윈이 끝난 «뒤»에 찍는다. 여기를 줄이면 20회차 사고가 되살아난다.
                yield return WaitSeconds(SettleSeconds);

                if (game.CurrentLevel == null || game.CurrentLevel.Level.Code != code)
                {
                    Line($"  ❌ {code} 로 안 들어갔다 (지금 {(game.CurrentLevel == null ? "null" : game.CurrentLevel.Level.Code)})");
                    _failed++;
                    continue;
                }

                yield return Capture($"S{i + 1:00}_{code}");
            }
        }

        // ────────────────────────────────────────────────────────── 캡처

        /// <summary>
        /// 월드 한 장 · UI 한 장으로 <b>나눠</b> 남긴다.
        ///
        /// <para>
        /// ★★ 겹쳐 그리지 «않는» 이유 — UI 카메라는 URP <c>Overlay</c> 라 <c>Camera.Render()</c> 가
        /// 스택을 안 따라가고, <c>Base</c> 로 돌려 같은 RT 에 그리면 <b>URP 가 색까지 지워 월드가 사라진다</b>
        /// [②-d 실측]. 그래서 <b>둘을 따로</b> 남기고 대조도 따로 한다.
        /// </para>
        /// </summary>
        private IEnumerator Capture(string name)
        {
            yield return null;

            Camera world = FindCamera("GameCamera");
            Camera ui = FindCamera("UICamera");

            if (world == null)
            {
                Line($"  ❌ {name}: 게임 카메라를 못 찾았다");
                _failed++;
                yield break;
            }

            WriteOne(world, Path.Combine(OutDir, name + "_world.png"), false, name);

            if (ui != null)
                WriteOne(ui, Path.Combine(OutDir, name + "_ui.png"), true, name);
        }

        private static Camera FindCamera(string cameraName)
        {
            Camera[] cameras = Camera.allCameras;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].name == cameraName)
                    return cameras[i];
            }

            return null;
        }

        /// <summary>
        /// ⚠ <c>ScreenCapture.CaptureScreenshot</c> 은 <b>배치모드에서 파일을 안 만든다</b>
        /// [②-d 실측 — 30프레임 기다려도 안 생겼다]. 카메라를 RenderTexture 에 직접 그린다.
        /// </summary>
        private void WriteOne(Camera camera, string path, bool overlayToBase, string name)
        {
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            var data = overlayToBase ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            CameraRenderType previousType = data == null ? CameraRenderType.Overlay : data.renderType;

            try
            {
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

                if (data != null)
                    data.renderType = CameraRenderType.Base;

                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;

                var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                readback.Apply();

                File.WriteAllBytes(path, readback.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(readback);
            }
            catch (Exception e)
            {
                Line($"  ❌ {name} 캡처 실패: {path} — {e.Message}");
                _failed++;
            }
            finally
            {
                camera.targetTexture = previousTarget;

                if (data != null)
                    data.renderType = previousType;

                RenderTexture.active = previousActive;

                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }

            var info = new FileInfo(path);

            if (info.Exists)
            {
                _shots++;
                Line($"  📷 {path} ({info.Length} bytes)");
            }
            else
            {
                _failed++;
                Line($"  ❌ 캡처 파일이 안 생겼다: {path}");
            }
        }

        // ────────────────────────────────────────────────────────── 보조

        /// <summary>
        /// <b>검사가 자기 시작 상태를 만든다</b> (재발방지 #61).
        /// ⚠ <c>BlumgiProgress.PrefKey</c> 는 이미 공개된 상수다 — 문을 뚫지 않았다.
        /// </summary>
        private void ResetProgress()
        {
            PlayerPrefs.DeleteKey(BlumgiProgress.PrefKey);
            PlayerPrefs.DeleteKey(BlumgiProgress.KeyWorldLastLevelPrefix + "1");
            PlayerPrefs.Save();
        }

        /// <summary>⚠ 먼저 «넘기고» 그 다음에 «더한다» — 캡처 프레임이 길어 반대로 하면 덜 기다린다.</summary>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;

            while (t < seconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
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
                Log.Warning($"캡처 전문을 못 썼다: {e.Message}");
            }
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            // ★ 다리가 시킨 실행이면 «에디터를 끄지 않는다» — 사람이 쓰던 창이 사라진다.
            if (SessionState.GetBool(BlumgiRenderPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }
}
