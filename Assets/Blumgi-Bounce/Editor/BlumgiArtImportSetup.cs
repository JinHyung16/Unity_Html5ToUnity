using System.Collections.Generic;
using System.IO;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Extensions;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Blumgi Bounce 아트의 임포트 설정을 <b>코드로</b> 건다.
    /// 사람에게 인스펙터를 요구하지 않는다 (CLAUDE.md 「사람에게 유니티 조작을 요구하지 않는다」).
    ///
    /// <para>
    /// ★ <b>PPU 가 두 값이다.</b> 확정 E 가 <b>게임 월드(world 1:1 · PPU 1)</b> 와
    /// <b>UI 캔버스(1920×1080)</b> 를 갈랐기 때문이다.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><c>Art/Game/**</c> → <b>PPU 1</b>. 텍스처 1 px = 원본 world 1 px = Unity 1 unit.</item>
    /// <item><c>Art/UI/**</c> → <b>PPU 100</b>. ⚠ <b>캔버스 <c>referencePixelsPerUnit</c> 와 같은 값이어야 한다</b> —
    /// 어긋나면 <c>Image</c> 가 9-slice 경계를 그 비로 나눠 써 <b>모서리가 통째로 늘어난다</b>
    /// (첫 게임 실측 사고: 둥근 사각형이 타원이 됐다).</item>
    /// </list>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiArtImportSetup.Setup</c></para>
    /// </summary>
    public static class BlumgiArtImportSetup
    {
        private const string ArtFolder = "Assets/Blumgi-Bounce/Art";
        private const string GameFolder = ArtFolder + "/Game";

        // ★ PPU 를 여기 «복사»하지 않는다. 확정 E 가 PD 재검토 중이라(Box2D 미터 가설 → PPU 50 후보)
        //   상수가 두 곳에 있으면 한쪽만 고쳐지고 텍스처와 배치가 갈린다.
        //   ⇒ 유일한 정본은 `BlumgiUnits.WorldPixelsPerUnit` 하나다.

        /// <summary>
        /// 반복(타일링)하는 텍스처. <c>Clamp</c> 로 두면 이음매에서 <b>늘어난 픽셀 한 줄</b>이 보인다.
        /// 격자·야자수 기둥·레벨 도트 바가 전부 원본에서 타일드 배경이다 [UIUX 2-g · 05_연출 §1].
        /// </summary>
        private static readonly HashSet<string> Tiled = new HashSet<string>
        {
            "grid_tile", "palm_trunk", "ui_dot_empty", "ui_dot_full",
        };

        public static void Setup()
        {
            if (AssetDatabase.IsValidFolder(ArtFolder) == false)
            {
                Log.Error($"아트 폴더가 없다: {ArtFolder} — 먼저 BlumgiSpriteBuilder.BuildAll 을 돌린다.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });

            if (guids.IsNullOrEmpty())
            {
                Log.Error($"아트가 0개다: {ArtFolder}");
                return;
            }

            var changed = new List<string>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                    continue;

                if (Apply(importer, path))
                {
                    importer.SaveAndReimport();
                    changed.Add(Path.GetFileName(path));
                }
            }

            AssetDatabase.SaveAssets();

            int notMultipleOfFour = ReportSizes(guids);

            Log.Success($"Blumgi 아트 임포트 설정 — 대상 {guids.Length}개 · 변경 {changed.Count}개");

            if (notMultipleOfFour > 0)
                Log.Error($"4의 배수가 아닌 텍스처가 {notMultipleOfFour}개 남았다 (확정표 7 · ASTC)");
        }

        private static bool Apply(TextureImporter importer, string path)
        {
            bool dirty = false;
            string file = Path.GetFileNameWithoutExtension(path);
            bool isWorld = path.StartsWith(GameFolder);

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            // Pivot Center — 원본 좌표가 전부 «중심»이다 (블록 중심 · 림 중심 · 발사대 중심) [UIUX 2-d].
            //
            // ★ 메시 타입도 여기서 정한다. 늘리거나(9-slice) 반복하는(Tiled) 스프라이트는
            //   ⚠ <b>Full Rect 가 아니면 유니티가 그리기를 거부하거나 경고를 낸다</b> —
            //   Tight 메시는 «투명 부분을 잘라낸 다각형»이라 타일 경계가 맞지 않는다.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            SpriteMeshType meshType = NeedsFullRect(file) ? SpriteMeshType.FullRect : SpriteMeshType.Tight;

            // ★ 대부분은 Center 지만 «프레임끼리 자리가 맞아야 하는» 것은 원점을 따로 준다 (아래 CustomPivot).
            Vector2? custom = CustomPivot(file);
            int alignment = custom.HasValue ? (int)SpriteAlignment.Custom : (int)SpriteAlignment.Center;
            Vector2 pivot = custom ?? new Vector2(0.5f, 0.5f);

            if (settings.spriteAlignment != alignment
                || settings.spriteMeshType != meshType
                || (custom.HasValue && settings.spritePivot != pivot))
            {
                settings.spriteAlignment = alignment;
                settings.spritePivot = pivot;
                settings.spriteMeshType = meshType;
                importer.SetTextureSettings(settings);
                dirty = true;
            }

            float ppu = isWorld ? BlumgiUnits.WorldPixelsPerUnit : BlumgiUnits.UiPixelsPerUnit;

            if (Mathf.Approximately(importer.spritePixelsPerUnit, ppu) == false)
            {
                importer.spritePixelsPerUnit = ppu;
                dirty = true;
            }

            // ⚠ 밉맵을 만들면 축소될 때 원본보다 흐려진다. 원본은 브라우저가 그대로 그린다.
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (importer.alphaIsTransparency == false)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            TextureWrapMode wrap = Tiled.Contains(file) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            if (importer.wrapMode != wrap)
            {
                importer.wrapMode = wrap;
                dirty = true;
            }

            // 원본은 1280 world 를 임의 크기로 «매끄럽게» 스케일해 그린다 [UIUX 1-a] — Point 로 두면 계단이 생긴다.
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                dirty = true;
            }

            int border = GetSliceBorder(file);

            if (border > 0)
            {
                var b = new Vector4(border, border, border, border);

                if (importer.spriteBorder != b)
                {
                    importer.spriteBorder = b;
                    dirty = true;
                }
            }

            // 모바일 압축 — 확정표 1-b (Android · ASTC). 4의 배수 규칙의 근거가 여기다.
            var android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 50,
                maxTextureSize = 2048,
            };

            TextureImporterPlatformSettings current = importer.GetPlatformTextureSettings("Android");

            if (current.overridden == false || current.format != android.format)
            {
                importer.SetPlatformTextureSettings(android);
                dirty = true;
            }

            return dirty;
        }

        /// <summary>
        /// <b>원점을 따로 줘야 하는 스프라이트</b>. 없으면 <c>null</c> = Pivot Center.
        ///
        /// <para>
        /// ★ <c>blob_eyes_shout</c>(프레임 2) 는 <b>세로가 24 → 48 로 두 배</b>라, Center 로 두면
        /// 프레임이 바뀌는 순간 <b>눈이 12 px 아래로 뚝 떨어진다</b>. 원본은 세 프레임의 원점이
        /// 전부 «위에서 9~10 px» 라 <b>눈이 제자리에 있고 입만 아래로 자란다</b>
        /// [실측 <c>org</c> f0 0.409×22 = 9.0 · f1 0.357×28 = 10.0 · f2 0.191×47 = 9.0].
        /// </para>
        ///
        /// <para>
        /// 우리 <c>blob_eyes_idle</c> 은 24 px 이고 Center 원점이 «위에서 12 px», 눈 중심이 «위에서 8 px»
        /// 이라 <b>원점−눈 = 4 px</b> 다. <c>shout</c> 의 눈 중심은 위에서 8.5 px 이므로
        /// 원점을 <b>위에서 12.5 px</b> 에 두면 두 프레임의 눈이 정확히 겹친다 ⇒ y = (48 − 12.5) / 48.
        /// ⚠ <c>idle</c>·<c>angry</c> 는 <b>일부러 안 건드린다</b> — 이미 프리팹 배치가 그 원점 기준이다.
        /// </para>
        /// </summary>
        private static Vector2? CustomPivot(string file)
        {
            switch (file)
            {
                case "blob_eyes_shout": return new Vector2(0.5f, (48f - 12.5f) / 48f);
                default: return null;
            }
        }

        /// <summary>
        /// <b>Full Rect</b> 가 필요한 스프라이트 — 9-slice 로 늘리거나, Tiled 로 반복하거나,
        /// 판으로 늘려 쓰는 것들이다. 그 밖에는 Tight 로 두어 <b>블록 462장의 오버드로를 줄인다</b>.
        /// </summary>
        private static bool NeedsFullRect(string file)
        {
            switch (file)
            {
                case "grid_tile":
                case "palm_trunk":
                case "solid":
                case "ui_dot_empty":
                case "ui_dot_full":
                case "ui_panel_9p":
                case "ui_tut_screen":
                case "ui_gradient":
                case "ui_card":
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 9-slice 경계. <b>굽는 쪽과 같은 값</b>이어야 한다 — 두 곳에 있으면 한쪽이 낡는다.
        /// <c>BlumgiSpriteBuilder.BuildPanel9P</c> 가 96×96 · 라운드 반경 26 으로 굽는다 ⇒ 경계 = 반경 + 외곽선.
        /// </summary>
        private static int GetSliceBorder(string file)
        {
            switch (file)
            {
                case "ui_panel_9p": return 32;   // radius 26 + stroke 6
                default: return 0;
            }
        }

        /// <summary>확정표 7 「4의 배수」가 실제로 지켜졌는지 <b>기계로</b> 확인한다.</summary>
        private static int ReportSizes(string[] guids)
        {
            int bad = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (texture == null)
                    continue;

                if (texture.width % 4 == 0 && texture.height % 4 == 0)
                    continue;

                bad++;
                Log.Warning($"4의 배수가 아니다: {Path.GetFileName(path)} {texture.width}×{texture.height}");
            }

            return bad;
        }
    }
}
