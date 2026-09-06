using System;
using System.Collections.Generic;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 인물·적·수집물 — <b>도트 맵</b>으로 찍는다 (원본을 «보고» 실루엣·팔레트를 맞춘 재제작. 픽셀 복사 없음).
    /// <para>맵 규약은 <see cref="Blit"/> — 첫 줄이 위, <c>.</c> 투명, <c>k</c> 외곽선.</para>
    /// </summary>
    public static partial class UndeadSpriteBuilder
    {
        private static readonly Color HeroRed = Hex("D33A3A");
        private static readonly Color GemGreen = Hex("4FCF5F");
        private static readonly Color GemGreenLight = Hex("A5F2A8");
        private static readonly Color GemGreenDark = Hex("2C8A3A");

        // ══════════════════════════════ 히어로 — 6×2 격자 16×16. 0행 대기 5컷 · 1행 이동 5컷 [실측]

        private static readonly Dictionary<char, Color> HeroPal =
            Pal("r=D33A3A R=8E2323 a=D9DCE3 m=9BA3B4 d=5C6273 v=2B2F3A b=6B4A2E s=F0C8A0");

        /// <summary>몸통 12줄 — 투구(회색) · 빨간 깃털 · 어두운 눈가리개 · 가슴판.</summary>
        private static readonly string[] HeroBody =
        {
            "....rr..........",
            "...rrrkk........",
            "...rkaaaak......",
            "..kkaaaaaak.....",
            "..kaammmmak.....",
            "..kaavvvvak.....",
            "..kaaaaaaak.....",
            "..kaaaaaaak.....",
            ".kdkmmmmmmkdk...",
            ".kdkmaaaamkdk...",
            "..kkmmmmmmkk....",
            "...kmmkkmmk.....",
        };

        private static readonly string[] HeroLegsStand =
        {
            "...kbbk.kbbk....",
            "...kbbk.kbbk....",
            "...kkkk.kkkk....",
            "................",
        };

        private static readonly string[] HeroLegsApart =
        {
            "..kbbk...kbbk...",
            "..kbbk...kbbk...",
            "..kkkk...kkkk...",
            "................",
        };

        private static readonly string[] HeroLegsTogether =
        {
            "....kbbkbbk.....",
            "....kbbkbbk.....",
            "....kkkkkkk.....",
            "................",
        };

        private static Texture2D BakeHero(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int[] idleBob = { 0, 0, -1, -1, 0 };
            int[] runBob = { 0, -1, 0, -1, 0 };
            string[][] runLegs = { HeroLegsApart, HeroLegsStand, HeroLegsTogether, HeroLegsStand, HeroLegsApart };

            for (int col = 0; col < a.UsedCols; col++)
            {
                // 0행 = 대기 (Unity 텍스처는 아래가 0 → 표의 «0행»은 위쪽 줄이다)
                int idleY = (a.Rows - 1) * a.FrameHeight;
                int runY = (a.Rows - 2) * a.FrameHeight;
                int ox = col * a.FrameWidth;

                Shadow(tex, ox, idleY, 3, 10);
                Blit(tex, ox, idleY + 4 + idleBob[col], HeroBody, HeroPal);
                Blit(tex, ox, idleY + idleBob[col], HeroLegsStand, HeroPal);

                Shadow(tex, ox, runY, 3, 10);
                Blit(tex, ox, runY + 4 + runBob[col], HeroBody, HeroPal);
                Blit(tex, ox, runY + runBob[col], runLegs[col], HeroPal);
            }

            return tex;
        }

        // ══════════════════════════════ 적 — 4×1 격자 (좌우 반전은 렌더러가 한다 [실측 scale.x 부호])

        private static readonly Dictionary<char, Color> ZombiePal =
            Pal("g=5FA83E G=3F7A2A f=E8B48A e=2B1F2E b=8C5A3C B=5E3A26 p=3F3448");

        private static readonly string[] ZombieStand =
        {
            "....kgggk.......",
            "...kggggggk.....",
            "...kGGGGGGk.....",
            "...kffffffk.....",
            "...kfekffek.....",
            "...kffffffk.....",
            "....kffffk......",
            "...kbbbbbbk.....",
            "..kbbbbbbbbk....",
            ".kbkbbbbbbkbk...",
            ".kfkbbbbbbkfk...",
            "..kkbbbbbbkk....",
            "...kBBBBBBk.....",
            "...kppkkppk.....",
            "...kppk.kppk....",
            "...kBBk.kBBk....",
            "...kkkk.kkkk....",
        };

        private static readonly string[] ZombieStep =
        {
            "....kgggk.......",
            "...kggggggk.....",
            "...kGGGGGGk.....",
            "...kffffffk.....",
            "...kfekffek.....",
            "...kffffffk.....",
            "....kffffk......",
            "...kbbbbbbk.....",
            "..kbbbbbbbbk....",
            ".kbkbbbbbbkbk...",
            ".kfkbbbbbbkfk...",
            "..kkbbbbbbkk....",
            "...kBBBBBBk.....",
            "..kppkkkkppk....",
            "..kppk..kppk....",
            "..kBBk..kBBk....",
            "..kkkk..kkkk....",
        };

        /// <summary>좀비 — 초록 모자 · 갈색 몸 · 앞으로 뻗은 팔.</summary>
        private static Texture2D BakeZombie(UndeadArtData a)
        {
            return BakeWalker(a, ZombiePal, ZombieStand, ZombieStep);
        }

        private static readonly Dictionary<char, Color> SkeletonPal = Pal("w=E8E4D8 s=B8B2A4 e=2B1F2E");

        private static readonly string[] SkeletonStand =
        {
            "....kwwwwk......",
            "...kwwwwwwk.....",
            "...kwwwwwwk.....",
            "...kwekwwek.....",
            "...kwwwwwwk.....",
            "....kwkkwk......",
            ".....kwwk.......",
            "..kkkwwwwkkk....",
            ".kwkwswswskwk...",
            ".kwkwwwwwwkwk...",
            "..kkwswswskk....",
            "...kwwwwwwk.....",
            "....kswwsk......",
            "....kwkkwk......",
            "....kwk.kwk.....",
            "....kwk.kwk.....",
            "....kkk.kkk.....",
        };

        private static readonly string[] SkeletonStep =
        {
            "....kwwwwk......",
            "...kwwwwwwk.....",
            "...kwwwwwwk.....",
            "...kwekwwek.....",
            "...kwwwwwwk.....",
            "....kwkkwk......",
            ".....kwwk.......",
            "..kkkwwwwkkk....",
            ".kwkwswswskwk...",
            ".kwkwwwwwwkwk...",
            "..kkwswswskk....",
            "...kwwwwwwk.....",
            "....kswwsk......",
            "...kwkkkkwk.....",
            "...kwk..kwk.....",
            "...kwk..kwk.....",
            "...kkk..kkk.....",
        };

        /// <summary>해골 — 뼈색 · 어두운 눈구멍 · 갈비.</summary>
        private static Texture2D BakeSkeleton(UndeadArtData a)
        {
            return BakeWalker(a, SkeletonPal, SkeletonStand, SkeletonStep);
        }

        /// <summary>걷는 적 공통 — [서기 · 걷기 · 서기(내려앉음) · 걷기(내려앉음)] 4컷.</summary>
        private static Texture2D BakeWalker(UndeadArtData a, Dictionary<char, Color> pal, string[] stand, string[] step)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            string[][] frames = { stand, step, stand, step };
            int[] bob = { 0, 0, -1, -1 };

            for (int col = 0; col < a.Cols; col++)
            {
                int oy = a.FrameHeight - frames[col].Length;   // 맵이 프레임보다 짧으면 위쪽을 비운다
                Shadow(tex, col * a.FrameWidth, 0, 3, 10);
                Blit(tex, col * a.FrameWidth, System.Math.Max(0, oy + bob[col]), frames[col], pal);
            }

            return tex;
        }

        private static readonly Dictionary<char, Color> BatPal = Pal("b=3F63C7 d=2A3F8C r=E23B3B");

        private static readonly string[] BatSpread =
        {
            "................",
            "................",
            ".....k....k.....",
            "....kbk..kbk....",
            "kk..kbbkkbbk..kk",
            "kbbkkbbbbbbkkbbk",
            "kbbbbbrbbrbbbbbk",
            ".kbbbbbbbbbbbbk.",
            "..kddkbbbbkddk..",
            "...kk.kbbk.kk...",
            "......kkkk......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        };

        private static readonly string[] BatUp =
        {
            "kk............kk",
            "kbk..........kbk",
            "kbbk.k....k.kbbk",
            "kbbbkbk..kbkbbbk",
            ".kbbkbbkkbbkbbk.",
            "..kbbbbbbbbbbk..",
            "...kbbrbbrbbk...",
            "....kbbbbbbk....",
            "....kbbbbbbk....",
            ".....kbbbbk.....",
            "......kkkk......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        };

        /// <summary>박쥐 — 파란 몸 · 빨간 눈. [펼침 · 올림 · 펼침 · 올림] 4컷.</summary>
        private static Texture2D BakeBat(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            string[][] frames = { BatSpread, BatUp, BatSpread, BatUp };
            int[] bob = { 0, -1, -2, -1 };

            for (int col = 0; col < a.Cols; col++)
                Blit(tex, col * a.FrameWidth, System.Math.Max(0, a.FrameHeight - frames[col].Length + bob[col]), frames[col], BatPal);

            return tex;
        }

        private static readonly Dictionary<char, Color> EyePal = Pal("w=E4E7EE s=B9BFCC i=6F86A8 t=8B93A3");

        private static readonly string[] EyeCenter =
        {
            "................",
            "....kkkkkkk.....",
            "...kwwwwwwwk....",
            "..kwwwwwwwwwk...",
            ".kwwwwkkkwwwwk..",
            ".kwwwkiiikwwwk..",
            ".kwwwkikkkwwwk..",
            ".kwwwkiiikwwwk..",
            ".kwwwwkkkwwwwk..",
            ".kswwwwwwwwwsk..",
            "..kssswwwsssk...",
            "...kkssssskk....",
            "....kkkkkkk.....",
            "...kt.kt.kt.....",
            "..kt..kt..kt....",
            "..kt.kt..kt.....",
            "...kt.kt.kt.....",
            "................",
            "................",
        };

        private static readonly string[] EyeSide =
        {
            "................",
            "....kkkkkkk.....",
            "...kwwwwwwwk....",
            "..kwwwwwwwwwk...",
            ".kwwwwwkkkwwwk..",
            ".kwwwwkiiikwwk..",
            ".kwwwwkikkkwwk..",
            ".kwwwwkiiikwwk..",
            ".kwwwwwkkkwwwk..",
            ".kswwwwwwwwwsk..",
            "..kssswwwsssk...",
            "...kkssssskk....",
            "....kkkkkkk.....",
            "...kt.kt.kt.....",
            "..kt..kt..kt....",
            "..kt.kt..kt.....",
            "...kt.kt.kt.....",
            "................",
            "................",
        };

        /// <summary>눈알 — 회백색 안구 · 촉수. 동공이 [가운데 · 오른쪽 · 가운데 · 왼쪽] 으로 움직인다.</summary>
        private static Texture2D BakeEye(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            string[][] frames = { EyeCenter, EyeSide, EyeCenter, EyeSide };
            bool[] flip = { false, false, false, true };

            for (int col = 0; col < a.Cols; col++)
                Blit(tex, col * a.FrameWidth, 0, frames[col], EyePal, flip[col]);

            return tex;
        }

        private static readonly Dictionary<char, Color> RevenantPal = Pal("p=4B3A5E d=2E2340 r=E23B3B t=6A5A80");

        private static readonly string[] RevenantMap =
        {
            "......kkkk......",
            ".....kddddk.....",
            "....kddddddk....",
            "....kdrddrdk....",
            "....kddddddk....",
            "...kdtddddtdk...",
            "...kpppppppdk...",
            "..kpppppppppk...",
            "..kpkpppppkpk...",
            "...kkpppppkk....",
            "....kpppppk.....",
            "....kpkpkpk.....",
            ".....kkkkk......",
            "......k.k.......",
            "................",
            "................",
        };

        /// <summary>망령 — 어두운 보라 두건 · 빨간 눈. 떠서 흔들린다.</summary>
        private static Texture2D BakeRevenant(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int[] bob = { 0, -1, -1, 0 };

            for (int col = 0; col < a.Cols; col++)
                Blit(tex, col * a.FrameWidth, System.Math.Max(0, a.FrameHeight - RevenantMap.Length + bob[col]), RevenantMap, RevenantPal);

            return tex;
        }

        // ══════════════════════════════ 수집물

        /// <summary>보석 — 5컷 회전. 폭이 [8·6·3·6·8] 로 줄었다 늘어난다. 왼쪽 밝고 오른쪽 어둡다.</summary>
        private static Texture2D BakeGem(UndeadArtData a, Color body, Color light, Color dark)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int[] halfWidth = { 4, 3, 1, 3, 4 };
            bool[] mirror = { false, false, false, true, true };

            for (int col = 0; col < a.Cols; col++)
            {
                int ox = col * a.FrameWidth;
                int cx = a.FrameWidth / 2;
                int hw = halfWidth[col];
                int top = 13;
                int bottom = 2;

                for (int y = bottom; y <= top; y++)
                {
                    // 위 40% 는 평평한 윗면, 아래는 뾰족하게
                    double t = (y - bottom) / (double)(top - bottom);
                    int w = t > 0.6 ? hw : (int)System.Math.Round(hw * (t / 0.6));

                    if (t > 0.9)
                        w = System.Math.Max(1, hw - 1);

                    for (int x = -w; x <= w; x++)
                    {
                        bool leftFacet = mirror[col] ? x > 0 : x < 0;
                        Color c = x == 0 ? body : (leftFacet ? light : dark);

                        if (t > 0.6 && t <= 0.9 && System.Math.Abs(x) < w)
                            c = body;

                        Put(tex, ox + cx + x, y, c);
                    }
                }

                OutlineSilhouette(tex, ox, 0, a.FrameWidth, a.FrameHeight);
            }

            return tex;
        }

        /// <summary>HUD 보석 아이콘 — 보석 한 컷에 흰 테두리.</summary>
        private static Texture2D BakeGemIcon(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cx = a.SheetWidth / 2;

            for (int y = 2; y <= 13; y++)
            {
                double t = (y - 2) / 11.0;
                int w = t > 0.6 ? 4 : (int)System.Math.Round(4 * (t / 0.6));

                for (int x = -w; x <= w; x++)
                    Put(tex, cx + x, y, x < 0 ? GemGreenLight : (x == 0 ? GemGreen : GemGreenDark));
            }

            // 흰 테두리 → 그 바깥에 검은 외곽선
            var white = new List<Vector2Int>();

            for (int y = 0; y < a.SheetHeight; y++)
            {
                for (int x = 0; x < a.SheetWidth; x++)
                {
                    if (tex.GetPixel(x, y).a > 0.01f)
                        continue;

                    if (Filled(tex, x - 1, y, 0, 0, 16, 16) || Filled(tex, x + 1, y, 0, 0, 16, 16) ||
                        Filled(tex, x, y - 1, 0, 0, 16, 16) || Filled(tex, x, y + 1, 0, 0, 16, 16))
                        white.Add(new Vector2Int(x, y));
                }
            }

            for (int i = 0; i < white.Count; i++)
                tex.SetPixel(white[i].x, white[i].y, Color.white);

            OutlineSilhouette(tex, 0, 0, a.SheetWidth, a.SheetHeight);
            return tex;
        }

        private static readonly Dictionary<char, Color> HeartPal = Pal("r=E23B3B R=9E2424 w=FFB6B6");

        private static readonly string[] HeartMap =
        {
            "................",
            "...kkk...kkk....",
            "..krrrk.krrrk...",
            ".krwrrrkrrrrrk..",
            ".krrrrrrrrrrrk..",
            ".krrrrrrrrrrrk..",
            ".kRrrrrrrrrrRk..",
            "..kRrrrrrrrRk...",
            "...kRrrrrrRk....",
            "....kRrrrRk.....",
            ".....kRrRk......",
            "......kRk.......",
            ".......k........",
            "................",
            "................",
            "................",
        };

        /// <summary>하트 32×32 — 16×16 도트를 2배로.</summary>
        private static Texture2D BakeHeart(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            BlitScaled(tex, 0, 0, HeartMap, HeartPal, 2);
            return tex;
        }

        private static readonly Dictionary<char, Color> FragmentPal = Pal("p=B36BF0 l=E3B8FF d=6E3AA8");

        private static readonly string[] FragmentMap =
        {
            "......kk........",
            ".....klpk.......",
            ".....klpk.......",
            "....klppdk......",
            "....klppdk......",
            "...klpppddk.....",
            "...klpppddk.....",
            "...kppppddk.....",
            "....kpppdk......",
            "....kppddk......",
            ".....kpdk.......",
            ".....kpdk.......",
            "......kk........",
            "................",
            "................",
            "................",
        };

        /// <summary>아케인 조각 — 보라 수정 조각.</summary>
        /// <summary>
        /// 마법 조각 — 원본 <c>energy_idle</c> 은 <b>16×16 컷 30장</b>이다 [소스 <c>for(e&lt;30)</c>].
        /// <para>⚠ 예전에는 1컷이라 «가만히 있는 보석»이었다. 컷마다 빛이 도는 위상을 준다.</para>
        /// </summary>
        private static Texture2D BakeFragment(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cols = Math.Max(1, a.Cols);

            for (int i = 0; i < cols; i++)
            {
                int ox = i * a.FrameWidth;
                Blit(tex, ox, 0, FragmentMap, FragmentPal);

                // 빛나는 점이 한 바퀴 돈다 — 30컷을 한 주기로
                double angle = Math.PI * 2.0 * i / cols;
                int gx = (int)Math.Round(8 + 4.5 * Math.Cos(angle));
                int gy = (int)Math.Round(8 + 4.5 * Math.Sin(angle));
                Put(tex, ox + gx, gy, FragmentPal['l']);
            }

            return tex;
        }

        /// <summary>체력바 한 칸 12×6 [소스] — 빨강, 아래 한 줄 어둡게.</summary>
        /// <summary>
        /// 체력 칸 <b>바탕</b> — 목숨이 줄어도 <b>남는다</b>
        /// [소스 <c>rect(0,0,12,6)</c> · <c>fill 0x4A0D0D</c> · <c>stroke {color:0, width:1, pixelLine}</c>].
        ///
        /// <para>
        /// ⚠ 원본은 이 그림을 <b>아틀라스가 아니라 코드로</b> 그린다 — 그래서 실측 규격이 없고,
        /// <b>소스의 사각형·색·선폭</b>이 곧 규격이다 (표의 <c>DrawnByCode</c>).
        /// </para>
        ///
        /// <para>⚠ [사고] 예전에는 색을 <c>E24040</c> 으로 «지어냈고» 명암 두 줄도 우리가 넣은 것이었다.</para>
        /// </summary>
        private static Texture2D BakeHpSegmentBg(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Rect(tex, 0, 0, a.SheetWidth, a.SheetHeight, HpSegmentBack);
            Outline1(tex, a.SheetWidth, a.SheetHeight, Color.black);
            return tex;
        }

        /// <summary>
        /// 체력 칸 <b>채움</b> — 목숨 수만큼만 보인다
        /// [소스 <c>rect</c> (12−2)×(6−2) · <c>fill 0xFF2B2B</c> · 여백 <c>padding 1</c>].
        /// </summary>
        private static Texture2D BakeHpSegmentFill(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Rect(tex, 0, 0, a.SheetWidth, a.SheetHeight, HpSegmentFill);
            return tex;
        }

        /// <summary>체력 칸 바탕색 [소스 — <c>4853005</c> = <c>#4A0D0D</c>].</summary>
        private static readonly Color HpSegmentBack = Hex("4A0D0D");

        /// <summary>체력 칸 채움색 [소스 — <c>16722731</c> = <c>#FF2B2B</c>].</summary>
        private static readonly Color HpSegmentFill = Hex("FF2B2B");

        /// <summary>1px 테두리 — <c>stroke({width:1, pixelLine:true})</c> 는 «안쪽 한 줄»이다.</summary>
        private static void Outline1(Texture2D tex, int w, int h, Color color)
        {
            Rect(tex, 0, 0, w, 1, color);
            Rect(tex, 0, h - 1, w, 1, color);
            Rect(tex, 0, 0, 1, h, color);
            Rect(tex, w - 1, 0, 1, h, color);
        }

        // ══════════════════════════════ NPC — 48×48 프레임 안에 «사람 크기» 도트 (16 폭 · 약 30 높이)

        private static readonly Dictionary<char, Color> WarriorPal =
            Pal("h=B9BFCC H=7A8090 s=F0C8A0 e=2B1F2E c=E8E4D8 b=6B4A2E B=4A3220 n=8A8FA0");

        /// <summary>전사 — 뿔 투구 · 흰 셔츠 · 갈색 바지 (서 있는 자세 — 도트 재사용용).</summary>
        private static readonly string[] WarriorStand =
        {
            "..k.......k.....",
            ".kHk.....kHk....",
            ".kkhhhhhhhkk....",
            "..khhhhhhhk.....",
            "..khhHHHhhk.....",
            "..kssssssss.....",
            "..ksesssesk.....",
            "..kssssssk......",
            "...ksssssk......",
            "....kkkkk.......",
            "..kkccccckk.....",
            ".kskcccccksk....",
            ".kskcccccksk....",
            "..kkccccckk.....",
            "...kbbbbbk......",
            "...kbbkbbk......",
            "...kbbk.kbbk....",
            "...kBBk.kBBk....",
            "...kkkk.kkkk....",
        };

        /// <summary>전사 도트 — 눕힌 실루엣 (회전은 픽셀아트를 깨뜨려 직접 찍는다).</summary>
        private static readonly string[] WarriorLayMap =
        {
            "..............................",
            "..............................",
            ".........kkkkkkkkkkkk.........",
            "..kk...kkccccccccccccbbbkkk...",
            ".kHhk.kscccccccccccbbbbbbBBk..",
            ".khhkkkssccccccccccbbbbbbBBk..",
            ".khhssssecccccccccbbbbbkkkkk..",
            "..khhsssseccccccccbbbbbk......",
            "...kkksssskkkkkkkkkkkkkk......",
            ".....kkkk.....................",
            "..............................",
        };

        /// <summary>
        /// 쓰러진 전사 — 시트는 192×48(4컷)인데 <b>원본은 «인덱스 2~3»만 쓴다</b>
        /// [소스 <c>for(s=2;s&lt;4;s++) layFrames.push(...)</c>]. 그래서 그 두 칸만 채운다.
        /// <para>⚠ 표의 <c>UsedCols</c> 가 2 인 것이 이 사실이다 — 앞 두 칸을 채우면 «다른 그림»이 재생된다.</para>
        /// </summary>
        private static Texture2D BakeWarriorLay(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);

            // ⚠ 원본은 «아틀라스의» 2~3칸을 쓰지만, 우리 시트는 우리가 굽고
            //   로더는 «앞에서부터» UsedCols 개를 자른다 — 그래서 앞 두 칸에 그린다.
            for (int i = 0; i < 2; i++)
                Blit(tex, i * a.FrameWidth + 8, 2 + (i % 2), WarriorLayMap, WarriorPal);

            return tex;
        }

        /// <summary>
        /// 서 있는 전사 — <b>4컷</b> [소스 <c>warrior_idle</c> 192×48 · <c>for(s&lt;4)</c> · 9fps].
        /// <para>⚠ 예전 회차에는 이 시트가 <b>아예 없었다</b> — 구조된 전사가 누운 그림 그대로 따라다녔다.</para>
        /// </summary>
        private static Texture2D BakeWarriorIdle(UndeadArtData a)
        {
            return BakeNpc(a, WarriorStand, WarriorPal);
        }

        /// <summary>
        /// 달리는 전사 — <b>8컷</b> [소스 <c>warrior_run</c> 384×48 · <c>for(s&lt;8)</c> · 9fps].
        /// <para>다리를 두 칸 폭으로 교차시킨다 — 원본 픽셀이 없으니 «걸음의 양»만 옮긴다.</para>
        /// </summary>
        private static Texture2D BakeWarriorRun(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            int cols = Math.Max(1, a.Cols);
            int ox = (a.FrameWidth - WarriorStand[0].Length) / 2;

            for (int i = 0; i < cols; i++)
            {
                // 한 걸음 주기: 몸이 오르내리고(0~1px) 다리가 벌어졌다 모인다
                double phase = Math.PI * 2.0 * i / cols;
                int bob = Math.Sin(phase) > 0.0 ? 1 : 0;
                int stride = (int)Math.Round(Math.Sin(phase));

                Blit(tex, i * a.FrameWidth + ox, 2 + bob, WarriorStand, WarriorPal);

                // 다리 두 줄을 좌우로 민다 (몸통 아래 4줄)
                if (stride == 0)
                    continue;

                for (int y = 2 + bob; y < 2 + bob + 4; y++)
                {
                    for (int x = 0; x < a.FrameWidth; x++)
                    {
                        int sx = i * a.FrameWidth + x;
                        Color c = tex.GetPixel(sx, y);

                        if (c.a <= 0f)
                            continue;

                        int dx = sx + stride;

                        if (dx < i * a.FrameWidth || dx >= (i + 1) * a.FrameWidth)
                            continue;

                        tex.SetPixel(dx, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        private static readonly Dictionary<char, Color> MagePal = Pal("s=8E5A3C S=6B4028 e=2B1F2E r=C43C3C R=8E2323 p=3E5C9E P=2C4070 b=4A3220");

        /// <summary>마법사 — 대머리 갈색 피부 · 빨간 셔츠 · 파란 바지 · 두 팔 벌림 [원본 «보고» 재제작].</summary>
        private static readonly string[] MageMap =
        {
            ".....kkkkkk.....",
            "....kssssssk....",
            "...kssssssssk...",
            "...ksesssssek...",
            "...kssssssssk...",
            "...kSssssssSk...",
            "....kssssssk....",
            ".....kkkkkk.....",
            "..kkkrrrrrrkkk..",
            ".ksskrrrrrrkssk.",
            ".kskrrRrrRrrksk.",
            ".kkkrrrrrrrrkkk.",
            "...krrrrrrrrk...",
            "...kRRRRRRRRk...",
            "...kppppppppk...",
            "...kpppkkpppk...",
            "...kpppk.kpppk..",
            "...kPPPk.kPPPk..",
            "...kbbbk.kbbbk..",
            "...kkkkk.kkkkk..",
        };

        private static Texture2D BakeMage(UndeadArtData a)
        {
            return BakeNpc(a, MageMap, MagePal);
        }

        private static readonly Dictionary<char, Color> FamilyPal = Pal("g=6FA85A G=4A7A3A s=F0C8A0 e=2B1F2E c=E8E4D8 b=4A3220");

        /// <summary>가족(아이) — 초록 두건 · 밝은 얼굴 [원본 family_npc «보고» 재제작].</summary>
        private static readonly string[] FamilyMap =
        {
            ".....kkkkkk.....",
            "....kggggggk....",
            "...kggggggggk...",
            "...kgkssssskgk..",
            "...kgssssssgk...",
            "...kgsesssesgk..",
            "...kgssssssgk...",
            "....kgssssgk....",
            ".....kkkkkk.....",
            "...kkggggggkk...",
            "..ksGggggggGsk..",
            "..kkGggggggGkk..",
            "....kGGGGGGk....",
            "....kggkkggk....",
            "....kggk.kggk...",
            "....kbbk.kbbk...",
            "....kkkk.kkkk...",
        };

        private static Texture2D BakeFamily(UndeadArtData a)
        {
            return BakeNpc(a, FamilyMap, FamilyPal);
        }

        private static readonly Dictionary<char, Color> FarmerPal = Pal("h=D8546A H=A83A4E s=F0C8A0 e=2B1F2E d=8E5AB8 D=6A3F90 p=3E5C9E b=4A3220");

        /// <summary>농부 — 분홍 머리 · 보라 옷 · 파란 바지 [원본 «보고» 재제작].</summary>
        private static readonly string[] FarmerMap =
        {
            "....kkkkkkk.....",
            "...khhhhhhhk....",
            "..khhhhhhhhhk...",
            "..khhkssssskh...",
            "..khhsssssshk...",
            "..khhsesssehk...",
            "..khhsssssshk...",
            "...kHhsssshHk...",
            "...kHkkkkkkHk...",
            "...kkddddddkk...",
            "..ksdddddddddk..",
            "..kskdddddddsk..",
            "...kkDDDDDDDk...",
            "....kDDDDDDk....",
            "....kppkkppk....",
            "....kppk.kppk...",
            "....kbbk.kbbk...",
            "....kkkk.kkkk...",
        };

        private static Texture2D BakeFarmer(UndeadArtData a)
        {
            return BakeNpc(a, FarmerMap, FarmerPal);
        }

        /// <summary>
        /// NPC 공통 — 프레임 가운데 아래에 앉힌다 (피벗 (0.5, 0) [실측]).
        ///
        /// <para>
        /// ★★ <b>컷 수는 표가 든다</b> [소스 <c>loadFrames</c> 직독] — 마법사 3 · 농부 2 · 가족 2 · 양 6.
        /// 예전에는 전부 «1컷»이라 원본에서 미세하게 흔들리는 NPC 들이 우리에게선 <b>정지 그림</b>이었다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>컷마다 «몸을 1px 들었다 놓는다»</b> — 원본 idle 은 그 정도의 흔들림이다.
        /// 컷별 도트를 따로 그릴 근거(원본 픽셀)가 없으므로 <b>움직임의 «양»만</b> 옮긴다.
        /// </para>
        ///
        /// <para>⚠ 도트가 프레임보다 작으면 «정수 배율»로 키운다 — 반칸 확대는 픽셀아트를 깨뜨린다.</para>
        /// </summary>
        private static Texture2D BakeNpc(UndeadArtData a, string[] map, Dictionary<char, Color> pal)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);

            int cols = Math.Max(1, a.Cols);
            int rows = Math.Max(1, a.Rows);
            int scale = Math.Max(1, Math.Min(a.FrameWidth / map[0].Length, (a.FrameHeight - 2) / map.Length));
            int drawW = map[0].Length * scale;
            int ox = (a.FrameWidth - drawW) / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int bob = i % 2;   // 1px 흔들림 — 원본 idle 의 «양»만 옮긴 것이다
                    int x = i * a.FrameWidth + ox;
                    int y = row * a.FrameHeight + 2 + bob;

                    if (scale == 1)
                        Blit(tex, x, y, map, pal);
                    else
                        BlitScaled(tex, x, y, map, pal, scale);
                }
            }

            return tex;
        }

        private static readonly Dictionary<char, Color> SheepPal = Pal("w=E8D9C3 s=C9B79C f=5C4A3E F=3E3028 e=1A1423 h=8A7A6A");

        /// <summary>양 24×24 — 둥근 털 몸 · 어두운 얼굴(왼쪽) · 다리 넷 [원본 «보고» 재제작].</summary>
        private static readonly string[] SheepMap =
        {
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........kkkkkkkkkk......",
            ".......kwwwwwwwwwwk.....",
            "..kkk.kwwwwwwwwwwwwk....",
            ".kfffkkwwwwwwwwwwwwk....",
            ".kfefffwwwwwwwwwwwwk....",
            ".kfffffwwwwwwwwwwwwk....",
            "..kFFfkwwwwwwwwwwwwk....",
            "...kkkkwwwwwwwwwwwwk....",
            "......kwswwwwwwswwwk....",
            "......kswwwwwwwwwwsk....",
            ".......kkssswwwsskk.....",
            "........kFkkkkkFk.......",
            "........kFk...kFk.......",
            "........kkk...kkk.......",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        };

        /// <summary>
        /// 양 — 원본은 <b>128×128 컷</b>을 <b>idle 6 · run 6</b> 두 줄로 든다
        /// [소스 <c>for(e&lt;6) idleFrames</c> / <c>runFrames</c> · 768×256 · baseScale 1.16].
        /// <para>⚠ 예전에는 24×24 «한 장»이라 크기도 컷 수도 원본과 달랐다.</para>
        /// <para>⚠ run 줄은 idle 과 같은 도트에 걸음 위상만 준다 — 원본 픽셀이 없다.</para>
        /// </summary>
        private static Texture2D BakeSheep(UndeadArtData a)
        {
            return BakeNpc(a, SheepMap, SheepPal);
        }
    }
}
