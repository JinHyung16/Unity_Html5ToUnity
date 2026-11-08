using System;
using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 영토 색 배정 · 국경 색 · 고정 색 — 원본 테마 기본 클래스의 색 규칙을 C# 으로 다시 짰다 (R11).
    ///
    /// <para>
    /// 배정기는 종류마다 따로다 `[소스] BMmXchgl.js:this.humanColorAllocator=new nm($p,tm),this.botColorAllocator=new nm(em,em),…this.nationColorAllocator=new nm(Qp,Qp)`.
    /// 영토 색 = 종류별 배정기에 플레이어 id 로 묻는다 `[소스] territoryColor(e){let t=e.team();return t===null?e.type()===ke.Human?…`.
    /// ⚠ 원본은 플레이어 뷰가 처음 생길 때 묻는다 — 그 순서(플레이어 목록 순서)대로 불러야 같은 색이 나온다.
    /// </para>
    ///
    /// <para>
    /// 국경 = 영토 색 HSL 명도 −12.5 를 반올림 `[소스] borderColor(e){return e.darken(.125)}` · `new Rt((s?…focusedBorderColor():a).toHex())`.
    /// 내 플레이어 국경만 (230,230,230) 이다. 방어초소 국경 = 국경 색 명도 −20 / −40 (팔레트에 넣을 때 소수 버림).
    /// </para>
    ///
    /// <para>
    /// 팀전 영토 색 = 팀 색 변형 목록에서 «플레이어 id 해시 % 목록 길이» 번째 `[소스] assignTeamPlayerColor(e,t){…n[se(t)%n.length]`.
    /// 이름 있는 7팀은 기본 색 하나에서 명도를 올리고 채도를 내린 64색 `[소스] function Zp(e){`, 봇 팀은 한 색, 사람 · 네이션 팀은 파랑 · 빨강 목록,
    /// 그 밖(「Team 8」…)은 팀 이름으로 사람 목록 배정기에서 받은 한 색이다 `[소스] default:return[this.assignColor(e)]`.
    /// 팀 색은 소수라 팔레트 영토 칸은 소수 버림, 국경은 소수 색을 어둡게 한 뒤 반올림이다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsPlayerColors
    {
        /// <summary>`[소스] borderColor(e){return e.darken(.125)}`</summary>
        public const double BorderDarken = 0.125;

        /// <summary>`[소스] defendedBorderColors(e){return{light:e.darken(.2),dark:e.darken(.4)}}`</summary>
        public const double DefendedLightDarken = 0.2;

        public const double DefendedDarkDarken = 0.4;

        /// <summary>내 플레이어 국경 `[소스] focusedBorderColor(){return O({r:230,g:230,b:230})}`</summary>
        public static readonly Color32 FocusedBorder = C(230, 230, 230);

        /// <summary>전술 보기 관계 색 `[소스] this._selfColor=O({r:0,g:255,b:0}),this._allyColor=O({r:255,g:255,b:0}),this._neutralColor=O({r:128,g:128,b:128}),this._enemyColor=O({r:255,g:0,b:0})`</summary>
        public static readonly Color32 SelfColor = C(0, 255, 0);

        public static readonly Color32 AllyColor = C(255, 255, 0);
        public static readonly Color32 NeutralColor = C(128, 128, 128);
        public static readonly Color32 EnemyColor = C(255, 0, 0);

        /// <summary>테마 배경 `[소스] this.background=O({r:27,g:35,b:48})`. ⚠ GL 합성은 이것이 아니라 캔버스 배경 토큰을 쓴다 (<see cref="FrontWarsTerrainColors.CanvasBgDarkPreset"/>).</summary>
        public static readonly Color32 ThemeBackground = C(27, 35, 48);

        /// <summary>공격 국경 — 내가 거는 · 나에게 오는 `[소스] zo=O({r:240,g:26,b:26}),Bo=O({r:237,g:104,b:104})` · `this.#D.build(this._view,zo,Bo)`.</summary>
        public static readonly Color32 OutgoingAttackBorder = C(240, 26, 26);

        public static readonly Color32 IncomingAttackBorder = C(237, 104, 104);

        /// <summary>낙진 5색 `[소스] this.falloutColorList=[O({r:120,g:255,b:71}),…]` — 팔레트 0행 칸 1 · 4 · 5 · 6 · 7 에 순서대로 `[소스] Ao=[Do,4,5,6,7]`.</summary>
        private static readonly Color32[] FalloutPalette =
        {
            C(120, 255, 71), C(130, 255, 85), C(110, 245, 65), C(125, 255, 75), C(115, 250, 68)
        };

        /// <summary>낙진 색이 들어가는 0행 칸 `[소스] Ao=[Do,4,5,6,7]` (Do=1).</summary>
        private static readonly int[] FalloutSlotOrder = { 1, 4, 5, 6, 7 };

        /// <summary>`[소스] BMmXchgl.js:nationColorAllocator=new nm(Qp,Qp)` — 97색.</summary>
        private static readonly Color32[] NationPalette =
        {
            C(230, 100, 100), C(100, 180, 230), C(230, 180, 80), C(180, 100, 230),
            C(80, 200, 120), C(230, 130, 180), C(100, 160, 80), C(230, 150, 100),
            C(80, 130, 190), C(210, 210, 100), C(190, 100, 130), C(100, 210, 210),
            C(210, 140, 80), C(150, 110, 190), C(180, 210, 120), C(210, 100, 160),
            C(100, 140, 110), C(230, 180, 180), C(120, 120, 190), C(190, 170, 100),
            C(100, 180, 160), C(210, 160, 200), C(170, 190, 100), C(100, 130, 150),
            C(230, 140, 140), C(140, 180, 220), C(200, 160, 110), C(180, 130, 180),
            C(130, 200, 130), C(220, 120, 120), C(120, 160, 200), C(200, 200, 140),
            C(160, 120, 160), C(140, 180, 140), C(200, 130, 110), C(130, 170, 190),
            C(190, 180, 160), C(170, 140, 190), C(160, 190, 160), C(190, 150, 130),
            C(140, 150, 180), C(180, 170, 140), C(150, 130, 150), C(170, 190, 180),
            C(190, 140, 150), C(130, 180, 170), C(180, 160, 180), C(160, 180, 140),
            C(170, 150, 170), C(100, 180, 230), C(230, 180, 80), C(180, 100, 230),
            C(80, 200, 120), C(230, 130, 180), C(100, 160, 80), C(230, 150, 100),
            C(80, 130, 190), C(210, 210, 100), C(190, 100, 130), C(100, 210, 210),
            C(210, 140, 80), C(150, 110, 190), C(180, 210, 120), C(210, 100, 160),
            C(100, 140, 110), C(230, 180, 180), C(120, 120, 190), C(190, 170, 100),
            C(100, 180, 160), C(210, 160, 200), C(170, 190, 100), C(100, 130, 150),
            C(230, 140, 140), C(140, 180, 220), C(200, 160, 110), C(180, 130, 180),
            C(130, 200, 130), C(220, 120, 120), C(120, 160, 200), C(200, 200, 140),
            C(160, 120, 160), C(140, 180, 140), C(200, 130, 110), C(130, 170, 190),
            C(190, 180, 160), C(170, 140, 190), C(160, 190, 160), C(190, 150, 130),
            C(140, 150, 180), C(180, 170, 140), C(150, 130, 150), C(170, 190, 180),
            C(190, 140, 150), C(130, 180, 170), C(180, 160, 180), C(160, 180, 140),
            C(170, 150, 170)
        };

        /// <summary>`[소스] BMmXchgl.js:humanColorAllocator=new nm($p,tm)` 첫 목록 — 63색.</summary>
        private static readonly Color32[] HumanPalette =
        {
            C(16, 185, 129), C(34, 197, 94), C(45, 212, 191), C(48, 178, 180),
            C(52, 211, 153), C(56, 189, 248), C(59, 130, 246), C(67, 190, 84),
            C(74, 222, 128), C(79, 70, 229), C(82, 183, 136), C(96, 165, 250),
            C(99, 202, 253), C(110, 231, 183), C(124, 58, 237), C(125, 211, 252),
            C(132, 204, 22), C(133, 77, 14), C(134, 239, 172), C(147, 51, 234),
            C(147, 197, 253), C(151, 255, 187), C(163, 230, 53), C(167, 139, 250),
            C(168, 85, 247), C(179, 136, 255), C(186, 255, 201), C(190, 92, 251),
            C(192, 132, 252), C(202, 138, 4), C(202, 225, 255), C(204, 204, 255),
            C(217, 70, 239), C(220, 38, 38), C(220, 220, 255), C(220, 240, 250),
            C(230, 250, 210), C(230, 255, 250), C(233, 213, 255), C(234, 88, 12),
            C(234, 179, 8), C(235, 75, 75), C(236, 72, 153), C(239, 68, 68),
            C(240, 171, 252), C(240, 240, 200), C(244, 114, 182), C(245, 101, 101),
            C(245, 158, 11), C(248, 113, 113), C(249, 115, 22), C(250, 215, 225),
            C(250, 250, 210), C(251, 113, 133), C(251, 146, 60), C(251, 191, 36),
            C(251, 235, 245), C(252, 165, 165), C(252, 211, 77), C(253, 164, 175),
            C(255, 204, 229), C(255, 223, 186), C(255, 240, 200)
        };

        /// <summary>`[소스] BMmXchgl.js:new nm($p,tm)` 둘째 목록 — 첫 목록이 바닥나면 (첫 + 둘째)로 다시 채운다. 100색.</summary>
        private static readonly Color32[] HumanFallbackExtra =
        {
            C(0, 5, 0), C(0, 15, 0), C(0, 25, 0), C(0, 35, 0),
            C(0, 45, 0), C(0, 55, 0), C(0, 65, 0), C(0, 75, 0),
            C(0, 85, 0), C(0, 95, 0), C(0, 105, 0), C(0, 115, 0),
            C(0, 125, 0), C(0, 135, 0), C(0, 145, 0), C(0, 155, 0),
            C(0, 165, 0), C(0, 175, 0), C(0, 185, 0), C(0, 195, 5),
            C(0, 205, 10), C(0, 215, 15), C(0, 225, 20), C(0, 235, 25),
            C(0, 245, 30), C(0, 255, 35), C(10, 255, 45), C(20, 255, 55),
            C(30, 255, 65), C(40, 255, 75), C(50, 255, 85), C(60, 255, 95),
            C(70, 255, 105), C(80, 255, 115), C(90, 255, 125), C(100, 255, 135),
            C(110, 255, 145), C(120, 255, 155), C(130, 255, 165), C(140, 255, 175),
            C(150, 200, 255), C(150, 255, 185), C(160, 215, 255), C(160, 255, 195),
            C(170, 190, 255), C(170, 225, 255), C(170, 255, 205), C(180, 180, 255),
            C(180, 235, 250), C(180, 255, 215), C(190, 140, 195), C(190, 245, 240),
            C(190, 255, 225), C(195, 145, 200), C(200, 150, 205), C(200, 170, 255),
            C(200, 255, 215), C(200, 255, 235), C(205, 155, 210), C(210, 160, 215),
            C(210, 255, 245), C(215, 165, 220), C(215, 255, 200), C(220, 160, 255),
            C(220, 170, 225), C(220, 255, 255), C(225, 175, 230), C(225, 255, 175),
            C(230, 180, 235), C(230, 250, 255), C(235, 150, 255), C(235, 185, 240),
            C(240, 190, 245), C(240, 240, 255), C(240, 250, 160), C(245, 160, 240),
            C(245, 195, 250), C(245, 245, 175), C(250, 200, 255), C(250, 230, 255),
            C(255, 170, 225), C(255, 185, 215), C(255, 195, 235), C(255, 200, 220),
            C(255, 205, 245), C(255, 205, 255), C(255, 210, 230), C(255, 210, 250),
            C(255, 210, 255), C(255, 215, 195), C(255, 215, 245), C(255, 220, 235),
            C(255, 220, 250), C(255, 225, 180), C(255, 225, 255), C(255, 230, 245),
            C(255, 235, 200), C(255, 235, 235), C(255, 240, 220), C(255, 245, 210)
        };

        /// <summary>`[소스] BMmXchgl.js:botColorAllocator=new nm(em,em)` — 49색.</summary>
        private static readonly Color32[] BotPalette =
        {
            C(190, 120, 120), C(120, 160, 190), C(190, 160, 100), C(160, 120, 190),
            C(100, 170, 130), C(190, 130, 160), C(120, 150, 100), C(190, 140, 120),
            C(100, 120, 160), C(170, 170, 120), C(160, 120, 130), C(120, 170, 170),
            C(170, 140, 100), C(140, 120, 160), C(150, 170, 130), C(170, 120, 140),
            C(120, 140, 120), C(180, 160, 160), C(130, 130, 160), C(160, 150, 120),
            C(120, 160, 150), C(170, 150, 170), C(150, 160, 120), C(120, 130, 140),
            C(180, 140, 140), C(140, 160, 170), C(170, 150, 130), C(160, 130, 160),
            C(130, 170, 130), C(170, 130, 130), C(130, 150, 170), C(170, 170, 140),
            C(150, 130, 150), C(140, 160, 140), C(170, 130, 120), C(130, 150, 160),
            C(160, 160, 150), C(150, 140, 160), C(150, 170, 150), C(160, 140, 130),
            C(140, 150, 160), C(160, 150, 140), C(140, 130, 140), C(150, 160, 160),
            C(160, 140, 150), C(130, 160, 150), C(160, 150, 160), C(150, 160, 140),
            C(150, 140, 150)
        };

        private readonly FrontWarsColorAllocator _human = new FrontWarsColorAllocator(HumanPalette, HumanFallbackExtra);
        private readonly FrontWarsColorAllocator _bot = new FrontWarsColorAllocator(BotPalette, BotPalette);
        private readonly FrontWarsColorAllocator _nation = new FrontWarsColorAllocator(NationPalette, NationPalette);

        /// <summary>팀 이름 없는 팀용 배정기 `[소스] this.teamColorAllocator=new nm($p,tm)`.</summary>
        private readonly FrontWarsColorAllocator _team = new FrontWarsColorAllocator(HumanPalette, HumanFallbackExtra);

        /// <summary>플레이어 id → 팀 색 (소수 rgb) `[소스] this.teamPlayerColors=new Map`.</summary>
        private readonly Dictionary<string, double[]> _teamPlayerColors = new Dictionary<string, double[]>();

        /// <summary>
        /// 이름 있는 팀의 기본 색 (HSL) `[소스] BMmXchgl.js:var Fp=O({h:0,s:82,l:56}),Ip=O({h:224,s:100,l:58}),Lp=O({h:172,s:66,l:50}),Rp=O({h:271,s:81,l:56}),zp=O({h:45,s:93,l:47}),Bp=O({h:25,s:95,l:53}),Vp=O({h:128,s:49,l:50})`
        /// · 팀 이름과 짝 `[소스] getTeamColorVariations(e){switch(e){case Ze.Blue:return Wp;case Ze.Red:return Up;…}`.
        /// </summary>
        private static readonly Dictionary<string, double[]> TeamBaseHsl = new Dictionary<string, double[]>
        {
            { "Red", new double[] { 0, 82, 56 } },
            { "Blue", new double[] { 224, 100, 58 } },
            { "Teal", new double[] { 172, 66, 50 } },
            { "Purple", new double[] { 271, 81, 56 } },
            { "Yellow", new double[] { 45, 93, 47 } },
            { "Orange", new double[] { 25, 95, 53 } },
            { "Green", new double[] { 128, 49, 50 } }
        };

        /// <summary>봇 팀 한 색 `[소스] Hp=O({h:36,s:10,l:80}),…Xp=[O(Hp)]`.</summary>
        private static readonly double[] BotTeamHsl = { 36, 10, 80 };

        /// <summary>변형 수 · 끝으로 갈수록 채도 −30% · 명도 +30 `[소스] Array.from({length:64},(e,i)=>{let a=i/63,o=n*(1-.3*a),s=Math.min(100,r+a*30)`.</summary>
        private const int TeamShadeCount = 64;
        private const double TeamShadeSaturationDrop = 0.3;
        private const double TeamShadeLightnessRise = 30;
        private const double MaxLightness = 100;

        private static readonly Dictionary<string, double[][]> TeamShadeCache = new Dictionary<string, double[][]>();

        /// <summary>
        /// 이름 있는 팀(봇 · 사람 · 네이션 포함)의 색 변형 — 소수 rgb. 이름 없는 팀이면 null.
        /// <para>사람 팀 = 파랑 목록 · 네이션 팀 = 빨강 목록 `[소스] case Xe:return Wp;case $e:return Up`.</para>
        /// </summary>
        private static double[][] TeamShades(string team)
        {
            if (team == FrontWarsGame.HumansTeam)
                team = "Blue";
            else if (team == FrontWarsGame.NationsTeam)
                team = "Red";

            if (TeamShadeCache.TryGetValue(team, out double[][] cached))
                return cached;

            double[][] shades;

            if (team == FrontWarsGame.BotTeam)
            {
                FrontWarsColorMath.HslToRgb(BotTeamHsl[0], BotTeamHsl[1], BotTeamHsl[2], out double r, out double g, out double b);
                shades = new[] { new[] { r, g, b } };
            }
            else if (TeamBaseHsl.TryGetValue(team, out double[] hsl))
            {
                // 기본 색을 색 객체로 만든 뒤 hsl 을 정수로 다시 읽는다 `[소스] let{h:t,s:n,l:r}=e.toHsl()`
                FrontWarsColorMath.HslToRgb(hsl[0], hsl[1], hsl[2], out double br, out double bg, out double bb);
                FrontWarsColorMath.RgbToHslRounded(br, bg, bb, out double h, out double s, out double l);

                shades = new double[TeamShadeCount][];

                for (int i = 0; i < TeamShadeCount; i++)
                {
                    double a = i / (double)(TeamShadeCount - 1);
                    double shadeS = s * (1 - TeamShadeSaturationDrop * a);
                    double shadeL = Math.Min(MaxLightness, l + a * TeamShadeLightnessRise);
                    FrontWarsColorMath.HslToRgb(h, shadeS, shadeL, out double r, out double g, out double b);
                    shades[i] = new[] { r, g, b };
                }
            }
            else
            {
                return null;
            }

            TeamShadeCache[team] = shades;
            return shades;
        }

        /// <summary>팀 색 변형 목록 (팔레트 바이트 — 소수 버림). 이름 없는 팀이면 빈 목록.</summary>
        public static IReadOnlyList<Color32> TeamVariations(string team)
        {
            var result = new List<Color32>();
            double[][] shades = team == null ? null : TeamShades(team);

            if (shades != null)
            {
                foreach (double[] c in shades)
                    result.Add(FrontWarsColorMath.Truncated(c[0], c[1], c[2]));
            }

            return result;
        }

        /// <summary>`[소스] assignTeamPlayerColor(e,t){if(this.teamPlayerColors.has(t))return this.teamPlayerColors.get(t);let n=this.getTeamColorVariations(e),r=n[se(t)%n.length]`.</summary>
        private double[] TeamPlayerColor(string team, string playerId)
        {
            if (_teamPlayerColors.TryGetValue(playerId, out double[] existing))
                return existing;

            double[][] shades = TeamShades(team);

            if (shades == null)
            {
                Color32 c = _team.AssignColor(team);
                shades = new[] { new double[] { c.r, c.g, c.b } };
            }

            double[] color = shades[FrontWarsHash.Of(playerId) % shades.Length];
            _teamPlayerColors[playerId] = color;
            return color;
        }

        /// <summary>
        /// 플레이어 영토 색 (팔레트 바이트). 팀이 있으면 팀 색, 없으면 종류별 배정기 `[소스] territoryColor(e){let t=e.team();return t===null?…:this.teamColorAllocator.assignTeamPlayerColor(t,e.id())}`.
        /// </summary>
        public Color32 TerritoryColor(FrontWarsPlayer player)
        {
            return TerritoryColor(player.Team, player.Type, player.Id);
        }

        /// <summary>팀 · 종류 · id 로 영토 색 — 팀이 null 이면 개인전 배정.</summary>
        public Color32 TerritoryColor(string team, EFrontWarsPlayerType type, string playerId)
        {
            if (team == null)
                return TerritoryColor(type, playerId);

            double[] c = TeamPlayerColor(team, playerId);
            return FrontWarsColorMath.Truncated(c[0], c[1], c[2]);
        }

        /// <summary>
        /// 플레이어 국경 색 — 내 플레이어면 (230,230,230). 팀 색은 소수 색을 어둡게 한 뒤 반올림 `[소스] a=this.game.theme().borderColor(i)` · `new Rt((s?…:a).toHex())`.
        /// </summary>
        public Color32 BorderColor(FrontWarsPlayer player, bool isMyPlayer)
        {
            return BorderColor(player.Team, player.Type, player.Id, isMyPlayer);
        }

        public Color32 BorderColor(string team, EFrontWarsPlayerType type, string playerId, bool isMyPlayer)
        {
            if (team == null)
                return BorderColor(TerritoryColor(type, playerId), isMyPlayer);

            double[] c = TeamPlayerColor(team, playerId);
            return isMyPlayer ? FocusedBorder : FrontWarsColorMath.DarkenRounded(c[0], c[1], c[2], BorderDarken);
        }

        /// <summary>팀 대표 색 = 변형 목록 첫 색, 16진 반올림 (화면 글자 · 표시용) `[소스] teamColor(e){return this.teamColorAllocator.assignTeamColor(e)}`.</summary>
        public Color32 TeamColor(string team)
        {
            double[][] shades = TeamShades(team);

            if (shades == null)
                return _team.AssignColor(team);

            return FrontWarsColorMath.Rounded(shades[0][0], shades[0][1], shades[0][2]);
        }

        public static IReadOnlyList<Color32> HumanColors
        {
            get { return HumanPalette; }
        }

        public static IReadOnlyList<Color32> HumanFallbackColors
        {
            get { return HumanFallbackExtra; }
        }

        public static IReadOnlyList<Color32> BotColors
        {
            get { return BotPalette; }
        }

        public static IReadOnlyList<Color32> NationColors
        {
            get { return NationPalette; }
        }

        public static IReadOnlyList<Color32> FalloutColors
        {
            get { return FalloutPalette; }
        }

        public static IReadOnlyList<int> FalloutSlots
        {
            get { return FalloutSlotOrder; }
        }

        /// <summary>
        /// 종류와 id 로 영토 색을 받는다. 같은 id 는 늘 같은 색. 네이션(FakeHuman)은 네이션 목록 `[소스] …:this.nationColorAllocator.assignColor(e.id())`.
        /// </summary>
        public Color32 TerritoryColor(EFrontWarsPlayerType type, string playerId)
        {
            switch (type)
            {
                case EFrontWarsPlayerType.Human:
                    return _human.AssignColor(playerId);
                case EFrontWarsPlayerType.Bot:
                    return _bot.AssignColor(playerId);
                default:
                    return _nation.AssignColor(playerId);
            }
        }

        /// <summary>국경 색 — 내 플레이어면 (230,230,230), 아니면 영토 색 명도 −12.5 반올림.</summary>
        public static Color32 BorderColor(Color32 territory, bool isMyPlayer)
        {
            return isMyPlayer ? FocusedBorder : FrontWarsColorMath.DarkenRounded(territory, BorderDarken);
        }

        /// <summary>방어초소 국경 체크 무늬의 밝은 칸 (팔레트 바이트).</summary>
        public static Color32 DefendedBorderLight(Color32 border)
        {
            return FrontWarsColorMath.DarkenTruncated(border, DefendedLightDarken);
        }

        /// <summary>방어초소 국경 체크 무늬의 어두운 칸 (팔레트 바이트).</summary>
        public static Color32 DefendedBorderDark(Color32 border)
        {
            return FrontWarsColorMath.DarkenTruncated(border, DefendedDarkDarken);
        }

        private static Color32 C(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }
    }
}
