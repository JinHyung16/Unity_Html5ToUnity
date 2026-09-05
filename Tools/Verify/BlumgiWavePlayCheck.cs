using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 24회차 신설 — <b>WELCOME 글자 흔들림 «재는 도구»</b>.
    ///
    /// <para>
    /// 정본은 <c>04 §4-e-2</c> · 골든 표는 <c>07 §17-c-3</c> 이다. 이 도구는 그 표를 읽어 주는 <b>일회용</b>이라
    /// <b>이관이 끝나면 통째로 지운다</b> (CLAUDE.md 「재는 도구는 이관이 끝나면 지운다」).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>왜 필요했나</b> — 흔들림은 «정지 캡처에 안 찍히고», 개수·좌표 검사에도 안 잡힌다.
    /// 특히 <b>60 Hz 계단</b>은 「매 프레임 갱신」과 수치가 «거의» 같아서 눈으로도 놓치기 쉽다 (재발방지 #60).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 검사용 문을 뚫지 않았다</b> — <see cref="BlumgiWaveText"/> 가 이미 TMP 메시를
    /// 건드리므로 <b>그 결과인 버텍스 y 를 «읽기만»</b> 한다. 채널마다 평균을 빼서 «흔들림 성분»만 남긴다
    /// (기준선을 몰라도 진폭·주기·위상차·계단은 전부 판정된다).
    /// </para>
    ///
    /// <para>
    /// 실행: <c>PLAYMODE=1 GRAPHICS=1
    /// Tools/unity-batch.sh JinHyung.EditorTools.BlumgiWavePlayCheck.RunAll</c>
    /// </para>
    /// </summary>
    public static class BlumgiWavePlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiWavePlayCheck.Armed";
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

            var go = new GameObject("BlumgiWaveProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiWaveProbe>();
#endif
        }
    }

    /// <summary>실제 채록기.</summary>
    public sealed class BlumgiWaveProbe : MonoBehaviour
    {
        private const string ReportPath = "Temp/BlumgiWaveReport.txt";
        private const string OutDir = "Temp/BlumgiWave";

        private const int Width = 1920;
        private const int Height = 1080;

        /// <summary>채록 길이 — 주기 1.44 s 의 두 바퀴가 넘게.</summary>
        private const float SampleSeconds = 3.2f;

        private const float StepTimeoutSeconds = 40f;

        // ── 원본 실측 [24회차 · 정본 04 §4-e-2]
        private const float OriginalPeriod = 1.44f;
        private const float OriginalPhaseDegPerChar = 50f;
        private const float OriginalUpdateHz = 60f;

        private readonly StringBuilder _report = new StringBuilder(1 << 14);

        private readonly List<float> _times = new List<float>(1024);
        private readonly List<float[]> _samples = new List<float[]>(1024);

        private int _passed;
        private int _failed;
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
                    Line($"  ❌ 채록 중 예외: {e}");
                    _failed++;
                    break;
                }

                if (moved == false)
                    break;

                yield return body.Current;
            }

            Line(string.Empty);
            Line("========================================");
            Line($"═══ 글자 흔들림: {_passed}/{_passed + _failed} 통과 · 캡처 {_shots}장 · 전문 {ReportPath}");

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
                Line("  ❌ 씬에 BlumgiGameInitialize 가 없다");
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

            BlumgiGameRoot.Instance.GameFlow.ChangeScreen(EBlumgiScreenType.Welcome);

            yield return WaitSeconds(1.0f);

            // ── 흔드는 텍스트를 찾는다
            BlumgiWaveText wave = FindFirstObjectByType<BlumgiWaveText>(FindObjectsInactive.Exclude);

            if (wave == null)
            {
                Line("  ❌ WELCOME 화면에 BlumgiWaveText 가 «없다» — 프리팹을 다시 굽는다");
                _failed++;
                yield break;
            }

            var text = wave.GetComponent<TMP_Text>();

            if (text == null || text.textInfo == null || text.textInfo.characterCount == 0)
            {
                Line("  ❌ TMP_Text 가 비어 있다");
                _failed++;
                yield break;
            }

            Line($"  ▶ 대상 = {Path(wave.transform)} · 문자열 「{text.text}」");

            // ── 프레임마다 «보이는 글자»의 버텍스 y 를 읽는다
            var visible = new List<int>(16);
            TMP_TextInfo info = text.textInfo;

            for (int i = 0; i < info.characterCount; i++)
            {
                if (info.characterInfo[i].isVisible)
                    visible.Add(i);
            }

            if (visible.Count < 3)
            {
                Line($"  ❌ 보이는 글자가 {visible.Count}개뿐이다 — 채록이 성립하지 않는다");
                _failed++;
                yield break;
            }

            float t = 0f;

            while (t < SampleSeconds)
            {
                // ⚠ BlumgiWaveText 는 LateUpdate 에서 쓴다 — «다음 프레임 Update»에서 읽으면
                //   앞 프레임의 LateUpdate 결과가 메시에 그대로 남아 있다.
                // ⚠⚠ `WaitForEndOfFrame` 을 쓰면 «배치모드에서 영영 안 깨어난다» [실측 — 300 s 타임아웃].
                yield return null;

                info = text.textInfo;
                var row = new float[visible.Count];

                for (int k = 0; k < visible.Count; k++)
                {
                    TMP_CharacterInfo character = info.characterInfo[visible[k]];
                    Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                    int v = character.vertexIndex;

                    // 네 꼭짓점 평균 = 그 글자의 세로 중심
                    row[k] = (vertices[v].y + vertices[v + 1].y + vertices[v + 2].y + vertices[v + 3].y) * 0.25f;
                }

                _times.Add(t);
                _samples.Add(row);

                t += Time.unscaledDeltaTime;
            }

            Line($"  ▶ 채록 {_samples.Count}프레임 / {SampleSeconds:F2}s · 글자 {visible.Count}개" +
                 $" · dt 중앙 {Median(FrameDeltas()) * 1000f:F2} ms");
            Line(string.Empty);

            Analyze(visible.Count);

            // ── #60: «눈으로도» 본다. 연속 프레임을 몇 장 남긴다.
            // ⓐ «바로 붙은» 두 프레임 — 계단이면 그림이 «똑같아야» 한다
            yield return CaptureSeries("H", 2, 0f);

            // ⓑ 한 주기(1.44 s)를 12등분한 필름스트립 — 파도가 «어느 쪽으로» 흐르는지 눈으로 본다
            yield return CaptureSeries("P", 12, OriginalPeriod / 12f);
        }

        // ────────────────────────────────────────────────────────── 판정

        private void Analyze(int channels)
        {
            int n = _samples.Count;

            // ── 채널마다 평균을 빼서 «흔들림 성분»만 남긴다 (기준선을 몰라도 된다)
            var ac = new float[channels][];

            for (int c = 0; c < channels; c++)
            {
                ac[c] = new float[n];
                float sum = 0f;

                for (int i = 0; i < n; i++)
                    sum += _samples[i][c];

                float mean = sum / n;

                for (int i = 0; i < n; i++)
                    ac[c][i] = _samples[i][c] - mean;
            }

            // ── ① 60 Hz 계단 — «값이 바뀐 간격»이 원본과 같은가
            Line("── ① 60 Hz 계단 [원본 실측: 값 갱신 간격 중앙 16.7 ms · n=252]");

            var changeGaps = new List<float>(256);
            int held = 0;
            float last = _times[0];

            for (int i = 1; i < n; i++)
            {
                bool changed = false;

                for (int c = 0; c < channels; c++)
                {
                    if (Mathf.Abs(ac[c][i] - ac[c][i - 1]) > 1e-5f)
                    {
                        changed = true;
                        break;
                    }
                }

                if (changed)
                {
                    changeGaps.Add(_times[i] - last);
                    last = _times[i];
                }
                else
                {
                    held++;
                }
            }

            float medianGap = changeGaps.Count > 0 ? Median(changeGaps) : 0f;
            float dtMedian = Median(FrameDeltas());

            Line($"     프레임 {n} · 값이 «안 바뀐» 프레임 {held} · 값이 바뀐 횟수 {changeGaps.Count}");
            Line($"     값 갱신 간격 중앙 = {medianGap * 1000f:F2} ms   (원본 16.7 ms · 프레임 dt 중앙 {dtMedian * 1000f:F2} ms)");

            // 재생 fps 가 60 근처면 「계단」과 「매 프레임」이 구분되지 않는다 — 그 사실을 «판정»한다.
            bool canJudge = dtMedian < 1f / OriginalUpdateHz * 0.75f;

            if (canJudge == false)
            {
                Line($"     ⚠ **판정 불가** — 재생 프레임 간격({dtMedian * 1000f:F2} ms)이 계단폭(16.67 ms)보다 촘촘하지 않다.");
                Line($"        계단이 있는지 없는지 원리적으로 못 가른다 — 이 항목은 «미측정»이지 «통과»가 아니다.");
                Line($"        (계단 재현 여부는 코드 상수 UpdateHz = 60 과 §17-c-3 골든으로 본다)");
            }
            else
            {
                Check(held > n / 4,
                      $"계단이 «있다» — {n}프레임 중 {held}프레임이 앞 프레임과 «같은 값»이다",
                      $"계단이 «없다» — 매 프레임 값이 바뀐다 (원본보다 부드럽다)");

                Check(Mathf.Abs(medianGap - 1f / OriginalUpdateHz) < 0.004f,
                      $"갱신 간격 {medianGap * 1000f:F2} ms 가 원본 16.67 ms 와 맞는다",
                      $"갱신 간격 {medianGap * 1000f:F2} ms 가 원본 16.67 ms 와 «다르다»");
            }

            Line(string.Empty);

            // ── ② 주기 — 채널 0 의 영교차 간격
            Line("── ② 주기 [원본 실측: 1.44 s = 360/250 °/s]");

            float period = EstimatePeriod(ac[0], _times);

            if (period <= 0f)
            {
                Line("     ⚠ 영교차가 모자라 주기를 못 냈다 — 미측정");
            }
            else
            {
                Line($"     추정 주기 = {period:F4} s");
                Check(Mathf.Abs(period - OriginalPeriod) < 0.06f,
                      $"주기 {period:F4} s 가 원본 1.44 s 와 맞는다 (±0.06)",
                      $"주기 {period:F4} s 가 원본 1.44 s 와 «다르다»");
            }

            Line(string.Empty);

            // ── ③ 진폭이 채널마다 같은가 (원본 7/7 이 4.999)
            Line("── ③ 채널별 진폭 [원본 실측: 7/7 이 4.9989~4.9995 world · 채널 간 «같다»]");

            var amps = new float[channels];
            float ampMin = float.MaxValue;
            float ampMax = float.MinValue;

            // ⚠⚠ [사고] 처음엔 «RMS·√2» 로 냈다가 채널마다 4.05~4.34 로 «갈린다»는 오판을 냈다.
            //   채록 길이(3.2 s)가 주기(1.44 s)의 «정수 배»가 아니라 RMS 가 채널 위상에 따라 흔들린 것이다.
            //   ⇒ 한 주기만 넘으면 위상과 무관한 «peak-to-peak / 2» 로 잰다.
            for (int c = 0; c < channels; c++)
            {
                float lo = float.MaxValue;
                float hi = float.MinValue;

                for (int i = 0; i < n; i++)
                {
                    lo = Mathf.Min(lo, ac[c][i]);
                    hi = Mathf.Max(hi, ac[c][i]);
                }

                amps[c] = (hi - lo) * 0.5f;
                ampMin = Mathf.Min(ampMin, amps[c]);
                ampMax = Mathf.Max(ampMax, amps[c]);

                Line($"     ch{c} 진폭(p-p/2) = {amps[c]:F4} (캔버스 px)");
            }

            Check(ampMax - ampMin < ampMax * 0.06f,
                  $"채널 간 진폭이 «같다» — {ampMin:F4} ~ {ampMax:F4} (편차 {(ampMax - ampMin) / ampMax * 100f:F1} %)",
                  $"채널 간 진폭이 갈린다 — {ampMin:F4} ~ {ampMax:F4}");

            // ★ [해소 · 25회차] 배율이 닫혔다 — `WELCOME` 은 `Main` 레이어라 1.6875 다
            //   (뷰포트 1138.382×640 vs 기준 2276.766×1280 = 정확히 2배 · 스크린샷 픽셀 검산 오차 0.35 %).
            Line($"     참고 — 기대치 = 5.000 world × 1.6875(`Main`) = {5f * 1.6875f:F5} px [실측 25회차 · 닫힘]");
            Line($"     ⚠ 절대값은 여전히 «채점하지 않는다» — 캔버스 스케일러·서체 메트릭이 섞이는 자리라");
            Line($"        여기서 재는 것은 «채널 간 일치 · 주기 · 위상»이다. 배율 자체는 뷰포트로 닫았다.");
            Line(string.Empty);

            // ── ④ 글자 간 위상차 — 부호까지 [원본 실측: 뒤 글자가 +50.00°]
            Line("── ④ 글자 간 위상차 [원본 실측: 뒤 글자가 «늦다» +50.00° · 6/6]");

            if (period <= 0f)
            {
                Line("     ⚠ 주기를 못 내 위상차를 못 낸다 — 미측정");
            }
            else
            {
                float okCount = 0f;

                for (int c = 1; c < channels; c++)
                {
                    float lag = CrossLagSeconds(ac[c - 1], ac[c], _times, period);
                    float deg = lag / period * 360f;

                    while (deg > 180f) deg -= 360f;
                    while (deg < -180f) deg += 360f;

                    bool ok = Mathf.Abs(deg - OriginalPhaseDegPerChar) < 12f;

                    if (ok)
                        okCount++;

                    Line($"     ch{c - 1}→ch{c} 위상차 = {deg:+0.00;-0.00}°   (원본 +50.00°) {(ok ? "✅" : "❌")}");
                }

                Check(okCount == channels - 1,
                      $"글자 간 위상차 {channels - 1}/{channels - 1} 이 원본 +50.00° 와 맞는다 (부호 포함)",
                      $"글자 간 위상차가 {okCount}/{channels - 1} 만 맞는다 — ★ 부호가 뒤집히면 파도가 반대로 흐른다");
            }

            Line(string.Empty);
        }

        // ────────────────────────────────────────────────────────── 신호 처리

        private List<float> FrameDeltas()
        {
            var d = new List<float>(_times.Count);

            for (int i = 1; i < _times.Count; i++)
                d.Add(_times[i] - _times[i - 1]);

            return d;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0)
                return 0f;

            var copy = new List<float>(values);
            copy.Sort();

            return copy[copy.Count / 2];
        }

        /// <summary>상승 영교차 사이의 평균 간격 = 주기.</summary>
        private static float EstimatePeriod(float[] signal, List<float> times)
        {
            var crossings = new List<float>(16);

            for (int i = 1; i < signal.Length; i++)
            {
                if (signal[i - 1] < 0f && signal[i] >= 0f)
                {
                    float span = signal[i] - signal[i - 1];
                    float frac = Mathf.Approximately(span, 0f) ? 0f : -signal[i - 1] / span;
                    crossings.Add(Mathf.Lerp(times[i - 1], times[i], frac));
                }
            }

            if (crossings.Count < 2)
                return -1f;

            return (crossings[crossings.Count - 1] - crossings[0]) / (crossings.Count - 1);
        }

        /// <summary>b 가 a 보다 «얼마나 늦은가»(초). 상관 최대인 지연을 한 주기 안에서 찾는다.</summary>
        private static float CrossLagSeconds(float[] a, float[] b, List<float> times, float period)
        {
            float best = 0f;
            float bestScore = float.MinValue;
            float dt = (times[times.Count - 1] - times[0]) / (times.Count - 1);
            int maxShift = Mathf.Max(1, Mathf.RoundToInt(period / dt));

            for (int s = -maxShift; s <= maxShift; s++)
            {
                float score = 0f;
                int count = 0;

                for (int i = 0; i < a.Length; i++)
                {
                    int j = i + s;

                    if (j < 0 || j >= b.Length)
                        continue;

                    score += a[i] * b[j];
                    count++;
                }

                if (count == 0)
                    continue;

                score /= count;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = s * dt;
                }
            }

            // b[i+s] 가 a[i] 와 겹친다 ⇒ b 는 a 보다 s·dt 만큼 «앞선다» ⇒ 지연은 −s·dt
            return -best;
        }

        // ────────────────────────────────────────────────────────── 캡처 (#60 — 눈으로도 본다)

        private IEnumerator CaptureSeries(string prefix, int count, float spacingSeconds)
        {
            Line($"── ⑤ 캡처 {prefix} — {count}장 · 간격 {spacingSeconds * 1000f:F0} ms (#60 — «눈으로» 본다)");

            Camera ui = FindCamera("UICamera");

            if (ui == null)
            {
                Line("     ❌ UICamera 를 못 찾았다");
                _failed++;
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                if (spacingSeconds > 0f)
                    yield return WaitSeconds(spacingSeconds);
                else
                    yield return null;

                WriteOne(ui, System.IO.Path.Combine(OutDir, $"{prefix}{i:00}_welcome_ui.png"));
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

        private void WriteOne(Camera camera, string path)
        {
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
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

                _shots++;
                Line($"     📷 {path}");
            }
            catch (Exception e)
            {
                Line($"     ❌ 캡처 실패: {path} — {e.Message}");
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
        }

        // ────────────────────────────────────────────────────────── 보조

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);

            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }

            return sb.ToString();
        }

        private void Check(bool ok, string pass, string fail)
        {
            if (ok)
            {
                _passed++;
                Line("     ✅ " + pass);
            }
            else
            {
                _failed++;
                Line("     ❌ " + fail);
            }
        }

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
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, _report.ToString());

                // 원시 채록도 남긴다 — 바깥에서 다시 피팅할 수 있게.
                var csv = new StringBuilder(1 << 16);
                csv.AppendLine("t," + string.Join(",", ChannelHeaders()));

                for (int i = 0; i < _samples.Count; i++)
                {
                    csv.Append(_times[i].ToString("F6", CultureInfo.InvariantCulture));

                    for (int c = 0; c < _samples[i].Length; c++)
                    {
                        csv.Append(',');
                        csv.Append(_samples[i][c].ToString("F6", CultureInfo.InvariantCulture));
                    }

                    csv.AppendLine();
                }

                File.WriteAllText("Temp/BlumgiWaveRaw.csv", csv.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"전문을 못 썼다: {e.Message}");
            }
        }

        private string[] ChannelHeaders()
        {
            int c = _samples.Count > 0 ? _samples[0].Length : 0;
            var headers = new string[c];

            for (int i = 0; i < c; i++)
                headers[i] = "ch" + i;

            return headers;
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(BlumgiWavePlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }
}
