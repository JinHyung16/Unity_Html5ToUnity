using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UndeadSlayer;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Undead Slayer 아트의 임포트 설정을 <b>코드로</b> 건다.
    /// 사람에게 인스펙터를 요구하지 않는다 (CLAUDE.md 「사람에게 유니티 조작을 요구하지 않는다」).
    ///
    /// <para>
    /// ★ <b>PPU 가 두 값이다.</b> 월드는 <see cref="UndeadUnits.WorldPixelsPerUnit"/>(24 = 지형 타일 한 칸),
    /// UI 는 <see cref="UndeadUnits.UiPixelsPerUnit"/>(100 = 캔버스 <c>referencePixelsPerUnit</c> 와 같은 값).
    /// ⚠ UI 쪽이 캔버스와 어긋나면 <c>Image</c> 가 9-slice 경계를 그 비로 나눠 써 <b>모서리가 통째로 늘어난다</b>.
    /// </para>
    ///
    /// <para>
    /// ★ <b>격자·피벗·나인슬라이스는 «데이터 표»가 정한다</b> — <c>UndeadArtTable</c> 의 실측값이다.
    /// 여기에 숫자를 다시 적지 않는다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>픽셀아트라 <c>FilterMode.Point</c> · 압축 없음</b>이다. Bilinear 로 두면 확대할 때 뭉개져
    /// 원본과 «질감»이 갈린다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadArtImportSetup.Setup</c></para>
    /// </summary>
    public static class UndeadArtImportSetup
    {
        private const string ArtFolder = "Assets/Undead-Slayer/Art";
        private const string GameFolder = ArtFolder + "/Game";
        private const string DataFolder = "Assets/Undead-Slayer/Data";

        /// <summary>지형 타일셋 — 표에 없다(아틀라스가 아니라 별도 텍스처다). 24×24 격자로 자른다.</summary>
        private const string TilesetName = "biome_graveyard_tiles";

        public static void Setup()
        {
            if (AssetDatabase.IsValidFolder(ArtFolder) == false)
            {
                Log.Error($"아트 폴더가 없다: {ArtFolder} — 먼저 UndeadSpriteBuilder.BuildAll 을 돌린다.");
                return;
            }

            var art = new UndeadArtDataContainer();
            art.LoadJson(File.ReadAllText(Path.Combine(DataFolder, art.Name + ".json")));

            var byCode = new Dictionary<string, UndeadArtData>(art.Count);

            for (int i = 0; i < art.AllValues.Count; i++)
                byCode[art.AllValues[i].Code] = art.AllValues[i];

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });
            int done = 0;
            int sliced = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                    continue;

                string code = Path.GetFileNameWithoutExtension(path);
                bool isGame = path.StartsWith(GameFolder);

                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;

                // ★★ NPOT 스케일을 «반드시» 끈다.
                //   기본값(ToNearest)이면 96×32 시트가 «128×32» 로 늘어나 격자가 통째로 어긋나고,
                //   높이 17 짜리 시트는 16 으로 줄어 «한 줄도 안 잘린다».
                //   [사고] 실제로 히어로가 6열이 아니라 8열로 잘렸고 enemy1 은 스프라이트가 0개였다.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = isGame
                    ? UndeadUnits.WorldPixelsPerUnit
                    : UndeadUnits.UiPixelsPerUnit;

                var settings = new TextureImporterPlatformSettings
                {
                    name = "DefaultTexturePlatform",
                    // ⚠ 픽셀아트는 «압축하지 않는다» — ASTC 블록 경계에서 도트가 번진다.
                    textureCompression = TextureImporterCompression.Uncompressed,
                    overridden = true,
                };

                importer.SetPlatformTextureSettings(settings);

                if (code == TilesetName)
                {
                    var tileset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    if (tileset != null)
                        SliceGrid(importer, 24, 24, tileset.width / 24, tileset.height / 24, new Vector2(0.5f, 0.5f));   // 한 줄 14칸 (UndeadTerrainView.ETile)
                    sliced++;
                }
                else if (byCode.TryGetValue(code, out UndeadArtData a))
                {
                    var pivot = new Vector2((float)a.PivotX, (float)a.PivotY);

                    if (a.Cols * a.Rows > 1)
                    {
                        SliceGrid(importer, a.FrameWidth, a.FrameHeight, a.Cols, a.Rows, pivot);
                        sliced++;
                    }
                    else
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;

                        // ★ 나인슬라이스는 «테두리»가 있어야 늘어난다. 원본은 6×6·16×16 같은
                        //   작은 타일을 크게 늘려 쓴다 [실측] — 테두리 없이 두면 통째로 늘어난다.
                        int border = a.NineSlice ? Mathf.Max(1, Mathf.Min(16, Mathf.Min(a.SheetWidth, a.SheetHeight) / 3)) : 0;

                        TextureImporterSettings s = new TextureImporterSettings();
                        importer.ReadTextureSettings(s);
                        s.spriteAlignment = (int)SpriteAlignment.Custom;
                        s.spritePivot = pivot;
                        s.spriteBorder = new Vector4(border, border, border, border);
                        importer.SetTextureSettings(s);
                    }
                }
                else
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                done++;
            }

            AssetDatabase.Refresh();
            Log.Success($"Undead Slayer 아트 임포트 설정 완료 — {done}장 (격자 자르기 {sliced}장) · " +
                        $"월드 PPU {UndeadUnits.WorldPixelsPerUnit} · UI PPU {UndeadUnits.UiPixelsPerUnit} · Point · 무압축");
        }

        /// <summary>
        /// 시트를 격자로 자른다. <b>이름은 <c>&lt;코드&gt;_&lt;행&gt;_&lt;열&gt;</c></b> —
        /// 중앙 틱 렌더러가 «행 = 상태 · 열 = 컷»으로 읽는다 (히어로는 0행 대기 · 1행 이동 [실측]).
        /// </summary>
        private static void SliceGrid(TextureImporter importer, int frameWidth, int frameHeight,
                                      int cols, int rows, Vector2 pivot)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;

            string code = Path.GetFileNameWithoutExtension(importer.assetPath);

            // ⚠ 격자 수를 «임포트된 텍스처 크기»에서 뽑지 않는다 — 임포터 설정(NPOT·최대 크기)에
            //   따라 그 값이 변한다. 데이터 표의 실측 격자가 정본이다.
            var metas = new List<SpriteMetaData>(cols * rows);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    // ⚠ 텍스처 y 는 «아래»가 0 이고 원본 시트의 행은 «위»가 0 이다 — 여기서 뒤집는다.
                    int y = (rows - 1 - row) * frameHeight;

                    metas.Add(new SpriteMetaData
                    {
                        name = $"{code}_{row}_{col}",
                        rect = new Rect(col * frameWidth, y, frameWidth, frameHeight),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = pivot,
                    });
                }
            }

#pragma warning disable CS0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        }
    }
}
