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

        // ══════════════════════════════ 지형 타일셋 — 24×24 · 한 줄 25칸 (UndeadTerrainView.ETile 순서)
        //   ⚠ 색은 원본 타일 텍스처를 «디머 없이» 본 값이다 [회차 11 정정 — 회차 10 은 시작 화면(디머 α.5 아래)에서 집어 전체가 어두웠다].

        private static readonly Color GroundBase = Hex("64779B");
        private static readonly Color GroundDark = Hex("5A6C87");
        private static readonly Color GroundDarker = Hex("4E5F78");
        private static readonly Color GrassBase = Hex("8E648E");
        private static readonly Color GrassTuft = Hex("59549F");
        private static readonly Color GrassLight = Hex("915795");
        private static readonly Color Fringe = Hex("59549F");
        private static readonly Color FringeDark = Hex("454375");
        private static readonly Color Bone = Hex("8C9CB8");
        private static readonly Color Skull = Hex("9CACC8");

        /// <summary>
        /// 타일셋 한 줄 — <b>원본 바이옴 1 이 실제로 쓰는 39종</b> [소스 <c>au</c>·<c>hu</c> 직독].
        /// <para>⚠ 칸 순서는 <see cref="UndeadTerrainView.ETile"/> 이 정본이다. 여기서 순서를 만들지 않는다.</para>
        /// </summary>
        private static Texture2D BakeTileset()
        {
            const int tile = 24;
            int cols = UndeadTerrainView.TilesetColumns;
            Texture2D tex = New(tile * cols, tile);

            for (int i = 0; i < cols; i++)
            {
                var kind = (UndeadTerrainView.ETile)i;
                int ox = i * tile;

                switch (kind)
                {
                    case UndeadTerrainView.ETile.Ground:
                        GroundTile(tex, ox, i);
                        break;

                    case UndeadTerrainView.ETile.Grass:
                        GrassTile(tex, ox, i, 0);
                        break;
                    case UndeadTerrainView.ETile.GrassA:
                        GrassTile(tex, ox, i, 1);
                        break;
                    case UndeadTerrainView.ETile.GrassB:
                        GrassTile(tex, ox, i, 2);
                        break;
                    case UndeadTerrainView.ETile.GrassC:
                        GrassTile(tex, ox, i, 3);
                        break;

                    case UndeadTerrainView.ETile.Road:
                        RoadTile(tex, ox, i, 0);
                        break;
                    case UndeadTerrainView.ETile.RoadA:
                        RoadTile(tex, ox, i, 1);
                        break;
                    case UndeadTerrainView.ETile.RoadB:
                        RoadTile(tex, ox, i, 2);
                        break;
                    case UndeadTerrainView.ETile.RoadC:
                        RoadTile(tex, ox, i, 3);
                        break;

                    default:
                        if (kind >= UndeadTerrainView.ETile.GroundDetail0 && kind <= UndeadTerrainView.ETile.GroundDetail9)
                        {
                            GroundTile(tex, ox, i);
                            DetailGlyph(tex, ox, kind - UndeadTerrainView.ETile.GroundDetail0);
                        }
                        else if (kind >= UndeadTerrainView.ETile.GrassNW && kind <= UndeadTerrainView.ETile.GrassInSE)
                        {
                            GrassTile(tex, ox, i, 0);
                            FringeTile(tex, ox, EdgeMask(kind), i);
                        }
                        else
                        {
                            // 길 가장자리 — 길 바닥에 «흙 쪽» 변이 스며든다
                            RoadTile(tex, ox, i, 0);
                            RoadFringe(tex, ox, EdgeMask(kind));
                        }
                        break;
                }
            }

            return tex;
        }

        private const int MaskN = 1;
        private const int MaskS = 2;
        private const int MaskW = 4;
        private const int MaskE = 8;

        /// <summary>
        /// 가장자리 타일이 «어느 변»을 바깥으로 두나 — 굽는 쪽 전용 표다.
        /// <para>안쪽 모서리(<c>*In**</c>)는 <b>대각 한 칸만</b> 바깥이라 두 변을 옅게 두른다.</para>
        /// </summary>
        private static int EdgeMask(UndeadTerrainView.ETile tile)
        {
            switch (tile)
            {
                case UndeadTerrainView.ETile.GrassNW: return MaskN | MaskW;
                case UndeadTerrainView.ETile.GrassN: return MaskN;
                case UndeadTerrainView.ETile.GrassNE: return MaskN | MaskE;
                case UndeadTerrainView.ETile.GrassW: return MaskW;
                case UndeadTerrainView.ETile.GrassE: return MaskE;
                case UndeadTerrainView.ETile.GrassSW: return MaskS | MaskW;
                case UndeadTerrainView.ETile.GrassS: return MaskS;
                case UndeadTerrainView.ETile.GrassSE: return MaskS | MaskE;

                // 안쪽 모서리 — 대각만 비어 있다
                case UndeadTerrainView.ETile.GrassInNW: return MaskN | MaskW;
                case UndeadTerrainView.ETile.GrassInNE: return MaskN | MaskE;
                case UndeadTerrainView.ETile.GrassInSW: return MaskS | MaskW;
                case UndeadTerrainView.ETile.GrassInSE: return MaskS | MaskE;

                case UndeadTerrainView.ETile.RoadNW: return MaskN | MaskW;
                case UndeadTerrainView.ETile.RoadN: return MaskN;
                case UndeadTerrainView.ETile.RoadNE: return MaskN | MaskE;
                case UndeadTerrainView.ETile.RoadW: return MaskW;
                case UndeadTerrainView.ETile.RoadE: return MaskE;
                case UndeadTerrainView.ETile.RoadSW: return MaskS | MaskW;
                case UndeadTerrainView.ETile.RoadS: return MaskS;
                case UndeadTerrainView.ETile.RoadSE: return MaskS | MaskE;
            }

            return 0;
        }

        /// <summary>길 가장자리 — 바깥 변 두세 줄이 흙으로 흐려진다.</summary>
        private static void RoadFringe(Texture2D tex, int ox, int mask)
        {
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    int depth = 99;

                    if ((mask & MaskN) != 0) depth = Math.Min(depth, 23 - y);
                    if ((mask & MaskS) != 0) depth = Math.Min(depth, y);
                    if ((mask & MaskW) != 0) depth = Math.Min(depth, x);
                    if ((mask & MaskE) != 0) depth = Math.Min(depth, 23 - x);

                    if (depth > 2 + Noise(x, y, 71) % 2)
                        continue;

                    Put(tex, ox + x, y, depth == 0 ? GroundBase : GroundDark);
                }
            }
        }

        private static void GroundTile(Texture2D tex, int ox, int seed)
        {
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    int n = Noise(x, y, seed);
                    Put(tex, ox + x, y, n % 29 == 0 ? GroundDark : GroundBase);
                }
            }
        }

        private static void GrassTile(Texture2D tex, int ox, int seed, int variant)
        {
            Rect(tex, ox, 0, 24, 24, GrassBase);

            if (variant == 0)
                return;

            // 풀잎 두세 가닥 [원본 «보고» — 보라 잎 · 밝은 분홍 점]
            int count = variant == 1 ? 2 : (variant == 2 ? 3 : 4);

            for (int k = 0; k < count; k++)
            {
                int gx = 4 + (Noise(k, seed, 41) % 16);
                int gy = 4 + (Noise(k, seed, 43) % 14);
                Put(tex, ox + gx, gy, GrassTuft);
                Put(tex, ox + gx, gy + 1, GrassTuft);
                Put(tex, ox + gx + 1, gy + 2, GrassTuft);
                Put(tex, ox + gx - 1, gy + 2, GrassLight);
            }
        }

        /// <summary>길 능선 — 바닥에 어두운 돌 자국 [원본 «보고» 95~97].</summary>
        private static void RoadTile(Texture2D tex, int ox, int seed, int variant)
        {
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    int n = Noise(x, y, seed);
                    Put(tex, ox + x, y, n % 7 == 0 ? GroundBase : GroundDark);
                }
            }

            if (variant == 0)
                return;

            Ellipse(tex, ox + 8 + variant * 4, 12 - variant * 3, 4, 2, GroundDarker);
            Ellipse(tex, ox + 16 - variant * 2, 7 + variant * 5, 3, 2, GroundBase);
        }

        /// <summary>풀밭 가장자리 — 바깥(바닥) 쪽 변에 보랏빛 풀 띠, 안쪽 경계는 들쭉날쭉. 맨 끝 두 줄은 바닥 색이 비친다.</summary>
        private static void FringeTile(Texture2D tex, int ox, int mask, int seed)
        {
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    int depth = 99;

                    if ((mask & MaskN) != 0) depth = Math.Min(depth, 23 - y);
                    if ((mask & MaskS) != 0) depth = Math.Min(depth, y);
                    if ((mask & MaskW) != 0) depth = Math.Min(depth, x);
                    if ((mask & MaskE) != 0) depth = Math.Min(depth, 23 - x);

                    int band = 7 + (Noise(x / 2, y / 2, seed) % 5);

                    if (depth < 2)
                        Put(tex, ox + x, y, GroundBase);
                    else if (depth < band)
                        Put(tex, ox + x, y, Noise(x, y, seed + 7) % 4 == 0 ? FringeDark : Fringe);
                    else if (depth == band && Noise(x, y, seed + 9) % 2 == 0)
                        Put(tex, ox + x, y, FringeDark);
                }
            }
        }

        /// <summary>바닥 장식 10종 — 해골 · 뼈 · 돌 · 갈라진 틈 · 풀잎 … [원본 «보고» 4·5·22·23·24·41·42·43·60·61].</summary>
        private static void DetailGlyph(Texture2D tex, int ox, int kind)
        {
            switch (kind)
            {
                case 0: SkullGlyph(tex, ox + 9, 8); break;
                case 1: BoneGlyph(tex, ox + 5, 6); BoneGlyph(tex, ox + 13, 14); break;
                case 2: Ellipse(tex, ox + 12, 12, 3, 2, GroundDark); Put(tex, ox + 11, 14, GroundBase); break;
                case 3: for (int i = 0; i < 9; i++) Put(tex, ox + 6 + i, 10 + (i % 3 == 0 ? 1 : 0), GroundDarker); break;
                case 4: TuftGlyph(tex, ox + 7, 9); TuftGlyph(tex, ox + 15, 5); break;
                case 5: SkullGlyph(tex, ox + 4, 12); BoneGlyph(tex, ox + 12, 5); break;
                case 6: Ellipse(tex, ox + 7, 8, 2, 1, GroundDark); Ellipse(tex, ox + 16, 15, 2, 1, GroundDark); break;
                case 7: BoneGlyph(tex, ox + 8, 10); break;
                case 8: for (int i = 0; i < 7; i++) Put(tex, ox + 12 + (i % 2), 5 + i, GroundDarker); break;
                default: SkullGlyph(tex, ox + 12, 4); TuftGlyph(tex, ox + 5, 15); break;
            }
        }

        private static void TuftGlyph(Texture2D tex, int x, int y)
        {
            Put(tex, x, y, GrassTuft);
            Put(tex, x, y + 1, GrassTuft);
            Put(tex, x - 1, y + 2, GrassTuft);
            Put(tex, x + 1, y + 2, GrassTuft);
            Put(tex, x + 2, y + 3, GrassTuft);
        }

        private static void SkullGlyph(Texture2D tex, int x, int y)
        {
            Rect(tex, x, y + 2, 6, 4, Skull);
            Rect(tex, x + 1, y + 1, 4, 1, Skull);
            Rect(tex, x + 1, y + 6, 4, 1, Skull);
            Put(tex, x + 1, y + 4, GroundBase);
            Put(tex, x + 4, y + 4, GroundBase);
            Rect(tex, x + 1, y, 1, 1, Skull);
            Rect(tex, x + 4, y, 1, 1, Skull);
        }

        private static void BoneGlyph(Texture2D tex, int x, int y)
        {
            Rect(tex, x + 1, y + 1, 5, 1, Bone);
            Put(tex, x, y, Bone);
            Put(tex, x, y + 2, Bone);
            Put(tex, x + 6, y, Bone);
            Put(tex, x + 6, y + 2, Bone);
        }
    }
}
