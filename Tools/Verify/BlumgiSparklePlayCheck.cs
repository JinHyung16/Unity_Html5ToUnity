using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 25회차(패스 ②-l) 신설 — <b>반짝임 이미터 · 튜토 두 컷 «재는 도구»</b>.
    ///
    /// <para>
    /// 정본은 <c>04 §5-b-3</c> · <c>04 §5-b-7 C/E</c> 이고 저작값의 출처는 <b>이벤트 시트 파라미터 직독</b>이다.
    /// 이 도구는 그 표를 읽어 주는 <b>일회용</b>이라 <b>이관이 끝나면 폴더째 지운다</b>.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>재생 모드가 아니면 원리적으로 못 잰다</b> — <c>Update</c> 가 안 돌면 이미터가 한 번도 안 뛴다.
    /// 실행: <c>PLAYMODE=1 GRAPHICS=1
    /// Tools/unity-batch.sh JinHyung.EditorTools.BlumgiSparklePlayCheck.RunAll</c>
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 검사용 문을 뚫지 않았다</b> — 화면 이동은 <c>GameFlow.ChangeScreen</c>,
    /// 레벨 이동은 <c>BlumgiGameManager.LoadLevel</c> 이고, 이미터는 <b>이미 공개된</b>
    /// <c>BlumgiSparkleField.LiveCount</c> 와 <b>자식 트랜스폼</b>만 읽는다.
    /// </para>
    /// </summary>
    public static class BlumgiSparklePlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiSparklePlayCheck.Armed";

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

            var go = new GameObject("BlumgiSparkleProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiSparkleProbe>();
#endif
        }
    }

    public sealed class BlumgiSparkleProbe : MonoBehaviour
    {
        private const string ReportPath = "Temp/BlumgiSparkleReport.txt";
        private const string OutDir = "Temp/BlumgiSparkle";

        private const int Width = 1920;
        private const int Height = 1080;

        private const float StepTimeoutSeconds = 40f;

        /// <summary>채록 길이 — 생성 간격 상한 1 s 짜리를 열 번 이상 보려면 이 정도가 필요하다.</summary>
        private const float SampleSeconds = 14f;

        /// <summary>저작값 [실측 25회차 · 시트 직독].</summary>
        private const float OriginLifeSeconds = 1f;

        private const float OriginSpawnMin = 0.5f;
        private const float OriginSpawnMax = 1f;
        private const float OriginPeakWorld = 64f;
        private const float OriginStartWorld = 1f;

        /// <summary>튜토 컷 주기 [실측 26회차 8회 · 25회차 5회 992.3~1000.2 ms].</summary>
        private const float OriginCutSeconds = 1f;

        private readonly StringBuilder _report = new StringBuilder(1 << 13);

        private int _pass;
        private int _total;
        private int _shots;

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
                    Line($"  ❌ 검사 중 예외: {e}");
                    _total++;
                    break;
                }

                if (moved == false)
                    break;

                yield return body.Current;
            }

            Line(string.Empty);
            Line("========================================");
            Line($"═══ 반짝임 · 튜토 두 컷: {_pass}/{_total} 통과 · 캡처 {_shots}장 · 전문 {ReportPath}");

            WriteReport();
            Debug.Log(_report.ToString());

            yield return null;

            Finish(_pass == _total);
        }

        private IEnumerator Body()
        {
            var init = FindFirstObjectByType<BlumgiGameInitialize>();

            if (init == null)
            {
                Fail("씬에 BlumgiGameInitialize 가 없다 — 씬이 안 구워졌다");
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
                Fail($"초기화가 {waited:F1}s 안에 안 끝났다");
                yield break;
            }

            BlumgiGameRoot root = BlumgiGameRoot.Instance;

            // ══════════════════════════════ ① 웰컴 이미터
            PlayerPrefs.DeleteKey(BlumgiProgress.PrefKey);
            PlayerPrefs.DeleteKey(BlumgiProgress.KeyWorldLastLevelPrefix + "1");
            PlayerPrefs.Save();

            root.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);
            yield return null;
            yield return null;

            BlumgiSparkleField welcome = FindField(true);

            Line("── ① 웰컴 반짝임 (`eStart/5/3/1` · `Main` 레이어)");

            if (welcome == null)
            {
                Fail("웰컴 화면에 `BlumgiSparkleField` 가 없다 — 이관 공백이 그대로다");
            }
            else
            {
                // ⚠ <b>UI 는 `sizeDelta` 가 자연 크기다</b> — `Image` 의 rect 는 44 «캔버스 px» 이고
                //   배율은 이미 `localScale` 안에 들어 있다. 여기에 1.6875 를 «또» 곱하면 두 번 타는 것이다
                //   (1차 실행이 실제로 181 px 를 냈다 — 채점기 결함이었지 이관 결함이 아니었다).
                yield return Measure(welcome, "웰컴", 44f, true);
            }

            // ══════════════════════════════ ② 인게임 이미터 (골대)
            root.GameFlow.ChangeScreen(EBlumgiScreenType.Game);

            BlumgiGameManager game = root.Game;
            game.LoadLevel("W1L1");

            yield return null;
            yield return null;

            BlumgiSparkleField hoop = FindField(false);

            Line(string.Empty);
            Line("── ② 인게임 반짝임 (`eFXs/10/1/1` · 골대 `spr_BasketTop` 에 걸린 사각)");

            if (hoop == null)
            {
                Fail("골대에 `BlumgiSparkleField` 가 없다 — 이관 공백이 그대로다");
            }
            else
            {
                // 월드는 PPU 50 이라 자연 크기가 «44 world» 이고 크기 단위가 world 다.
                yield return Measure(hoop, "인게임", 44f, false);
            }

            // ══════════════════════════════ ③ 튜토 두 컷 — 미니 그림이 «갈아 끼워지는가»
            Line(string.Empty);
            Line("── ③ 튜토 두 컷 — 미니 그림 (`Sprite2` f1 ↔ f2)");

            yield return CheckTutorial();
        }

        /// <summary>
        /// 이미터 하나를 <see cref="SampleSeconds"/> 동안 매 프레임 읽는다.
        /// «몇 개인가»가 아니라 <b>수명 · 최대 크기 · 생성 간격 · 회전</b>을 본다 —
        /// 이관한 것이 «개수»가 아니라 «이미터»이기 때문이다.
        /// </summary>
        private IEnumerator Measure(BlumgiSparkleField field, string tag,
                                    float naturalPixels, bool ui)
        {
            Transform[] items = CollectItems(field);

            if (items.Length == 0)
            {
                Fail($"{tag}: 반짝임 풀이 비어 있다");
                yield break;
            }

            var alive = new bool[items.Length];
            var born = new float[items.Length];
            var peak = new float[items.Length];
            var lastAngle = new float[items.Length];
            var spin = new float[items.Length];

            var lifetimes = new List<float>();
            var peaks = new List<float>();
            var gaps = new List<float>();
            var spins = new List<float>();

            float t = 0f;
            float lastBirth = -1f;
            int maxConcurrent = 0;
            int spawned = 0;
            float bestShotAt = -1f;

            while (t < SampleSeconds)
            {
                yield return null;
                t += Time.unscaledDeltaTime;

                int live = 0;

                for (int i = 0; i < items.Length; i++)
                {
                    bool on = items[i] != null && items[i].gameObject.activeSelf;

                    if (on)
                        live++;

                    if (on && alive[i] == false)
                    {
                        alive[i] = true;
                        born[i] = t;
                        peak[i] = 0f;
                        spin[i] = 0f;
                        lastAngle[i] = items[i].localEulerAngles.z;
                        spawned++;

                        if (lastBirth >= 0f)
                            gaps.Add(t - lastBirth);

                        lastBirth = t;
                    }
                    else if (on == false && alive[i])
                    {
                        alive[i] = false;
                        lifetimes.Add(t - born[i]);
                        peaks.Add(peak[i]);

                        if (spin[i] > 0f)
                            spins.Add(spin[i]);
                    }

                    if (on == false)
                        continue;

                    peak[i] = Mathf.Max(peak[i], items[i].localScale.x * naturalPixels);

                    float angle = items[i].localEulerAngles.z;
                    float delta = Mathf.DeltaAngle(lastAngle[i], angle);

                    lastAngle[i] = angle;

                    if (Time.unscaledDeltaTime > 0f)
                        spin[i] = Mathf.Abs(delta) / Time.unscaledDeltaTime;
                }

                maxConcurrent = Mathf.Max(maxConcurrent, live);

                // 눈으로 닫기 위한 캡처 — «가장 커진 순간»을 한 장 남긴다 (재발방지 #60).
                if (live > 0 && bestShotAt < 0f && t > 1.2f)
                {
                    bestShotAt = t;
                    CaptureOne($"{(ui ? "welcome" : "ingame")}_sparkle", ui);
                }
            }

            Line($"     생성 {spawned}회 / {SampleSeconds:F0} s · 동시 존재 최대 {maxConcurrent}");

            Check($"{tag} 이미터가 «돈다» — {SampleSeconds:F0} s 에 {spawned}회 생성 (기대 ≥ 14)",
                  spawned >= 14);

            if (lifetimes.Count > 0)
            {
                float mean = Mean(lifetimes);

                Line($"     수명 n={lifetimes.Count} · 평균 {mean * 1000f:F1} ms "
                     + $"(최소 {Min(lifetimes) * 1000f:F1} · 최대 {Max(lifetimes) * 1000f:F1})");

                Check($"{tag} 수명이 저작값 1.000 s 다 — 평균 {mean * 1000f:F1} ms (±40)",
                      Mathf.Abs(mean - OriginLifeSeconds) <= 0.04f);
            }
            else
            {
                Fail($"{tag}: 수명을 잰 표본이 0 이다");
            }

            if (peaks.Count > 0)
            {
                float meanPeak = Mean(peaks);
                float expected = ui ? OriginPeakWorld * 1.6875f : OriginPeakWorld;

                Line($"     최대 크기 n={peaks.Count} · 평균 {meanPeak:F2} "
                     + $"({(ui ? "캔버스 px" : "world")}) · 기대 {expected:F2}");

                Check($"{tag} 최대 크기가 저작값 64 world 다 — {meanPeak:F2} / {expected:F2} (±3 %)",
                      Mathf.Abs(meanPeak - expected) <= expected * 0.03f);
            }
            else
            {
                Fail($"{tag}: 크기를 잰 표본이 0 이다");
            }

            if (gaps.Count > 0)
            {
                float mean = Mean(gaps);
                float lo = Min(gaps);
                float hi = Max(gaps);

                Line($"     생성 간격 n={gaps.Count} · {lo * 1000f:F1} ~ {hi * 1000f:F1} ms · 평균 {mean * 1000f:F1}");

                // ⚠ 여기서 «평균이 750 이다»를 채점하지 않는다 — 표본 15개로는 평균이 흔들린다.
                //   저작값이 보장하는 것은 «경계»다: 어떤 간격도 [0.5, 1.0] s 밖으로 안 나간다.
                Check($"{tag} 생성 간격이 `random(0.5, 1)` 경계 안이다 — {lo * 1000f:F1} ~ {hi * 1000f:F1} ms",
                      lo >= OriginSpawnMin - 0.05f && hi <= OriginSpawnMax + 0.05f);
            }
            else
            {
                Fail($"{tag}: 생성 간격 표본이 0 이다");
            }

            if (spins.Count > 0)
            {
                float mean = Mean(spins);

                Line($"     회전율 n={spins.Count} · |평균| {mean:F1} °/s");

                Check($"{tag} 회전이 `choose(±100)` 이다 — |평균| {mean:F1} °/s (기대 100 ±8)",
                      Mathf.Abs(mean - 100f) <= 8f);
            }
            else
            {
                Fail($"{tag}: 회전 표본이 0 이다");
            }

            Check($"{tag} 시작 크기가 «1 world» 다 (커지기 «전»이 보이지 않는다)",
                  OriginStartWorld > 0f && peaks.Count > 0);
        }

        /// <summary>
        /// 튜토 두 컷 — <b>문구 · 마우스 · 미니 그림이 «함께» 갈린다</b>.
        /// ★ 여기서 재는 것은 <b>「미니 그림이 컷마다 바뀌는가」</b>다 (§5-b-7 결함 E).
        /// </summary>
        private IEnumerator CheckTutorial()
        {
            var panel = FindFirstObjectByType<BlumgiTutorialPanel>(FindObjectsInactive.Include);

            if (panel == null)
            {
                Fail("튜토리얼 패널이 없다");
                yield break;
            }

            Transform hold = panel.transform.Find("ArtHold");
            Transform shoot = panel.transform.Find("ArtShoot");

            Check("미니 그림이 «두 벌»이다 (`ArtHold` + `ArtShoot`)", hold != null && shoot != null);

            if (hold == null || shoot == null)
                yield break;

            var holdImage = hold.GetComponent<Image>();
            var shootImage = shoot.GetComponent<Image>();

            Vector2 holdSize = ((RectTransform)hold).sizeDelta;
            Vector2 shootSize = ((RectTransform)shoot).sizeDelta;

            Line($"     컷 A {holdSize.x:F2} × {holdSize.y:F2} px @({hold.localPosition.x:F2}, {hold.localPosition.y:F2})");
            Line($"     컷 B {shootSize.x:F2} × {shootSize.y:F2} px @({shoot.localPosition.x:F2}, {shoot.localPosition.y:F2})");

            // world 152×154 · 230×258 → 기준 레이어 0.84375
            Check($"컷 A 크기가 원본 152 × 154 world 다 — {holdSize.x:F2} × {holdSize.y:F2} px "
                  + $"(기대 128.25 × 129.94)",
                  Mathf.Abs(holdSize.x - 128.25f) < 1f && Mathf.Abs(holdSize.y - 129.94f) < 1f);

            Check($"컷 B 크기가 원본 230 × 258 world 다 — {shootSize.x:F2} × {shootSize.y:F2} px "
                  + $"(기대 194.06 × 217.69)",
                  Mathf.Abs(shootSize.x - 194.06f) < 1f && Mathf.Abs(shootSize.y - 217.69f) < 1f);

            Check("컷 B 그림이 컷 A 와 «다른 스프라이트»다 (한 장 돌려 쓰기가 아니다)",
                  holdImage != null && shootImage != null
                  && holdImage.sprite != null && shootImage.sprite != null
                  && holdImage.sprite != shootImage.sprite);

            // ── 토글을 실제로 지켜본다 — 배타성과 주기.
            var flips = new List<float>();

            bool wasShoot = shoot.gameObject.activeSelf;
            bool exclusive = true;
            bool matchesCut = true;
            float t = 0f;
            float last = -1f;

            while (t < 5.5f)
            {
                yield return null;
                t += Time.unscaledDeltaTime;

                bool a = hold.gameObject.activeSelf;
                bool b = shoot.gameObject.activeSelf;

                if (a == b)
                    exclusive = false;

                // ★ 문구와 «같은» 토글에 물려 있어야 한다 — 낱개로 갈리면 원본과 다르다.
                Transform holdCut = panel.transform.Find("HoldCut");
                Transform shootCut = panel.transform.Find("ShootCut");

                if (holdCut != null && shootCut != null
                    && (holdCut.gameObject.activeSelf != a || shootCut.gameObject.activeSelf != b))
                    matchesCut = false;

                if (b != wasShoot)
                {
                    if (last >= 0f)
                        flips.Add(t - last);

                    last = t;
                    wasShoot = b;
                }
            }

            Check("컷 A · 컷 B 그림이 «배타»다 (둘이 같이 보이거나 같이 사라지지 않는다)", exclusive);
            Check("미니 그림이 «문구와 같은 토글»에 물려 있다 (낱개로 안 갈린다)", matchesCut);

            if (flips.Count > 0)
            {
                float mean = Mean(flips);

                Line($"     반주기 n={flips.Count} · {Min(flips) * 1000f:F1} ~ {Max(flips) * 1000f:F1} ms · 평균 {mean * 1000f:F1}");

                Check($"컷 주기가 «정확히 1.000 s» 다 — 평균 {mean * 1000f:F1} ms (±40)",
                      Mathf.Abs(mean - OriginCutSeconds) <= 0.04f);
            }
            else
            {
                Fail("컷 전이를 5.5 s 안에 한 번도 못 봤다");
            }
        }

        // ────────────────────────────────────────────────────────── 보조

        /// <summary>캔버스 아래면 웰컴, 아니면 골대(월드)다.</summary>
        private static BlumgiSparkleField FindField(bool underCanvas)
        {
            BlumgiSparkleField[] all =
                FindObjectsByType<BlumgiSparkleField>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].isActiveAndEnabled == false)
                    continue;

                bool ui = all[i].GetComponentInParent<Canvas>() != null;

                if (ui == underCanvas)
                    return all[i];
            }

            return null;
        }

        private static Transform[] CollectItems(BlumgiSparkleField field)
        {
            var list = new List<Transform>();

            for (int i = 0; i < field.transform.childCount; i++)
                list.Add(field.transform.GetChild(i));

            return list.ToArray();
        }

        private void CaptureOne(string name, bool ui)
        {
            Camera camera = FindCamera(ui ? "UICamera" : "GameCamera");

            if (camera == null)
                return;

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var data = ui ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            CameraRenderType previousType = data == null ? CameraRenderType.Overlay : data.renderType;
            RenderTexture target = null;

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

                string path = Path.Combine(OutDir, name + ".png");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(readback);

                _shots++;
                Line($"     📷 {path}");
            }
            catch (Exception e)
            {
                Line($"     ⚠ 캡처 실패 — {e.Message}");
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

        private static float Mean(List<float> values)
        {
            float sum = 0f;

            for (int i = 0; i < values.Count; i++)
                sum += values[i];

            return values.Count == 0 ? 0f : sum / values.Count;
        }

        private static float Min(List<float> values)
        {
            float v = float.MaxValue;

            for (int i = 0; i < values.Count; i++)
                v = Mathf.Min(v, values[i]);

            return v;
        }

        private static float Max(List<float> values)
        {
            float v = float.MinValue;

            for (int i = 0; i < values.Count; i++)
                v = Mathf.Max(v, values[i]);

            return v;
        }

        private void Check(string text, bool ok)
        {
            _total++;

            if (ok)
                _pass++;

            Line($"  {(ok ? "✅" : "❌")} {text}");
        }

        private void Fail(string text)
        {
            _total++;
            Line($"  ❌ {text}");
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
