using System.IO;
using JinHyung.Core;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 원본이 <b>CSS 로 그리던 것</b>을 스프라이트로 굽는다
    /// (<c>06_리소스.md</c> 「② 제작해야 하는 리소스」).
    ///
    /// <para>
    /// <c>border-radius</c> → 9-slice 둥근 사각형 ·
    /// <c>box-shadow inset</c> → 보드 스프라이트에 구워 넣는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이것은 「구성하는 도구」다 — 지우지 않는다.</b>
    /// 없으면 아트를 다시 못 굽고 「사람이 그림 편집기로」로 돌아간다.
    /// 그리고 <b>이 게임 전용</b>이라 게임 폴더에 산다 (공용 아님).
    /// </para>
    ///
    /// <para>
    /// 실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UiSpriteBuilder.BuildAll</c>
    /// </para>
    /// </summary>
    public static class UiSpriteBuilder
    {
        private const string OutDir = "Assets/Candy-Crush-Game/Art/UI";

        /// <summary>곡선 계단을 없애는 슈퍼샘플 배율.</summary>
        private const int SuperSample = 4;

        // ── 보드 (원본 .grid — style.css:12~23)
        private const int BoardSize = 128;
        private const int BoardRadius = 16;      // 원본 10 × 1.6
        private const int BoardPad = 24;         // 블러용 여백 (아래)

        /// <summary>
        /// 원본 값은 <c>rgba(109,127,151,0.5)</c> = <b>128</b> 이다.
        /// 그대로 구우면 <b>원본보다 밝게 나온다</b> — 원본은 감마 공간에서 섞고
        /// 우리 프로젝트는 Linear 에서 섞기 때문이다.
        ///
        /// <para>
        /// 154 는 눈대중이 아니라 계산이다. 대표 배경 위에서<br/>
        /// 감마 0.5 합성 = (181,168,147) · Linear 154/255 합성 = (184,166,148) — <b>최대 채널차 3.4</b>.<br/>
        /// 128 로 두면 (197,174,148) 로 <b>16.2</b> 만큼 밝다.
        /// </para>
        ///
        /// <para>⚠ 원본 값 자체는 문서와 코드에 <b>0.5 그대로</b> 남아 있다. 보정은 여기에만 있다.</para>
        /// </summary>
        private const int BoardAlpha = 154;

        /// <summary>
        /// 원본 그림자는 <c>rgba(0,0,0,0.5)</c> = 128. 위와 같은 이유로 Linear 에서는 약하게 나온다.
        /// 230 은 원본 캡처의 위·옆 가장자리 실측색과 <b>최대 채널차 6</b> 이 되는 값이다.
        /// </summary>
        private const int ShadowStrength = 230;

        /// <summary>원본 <c>0 2px 4px inset</c> — 오프셋 2×1.6 ≒ 3 · 블러 4×1.6 → σ 3.2.</summary>
        private const int ShadowOffsetY = 3;
        private const float ShadowSigma = 3.2f;

        public static void BuildAll()
        {
            Directory.CreateDirectory(OutDir);

            // ── 흰색 둥근 사각형 3종. 색은 런타임에 Image.color 로 물들인다.
            //    ⚠ 색을 구우면 같은 모양을 색마다 따로 들고 있어야 한다.
            foreach (int radius in new[] { 8, 16, 32 })
                WritePng($"ui_round_{radius}.png", WhiteRound(radius));

            WritePng("ui_board.png", Board());

            AssetDatabase.Refresh();
            Log.Success("UI 스프라이트 4종을 구웠다 — 9-slice 경계·PPU 는 ArtImportSetup 이 붙인다");
        }

        // ────────────────────────────── 둥근 사각형

        /// <summary>
        /// 둥근 사각형의 «덮개 비율». 슈퍼샘플로 재서 평균낸다 —
        /// 이진 마스크를 그대로 줄이면 모서리에 계단이 남는다.
        /// </summary>
        private static float[] RoundedMask(int size, int radius)
        {
            int big = size * SuperSample;
            int bigRadius = radius * SuperSample;
            var mask = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;

                    for (int sy = 0; sy < SuperSample; sy++)
                    {
                        for (int sx = 0; sx < SuperSample; sx++)
                        {
                            int px = x * SuperSample + sx;
                            int py = y * SuperSample + sy;

                            if (IsInsideRounded(px, py, big - 1, big - 1, bigRadius))
                                inside++;
                        }
                    }

                    mask[y * size + x] = inside / (float)(SuperSample * SuperSample);
                }
            }

            return mask;
        }

        /// <summary>모서리를 원으로 깎은 사각형 안인가. 모서리 밖은 «원 밖»이다.</summary>
        private static bool IsInsideRounded(int x, int y, int maxX, int maxY, int radius)
        {
            if (x < 0 || y < 0 || x > maxX || y > maxY)
                return false;

            int cx = Mathf.Clamp(x, radius, maxX - radius);
            int cy = Mathf.Clamp(y, radius, maxY - radius);

            int dx = x - cx;
            int dy = y - cy;

            return dx * dx + dy * dy <= radius * radius;
        }

        private static Texture2D WhiteRound(int radius)
        {
            int size = radius * 2 + 8;
            float[] mask = RoundedMask(size, radius);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(mask[i] * 255f));

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // ────────────────────────────── 보드

        private static Texture2D Board()
        {
            const int Size = BoardSize;
            float[] mask = RoundedMask(Size, BoardRadius);

            // ── 안쪽 그림자
            //    ⚠ «여백을 둔 캔버스»에서 블러하고 잘라낸다. 이미지 경계에 딱 붙여 놓고 블러하면
            //      바깥도 같은 값으로 늘려 잡아 «옆면에 그림자가 아예 안 생긴다» —
            //      원본은 옆에도 든다 (G4 실측: 옆 가장자리 실효알파 0.62 vs 보정 전 0.50).
            int padded = Size + BoardPad * 2;
            var inner = new float[padded * padded];

            for (int y = 0; y < padded; y++)
            {
                for (int x = 0; x < padded; x++)
                {
                    // 원본 offset(0, +2)×1.6 — 아래로 밀린 만큼 «위쪽»에 그림자가 남는다.
                    int localX = x - BoardPad;
                    int localY = y - BoardPad - ShadowOffsetY;

                    inner[y * padded + x] =
                        IsInsideRounded(localX, localY, Size - 1, Size - 1, BoardRadius) ? 1f : 0f;
                }
            }

            inner = Blur(inner, padded, ShadowSigma);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float coverage = mask[y * Size + x];
                    float baseAlpha = coverage * (BoardAlpha / 255f);

                    float insideValue = inner[(y + BoardPad) * padded + (x + BoardPad)];
                    float shadowAlpha = coverage * (1f - insideValue) * (ShadowStrength / 255f);

                    // 검은 그림자를 판 위에 얹는다 (알파 합성).
                    float outAlpha = shadowAlpha + baseAlpha * (1f - shadowAlpha);
                    Color32 color;

                    if (outAlpha <= 0f)
                    {
                        color = new Color32(109, 127, 151, 0);
                    }
                    else
                    {
                        // 그림자는 (0,0,0) 이라 판 색만 남는다.
                        float weight = baseAlpha * (1f - shadowAlpha) / outAlpha;

                        color = new Color32((byte)Mathf.RoundToInt(109 * weight),
                                            (byte)Mathf.RoundToInt(127 * weight),
                                            (byte)Mathf.RoundToInt(151 * weight),
                                            (byte)Mathf.RoundToInt(outAlpha * 255f));
                    }

                    // ⚠ 유니티 텍스처는 «아래에서 위로» 쌓인다 — 세로를 뒤집어 넣는다.
                    pixels[(Size - 1 - y) * Size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>분리형 가우시안. 가장자리는 «바깥으로 늘려» 잡는다.</summary>
        private static float[] Blur(float[] source, int size, float sigma)
        {
            int radius = Mathf.CeilToInt(sigma * 3f);
            var kernel = new float[radius * 2 + 1];
            float sum = 0f;

            for (int i = -radius; i <= radius; i++)
            {
                float weight = Mathf.Exp(-(i * i) / (2f * sigma * sigma));
                kernel[i + radius] = weight;
                sum += weight;
            }

            for (int i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            var horizontal = new float[source.Length];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float acc = 0f;

                    for (int i = -radius; i <= radius; i++)
                        acc += source[y * size + Mathf.Clamp(x + i, 0, size - 1)] * kernel[i + radius];

                    horizontal[y * size + x] = acc;
                }
            }

            var result = new float[source.Length];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float acc = 0f;

                    for (int i = -radius; i <= radius; i++)
                        acc += horizontal[Mathf.Clamp(y + i, 0, size - 1) * size + x] * kernel[i + radius];

                    result[y * size + x] = acc;
                }
            }

            return result;
        }

        private static void WritePng(string fileName, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(OutDir, fileName), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
