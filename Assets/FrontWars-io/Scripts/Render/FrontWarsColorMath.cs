using System;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 원본 색 계산 규칙 — 어둡게(HSL 명도 빼기) · Lab 변환 · CIEDE2000 거리. 규칙을 읽고 C# 으로 다시 짰다 (R11).
    ///
    /// <para>
    /// 원본은 색 객체 라이브러리 두 개를 섞어 쓴다: 어둡게 · Lab 은 한쪽 `[소스] BMmXchgl.js:e.prototype.darken=function(e){…O(Lt(this.rgba,-e))}` ·
    /// `e.prototype.toLab=function(){`, 거리는 다른 쪽의 2000 식 `[소스] BMmXchgl.js:var Bu={deltaE76:Sl,deltaECMC:wl,deltaE2000:dl`.
    /// 연산 순서를 원본과 같게 두어 소수 오차까지 맞춘다 (이관 때 정답지로 대조함 · <c>07_기대값.md</c>).
    /// </para>
    /// </summary>
    public static class FrontWarsColorMath
    {
        /// <summary>
        /// HSL 명도를 amount×100 만큼 뺀 색 (0~255 소수, 반올림 없음) `[소스] Lt=function(e,t){var n=Dt(e);return{h:n.h,s:n.s,l:dt(n.l+100*t,0,100)`.
        /// </summary>
        public static void Darken(double r, double g, double b, double amount, out double outR, out double outG, out double outB)
        {
            RgbToHsl(r, g, b, out double h, out double s, out double l);

            // 명도 빼기 `[소스] l:dt(n.l+100*t,0,100)`
            l = Clamp(l + 100 * -amount, 0, 100);

            HslToRgb(h, s, l, out outR, out outG, out outB);
        }

        /// <summary>rgb(0~255 소수) → hsl (반올림 없음) `[소스] St=function(e){` → `Dt=function(e){`.</summary>
        public static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
        {
            // rgb → hsv `[소스] St=function(e){`
            double max = Math.Max(r, Math.Max(g, b));
            double delta = max - Math.Min(r, Math.Min(g, b));
            double hue6 = delta != 0 ? (max == r ? (g - b) / delta : max == g ? 2 + (b - r) / delta : 4 + (r - g) / delta) : 0;
            h = 60 * (hue6 < 0 ? hue6 + 6 : hue6);
            double sv = max != 0 ? delta / max * 100 : 0;
            double v = max / 255 * 100;

            // hsv → hsl `[소스] Dt=function(e){`
            double i = (200 - sv) * v / 100;
            s = i > 0 && i < 200 ? sv * v / 100 / (i <= 100 ? i : 200 - i) * 100 : 0;
            l = i / 2;
        }

        /// <summary>색 객체의 hsl 읽기 — h · s · l 을 정수로 반올림 `[소스] toHsl=function(){` (roundHsla).</summary>
        public static void RgbToHslRounded(double r, double g, double b, out double h, out double s, out double l)
        {
            RgbToHsl(r, g, b, out h, out s, out l);
            h = JsRound(h);
            s = JsRound(s);
            l = JsRound(l);
        }

        /// <summary>
        /// hsl 객체 → rgb (0~255 소수, 반올림 없음) — 색 객체를 hsl 로 만들 때 이렇게 읽힌다 `[소스] wt=function(e){` → `Et=function(e){` → `Ct=function(e){`.
        /// </summary>
        public static void HslToRgb(double h, double s, double l, out double outR, out double outG, out double outB)
        {
            // hsl 객체 다시 읽기 `[소스] wt=function(e){return{h:ft(e.h),s:dt(e.s,0,100),l:dt(e.l,0,100)`
            h = double.IsInfinity(h) || double.IsNaN(h) ? 0 : h % 360;
            if (h < 0)
                h += 360;

            s = Clamp(s, 0, 100);
            l = Clamp(l, 0, 100);

            // hsl → hsv `[소스] Et=function(e){`
            double n = s * ((l < 50 ? l : 100 - l) / 100);
            double s2 = n > 0 ? 2 * n / (l + n) * 100 : 0;
            double v2 = l + n;

            // hsv → rgb `[소스] Ct=function(e){`
            double t = h / 360 * 6;
            double ns = s2 / 100;
            double nv = v2 / 100;
            double a = Math.Floor(t);
            double o = nv * (1 - ns);
            double p = nv * (1 - (t - a) * ns);
            double c = nv * (1 - (1 - t + a) * ns);
            int sector = (int)(a % 6);

            switch (sector)
            {
                case 0: outR = 255 * nv; outG = 255 * c; outB = 255 * o; break;
                case 1: outR = 255 * p; outG = 255 * nv; outB = 255 * o; break;
                case 2: outR = 255 * o; outG = 255 * nv; outB = 255 * c; break;
                case 3: outR = 255 * o; outG = 255 * p; outB = 255 * nv; break;
                case 4: outR = 255 * c; outG = 255 * o; outB = 255 * nv; break;
                default: outR = 255 * nv; outG = 255 * o; outB = 255 * p; break;
            }
        }

        /// <summary>어둡게 한 뒤 16진 문자열로 반올림한 색 — 국경 색이 이렇게 만들어진다 `[소스] this._borderColor=new Rt((…a).toHex())`.</summary>
        public static Color32 DarkenRounded(Color32 color, double amount)
        {
            return DarkenRounded(color.r, color.g, color.b, amount);
        }

        /// <summary>소수 색(팀 색)을 어둡게 한 뒤 16진 반올림.</summary>
        public static Color32 DarkenRounded(double red, double green, double blue, double amount)
        {
            Darken(red, green, blue, amount, out double r, out double g, out double b);
            return new Color32(RoundByte(r), RoundByte(g), RoundByte(b), 255);
        }

        /// <summary>16진 문자열로 반올림한 색 `[소스] toHex()`.</summary>
        public static Color32 Rounded(double r, double g, double b)
        {
            return new Color32(RoundByte(r), RoundByte(g), RoundByte(b), 255);
        }

        /// <summary>팔레트 바이트 배열에 넣은 색 (소수 버림) `[소스] #i(e,t,n){let{r,g:i,b:a}=t.rgba;this.#r(e,r,i,a,n)}`.</summary>
        public static Color32 Truncated(double r, double g, double b)
        {
            return new Color32(ToUint8(r), ToUint8(g), ToUint8(b), 255);
        }

        /// <summary>어둡게 한 뒤 바이트 배열에 넣은 색 (소수 버림) — 팔레트의 방어 국경 색 `[소스] this.#i(n+wo,i.light,255)` → Uint8Array.</summary>
        public static Color32 DarkenTruncated(Color32 color, double amount)
        {
            Darken(color.r, color.g, color.b, amount, out double r, out double g, out double b);
            return new Color32(ToUint8(r), ToUint8(g), ToUint8(b), 255);
        }

        /// <summary>
        /// sRGB → Lab(D50), 소수 둘째 자리 반올림 `[소스] e.prototype.toLab=function(){…{l:Ps(e.l,2),a:Ps(e.a,2),b:Ps(e.b,2)`.
        /// </summary>
        public static void ToLab(Color32 color, out double l, out double a, out double b)
        {
            double lr = Linearize(color.r);
            double lg = Linearize(color.g);
            double lb = Linearize(color.b);

            // sRGB(D65) → XYZ `[소스] x:100*(.4124564*t+.3575761*n+.1804375*r)`
            double x65 = 100 * (0.4124564 * lr + 0.3575761 * lg + 0.1804375 * lb);
            double y65 = 100 * (0.2126729 * lr + 0.7151522 * lg + 0.072175 * lb);
            double z65 = 100 * (0.0193339 * lr + 0.119192 * lg + 0.9503041 * lb);

            // D65 → D50 `[소스] x:1.0478112*e.x+.0228866*e.y+-.050127*e.z`
            double x = 1.0478112 * x65 + 0.0228866 * y65 + -0.050127 * z65;
            double y = 0.0295424 * x65 + 0.9904844 * y65 + -0.0170491 * z65;
            double z = -0.0092345 * x65 + 0.0150436 * y65 + 0.7521316 * z65;

            // 흰점으로 자르기 `[소스] Rs=96.422,zs=100,Bs=82.521`
            x = Clamp(x, 0, WhiteX);
            y = Clamp(y, 0, WhiteY);
            z = Clamp(z, 0, WhiteZ);

            double fy = LabF(y / WhiteY);
            double fz = LabF(z / WhiteZ);
            double fx = LabF(x / WhiteX);

            l = Round2(116 * fy - 16);
            a = Round2(500 * (fx - fy));
            b = Round2(200 * (fy - fz));
        }

        /// <summary>
        /// CIEDE2000 (kL = kC = kH = 1) — 입력은 Lab 좌표 그대로 `[소스] BMmXchgl.js:function dl(e,t,{kL:n=1,kC:r=1,kH:i=1}={}){`.
        /// </summary>
        public static double DeltaE2000(double l1, double a1, double b1, double l2, double a2, double b2)
        {
            double c1 = Chroma(a1, b1);
            double c2 = Chroma(a2, b2);

            if (c1 < 0)
                c1 = 0;
            if (c2 < 0)
                c2 = 0;

            double p = Pow7((c1 + c2) / 2);
            double m = 0.5 * (1 - Math.Sqrt(p / (p + Pow25To7)));
            double ap1 = (1 + m) * a1;
            double ap2 = (1 + m) * a2;
            double cp1 = Math.Sqrt(ap1 * ap1 + b1 * b1);
            double cp2 = Math.Sqrt(ap2 * ap2 + b2 * b2);
            double h1 = ap1 == 0 && b1 == 0 ? 0 : Math.Atan2(b1, ap1);
            double h2 = ap2 == 0 && b2 == 0 ? 0 : Math.Atan2(b2, ap2);

            if (h1 < 0)
                h1 += 2 * Math.PI;
            if (h2 < 0)
                h2 += 2 * Math.PI;

            h1 *= RadToDeg;
            h2 *= RadToDeg;

            double dL = l2 - l1;
            double dC = cp2 - cp1;
            double hDiff = h2 - h1;
            double hSum = h1 + h2;
            double hAbs = Math.Abs(hDiff);
            double dh;

            if (cp1 * cp2 == 0)
                dh = 0;
            else if (hAbs <= 180)
                dh = hDiff;
            else if (hDiff > 180)
                dh = hDiff - 360;
            else
                dh = hDiff + 360;

            double dH = 2 * Math.Sqrt(cp2 * cp1) * Math.Sin(dh * DegToRad / 2);
            double lMean = (l1 + l2) / 2;
            double cMean = (cp1 + cp2) / 2;
            double cMean7 = Pow7(cMean);
            double hMean;

            if (cp1 * cp2 == 0)
                hMean = hSum;
            else if (hAbs <= 180)
                hMean = hSum / 2;
            else if (hSum < 360)
                hMean = (hSum + 360) / 2;
            else
                hMean = (hSum - 360) / 2;

            double lm50 = (lMean - 50) * (lMean - 50);
            double sl = 1 + 0.015 * lm50 / Math.Sqrt(20 + lm50);
            double sc = 1 + 0.045 * cMean;
            double tt = 1;
            tt -= 0.17 * Math.Cos((hMean - 30) * DegToRad);
            tt += 0.24 * Math.Cos(2 * hMean * DegToRad);
            tt += 0.32 * Math.Cos((3 * hMean + 6) * DegToRad);
            tt -= 0.2 * Math.Cos((4 * hMean - 63) * DegToRad);
            double sh = 1 + 0.015 * cMean * tt;
            double hr = (hMean - 275) / 25;
            double dTheta = 30 * Math.Exp(-1 * (hr * hr));
            double rc = 2 * Math.Sqrt(cMean7 / (cMean7 + Pow25To7));
            double rt = -1 * Math.Sin(2 * dTheta * DegToRad) * rc;

            double tl = dL / (1 * sl);
            double tc = dC / (1 * sc);
            double th = dH / (1 * sh);
            double sum = tl * tl;
            sum += tc * tc;
            sum += th * th;
            sum += rt * tc * th;
            return Math.Sqrt(sum);
        }

        /// <summary>JS Math.round — 동점은 +∞ 쪽.</summary>
        public static double JsRound(double x)
        {
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f;
        }

        /// <summary>
        /// Lab → LCH 채도. a · b 가 둘 다 ε 미만이면 0 `[소스] fromBase(e){if(this.ε===void 0){…this.ε=t/1e5}` — ε = 250/1e5 (Lab a 범위 −125~125).
        /// </summary>
        private static double Chroma(double a, double b)
        {
            if (Math.Abs(a) < ChromaEpsilon && Math.Abs(b) < ChromaEpsilon)
                return 0;

            return Math.Sqrt(a * a + b * b);
        }

        private const double ChromaEpsilon = 250.0 / 1e5;

        /// <summary>`[소스] ol=25**7`</summary>
        private const double Pow25To7 = 6103515625.0;

        private const double RadToDeg = 180 / Math.PI;
        private const double DegToRad = Math.PI / 180;

        private const double WhiteX = 96.422;
        private const double WhiteY = 100;
        private const double WhiteZ = 82.521;

        /// <summary>`[소스] Us=216/24389,Ws=24389/27`</summary>
        private const double LabEpsilon = 216.0 / 24389.0;
        private const double LabKappa = 24389.0 / 27.0;

        /// <summary>`[소스] function ul(e){let t=e*e;return t*t*t*e}`</summary>
        private static double Pow7(double e)
        {
            double t = e * e;
            return t * t * t * e;
        }

        private static double LabF(double t)
        {
            return t > LabEpsilon ? Math.Cbrt(t) : (LabKappa * t + 16) / 116;
        }

        /// <summary>`[소스] Is=function(e){var t=e/255;return t<.04045?t/12.92:((t+.055)/1.055)**2.4}`</summary>
        private static double Linearize(byte channel)
        {
            double t = channel / 255.0;
            return t < 0.04045 ? t / 12.92 : Math.Pow((t + 0.055) / 1.055, 2.4);
        }

        /// <summary>`[소스] Ps=function(e,t,n){…Math.round(n*e)/n+0}` (t = 2)</summary>
        private static double Round2(double x)
        {
            return JsRound(100 * x) / 100 + 0.0;
        }

        private static double Clamp(double x, double min, double max)
        {
            return x > max ? max : x > min ? x : min;
        }

        /// <summary>16진 문자열 반올림 `[소스] ht=function(e){return{r:ut(e.r)…` · `xt=function(e){return yt[dt(e,0,255)]}`.</summary>
        private static byte RoundByte(double x)
        {
            return (byte)Clamp(JsRound(x), 0, 255);
        }

        /// <summary>JS Uint8Array 대입 — 0 쪽으로 자르고 256 으로 나눈 나머지.</summary>
        private static byte ToUint8(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                return 0;

            long t = (long)Math.Truncate(x);
            return (byte)(((t % 256) + 256) % 256);
        }
    }
}
