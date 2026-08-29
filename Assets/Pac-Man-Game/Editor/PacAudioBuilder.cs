using System;
using System.IO;
using JinHyung.Core;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 원본이 <b>Web Audio 오실레이터로 «합성»하던 소리</b>를 wav 로 굽는다 (확정 B).
    ///
    /// <para>
    /// ⚠ <b>원본에 오디오 파일이 0개다.</b> 파형·주파수·게인·엔벨로프가 전부 코드에 있고,
    /// 그 값이 <c>05_연출.md</c> 「사운드」 표의 정본이다 — <b>여기 상수는 그 표를 옮긴 것</b>이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 이것은 <b>「구성하는 도구」</b>다 — 지우지 않는다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.PacAudioBuilder.BuildAll</c></para>
    /// </summary>
    public static class PacAudioBuilder
    {
        private const string OutDir = "Assets/Pac-Man-Game/Art/Audio";
        private const int SampleRate = 44100;

        public static void BuildAll()
        {
            Directory.CreateDirectory(OutDir);

            WriteWav("sfx_pellet.wav", BuildPellet());
            WriteWav("sfx_death.wav", BuildDeath());
            WriteWav("bgm_siren.wav", BuildSiren());

            AssetDatabase.Refresh();
            Log.Success("Pac-Man 오디오 3종을 구웠다 (펠릿 · 사망 · 사이렌 루프)");
        }

        // ────────────────────────────── ① 펠릿 (script.js:119~135)

        /// <summary>square 680Hz · 게인 0.12 → 0.01 지수 감쇠 · 0.06초.</summary>
        private static float[] BuildPellet()
        {
            const float Seconds = 0.06f;
            const float Frequency = 680f;
            const float GainFrom = 0.12f;
            const float GainTo = 0.01f;

            int count = Mathf.RoundToInt(SampleRate * Seconds);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float gain = ExponentialRamp(GainFrom, GainTo, t / Seconds);
                samples[i] = Square(Frequency * t) * gain;
            }

            return samples;
        }

        // ────────────────────────────── ② 사망 (script.js:137~154)

        /// <summary>sawtooth 220 → 55Hz 지수 하강 · 게인 0.15 → 0.01 · 0.6초.</summary>
        private static float[] BuildDeath()
        {
            const float Seconds = 0.6f;
            const float FreqFrom = 220f;
            const float FreqTo = 55f;
            const float GainFrom = 0.15f;
            const float GainTo = 0.01f;

            int count = Mathf.RoundToInt(SampleRate * Seconds);
            var samples = new float[count];

            // ⚠ 주파수가 «변하는» 오실레이터는 위상을 누적해야 한다.
            //   매 샘플 sin(f*t) 로 계산하면 주파수가 바뀌는 순간 위상이 튀어 «딸깍」이 생긴다.
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float frequency = ExponentialRamp(FreqFrom, FreqTo, t / Seconds);
                float gain = ExponentialRamp(GainFrom, GainTo, t / Seconds);

                samples[i] = Sawtooth(phase) * gain;
                phase += frequency / SampleRate;
            }

            return samples;
        }

        // ────────────────────────────── ③ 배경 사이렌 (script.js:94~117)

        /// <summary>
        /// sawtooth · 게인 0.04 고정 · <c>freq = 180 + 130 * sin(t * 0.85)</c>.
        ///
        /// <para>
        /// 변조 주기가 <c>2π / 0.85 ≈ 7.39초</c> 라 <b>한 주기를 루프로</b> 굽는다.
        /// ⚠ 원본은 <c>Date.now()</c> 기반이라 «켠 시점과 무관한 절대 위상」이다 —
        /// 루프로 만드는 순간 그 성질은 사라진다. <b>「의도된 차이」로 등재한다.</b>
        /// </para>
        /// </summary>
        private static float[] BuildSiren()
        {
            const float ModulationRate = 0.85f;
            const float FreqBase = 180f;
            const float FreqSwing = 130f;
            const float Gain = 0.04f;

            float seconds = 2f * Mathf.PI / ModulationRate;   // ≈ 7.39
            int count = Mathf.RoundToInt(SampleRate * seconds);
            var samples = new float[count];

            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float frequency = FreqBase + FreqSwing * Mathf.Sin(t * ModulationRate);

                samples[i] = Sawtooth(phase) * Gain;
                phase += frequency / SampleRate;
            }

            return samples;
        }

        // ────────────────────────────── 파형

        /// <summary>Web Audio <c>square</c> — 위상 전반부 +1, 후반부 -1.</summary>
        private static float Square(float phase)
        {
            return (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;
        }

        /// <summary>Web Audio <c>sawtooth</c> — 위상 0~1 을 -1~+1 로 선형.</summary>
        private static float Sawtooth(float phase)
        {
            return 2f * (phase - Mathf.Floor(phase)) - 1f;
        }

        /// <summary>
        /// Web Audio <c>exponentialRampToValueAtTime</c> 과 같은 곡선.
        /// ⚠ 지수 램프는 <b>0 을 지날 수 없다</b> — 원본이 0.01 로 끝내는 이유가 이것이다.
        /// </summary>
        private static float ExponentialRamp(float from, float to, float t01)
        {
            float t = Mathf.Clamp01(t01);
            return from * Mathf.Pow(to / from, t);
        }

        // ────────────────────────────── wav

        /// <summary>16bit PCM 모노 wav. 유니티가 그대로 임포트한다.</summary>
        private static void WriteWav(string fileName, float[] samples)
        {
            int dataSize = samples.Length * 2;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });

                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);                       // fmt 청크 크기
                writer.Write((short)1);                 // PCM
                writer.Write((short)1);                 // 모노
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);           // 초당 바이트
                writer.Write((short)2);                 // 블록 정렬
                writer.Write((short)16);                // 비트 깊이

                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    // ⚠ 클리핑을 «먼저» 한다. 넘친 값을 그대로 변환하면 소리가 뒤집힌다.
                    float v = Mathf.Clamp(samples[i], -1f, 1f);
                    writer.Write((short)Mathf.RoundToInt(v * short.MaxValue));
                }

                File.WriteAllBytes(Path.Combine(OutDir, fileName), stream.ToArray());
            }
        }
    }
}
