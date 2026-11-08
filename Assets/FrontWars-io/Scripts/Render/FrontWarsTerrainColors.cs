using System;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지형 칸 색 — 원본 테마의 지형 색 식을 C# 으로 다시 짰다 (R11 · 코드 복사 아님).
    ///
    /// <para>
    /// 원본에는 테마 클래스가 둘이다: 기본 클래스(여기서 <c>Light</c>) `[소스] BMmXchgl.js:terrainColor(e,t){let n=e.magnitude(t);if(e.isImpassable(t))return this.impassable`
    /// 와 그것을 이어받아 지형 색만 바꾼 클래스(여기서 <c>Dark</c>) `[소스] BMmXchgl.js:cm=class extends sm{`.
    /// </para>
    ///
    /// <para>
    /// ⚠ 원본 게임은 <b>늘 Dark 클래스</b>를 만든다 `[소스] BMmXchgl.js:this.gameView=new qt(…,new cm)`.
    /// 설정의 프리셋(Dark · Light · Purple)은 클래스를 바꾸지 않고 물 색 토큰(<see cref="WaterDarkPreset"/> 등)과 캔버스 배경만 바꾼다.
    /// 그래서 기본값은 Dark + 물 색 #0e2e5a 다. Light 식은 원본에 있지만 게임에서 쓰이지 않는다.
    /// </para>
    ///
    /// <para>순수 C# 이다 (UnityEngine 은 Color32 만 쓴다). 이관 때 정답지로 대조함 (<c>07_기대값.md</c>).</para>
    /// </summary>
    public static class FrontWarsTerrainColors
    {
        // ── Light (기본 클래스) `[소스] BMmXchgl.js:sm=class{constructor(){`
        /// <summary>`[소스] this.shore=O({r:204,g:203,b:158})`</summary>
        public static readonly Color32 LightShore = new Color32(204, 203, 158, 255);

        /// <summary>`[소스] this.water=O({r:70,g:132,b:180})`</summary>
        public static readonly Color32 LightWater = new Color32(70, 132, 180, 255);

        /// <summary>`[소스] this.shorelineWater=O({r:100,g:143,b:255})`</summary>
        public static readonly Color32 LightShorelineWater = new Color32(100, 143, 255, 255);

        /// <summary>`[소스] this.impassable=O({r:72,g:74,b:84})`</summary>
        public static readonly Color32 LightImpassable = new Color32(72, 74, 84, 255);

        // ── Dark `[소스] BMmXchgl.js:cm=class extends sm{constructor(...e){`
        /// <summary>`[소스] this.darkShore=O({r:134,g:133,b:88})`</summary>
        public static readonly Color32 DarkShore = new Color32(134, 133, 88, 255);

        /// <summary>`[소스] this.darkShorelineWater=O({r:50,g:50,b:50})`</summary>
        public static readonly Color32 DarkShorelineWater = new Color32(50, 50, 50, 255);

        /// <summary>`[소스] this.darkImpassable=O({r:40,g:42,b:50})`</summary>
        public static readonly Color32 DarkImpassable = new Color32(40, 42, 50, 255);

        // ── 설정 프리셋의 물 색 토큰 `[소스] BVamDUD6.js:bs=[{id:`dark`…` — Dark 클래스의 물 색이 이 토큰에서 온다
        /// <summary>기본(Dark 프리셋) `[소스] BVamDUD6.js:rs={version:1,…waterColor:`#0e2e5a`}`</summary>
        public static readonly Color32 WaterDarkPreset = new Color32(0x0e, 0x2e, 0x5a, 255);

        /// <summary>Light 프리셋 `[소스] BVamDUD6.js:_s={…waterColor:`#2b5c9e`}`</summary>
        public static readonly Color32 WaterLightPreset = new Color32(0x2b, 0x5c, 0x9e, 255);

        /// <summary>Purple 프리셋 `[소스] BVamDUD6.js:vs={…waterColor:`#221244`}`</summary>
        public static readonly Color32 WaterPurplePreset = new Color32(0x22, 0x12, 0x44, 255);

        // ── 캔버스 배경 토큰 — GL 셰이더의 uBg 가 이것이다 `[소스] BMmXchgl.js:this._glTerrain.render(this.camera,this._canvasBg,this._dpr)`
        /// <summary>기본(Dark 프리셋) `[소스] BVamDUD6.js:canvasBg:`#1b232f``. ⚠ 테마 클래스의 background(27,35,48)와 파랑이 1 다르다.</summary>
        public static readonly Color32 CanvasBgDarkPreset = new Color32(0x1b, 0x23, 0x2f, 255);

        /// <summary>`[소스] BVamDUD6.js:_s={…canvasBg:`#2e455e`}`</summary>
        public static readonly Color32 CanvasBgLightPreset = new Color32(0x2e, 0x45, 0x5e, 255);

        /// <summary>`[소스] BVamDUD6.js:vs={…canvasBg:`#1a1030`}`</summary>
        public static readonly Color32 CanvasBgPurplePreset = new Color32(0x1a, 0x10, 0x30, 255);

        /// <summary>
        /// 칸 바이트 성질로 색을 낸다. 판정 순서가 원본과 같다: 지나갈 수 없음 → 해안 땅 → 물(해안 물 / 깊이) → 평지 · 고원 · 산.
        /// </summary>
        /// <param name="dark">Dark 클래스 식이면 참.</param>
        /// <param name="isLand">비트 7.</param>
        /// <param name="isOcean">비트 5 (물일 때만 의미).</param>
        /// <param name="isShoreline">
        /// 원본 <c>isShoreline</c> 결과 — 땅이면 해안 비트, 물이면 해안 비트 + 4방향에 땅. <see cref="FrontWarsTerrainMap.IsShoreline"/> 과 같다.
        /// </param>
        /// <param name="magnitude">비트 0~4 (0~31).</param>
        /// <param name="darkWater">Dark 클래스의 물 색 토큰. Light 에서는 무시한다.</param>
        public static Color32 TerrainColor(bool dark, bool isLand, bool isOcean, bool isShoreline, int magnitude, Color32 darkWater)
        {
            int n = magnitude;

            // 지나갈 수 없음 `[소스] if(e.isImpassable(t))return this.impassable`
            if (isLand && n == FrontWarsTerrainMap.MagnitudeMask)
                return dark ? DarkImpassable : LightImpassable;

            // 해안 땅 `[소스] if(e.isShore(t))return this.shore`
            if (isLand && isShoreline)
                return dark ? DarkShore : LightShore;

            if (isLand == false)
            {
                // 바다 · 호수는 같은 식 `[소스] case Ue.Ocean:case Ue.Lake:{`
                if (isShoreline)
                    return dark ? DarkShorelineWater : LightShorelineWater;

                if (dark)
                {
                    // `[소스] e.magnitude(t)<10?O({r:Math.max(a.r+9-n,0),…}):this.darkWater`
                    if (n < 10)
                        return Rgb(darkWater.r + 9 - n, darkWater.g + 9 - n, darkWater.b + 9 - n);

                    return new Color32(darkWater.r, darkWater.g, darkWater.b, 255);
                }

                // `[소스] r:Math.max(r.r-10+(11-Math.min(n,10)),0)`
                int shade = -10 + (11 - Math.Min(n, 10));
                return Rgb(LightWater.r + shade, LightWater.g + shade, LightWater.b + shade);
            }

            if (n < FrontWarsTerrainMap.HighlandMagnitude)
            {
                // 평지 `[소스] case Ue.Plains:return O({r:190,g:220-2*n,b:138})` · Dark `O({r:140,g:170-2*n,b:88})`
                return dark ? Rgb(140, 170 - 2 * n, 88) : Rgb(190, 220 - 2 * n, 138);
            }

            if (n < FrontWarsTerrainMap.MountainMagnitude)
            {
                // 고원 `[소스] case Ue.Highland:return O({r:200+2*n,g:183+2*n,b:138+2*n})` · Dark `r:150+2*n,g:133+2*n,b:88+2*n`
                return dark ? Rgb(150 + 2 * n, 133 + 2 * n, 88 + 2 * n) : Rgb(200 + 2 * n, 183 + 2 * n, 138 + 2 * n);
            }

            // 산 `[소스] let e=Math.max(0,Math.min(1,(n-20)/10));return O({r:Math.round(206+e*42),…b:Math.round(208+e*44)})`
            // Dark `Math.round(168+e*46)…Math.round(170+e*48)`
            double e = Math.Max(0.0, Math.Min(1.0, (n - 20) / 10.0));

            if (dark)
                return Rgb(JsRound(168 + e * 46), JsRound(168 + e * 46), JsRound(170 + e * 48));

            return Rgb(JsRound(206 + e * 42), JsRound(206 + e * 42), JsRound(208 + e * 44));
        }

        /// <summary>지도 칸 하나의 색 — 검사 · 디버그용 진입점.</summary>
        public static Color32 TerrainColor(FrontWarsTerrainMap map, int tile, bool dark, Color32 darkWater)
        {
            return TerrainColor(dark, map.IsLand(tile), map.IsOcean(tile), map.IsShoreline(tile), map.Magnitude(tile), darkWater);
        }

        /// <summary>
        /// 지도 전체를 RGBA 바이트로 칠한다 (칸 번호 순서 = 행 우선, 0 행이 맨 위). 알파는 늘 255 `[소스] a255:e.a*255|0`.
        /// <para>원본도 칸 성질 조합마다 색을 한 번만 계산해 쓴다 `[소스] Qa=class{static paint(e,t,n){let r=Array(1024)`.</para>
        /// </summary>
        public static void Paint(FrontWarsTerrainMap map, bool dark, Color32 darkWater, byte[] rgba)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            if (rgba == null || rgba.Length < map.TileCount * 4)
                throw new ArgumentException("rgba 버퍼가 칸 수×4 보다 작다");

            // 키 = 땅(1) · 바다(1) · 해안(1) · 높이(5) = 8비트 → 256칸. 색은 이 넷으로만 정해진다.
            var cache = new Color32[256];
            var filled = new bool[256];

            for (int t = 0; t < map.TileCount; t++)
            {
                bool land = map.IsLand(t);
                bool ocean = map.IsOcean(t);
                bool shore = map.IsShoreline(t);
                int mag = map.Magnitude(t);
                int key = (land ? 128 : 0) | (ocean ? 64 : 0) | (shore ? 32 : 0) | mag;

                if (filled[key] == false)
                {
                    cache[key] = TerrainColor(dark, land, ocean, shore, mag, darkWater);
                    filled[key] = true;
                }

                Color32 c = cache[key];
                int o = t * 4;
                rgba[o] = c.r;
                rgba[o + 1] = c.g;
                rgba[o + 2] = c.b;
                rgba[o + 3] = 255;
            }
        }

        /// <summary>원본 색 객체는 0~255 로 자른다 `[소스] mt=function(e){return{r:dt(e.r,0,255)…`.</summary>
        private static Color32 Rgb(int r, int g, int b)
        {
            return new Color32((byte)Math.Max(0, Math.Min(255, r)), (byte)Math.Max(0, Math.Min(255, g)), (byte)Math.Max(0, Math.Min(255, b)), 255);
        }

        /// <summary>JS Math.round — 반올림 동점은 +∞ 쪽.</summary>
        public static int JsRound(double x)
        {
            double f = Math.Floor(x);
            return (int)(x - f >= 0.5 ? f + 1 : f);
        }
    }
}
