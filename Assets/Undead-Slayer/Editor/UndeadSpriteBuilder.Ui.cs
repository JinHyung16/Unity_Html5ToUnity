using System;
using System.Collections.Generic;
using JinHyung.Data;
using JinHyung.UndeadSlayer;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// UI 패널·버튼·아이콘·말풍선·지형 타일셋 — 원본을 «보고» 맞춘 재제작 (픽셀 복사 없음).
    /// <para>나인슬라이스는 <b>테두리 + 안쪽</b>만 있으면 늘어난다 — 임포터가 표의 <c>NineSlice</c> 로 테두리를 준다.</para>
    /// </summary>
    public static partial class UndeadSpriteBuilder
    {
        private static readonly Color Paper = Hex("F0DFA6");
        private static readonly Color PaperDark = Hex("C9A85C");
        private static readonly Color PaperLight = Hex("FBF1CC");

        // ══════════════════════════════ 말풍선 — 두루마리 (가로 140×42 · 세로 140×102)

        private static Texture2D BakeScroll(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int w = a.SheetWidth;
            int h = a.SheetHeight;
            bool wide = h < 60;

            if (wide)
            {
                // 가운데 종이 + 양 끝 세로 두루마리
                Rect(tex, 10, 6, w - 20, h - 12, Paper);
                Rect(tex, 10, h - 8, w - 20, 2, PaperLight);
                Rect(tex, 10, 6, w - 20, 2, PaperDark);
                RoundRect(tex, 0, 0, 14, h, 5, PaperDark);
                RoundRect(tex, 2, 2, 10, h - 4, 4, Paper);
                RoundRect(tex, w - 14, 0, 14, h, 5, PaperDark);
                RoundRect(tex, w - 12, 2, 10, h - 4, 4, Paper);
            }
            else
            {
                // 가운데 종이 + 위아래 가로 두루마리
                Rect(tex, 6, 10, w - 12, h - 20, Paper);
                Rect(tex, 6, 10, 2, h - 20, PaperDark);
                Rect(tex, w - 8, 10, 2, h - 20, PaperLight);
                RoundRect(tex, 0, h - 14, w, 14, 5, PaperDark);
                RoundRect(tex, 2, h - 12, w - 4, 10, 4, Paper);
                RoundRect(tex, 0, 0, w, 14, 5, PaperDark);
                RoundRect(tex, 2, 2, w - 4, 10, 4, Paper);
            }

            OutlineSilhouette(tex, 0, 0, w, h);
            return tex;
        }

        // ══════════════════════════════ 나인슬라이스 패널·바·버튼

        /// <summary>테두리 + 안쪽 — 6×6·16×16 짜리를 크게 늘려 쓴다 [실측].</summary>
        private static Texture2D BakeFrame9(UndeadArtData a, Color fill, Color border, int thickness)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Rect(tex, 0, 0, a.SheetWidth, a.SheetHeight, fill);

            for (int t = 0; t < thickness; t++)
            {
                Rect(tex, t, t, a.SheetWidth - t * 2, 1, border);
                Rect(tex, t, a.SheetHeight - 1 - t, a.SheetWidth - t * 2, 1, border);
                Rect(tex, t, t, 1, a.SheetHeight - t * 2, border);
                Rect(tex, a.SheetWidth - 1 - t, t, 1, a.SheetHeight - t * 2, border);
            }

            return tex;
        }

        /// <summary>경험치 바 채움 — 초록, 위쪽 한 줄 밝게.</summary>
        private static Texture2D BakeBar(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Rect(tex, 0, 0, a.SheetWidth, a.SheetHeight, Hex("58B84A"));
            Rect(tex, 0, a.SheetHeight - 3, a.SheetWidth, 2, Hex("8FD870"));
            Rect(tex, 0, 0, a.SheetWidth, 2, Hex("3E8A34"));
            return tex;
        }

        /// <summary>카드·「선택」 바탕 34×34 — 흰 둥근 테두리 + 남색 안쪽 (틴트는 표가 준다 [실측 48b461]).</summary>
        private static Texture2D BakeRewardButton(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            RoundRect(tex, 0, 0, a.SheetWidth, a.SheetHeight, 9, Outline);
            RoundRect(tex, 1, 1, a.SheetWidth - 2, a.SheetHeight - 2, 8, Hex("F4F6FA"));
            RoundRect(tex, 4, 4, a.SheetWidth - 8, a.SheetHeight - 8, 5, Hex("1F2748"));
            return tex;
        }

        /// <summary>
        /// 「시작」 버튼 바탕 234×105 [소스 btn_shadowed · 9슬라이스 16] — 초록 판 + 연두 테 + 검은 외곽 + 그림자.
        /// </summary>
        private static Texture2D BakeShadowedButton(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int w = a.SheetWidth;
            int h = a.SheetHeight;
            const int shadow = 5;

            RoundRect(tex, shadow, 0, w - shadow, h - shadow, 22, new Color(0f, 0f, 0f, 0.55f));
            RoundRect(tex, 0, shadow, w - shadow, h - shadow, 22, Outline);
            RoundRect(tex, 2, shadow + 2, w - shadow - 4, h - shadow - 4, 20, Hex("6FD46B"));
            RoundRect(tex, 5, shadow + 5, w - shadow - 10, h - shadow - 10, 17, Hex("2E8B3E"));

            // 아래쪽 어두운 띠 · 위쪽 밝은 줄
            for (int y = shadow + 5; y < shadow + 14; y++)
            {
                for (int x = 5; x < w - shadow - 5; x++)
                {
                    if (tex.GetPixel(x, y) == Hex("2E8B3E"))
                        Put(tex, x, y, Hex("236B30"));
                }
            }

            for (int x = 20; x < w - shadow - 20; x++)
                Put(tex, x, h - 8, Hex("8BE087"));

            return tex;
        }

        // ══════════════════════════════ 아이콘 32×32

        /// <summary>총알 아이콘 — 하늘색 혜성. 피해는 붉은 불티, 발사속도는 잔상 둘.</summary>
        private static Texture2D BakeCometIcon(UndeadArtData a, bool damage)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int hx = 22;
            int hy = 16;

            for (int i = 0; i < 17; i++)
            {
                int x = hx - 4 - i;
                double t = i / 17.0;
                int half = (int)Math.Round(4.0 * (1.0 - t));

                for (int y = -half; y <= half; y++)
                    Put(tex, x, hy + y, t < 0.3 ? BoltLight : BoltBlue);

                if (damage == false && i % 4 == 1)
                {
                    Put(tex, x, hy + 7, BoltBlue);
                    Put(tex, x, hy - 7, BoltBlue);
                }
            }

            Disc(tex, hx, hy, 5, BoltBlue);
            Disc(tex, hx, hy, 4, BoltLight);
            Disc(tex, hx, hy, 2, BoltCore);

            if (damage)
            {
                Put(tex, hx + 6, hy + 4, HeroRed);
                Put(tex, hx + 7, hy - 3, HeroRed);
                Put(tex, hx - 2, hy + 8, HeroRed);
            }

            OutlineSilhouette(tex, 0, 0, a.SheetWidth, a.SheetHeight);
            return tex;
        }

        /// <summary>히어로 아이콘 — 투구 흉상을 2배로, 아래에 색 띠.</summary>
        private static Texture2D BakeHeroIcon(UndeadArtData a, Color band)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            string[] bust = new string[10];
            Array.Copy(HeroBody, bust, 10);

            Rect(tex, 4, 2, 24, 5, Alpha(band, 0.85f));
            BlitScaled(tex, 0, 6, bust, HeroPal, 2);
            return tex;
        }

        /// <summary>번개 아이콘 — 분홍 에너지 구슬. 갈래마다 표식이 다르다 (0 기본 · 1 피해 · 2 반경 · 3 속도).</summary>
        private static Texture2D BakeEnergyIcon(UndeadArtData a, int variant)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cx = 19;
            int cy = 16;
            Color pinkDeep = Hex("C86BE0");

            for (int i = 0; i < 12; i++)
            {
                int x = cx - 8 - i;
                int half = (int)Math.Round(3.0 * (1.0 - i / 12.0));

                for (int y = -half; y <= half; y++)
                    Put(tex, x, cy + y, i < 4 ? BoltPink : pinkDeep);
            }

            Disc(tex, cx, cy, variant == 1 ? 9 : 8, pinkDeep);
            Disc(tex, cx, cy, variant == 1 ? 7 : 6, BoltPink);
            Disc(tex, cx, cy, variant == 1 ? 5 : 4, Hex("F6D6FB"));
            Disc(tex, cx, cy, variant == 1 ? 3 : 2, BoltCore);

            if (variant == 2)
            {
                for (int d = 0; d < 360; d += 15)
                {
                    double ang = d * Math.PI / 180.0;
                    Put(tex, cx + (int)Math.Round(Math.Cos(ang) * 12), cy + (int)Math.Round(Math.Sin(ang) * 12), BoltPink);
                }
            }

            if (variant == 3)
            {
                for (int i = 0; i < 8; i++)
                {
                    Put(tex, cx - 10 - i, cy + 6, BoltPink);
                    Put(tex, cx - 10 - i, cy - 6, BoltPink);
                }
            }

            OutlineSilhouette(tex, 0, 0, a.SheetWidth, a.SheetHeight);
            return tex;
        }

        private static readonly string[] GlyphQuestion =
        {
            ".kkkk.",
            "kk..kk",
            "....kk",
            "...kk.",
            "..kk..",
            "..kk..",
            "......",
            "..kk..",
            "..kk..",
        };

        private static readonly string[] GlyphBang =
        {
            "..kk..",
            "..kk..",
            "..kk..",
            "..kk..",
            "..kk..",
            "..kk..",
            "......",
            "..kk..",
            "..kk..",
        };

        /// <summary>과제 아이콘 40×40 — 열림은 베이지 상자에 «?», 완료는 초록 원에 «!».</summary>
        private static Texture2D BakeQuestIcon(UndeadArtData a, bool complete)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cx = a.SheetWidth / 2;
            int cy = a.SheetHeight / 2;

            if (complete)
            {
                Disc(tex, cx, cy, 18, Outline);
                Disc(tex, cx, cy, 16, Hex("3FBF5A"));
                Disc(tex, cx - 2, cy + 2, 12, Hex("52D66E"));
                BlitScaled(tex, cx - 9, cy - 13, GlyphBang, Pal("k=1A1423"), 3);
            }
            else
            {
                RoundRect(tex, 2, 2, a.SheetWidth - 4, a.SheetHeight - 4, 7, Outline);
                RoundRect(tex, 4, 4, a.SheetWidth - 8, a.SheetHeight - 8, 6, Hex("E8C97A"));
                RoundRect(tex, 6, 6, a.SheetWidth - 12, a.SheetHeight - 12, 5, Hex("F2DC9A"));
                BlitScaled(tex, cx - 9, cy - 13, GlyphQuestion, Pal("k=1A1423"), 3);
            }

            return tex;
        }

        /// <summary>클래퍼보드 76×79 — 파란 상자 + 흰 줄무늬 + 재생 삼각형 (「모두 받기!」 광고 표식 · 화면엔 안 쓴다).</summary>
        private static Texture2D BakeClapper(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int w = a.SheetWidth;
            int h = a.SheetHeight;

            RoundRect(tex, 2, 2, w - 4, h - 22, 8, Outline);
            RoundRect(tex, 4, 4, w - 8, h - 26, 7, Hex("2F7BE0"));
            RoundRect(tex, 2, h - 24, w - 4, 22, 5, Outline);
            RoundRect(tex, 4, h - 22, w - 8, 18, 4, Hex("1F5CB0"));

            for (int i = 0; i < 4; i++)
            {
                for (int y = h - 20; y < h - 6; y++)
                    Rect(tex, 8 + i * 17 + (y - (h - 20)) / 2, y, 6, 1, Color.white);
            }

            for (int y = 0; y < 26; y++)
            {
                int half = 13 - Math.Abs(y - 13);
                Rect(tex, w / 2 - 8, h / 2 - 22 + y, half, 1, Color.white);
            }

            return tex;
        }

        // ══════════════════════════════ 과제 포인터 94×58 — 오른쪽을 가리키는 손

        private static Texture2D BakePointer(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Color hand = Hex("E8C97A");
            Color handDark = Hex("C9A85C");

            RoundRect(tex, 4, 4, 58, 50, 12, hand);          // 주먹
            RoundRect(tex, 50, 22, 40, 16, 7, hand);         // 검지
            RoundRect(tex, 30, 46, 22, 10, 5, hand);         // 엄지
            Rect(tex, 8, 8, 50, 6, handDark);                // 아래 그늘
            Rect(tex, 52, 24, 34, 3, handDark);
            Rect(tex, 22, 12, 2, 30, handDark);               // 손가락 사이
            Rect(tex, 34, 12, 2, 30, handDark);
            Rect(tex, 46, 12, 2, 28, handDark);

            OutlineSilhouette(tex, 0, 0, a.SheetWidth, a.SheetHeight);
            return tex;
        }

        // ⚠ 지형 타일셋을 «손으로 찍는» 코드는 회차 35 에 지웠다 —
        //   원본 타일 아틀라스를 실측해 규격으로 굽는다 (로비 타일셋과 같은 길).
        //   바이옴이 둘이 되면서 「손으로 찍은 묘지」와 「실측한 겨울」이 나란히 서게 되는데,
        //   그러면 «같은 게임 안에서 두 화풍»이 된다.

    }
}
