using System;
using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Undead Slayer 의 아트를 <b>전부 새로 굽는다</b> — 회차 10 「원본과 «보이는 만큼» 가깝게」.
    ///
    /// <para>
    /// ⚠ <b>원본 에셋은 상용(ANV Games / Poki)이라 추출·복사가 금지</b>다 (확정표 7 · <c>06_리소스.md</c>).
    /// 그래서 «존재하는 리소스»가 원리적으로 0개이고, <b>이 파일(과 partial 셋)이 리소스 목록의 전부</b>다.
    /// 원본을 «보고» 실루엣·팔레트·디테일 밀도를 맞춰 <b>우리 손으로 도트를 찍었다</b> — 픽셀 복사는 없다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>크기·격자·피벗은 «데이터 표»가 정한다</b> — <c>UndeadArtTable</c> 의 실측값을 읽는다.
    /// 여기에 숫자를 다시 적지 않는다. 도트 맵의 크기가 표와 다르면 <b>굽지 않고 오류</b>다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>픽셀아트라 슈퍼샘플을 쓰지 않는다.</b> 원본이 16×16 도트이므로 안티에일리어싱을 넣으면
    /// 확대했을 때 원본과 «질감»이 갈린다. 점 하나가 곧 한 픽셀이다.
    /// </para>
    ///
    /// <para>
    /// 파일 구성 — <c>.Characters</c>(인물·적·수집물) · <c>.Fx</c>(투사체·불·보스·나무) · <c>.Ui</c>(패널·아이콘·타일셋).
    /// 도트 맵 규약은 <see cref="Blit"/> — <b>첫 줄이 위</b>이고 <c>.</c> 은 투명, <c>k</c> 는 외곽선이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 이것은 <b>「구성하는 도구」</b>다 — 이관이 끝나도 <b>지우지 않는다</b>.
    /// 없으면 「사람이 그림 편집기로」로 돌아간다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadSpriteBuilder.BuildAll</c></para>
    /// </summary>
    public static partial class UndeadSpriteBuilder
    {
        private const string ArtRoot = "Assets/Undead-Slayer/Art";
        private const string GameDir = ArtRoot + "/Game";
        private const string UiDir = ArtRoot + "/UI";
        private const string DataFolder = "Assets/Undead-Slayer/Data";

        /// <summary>지형 타일셋 — 표에 없다(아틀라스가 아니라 «별도 텍스처»다 · 05_연출). 24×24 격자.</summary>
        public const string TilesetCode = "biome_graveyard_tiles";

        // ── 공통 팔레트. 원본 «화면»에서 집은 색조다 (아트 재제작 — 픽셀 동일이 아니라 «퀄리티 동일»이 기준 · 확정표 12).
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Outline = Hex("1A1423");

        public static void BuildAll()
        {
            EnsureFolder(GameDir);
            EnsureFolder(UiDir);

            UndeadArtDataContainer art = LoadArt();

            if (art == null)
                return;

            int baked = 0;
            int failed = 0;

            for (int i = 0; i < art.AllValues.Count; i++)
            {
                UndeadArtData a = art.AllValues[i];
                Texture2D tex;

                try
                {
                    tex = Bake(a);
                }
                catch (Exception e)
                {
                    Log.Error($"굽다 실패: {a.Code} — {e.Message}");
                    failed++;
                    continue;
                }

                if (tex == null)
                {
                    Log.Error($"굽는 방법이 없다: {a.Code} — 표에는 있는데 도트가 없다. 자리표시자로 굽는다");
                    tex = Placeholder(a);
                    failed++;
                }

                if (tex.width != a.SheetWidth || tex.height != a.SheetHeight)
                {
                    Log.Error($"{a.Code}: 구운 크기 {tex.width}×{tex.height} 가 표 {a.SheetWidth}×{a.SheetHeight} 와 다르다");
                    failed++;
                }

                string dir = a.Category == UndeadArtDataContainer.CategoryUi ? UiDir : GameDir;
                Save(tex, $"{dir}/{a.Code}.png");
                baked++;
            }

            Save(BakeTileset(), $"{GameDir}/{TilesetCode}.png");
            baked++;

            AssetDatabase.Refresh();

            if (failed > 0)
                Log.Error($"Undead Slayer 아트 굽기 — {baked}장 중 {failed}장 결함 (위 오류)");
            else
                Log.Success($"Undead Slayer 아트 굽기 완료 — {baked}장 (전부 재제작 · 원본 추출 0 · 타일셋 {JinHyung.UndeadSlayer.UndeadTerrainView.TilesetColumns}칸)");
        }

        // ══════════════════════════════ 개체별 굽기 — 표의 Code 로 갈린다

        private static Texture2D Bake(UndeadArtData a)
        {
            switch (a.Code)
            {
                // 인물·적·수집물 (.Characters)
                case "hero": return BakeHero(a);
                case "enemy1": return BakeZombie(a);
                case "enemy2": return BakeSkeleton(a);
                case "enemy_bat": return BakeBat(a);
                case "enemy_eye": return BakeEye(a);
                case "enemy_revenant": return BakeRevenant(a);
                case "gem": return BakeGem(a, GemGreen, GemGreenLight, GemGreenDark);
                case "gem_icon": return BakeGemIcon(a);
                case "heart": return BakeHeart(a);
                case "arcane_fragment": return BakeFragment(a);
                case "warrior_lay": return BakeWarriorLay(a);
                case "warrior_idle": return BakeWarriorIdle(a);
                case "warrior_run": return BakeWarriorRun(a);
                case "mage": return BakeMage(a);
                case "family": return BakeFamily(a);
                case "farmer": return BakeFarmer(a);
                case "sheep": return BakeSheep(a);
                case "hp_segment": return BakeHpSegment(a);

                // 투사체·불·보스·나무 (.Fx)
                case "projectile1": return BakeBolt(a);
                case "fire": return BakeFire(a);
                case "fire_inactive": return BakeFireInactive(a);
                case "fireball": return BakeFireball(a);
                case "meteor": return BakeMeteor(a);
                case "lightning": return BakeLightning(a);
                case "darksoul": return BakeDarkSoul(a);
                case "graveyard_evil_tree": return BakeTree(a, ETreeLook.Active);
                case "graveyard_evil_tree_activated": return BakeTree(a, ETreeLook.Activated);
                case "graveyard_evil_tree_inactive": return BakeTree(a, ETreeLook.Inactive);

                // UI (.Ui)
                case "bubble": return BakeScroll(a);
                case "bubble_medium": return BakeScroll(a);
                case "skill_bg": return BakeFrame9(a, Hex("1E2238"), Hex("E9EDF5"), 1);
                case "task_bg": return BakeFrame9(a, Hex("1E2238"), Hex("E9EDF5"), 1);
                case "bar": return BakeBar(a);
                case "lvl_bg": return BakeFrame9(a, Hex("F4F6FA"), Hex("58B84A"), 3);
                case "reward_button_bg": return BakeRewardButton(a);
                case "btn_shadowed": return BakeShadowedButton(a);
                case "icon_bullet_damage": return BakeCometIcon(a, true);
                case "icon_bullets_firerate": return BakeCometIcon(a, false);
                case "icon_hero_collectradius": return BakeHeroIcon(a, GemGreen);
                case "icon_hero_movespeed": return BakeHeroIcon(a, HeroRed);
                case "icon_lightning": return BakeEnergyIcon(a, 0);
                case "icon_lightning_damage": return BakeEnergyIcon(a, 1);
                case "icon_lightning_radius": return BakeEnergyIcon(a, 2);
                case "icon_lightning_speed": return BakeEnergyIcon(a, 3);
                case "icon_quest_open": return BakeQuestIcon(a, false);
                case "icon_quest_complete": return BakeQuestIcon(a, true);
                case "icon_clipperboard": return BakeClapper(a);
                case "quest_pointer": return BakePointer(a);
                default: return null;
            }
        }

        /// <summary>표에는 있는데 그리는 방법이 없는 것 — <b>눈에 띄는 자리표시자</b>로 굽는다.</summary>
        private static Texture2D Placeholder(UndeadArtData a)
        {
            Texture2D tex = New(a.SheetWidth, a.SheetHeight);
            Color fill = Hex("FF00FF");

            for (int y = 0; y < a.SheetHeight; y++)
            {
                for (int x = 0; x < a.SheetWidth; x++)
                {
                    bool edge = x == 0 || y == 0 || x == a.SheetWidth - 1 || y == a.SheetHeight - 1;
                    tex.SetPixel(x, y, edge || (x + y) % 8 == 0 ? fill : Clear);
                }
            }

            tex.Apply();
            return tex;
        }

        // ══════════════════════════════ 도트 맵

        /// <summary>
        /// 도트 맵을 찍는다. <b>첫 줄이 위</b>다(그림을 그리는 순서 그대로). <c>.</c> 은 투명.
        /// <para>팔레트는 <c>"k=1A1423 a=D9DCE3 …"</c> 꼴 — 글자 하나가 색 하나다.</para>
        /// <para>⚠ 줄 길이가 서로 다르면 <b>굽지 않고 예외</b>다 — 조용히 어긋난 도트는 화면에서 못 찾는다.</para>
        /// </summary>
        private static void Blit(Texture2D tex, int ox, int oy, string[] rows, Dictionary<char, Color> pal, bool flipX = false)
        {
            int h = rows.Length;
            int w = rows[0].Length;

            for (int i = 0; i < h; i++)
            {
                if (rows[i].Length != w)
                    throw new InvalidOperationException($"도트 맵 {i}번째 줄 길이 {rows[i].Length} ≠ {w}: \"{rows[i]}\"");

                int y = oy + (h - 1 - i);

                for (int x = 0; x < w; x++)
                {
                    char c = rows[i][x];

                    if (c == '.')
                        continue;

                    if (pal.TryGetValue(c, out Color color) == false)
                        throw new InvalidOperationException($"팔레트에 '{c}' 가 없다 (줄 {i})");

                    int px = ox + (flipX ? w - 1 - x : x);
                    Put(tex, px, y, color);
                }
            }
        }

        /// <summary>맵을 <paramref name="scale"/> 배로 키워 찍는다 — 큰 개체(나무·보스)는 굵은 도트로 그린다.</summary>
        private static void BlitScaled(Texture2D tex, int ox, int oy, string[] rows, Dictionary<char, Color> pal, int scale, bool flipX = false)
        {
            int h = rows.Length;
            int w = rows[0].Length;

            for (int i = 0; i < h; i++)
            {
                if (rows[i].Length != w)
                    throw new InvalidOperationException($"도트 맵 {i}번째 줄 길이 {rows[i].Length} ≠ {w}: \"{rows[i]}\"");

                for (int x = 0; x < w; x++)
                {
                    char c = rows[i][x];

                    if (c == '.')
                        continue;

                    if (pal.TryGetValue(c, out Color color) == false)
                        throw new InvalidOperationException($"팔레트에 '{c}' 가 없다 (줄 {i})");

                    int sx = flipX ? w - 1 - x : x;
                    Rect(tex, ox + sx * scale, oy + (h - 1 - i) * scale, scale, scale, color);
                }
            }
        }

        /// <summary><c>"k=1A1423 a=D9DCE3"</c> → 팔레트. <c>k</c> 가 없으면 외곽선 색을 넣는다.</summary>
        private static Dictionary<char, Color> Pal(string spec)
        {
            var pal = new Dictionary<char, Color> { { 'k', Outline } };
            string[] parts = spec.Split(' ');

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length < 3 || parts[i][1] != '=')
                    continue;

                pal[parts[i][0]] = Hex(parts[i].Substring(2));
            }

            return pal;
        }

        // ══════════════════════════════ 픽셀 유틸

        private static Texture2D New(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[w * h];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Clear;

            tex.SetPixels(pixels);
            return tex;
        }

        private static void Put(Texture2D tex, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height)
                return;

            tex.SetPixel(x, y, c);
        }

        private static void Rect(Texture2D tex, int ox, int oy, int w, int h, Color c)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    Put(tex, ox + x, oy + y, c);
            }
        }

        /// <summary>찬 원. 도트 스타일이라 반지름을 정수 격자로 판정한다.</summary>
        private static void Disc(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= r * r + r)
                        Put(tex, cx + x, cy + y, c);
                }
            }
        }

        /// <summary>찬 타원.</summary>
        private static void Ellipse(Texture2D tex, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = -ry; y <= ry; y++)
            {
                for (int x = -rx; x <= rx; x++)
                {
                    double nx = x / (rx + 0.5);
                    double ny = y / (ry + 0.5);

                    if (nx * nx + ny * ny <= 1.0)
                        Put(tex, cx + x, cy + y, c);
                }
            }
        }

        /// <summary>둥근 사각형 — UI 버튼·패널의 바탕.</summary>
        private static void RoundRect(Texture2D tex, int ox, int oy, int w, int h, int radius, Color c)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dx = x < radius ? radius - x : (x >= w - radius ? x - (w - 1 - radius) : 0);
                    int dy = y < radius ? radius - y : (y >= h - radius ? y - (h - 1 - radius) : 0);

                    if (dx * dx + dy * dy <= radius * radius + radius)
                        Put(tex, ox + x, oy + y, c);
                }
            }
        }

        /// <summary>칠해진 픽셀의 «바깥 테두리»에 외곽선을 두른다 — 원본 도트의 공통 스타일이다.</summary>
        private static void OutlineSilhouette(Texture2D tex, int ox, int oy, int w, int h)
        {
            OutlineSilhouette(tex, ox, oy, w, h, Outline);
        }

        private static void OutlineSilhouette(Texture2D tex, int ox, int oy, int w, int h, Color color)
        {
            var add = new List<Vector2Int>(w * h / 4);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int px = ox + x;
                    int py = oy + y;

                    if (px >= tex.width || py >= tex.height)
                        continue;

                    if (tex.GetPixel(px, py).a > 0.01f)
                        continue;

                    if (Filled(tex, px - 1, py, ox, oy, w, h) || Filled(tex, px + 1, py, ox, oy, w, h) ||
                        Filled(tex, px, py - 1, ox, oy, w, h) || Filled(tex, px, py + 1, ox, oy, w, h))
                    {
                        add.Add(new Vector2Int(px, py));
                    }
                }
            }

            for (int i = 0; i < add.Count; i++)
                tex.SetPixel(add[i].x, add[i].y, color);
        }

        /// <summary>발밑 그림자 — 원본 인물 도트의 맨 아랫줄에 있는 어두운 띠 [원본 «보고»].</summary>
        private static void Shadow(Texture2D tex, int ox, int oy, int fromX, int width)
        {
            var c = new Color(0.10f, 0.08f, 0.14f, 0.55f);

            for (int x = 0; x < width; x++)
                Put(tex, ox + fromX + x, oy, c);
        }

        private static bool Filled(Texture2D tex, int x, int y, int ox, int oy, int w, int h)
        {
            if (x < ox || y < oy || x >= ox + w || y >= oy + h)
                return false;

            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height)
                return false;

            return tex.GetPixel(x, y).a > 0.01f;
        }

        /// <summary>결정적 잡음 — 같은 씨앗은 언제나 같은 무늬다 (지형·불꽃의 «결»에 쓴다).</summary>
        private static int Noise(int x, int y, int seed)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 1274126177;
                n = (n ^ (n >> 13)) * 1274126177;
                return (n ^ (n >> 16)) & 0x7fffffff;
            }
        }

        private static void Save(Texture2D tex, string path)
        {
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static UndeadArtDataContainer LoadArt()
        {
            var art = new UndeadArtDataContainer();
            string path = Path.Combine(DataFolder, art.Name + ".json");

            if (File.Exists(path) == false)
            {
                Log.Error($"아트 표가 없다: {path}");
                return null;
            }

            art.LoadJson(File.ReadAllText(path));

            if (art.Validate(out string error) == false)
            {
                Log.Error($"아트 표가 검사를 통과하지 못했다 — 굽지 않는다\n{error}");
                return null;
            }

            return art;
        }

        private static void EnsureFolder(string path)
        {
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
