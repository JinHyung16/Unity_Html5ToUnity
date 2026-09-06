using System;
using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>실측 규격으로 굽는다</b> — 원본 스프라이트의 <b>실루엣 마스크</b>와 <b>팔레트</b>를 목표로 삼는다.
    ///
    /// <para>
    /// ★★ <b>왜 이 방식인가.</b> 예전에는 원본을 «보고» 도트를 손으로 찍었다. 그 결과 실루엣이
    /// 원본과 <b>절반만 겹쳤고</b>(IoU 평균 50.6%), 색 수도 원본의 절반 이하였다(19색 → 7색).
    /// 형태와 색이 사람 손끝에 달려 있으면 회차마다 흔들리고, <b>어떤 검사에도 안 걸린다</b>.
    /// </para>
    ///
    /// <para>
    /// ★ <b>지키는 선.</b> <b>실루엣과 팔레트는 실측값을 그대로 목표</b>로 쓰되,
    /// <b>색을 어디에 놓을지는 우리 규칙</b>이 정한다 — 원본의 픽셀 배치를 옮겨 적지 않는다.
    /// 규칙은 <see cref="DrawCut"/> 하나에 있다: 경계는 어둡게 · 안쪽은 밝게 · 위가 밝다,
    /// 그리고 <b>팔레트 비중대로 면적을 나눈다</b>.
    /// </para>
    ///
    /// <para>
    /// ⚠ 규격 파일은 <c>Editor/</c> 에 둔다 — 굽는 «입력»이지 런타임 자산이 아니다(빌드에 안 들어간다).
    /// </para>
    /// </summary>
    public static class UndeadArtSpecBaker
    {
        public const string SpecPath = "Assets/Undead-Slayer/Editor/UndeadArtSpecs.json";

        // ── 규격 모양 ─────────────────────────────────────────
        [Serializable]
        public sealed class SpecFile
        {
            [JsonProperty("specs")] public Dictionary<string, ArtSpec> Specs { get; set; }
        }

        [Serializable]
        public sealed class ArtSpec
        {
            [JsonProperty("frame")] public string Frame { get; set; }
            [JsonProperty("cutW")] public int CutW { get; set; }
            [JsonProperty("cutH")] public int CutH { get; set; }
            [JsonProperty("cuts")] public List<CutSpec> Cuts { get; set; }
        }

        [Serializable]
        public sealed class CutSpec
        {
            [JsonProperty("opaque")] public int Opaque { get; set; }
            [JsonProperty("fill")] public double Fill { get; set; }
            [JsonProperty("colors")] public int Colors { get; set; }
            [JsonProperty("palette")] public List<PaletteEntry> Palette { get; set; }

            /// <summary>실루엣 — <c>'#'</c> 는 불투명, <c>'.'</c> 는 투명. 줄 0 이 «위»다.</summary>
            [JsonProperty("mask")] public List<string> Mask { get; set; }

            /// <summary>
            /// 픽셀 배치 — 글자 하나가 <b>팔레트 인덱스</b>다 (<c>'.'</c> 는 투명).
            /// <para>⚠ 실루엣·팔레트만으로는 «그림»이 안 나온다 — 그 둘이 완전히 맞아도 픽셀 일치가 13~34% 였다.</para>
            /// </summary>
            [JsonProperty("pix")] public List<string> Pix { get; set; }

            /// <summary>
            /// 알파 <b>단계 표</b> — 없으면 <b>전부 불투명</b>이다.
            ///
            /// <para>
            /// ⚠ [사고] 알파를 «있다/없다» 둘로만 담았더니, 원본의 <b>부드러운 그림자</b>(알파 7~181)가
            /// 전부 <b>불투명</b>이 되어 버튼 뒤에 <b>검은 판</b>이 생겼다.
            /// 실루엣·팔레트·픽셀 배치가 다 맞는데도 그림이 달랐다 —
            /// <b>알파는 «색»이 아니라 따로 재야 하는 축</b>이다.
            /// </para>
            /// </summary>
            [JsonProperty("apal")] public List<int> AlphaPalette { get; set; }

            /// <summary>알파 배치 — 글자 하나가 <see cref="AlphaPalette"/> 인덱스다 (<c>'.'</c> 는 투명).</summary>
            [JsonProperty("apix")] public List<string> AlphaPix { get; set; }
        }

        [Serializable]
        public sealed class PaletteEntry
        {
            [JsonProperty("c")] public string Hex { get; set; }

            /// <summary>불투명 픽셀 중 이 색의 비중.</summary>
            [JsonProperty("p")] public double Ratio { get; set; }
        }

        /// <summary>
        /// 규격을 읽는다.
        /// <para>⚠ <b>캐시하지 않는다</b> — 정적 캐시를 두면 규격을 다시 재서 넣어도 «옛 값»으로 굽는다
        /// (도메인 리로드 전까지 살아 있다). 굽기는 자주 도는 일이 아니라 매번 읽어도 된다.</para>
        /// </summary>
        public static SpecFile Load()
        {
            if (File.Exists(SpecPath) == false)
            {
                Log.Error($"아트 규격이 없다: {SpecPath} — 원본을 다시 재야 한다");
                return null;
            }

            return JsonConvert.DeserializeObject<SpecFile>(File.ReadAllText(SpecPath));
        }

        public static ArtSpec Find(string code)
        {
            SpecFile file = Load();

            if (file == null || file.Specs == null)
                return null;

            return file.Specs.TryGetValue(code, out ArtSpec spec) ? spec : null;
        }

        /// <summary>
        /// 시트 한 장을 규격대로 굽는다.
        /// <para>⚠ 표의 <c>FrameWidth/Height</c> 와 규격의 컷 크기가 다르면 <b>표가 틀린 것</b>이다 — 굽지 않고 알린다.</para>
        /// </summary>
        public static Texture2D Bake(UndeadArtData a, ArtSpec spec, Func<int, int, Texture2D> newTexture,
                                     Action<Texture2D, int, int, Color> put)
        {
            if (spec.CutW != a.FrameWidth || spec.CutH != a.FrameHeight)
            {
                Log.Error($"{a.Code}: 표의 컷 {a.FrameWidth}x{a.FrameHeight} 가 실측 {spec.CutW}x{spec.CutH} 와 다르다");
                return null;
            }

            Texture2D tex = newTexture(a.SheetWidth, a.SheetHeight);
            int perRow = Math.Max(1, a.Cols);
            int want = Math.Max(1, a.UsedCols) * Math.Max(1, a.Rows);

            // ★ 원본이 «앞에서부터» 안 쓰는 시트가 있다 [표 CutOffset — warrior_lay 는 2번 컷부터].
            //   ⚠ 이걸 빼먹으면 «다른 자세»가 조용히 구워진다 — 크기도 컷 수도 맞아서 검사에 안 걸린다.
            int from = Math.Max(0, a.CutOffset);
            int count = Math.Min(want, Math.Max(0, spec.Cuts.Count - from));

            for (int i = 0; i < count; i++)
            {
                int col = i % perRow;
                int row = i / perRow;
                int ox = col * a.FrameWidth;

                // ⚠ 유니티 텍스처는 «아래»가 0 이다 — 시트의 첫 줄이 위로 가게 뒤집어 놓는다
                int oy = a.SheetHeight - (row + 1) * a.FrameHeight;

                DrawCut(tex, ox, oy, spec.Cuts[from + i], put);
            }

            // 컷이 모자라면 마지막 것을 반복한다 — 빈 칸은 «투명 프레임»이 되어 깜빡인다
            for (int i = count; i < want && count > 0; i++)
            {
                int col = i % perRow;
                int row = i / perRow;
                DrawCut(tex, col * a.FrameWidth, a.SheetHeight - (row + 1) * a.FrameHeight,
                        spec.Cuts[from + count - 1], put);
            }

            return tex;
        }

        /// <summary>
        /// 컷 하나를 그린다 — <b>여기가 «우리 규칙»의 전부</b>다.
        ///
        /// <para>
        /// ① 마스크에서 <b>경계까지의 거리</b>를 잰다 (BFS). ② 픽셀마다 점수를 매긴다 —
        /// <c>깊이</c>가 클수록(안쪽), <c>세로</c>가 위일수록 높다. ③ 팔레트를 <b>밝기 오름차순</b>으로
        /// 정렬해 <b>비중 누적</b>으로 점수 구간에 나눠 준다.
        /// </para>
        ///
        /// <para>
        /// ⇒ 어두운 색이 «경계와 아래»로, 밝은 색이 «안쪽과 위»로 간다 — 픽셀아트의 통상 명암이고,
        /// <b>면적 비중은 원본과 같아진다</b>. 원본의 «어느 픽셀이 무슨 색인지»는 쓰지 않는다.
        /// </para>
        /// </summary>
        /// <summary>팔레트 인덱스 글자 — 재는 쪽과 «같은 표»를 쓴다.</summary>
        private const string IndexChars =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+,-/:;<=>?@[]^_`{|}~";

        private static void DrawCut(Texture2D tex, int ox, int oy, CutSpec cut, Action<Texture2D, int, int, Color> put)
        {
            // ★★ 픽셀 배치가 규격에 있으면 «그대로» 칠한다.
            //   ⚠ 예전에는 실루엣과 팔레트만 목표로 삼고 안쪽은 우리 규칙으로 칠했다 —
            //     실루엣 IoU 100% · 팔레트 거리 0 인데 «픽셀 일치는 13~34%» 였고, 사람 눈에는 다른 그림이었다.
            //     16×16 도트에서는 픽셀 배치가 곧 형태다(눈 2px · 입 1px) — 칠하는 규칙이 낄 자리가 없다.
            if (cut.Pix != null && cut.Pix.Count > 0)
            {
                DrawPixels(tex, ox, oy, cut, put);
                return;
            }

            List<string> mask = cut.Mask;
            int h = mask.Count;
            int w = mask[0].Length;

            // ── ① 경계까지의 거리 (BFS)
            var depth = new int[w * h];
            var queue = new Queue<int>();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;

                    if (mask[y][x] != '#')
                    {
                        depth[i] = 0;
                        continue;
                    }

                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                || mask[y][x - 1] != '#' || mask[y][x + 1] != '#'
                                || mask[y - 1][x] != '#' || mask[y + 1][x] != '#';

                    if (edge)
                    {
                        depth[i] = 1;
                        queue.Enqueue(i);
                    }
                    else
                    {
                        depth[i] = int.MaxValue;
                    }
                }
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w;
                int y = i / w;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);

                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;

                    int ni = ny * w + nx;

                    if (mask[ny][nx] != '#' || depth[ni] <= depth[i] + 1)
                        continue;

                    depth[ni] = depth[i] + 1;
                    queue.Enqueue(ni);
                }
            }

            int maxDepth = 1;

            for (int i = 0; i < depth.Length; i++)
            {
                if (depth[i] != int.MaxValue && depth[i] > maxDepth)
                    maxDepth = depth[i];
            }

            // ── ② 팔레트를 «두 갈래»로 나눈다 — 윤곽 색과 몸 색
            //
            // ★ 픽셀아트 캐릭터는 «가로 층»으로 읽힌다(머리 → 몸 → 다리). 그래서 몸 색은
            //   세로 밴드로 나누고, 채도가 낮은 색(검정·회색 = 윤곽·그림자)만 «경계»로 보낸다.
            //   밝기 하나로만 줄 세우면 빨간 옷·파란 눈 같은 유채색이 «가로 띠»로 끼어들어
            //   무엇을 그린 것인지 안 읽힌다.
            var outline = new List<(Color color, double ratio, float luma)>(4);
            var body = new List<(Color color, double ratio, float luma)>(cut.Palette.Count);

            foreach (PaletteEntry e in cut.Palette)
            {
                if (ColorUtility.TryParseHtmlString("#" + e.Hex, out Color c) == false)
                    continue;

                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                Color.RGBToHSV(c, out _, out float sat, out _);

                // 채도가 낮고 어두우면 윤곽·그림자다 [원본 팔레트가 대개 그렇다]
                if (sat < 0.18f && luma < 0.35f)
                    outline.Add((c, e.Ratio, luma));
                else
                    body.Add((c, e.Ratio, luma));
            }

            if (outline.Count == 0 && body.Count == 0)
                return;

            // 윤곽이 하나도 안 갈렸으면 가장 어두운 색을 윤곽으로 쓴다
            if (outline.Count == 0)
            {
                body.Sort((a, b) => a.luma.CompareTo(b.luma));
                outline.Add(body[0]);
                body.RemoveAt(0);
            }

            outline.Sort((a, b) => a.luma.CompareTo(b.luma));
            body.Sort((a, b) => b.luma.CompareTo(a.luma));   // 밝은 것이 «위»로 간다

            double outlineRatio = 0.0;

            foreach (var o in outline)
                outlineRatio += o.ratio;

            // ── ③ 픽셀을 «윤곽»과 «몸»으로 가른다 — 경계에서 가까운 순으로 윤곽 몫을 뗀다
            var all = new List<int>(w * h);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y][x] == '#')
                        all.Add(y * w + x);
                }
            }

            if (all.Count == 0)
                return;

            all.Sort((a, b) =>
            {
                int da = depth[a];
                int db = depth[b];

                if (da != db)
                    return da.CompareTo(db);

                return a.CompareTo(b);
            });

            int outlineCount = Mathf.Clamp(Mathf.RoundToInt((float)outlineRatio * all.Count), 0, all.Count);

            // 윤곽 — 얕은(경계에 가까운) 픽셀부터. 어두운 색이 가장 바깥이다.
            int cursor = 0;
            double acc = 0.0;

            for (int i = 0; i < outline.Count; i++)
            {
                acc += outline[i].ratio;
                int end = i == outline.Count - 1
                    ? outlineCount
                    : Mathf.Clamp(Mathf.RoundToInt((float)(acc / System.Math.Max(1e-9, outlineRatio)) * outlineCount), cursor, outlineCount);

                for (; cursor < end; cursor++)
                    Paint(tex, ox, oy, all[cursor], w, h, outline[i].color, put);
            }

            // ── ④ 몸 — «세로 밴드»로 나눈다. 위에서부터 밝은 색 순서다.
            var rest = new List<int>(all.Count - cursor);

            for (int i = cursor; i < all.Count; i++)
                rest.Add(all[i]);

            if (body.Count == 0)
            {
                foreach (int i in rest)
                    Paint(tex, ox, oy, i, w, h, outline[outline.Count - 1].color, put);

                return;
            }

            // 같은 줄은 «같은 밴드»에 들어가야 층으로 읽힌다 — 세로(y) 우선으로 줄 세운다
            rest.Sort((a, b) =>
            {
                int ya = a / w;
                int yb = b / w;

                if (ya != yb)
                    return ya.CompareTo(yb);

                return a.CompareTo(b);
            });

            double bodyRatio = 0.0;

            foreach (var c in body)
                bodyRatio += c.ratio;

            cursor = 0;
            acc = 0.0;

            for (int i = 0; i < body.Count; i++)
            {
                acc += body[i].ratio;
                int end = i == body.Count - 1
                    ? rest.Count
                    : Mathf.Clamp(Mathf.RoundToInt((float)(acc / System.Math.Max(1e-9, bodyRatio)) * rest.Count), cursor, rest.Count);

                for (; cursor < end; cursor++)
                    Paint(tex, ox, oy, rest[cursor], w, h, body[i].color, put);
            }
        }

        /// <summary>규격의 픽셀 배치를 그대로 칠한다 — 글자 하나가 팔레트 인덱스다.</summary>
        private static void DrawPixels(Texture2D tex, int ox, int oy, CutSpec cut, Action<Texture2D, int, int, Color> put)
        {
            var colors = new Color[cut.Palette.Count];

            for (int i = 0; i < cut.Palette.Count; i++)
            {
                if (ColorUtility.TryParseHtmlString("#" + cut.Palette[i].Hex, out Color c))
                    colors[i] = c;
            }

            int h = cut.Pix.Count;
            bool hasAlpha = cut.AlphaPalette != null && cut.AlphaPix != null && cut.AlphaPix.Count == h;

            for (int y = 0; y < h; y++)
            {
                string line = cut.Pix[y];
                string aline = hasAlpha ? cut.AlphaPix[y] : null;

                for (int x = 0; x < line.Length; x++)
                {
                    char ch = line[x];

                    if (ch == '.')
                        continue;

                    int index = IndexChars.IndexOf(ch);

                    if (index < 0 || index >= colors.Length)
                        continue;

                    Color color = colors[index];

                    // ★★ 알파를 «규격대로» 칠한다 — 없으면 불투명이다.
                    //   ⚠ 이걸 안 하면 원본의 부드러운 그림자가 «불투명 판»이 된다.
                    if (aline != null && x < aline.Length && aline[x] != '.')
                    {
                        int a = IndexChars.IndexOf(aline[x]);

                        if (a >= 0 && a < cut.AlphaPalette.Count)
                            color.a = cut.AlphaPalette[a] / 255f;
                    }

                    // 규격 줄 0 이 «위»이므로 텍스처 y 를 뒤집는다
                    put(tex, ox + x, oy + (h - 1 - y), color);
                }
            }
        }

        /// <summary>마스크 줄 0 이 «위»이므로 텍스처 y 를 뒤집어 찍는다.</summary>
        private static void Paint(Texture2D tex, int ox, int oy, int index, int w, int h,
                                  Color color, Action<Texture2D, int, int, Color> put)
        {
            int x = index % w;
            int y = index / w;
            put(tex, ox + x, oy + (h - 1 - y), color);

        }
    }
}
