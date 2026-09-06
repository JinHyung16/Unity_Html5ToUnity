using System;
using System.Collections.Generic;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 투사체·불·보스·나무 — 원본을 «보고» 형태·색을 맞춘 재제작 (픽셀 복사 없음).
    /// 프레임이 많은 것(불덩이·유성·번개·보스)은 <b>결정적 절차</b>로 컷마다 변화를 준다.
    /// </summary>
    public static partial class UndeadSpriteBuilder
    {
        private static readonly Color BoltCore = Hex("FFFFFF");
        private static readonly Color BoltBlue = Hex("78C8FA");
        private static readonly Color BoltLight = Hex("C6E9FF");
        private static readonly Color BoltPink = Hex("E9A6F5");
        private static readonly Color FireYellow = Hex("FFE066");
        private static readonly Color FireOrange = Hex("FF9A2E");
        private static readonly Color FireRed = Hex("E2482E");
        private static readonly Color LogBrown = Hex("6B4A2E");
        private static readonly Color LogDark = Hex("4A3220");

        // ══════════════════════════════ 총알 — 36×16 · 10컷. 흰 머리 + 하늘색 꼬리(길어진다) + 분홍 불티

        private static Texture2D BakeBolt(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);

            for (int col = 0; col < a.Cols; col++)
            {
                int ox = col * a.FrameWidth;
                int hx = 28;
                int hy = 8;
                int tail = 10 + col * 2;                // 10 → 28

                // 꼬리 — 왼쪽으로 가늘어진다
                for (int i = 0; i < tail; i++)
                {
                    int x = hx - 3 - i;
                    double t = i / (double)tail;
                    int half = (int)Math.Round(3.0 * (1.0 - t));
                    Color c = t < 0.35 ? BoltLight : BoltBlue;

                    for (int y = -half; y <= half; y++)
                        Put(tex, ox + x, hy + y + (Noise(i, col, 7) % 3 == 0 ? 1 : 0) * (y == half ? 1 : 0), c);

                    if (Noise(i, col, 11) % 4 == 0 && x > 2)
                        Put(tex, ox + x, hy + (Noise(i, col, 13) % 2 == 0 ? half + 2 : -half - 2), BoltPink);
                }

                // 머리
                Disc(tex, ox + hx, hy, 4, BoltBlue);
                Disc(tex, ox + hx, hy, 3, BoltLight);
                Disc(tex, ox + hx, hy, 2, BoltCore);
                Put(tex, ox + hx + 5, hy, BoltLight);

                OutlineSilhouette(tex, ox, 0, a.FrameWidth, a.FrameHeight);
            }

            return tex;
        }

        // ══════════════════════════════ 불 — 24×24 · 3컷. 통나무 위 세 겹 불꽃

        private static Texture2D BakeFire(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);

            for (int col = 0; col < a.Cols; col++)
                DrawCampfire(tex, col * a.FrameWidth, 0, a.FrameWidth, col, 1.0);

            return tex;
        }

        /// <summary>꺼진 모닥불 [소스 <c>fire_inactive</c>] — 통나무만 남고 불꽃 자리에 재·연기.</summary>
        private static Texture2D BakeFireInactive(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cx = a.FrameWidth / 2;
            int baseY = 3;

            for (int i = 0; i < 12; i++)
            {
                Put(tex, cx - 6 + i, baseY + i / 4, LogBrown);
                Put(tex, cx - 6 + i, baseY + i / 4 + 1, LogDark);
                Put(tex, cx + 6 - i, baseY + i / 4, LogBrown);
                Put(tex, cx + 6 - i, baseY + i / 4 + 1, LogDark);
            }

            Color ash = Hex("6B6B72");
            Color smoke = Hex("9A9AA3");
            Ellipse(tex, cx, baseY + 5, 4, 2, ash);
            Ellipse(tex, cx + 1, baseY + 9, 2, 2, smoke);
            Ellipse(tex, cx - 1, baseY + 13, 1, 1, smoke);
            return tex;
        }

        private static void DrawCampfire(Texture2D tex, int ox, int oy, int size, int frame, double scale)
        {
            int cx = ox + size / 2;
            int baseY = oy + 3;

            // 통나무 둘 (교차)
            for (int i = 0; i < 12; i++)
            {
                Put(tex, cx - 6 + i, baseY + i / 4, LogBrown);
                Put(tex, cx - 6 + i, baseY + i / 4 + 1, LogDark);
                Put(tex, cx + 6 - i, baseY + i / 4, LogBrown);
                Put(tex, cx + 6 - i, baseY + i / 4 + 1, LogDark);
            }

            // 불꽃 — 빨강 › 주황 › 노랑. 컷마다 혀 끝이 흔들린다
            int flick = frame % 3 - 1;
            int r1 = (int)Math.Round(6 * scale);
            int r2 = (int)Math.Round(4.5 * scale);
            int r3 = (int)Math.Round(3 * scale);

            Ellipse(tex, cx, baseY + 6, r1, r1 + 1, FireRed);
            Ellipse(tex, cx + flick, baseY + 8, r2, r2 + 2, FireOrange);
            Ellipse(tex, cx - flick, baseY + 9, r3, r3 + 2, FireYellow);

            // 혀 끝 셋
            for (int t = -1; t <= 1; t++)
            {
                int tipX = cx + t * (r1 - 1) + (t == flick ? 1 : 0);
                int tipH = baseY + 6 + r1 + (t == 0 ? 3 : 1) + ((frame + t + 3) % 2);

                for (int y = baseY + 6; y <= tipH; y++)
                    Put(tex, tipX, y, y > tipH - 2 ? FireYellow : (t == 0 ? FireOrange : FireRed));
            }
        }

        // ══════════════════════════════ 불덩이 — 32×32 · 6컷. 심지가 커졌다 작아진다

        private static Texture2D BakeFireball(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int[] outer = { 10, 11, 12, 11, 10, 9 };
            int[] core = { 4, 5, 6, 5, 4, 3 };

            for (int col = 0; col < a.Cols; col++)
            {
                int ox = col * a.FrameWidth;
                int cx = a.FrameWidth / 2;
                int cy = a.FrameHeight / 2;

                Disc(tex, ox + cx, cy, outer[col], FireRed);
                Disc(tex, ox + cx, cy, outer[col] - 2, FireOrange);
                Disc(tex, ox + cx, cy, core[col], FireYellow);
                Disc(tex, ox + cx - 1, cy + 1, Math.Max(1, core[col] - 2), Hex("FFF6C8"));

                // 불티 — 결정적 잡음으로 컷마다 자리가 바뀐다
                for (int i = 0; i < 6; i++)
                {
                    double ang = (Noise(i, col, 3) % 360) * Math.PI / 180.0;
                    int d = outer[col] + 1 + Noise(i, col, 5) % 3;
                    Put(tex, ox + cx + (int)Math.Round(Math.Cos(ang) * d), cy + (int)Math.Round(Math.Sin(ang) * d),
                        i % 2 == 0 ? FireYellow : FireOrange);
                }

                OutlineSilhouette(tex, ox, 0, a.FrameWidth, a.FrameHeight);
            }

            return tex;
        }

        // ══════════════════════════════ 유성 — 48×32 · 8컷. 붉은 바위 + 왼쪽으로 뻗는 불꽃 꼬리

        private static Texture2D BakeMeteor(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Color rock = Hex("C84A3E");
            Color rockDark = Hex("8E2E28");
            Color crater = Hex("6E1E1A");

            for (int col = 0; col < a.Cols; col++)
            {
                int ox = col * a.FrameWidth;
                int rx = 36;
                int ry = 16;
                int tail = 16 + (col % 4) * 3;

                for (int i = 0; i < tail; i++)
                {
                    int x = rx - 8 - i;
                    double t = i / (double)tail;
                    int half = (int)Math.Round(7.0 * (1.0 - t));
                    Color c = t < 0.3 ? FireYellow : (t < 0.65 ? FireOrange : FireRed);
                    int wob = Noise(i, col, 17) % 3 - 1;

                    for (int y = -half; y <= half; y++)
                        Put(tex, ox + x, ry + y + (Math.Abs(y) == half ? wob : 0), c);
                }

                Disc(tex, ox + rx, ry, 9, rockDark);
                Disc(tex, ox + rx - 1, ry + 1, 7, rock);
                Disc(tex, ox + rx - 3, ry + 3, 2, crater);
                Disc(tex, ox + rx + 3, ry - 1, 1, crater);
                Put(tex, ox + rx + 1, ry + 5, crater);

                OutlineSilhouette(tex, ox, 0, a.FrameWidth, a.FrameHeight);
            }

            return tex;
        }

        // ══════════════════════════════ 번개 구슬 — 64×48 · 8컷. 분홍-흰 구슬 + 회전하는 번개 줄기

        private static Texture2D BakeLightning(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Color pinkDeep = Hex("C86BE0");

            for (int col = 0; col < a.Cols; col++)
            {
                int ox = col * a.FrameWidth;
                int cx = a.FrameWidth / 2;
                int cy = a.FrameHeight / 2;
                double baseAngle = col * (Math.PI / 4.0);

                // 바깥 흐림 → 구슬
                Disc(tex, ox + cx, cy, 12, Alpha(BoltPink, 0.45f));
                Disc(tex, ox + cx, cy, 9, BoltPink);
                Disc(tex, ox + cx, cy, 7, Hex("F6D6FB"));
                Disc(tex, ox + cx, cy, 5, BoltCore);

                // 번개 줄기 넷 — 지그재그. 컷마다 45° 돈다
                for (int s = 0; s < 4; s++)
                {
                    double ang = baseAngle + s * (Math.PI / 2.0);
                    int px = ox + cx;
                    int py = cy;

                    for (int i = 0; i < 12; i++)
                    {
                        double jag = ((Noise(i, s, col) % 3) - 1) * 0.5;
                        px = ox + cx + (int)Math.Round(Math.Cos(ang) * (8 + i) + Math.Cos(ang + Math.PI / 2) * jag * 3);
                        py = cy + (int)Math.Round(Math.Sin(ang) * (8 + i) * 0.7 + Math.Sin(ang + Math.PI / 2) * jag * 3);
                        Put(tex, px, py, i < 6 ? BoltCore : (i < 9 ? BoltPink : pinkDeep));
                    }
                }

                // 꼬리 잔상 (왼쪽)
                for (int i = 0; i < 10; i++)
                {
                    if (Noise(i, col, 23) % 3 != 0)
                        Put(tex, ox + cx - 14 - i, cy + (Noise(i, col, 29) % 7) - 3, i < 5 ? BoltPink : pinkDeep);
                }
            }

            return tex;
        }

        // ══════════════════════════════ 보스 darkSoul — 108×108 · 14×4. 검은 몸 · 주황 불꽃 뿔 · 날개

        private static readonly Dictionary<char, Color> SoulPal =
            Pal("d=141018 D=2E2636 o=FF8A2E y=FFD23A r=E23B3B w=FFFFFF");

        private static readonly string[] SoulBody =
        {
            "..........oo............",
            ".........oyyo...........",
            "........oyyyyo..........",
            "...o....kyyyyk....o.....",
            "..oyo..kddddddk..oyo....",
            "..kyk.kddddddddk.kyk....",
            "...kkkkddrddrddkkkk.....",
            "....kdddddddddddddk.....",
            "....kddDdddddddDddk.....",
            ".....kdddddddddddk......",
            "......kdddddddddk.......",
            "...kk..kdddddddk..kk....",
            "..kddk.kdddddddk.kddk...",
            ".kdddkkdddddddddkkdddk..",
            ".kddddddddddddddddddddk.",
            ".kdddddddddddddddddddk..",
            "..kddddddddddddddddddk..",
            "..kdddkdddddddddkdddk...",
            "...kkk.kdddddddk.kkk....",
            "........kdddddk.........",
            "........kdddddk.........",
            ".......kdddddddk........",
            ".......kddkkkddk........",
            "......kddk...kddk.......",
            "......kdk.....kdk.......",
            ".....kddk.....kddk......",
            ".....kdk.......kdk......",
            "....kdk.........kdk.....",
            "....kk...........kk.....",
        };

        /// <summary>날개를 든 컷 — 어깨 위로 뻗는다.</summary>
        private static readonly string[] SoulBodyWingsUp =
        {
            "..........oo............",
            ".........oyyo...........",
            "........oyyyyo..........",
            "...o....kyyyyk....o.....",
            "..oyo..kddddddk..oyo....",
            "..kyk.kddddddddk.kyk....",
            "kk.kkkkddrddrddkkkk..kk.",
            "kdk.kdddddddddddddk.kdk.",
            "kddkkddDdddddddDddkkddk.",
            ".kddddddddddddddddddddk.",
            "..kddddddddddddddddddk..",
            "...kkkkdddddddddkkkk....",
            ".......kdddddddk........",
            ".......kdddddddk........",
            ".......kdddddddk........",
            ".......kdddddddk........",
            "......kdddddddddk.......",
            "......kdddkdddkddk......",
            ".......kkkdddddkk.......",
            "........kdddddk.........",
            "........kdddddk.........",
            ".......kdddddddk........",
            ".......kddkkkddk........",
            "......kddk...kddk.......",
            "......kdk.....kdk.......",
            ".....kddk.....kddk......",
            ".....kdk.......kdk......",
            "....kdk.........kdk.....",
            "....kk...........kk.....",
        };

        private static Texture2D BakeDarkSoul(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Color shadow = new Color(0f, 0f, 0f, 0.35f);

            for (int row = 0; row < a.Rows; row++)
            {
                // 표의 0행이 «위»다 — 텍스처는 아래가 0 이므로 뒤집어 앉힌다
                int oy = (a.Rows - 1 - row) * a.FrameHeight;

                for (int col = 0; col < a.Cols; col++)
                {
                    int ox = col * a.FrameWidth;
                    int cx = ox + a.FrameWidth / 2;
                    bool wingsUp = row < 2 ? (col % 4 >= 2) : (row == 2 ? col >= 4 && col < 10 : false);
                    int bob = row < 2 ? (col % 7 < 4 ? 0 : 2) : 0;
                    float fade = row == 3 ? Mathf.Clamp01(1f - col / 10f) : 1f;

                    // 바닥 그림자
                    Ellipse(tex, cx, oy + 6, 16, 3, Alpha(shadow, shadow.a * fade));

                    string[] body = wingsUp ? SoulBodyWingsUp : SoulBody;
                    int bodyH = body.Length * 2;
                    int bodyW = body[0].Length * 2;

                    if (fade > 0f)
                    {
                        var tinted = new Dictionary<char, Color>(SoulPal.Count);

                        foreach (KeyValuePair<char, Color> kv in SoulPal)
                            tinted[kv.Key] = Alpha(kv.Value, kv.Value.a * fade);

                        BlitScaled(tex, cx - bodyW / 2, oy + 10 + bob, body, tinted, 2);
                    }

                    // 공격 컷 — 노란 초승달 베기 (앞쪽)
                    if (row == 2 && col >= 6 && col < 10)
                    {
                        int sweep = col - 6;
                        int r = 30 + sweep * 4;

                        for (int i = -40; i <= 40; i++)
                        {
                            double ang = (i / 40.0) * (Math.PI * 0.55) - Math.PI * 0.2 + sweep * 0.25;
                            int px = cx + (int)Math.Round(Math.Cos(ang) * r);
                            int py = oy + 30 + (int)Math.Round(Math.Sin(ang) * r * 0.6);
                            Put(tex, px, py, FireYellow);
                            Put(tex, px, py + 1, FireYellow);
                            Put(tex, px + 1, py, Alpha(FireYellow, 0.6f));
                        }
                    }

                    // 사망 컷 — 흩어지는 불티
                    if (row == 3)
                    {
                        for (int i = 0; i < 6 + col * 2; i++)
                        {
                            int px = cx + (Noise(i, col, 31) % 60) - 30;
                            int py = oy + 12 + (Noise(i, col, 37) % 70);
                            Put(tex, px, py, i % 3 == 0 ? FireOrange : (i % 3 == 1 ? FireYellow : Hex("E23B3B")));
                        }
                    }
                }
            }

            return tex;
        }

        // ══════════════════════════════ 사악한 나무 — 99×103. 33×34 도트를 3배로

        private static readonly string[] TreeMap =
        {
            "..........kkkkkkkkkkkkk..........",
            "........kkccccccccccccckk........",
            ".......kccccccccccccccccck.......",
            "......kcccccccccccccccccccck.....",
            "..kk.kBcccccccccccccccccccBk.kk..",
            ".kbbkkBBcccccccccccccccccBBkkbbk.",
            ".kbBBBBBBccccccccccccccccBBBBBbk.",
            "..kkBBBBBBBccccccccccccBBBBBBkk..",
            "....kBlBBBBBBBcccccccBBBBBBlBk...",
            "....kBlBBBBBBBBBBBBBBBBBBBBlBk...",
            "....kBlBBBffBBBBBBBBBBffBBBlBk...",
            "....kBlBBBfffBBBBBBBBfffBBBlBk...",
            "....kBlBBBBffBBBBBBBBffBBBBlBk...",
            "....kBBBBBBBBBBBBBBBBBBBBBBBBk...",
            "....kBBBBBBBBfBBBBBBfBBBBBBBBk...",
            "....kBlBBBBBBffBBBBffBBBBBBlBk...",
            "....kBlBBBBBffffffffffBBBBBlBk...",
            "....kBlBBBBfffBffffBfffBBBBlBk...",
            "....kBlBBBBBfBBBffBBBfBBBBBlBk...",
            "....kBlBBBBBBBBBBBBBBBBBBBBlBk...",
            "....kBBBBBBBBBBBBBBBBBBBBBBBBk...",
            "....kbBBBBBBBBBBBBBBBBBBBBBBbk...",
            "....kbBBBBBBBBBBBBBBBBBBBBBBbk...",
            "...kbbBBBBBBBBBBBBBBBBBBBBBBbbk..",
            "..kbbbBBBBBBBBBBBBBBBBBBBBBBbbbk.",
            ".kbbbkBBBBBBBBBBBBBBBBBBBBBBkbbbk",
            ".kbbk.kBBBBBBBBBBBBBBBBBBBBk.kbbk",
            "kbbk..kbBBBBBBBBBBBBBBBBBbk..kbbk",
            "kbk...kbbBBBBBBBBBBBBBBBbbk...kbk",
            "kk...kbbbbBBBBBBBBBBBBBbbbbk...kk",
            ".....kbbbbbbBBBBBBBBBbbbbbbk.....",
            "....kbbbbbbbbbbbbbbbbbbbbbbbk....",
            "....kkkkkkkkkkkkkkkkkkkkkkkkk....",
            ".................................",
            ".................................",
        };

        private enum ETreeLook { Active, Activated, Inactive }

        /// <summary>
        /// 사악한 나무 — 위가 뻥 뚫린 그루터기에 얼굴. 세 그림 [원본 «보고»]: 평소 · <b>켜짐(흰 테두리 — 히어로가 150 안)</b> · 터진 뒤(바랜 색).
        /// </summary>
        private static Texture2D BakeTree(UndeadArtData a, ETreeLook look)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Dictionary<char, Color> pal = look == ETreeLook.Inactive
                ? Pal("B=9C7A6E b=70544C l=B8968A c=3A2A40 f=5A4258")
                : Pal("B=C4653A b=8E4426 l=E0885A c=2A1533 f=3A1F3F");

            Ellipse(tex, a.SheetWidth / 2, 6, 40, 5, new Color(0f, 0f, 0f, 0.3f));
            BlitScaled(tex, 0, 1, TreeMap, pal, 3);

            if (look == ETreeLook.Activated)
                OutlineSilhouette(tex, 0, 0, a.SheetWidth, a.SheetHeight, Color.white);

            return tex;
        }
    }
}
