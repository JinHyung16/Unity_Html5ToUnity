using System;
using System.IO;
using JinHyung.Core;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 원본이 <b>캔버스에 직접 그리던 것</b>을 스프라이트로 굽는다
    /// (<c>05_연출.md</c> 드로잉 전수표가 정본).
    ///
    /// <para>
    /// ⚠ <b>원본에 이미지 파일이 0개다.</b> 가져올 것이 없어 전부 만든다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 이것은 <b>「구성하는 도구」</b>다 — 지우지 않는다. 없으면 아트를 다시 못 굽는다.
    /// <b>이 게임 전용</b>이라 게임 폴더에 산다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.PacSpriteBuilder.BuildAll</c></para>
    /// </summary>
    public static class PacSpriteBuilder
    {
        private const string OutDir = "Assets/Pac-Man-Game/Art";

        /// <summary>곡선 계단을 없애는 슈퍼샘플 배율.</summary>
        private const int SuperSample = 4;

        // ── 환산 배율 1.8 (04_UIUX규칙.md). 원본 칸 20 → 36.
        private const float Scale = 1.8f;
        private const int CellSize = 36;

        /// <summary>원본 벽은 <b>칸보다 작다</b> (16 / 20 = 0.8). 통로가 넓은 것이 이 게임의 모양이다.</summary>
        private const float WallRatio = 16f / 20f;

        /// <summary>팩맨·유령 반지름. 원본 <c>corridorTile * 0.6</c> = 12 → ×1.8.</summary>
        private const float EntityRadius = 12f * Scale;

        /// <summary>팩맨·유령 텍스처 한 변. 지름 43.2 가 들어가야 해서 칸보다 크다. 4의 배수.</summary>
        private const int EntitySize = 48;

        /// <summary>
        /// 입 벌림 프레임 수. <b>원본은 연속(sin)이라 이 수가 곧 파리티다.</b>
        /// 8은 「의도된 차이」로 등재한다 — 원본과 «같은 값»이 아니라 «같아 보이는 근사»다.
        /// </summary>
        public const int ChompFrames = 8;

        // ── 색 (05_연출.md 색 전수표)
        private static readonly Color WallFill = Hex("001B4D");
        private static readonly Color Cyan = Hex("00FFFF");
        private static readonly Color Yellow = Hex("FFD966");

        public static void BuildAll()
        {
            Directory.CreateDirectory(OutDir);

            WritePng("tile_wall.png", BuildWall());
            WritePng("tile_pellet.png", BuildDot(2f));
            WritePng("tile_power.png", BuildDot(4f));

            for (int i = 0; i < ChompFrames; i++)
                WritePng($"pac_{i}.png", BuildPacman(i));

            // ⚠ 유령은 «흰색으로 한 장» 굽고 런타임에 물들인다.
            //   둘은 모양이 같고 색만 다르다 — 같은 모양을 색마다 들고 있지 않는다.
            WritePng("ghost.png", BuildGhost());

            AssetDatabase.Refresh();
            Log.Success($"Pac-Man 스프라이트 {3 + ChompFrames + 1}종을 구웠다 (칸 {CellSize} · 배율 {Scale})");
        }

        // ────────────────────────────── 벽

        /// <summary>
        /// 원본 <c>drawMap</c> 의 벽 (<c>script.js:348~353</c>) —
        /// 채움 사각 + <b>안쪽으로 2px 들여 그린 테두리</b>.
        /// </summary>
        private static Texture2D BuildWall()
        {
            float wall = CellSize * WallRatio;          // 28.8
            float margin = (CellSize - wall) * 0.5f;    // 3.6

            // ★ 원본 strokeRect 는 «선 중심이 rect 경계»다. rect 가 벽 시작 +2px,
            //   lineWidth 2 이므로 선 밴드는 벽 시작 기준 [+1, +3] 이다.
            //   ⚠ 「rect 위치부터 선 폭만큼」으로 구우면 1px 안쪽으로 밀린다 (G4 실측: 구조 46.5%).
            float bandStart = margin + 1f * Scale;      // +1px → 1.8
            float bandEnd = margin + 3f * Scale;        // +3px → 5.4

            return Bake(CellSize, (x, y) =>
            {
                bool inWall = x >= margin && x <= CellSize - margin
                              && y >= margin && y <= CellSize - margin;

                if (inWall == false)
                    return Color.clear;

                float fromLeft = Mathf.Min(x - 0f, CellSize - x);
                float fromTop = Mathf.Min(y - 0f, CellSize - y);
                float edge = Mathf.Min(fromLeft, fromTop);   // 벽 사각형 가장자리까지 거리(칸 기준)

                bool onBorder = edge >= bandStart && edge <= bandEnd;
                return onBorder ? Cyan : WallFill;
            });
        }

        // ────────────────────────────── 펠릿

        /// <summary>원본 펠릿(반지름 2) · 파워펠릿(반지름 4). 칸 중앙에 원 하나.</summary>
        private static Texture2D BuildDot(float originRadius)
        {
            float radius = originRadius * Scale;
            float center = CellSize * 0.5f;

            return Bake(CellSize, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;
                return dx * dx + dy * dy <= radius * radius ? Yellow : Color.clear;
            });
        }

        // ────────────────────────────── 팩맨

        /// <summary>
        /// 원본 <c>drawPacman</c> (<c>script.js:386~418</c>) — 입을 벌린 부채꼴.
        ///
        /// <para>
        /// 입 벌림 = <c>0.08 + 0.28 * (0.5 + 0.5 * sin(t * 18))</c> (라디안).
        /// 프레임 <paramref name="frame"/> 은 그 sin 한 주기를 <see cref="ChompFrames"/> 등분한 것이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>오른쪽을 본 모습으로 굽는다.</b> 방향 회전은 런타임이 한다 —
        /// 방향마다 굽으면 같은 그림을 네 벌 들게 된다.
        /// </para>
        /// </summary>
        private static Texture2D BuildPacman(int frame)
        {
            float phase = frame / (float)ChompFrames * Mathf.PI * 2f;
            float mouth = 0.08f + 0.28f * (0.5f + 0.5f * Mathf.Sin(phase));

            float center = EntitySize * 0.5f;

            return Bake(EntitySize, (x, y) =>
            {
                float dx = x - center;
                float dy = y - center;

                if (dx * dx + dy * dy > EntityRadius * EntityRadius)
                    return Color.clear;

                // ⚠ 텍스처 y 는 위로 커지고 캔버스 y 는 아래로 커진다 — 각도 부호가 뒤집힌다.
                float angle = Mathf.Atan2(-dy, dx);

                if (angle < 0f)
                    angle += Mathf.PI * 2f;

                // 입은 0 라디안(오른쪽)을 중심으로 ±mouth 만큼 «비어» 있다.
                bool inMouth = angle < mouth || angle > Mathf.PI * 2f - mouth;
                return inMouth ? Color.clear : Yellow;
            });
        }

        // ────────────────────────────── 유령

        /// <summary>
        /// 원본 <c>drawGhost</c> (<c>script.js:420~446</c>) — 반원(위) + 사각(아래) + 눈.
        /// <b>흰색으로 굽는다.</b> 색은 런타임 tint 다.
        /// </summary>
        private static Texture2D BuildGhost()
        {
            float r = EntityRadius;
            float cx = EntitySize * 0.5f;

            // 원본은 (gx, gy) 를 중심으로 «위 반원 + 아래로 r 만큼 사각»을 그린다.
            // 그래서 몸 전체 높이가 2r 이고, 텍스처 한가운데에 두어야 위아래가 안 잘린다.
            // ⚠ 중심을 아래로 내리면 «몸 아래가 잘린다» (실측: 불투명 1090 — 기대 1483).
            float cy = EntitySize * 0.5f;

            // ⚠ `Bake` 의 샘플 좌표는 «이미 캔버스 방향»(y 가 아래로 큼)이다.
            //   여기서 또 뒤집으면 유령이 «거꾸로» 선다 (실측: 반원이 아래로 볼록했다).
            return Bake(EntitySize, (x, y) =>
            {
                float dx = x - cx;
                float dy = y - cy;

                bool inHead = dy <= 0f && dx * dx + dy * dy <= r * r;
                bool inBody = dy > 0f && dy <= r && Mathf.Abs(dx) <= r;

                if (inHead == false && inBody == false)
                    return Color.clear;

                // 눈 — 흰자 안의 «구멍»으로 만든다. 색을 tint 로 주므로 눈은 알파로 표현한다.
                float eyeY = cy - r * 0.25f;
                float py = y;
                float eyeR = r * 0.25f;
                float pupilR = r * 0.125f;

                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = cx + side * r / 3f;
                    float ddx = x - ex;
                    float ddy = py - eyeY;
                    float d2 = ddx * ddx + ddy * ddy;

                    // 눈동자는 몸 색이 비치면 안 되므로 «완전 투명»으로 뚫는다.
                    if (d2 <= pupilR * pupilR)
                        return Color.clear;

                    if (d2 <= eyeR * eyeR)
                        return Color.clear;
                }

                return Color.white;
            });
        }

        // ────────────────────────────── 굽기

        /// <summary>
        /// 슈퍼샘플로 덮개를 평균낸다 — 이진 판정을 그대로 쓰면 곡선에 계단이 남는다.
        /// </summary>
        private static Texture2D Bake(int size, Func<float, float, Color> sample)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[size * size];

            float step = 1f / SuperSample;
            float half = step * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int sy = 0; sy < SuperSample; sy++)
                    {
                        for (int sx = 0; sx < SuperSample; sx++)
                        {
                            Color c = sample(x + sx * step + half, y + sy * step + half);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    int n = SuperSample * SuperSample;
                    a /= n;

                    // 알파로 나눠 «색»을 되살린다 (프리멀티플라이 해제).
                    Color32 result = a <= 0f
                        ? new Color32(255, 255, 255, 0)
                        : new Color32(
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(r / n / a) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(g / n / a) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(b / n / a) * 255f),
                            (byte)Mathf.RoundToInt(a * 255f));

                    // ⚠ 유니티 텍스처는 아래에서 위로 쌓인다 — 세로를 뒤집어 넣는다.
                    pixels[(size - 1 - y) * size + x] = result;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void WritePng(string fileName, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(OutDir, fileName), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
