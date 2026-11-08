using System;
using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.FrontWarsIo;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이모지를 TMP 에 굽는다 — 글 안에 섞여 나오는 이모지(이벤트 목록 · 튜토리얼 목표 · 채팅)를 그림으로 그린다.
    /// <para>
    /// 우리 글꼴(Pretendard)에는 이모지 글리프가 없어 두부(□)로 뜬다. 이모지 «글꼴» 을 받는 대신
    /// 코드로 그린 표식(<see cref="FrontWarsIconFactory.GetEmoji"/>)을 한 장의 그림으로 모아 TMP 스프라이트 에셋으로 만든다 (R11 · 아트는 새로 그린다).
    /// </para>
    /// <para>TMP 는 글자를 못 찾으면 «이모지 대체 에셋 목록»(TMP 설정)에서 찾는다 — 구운 에셋을 그 목록에 넣는다.</para>
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsEmojiSpriteBake.Bake</c></para>
    /// </summary>
    public static class FrontWarsEmojiSpriteBake
    {
        private const string AtlasPath = "Assets/FrontWars-io/Art/UI/FrontWarsEmojiAtlas.png";
        private const string SpriteAssetPath = "Assets/FrontWars-io/Resources/Font/FrontWarsEmoji.asset";
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>표식 한 칸 크기 (그림 픽셀). 아이콘 공장이 그리는 크기와 같다.</summary>
        private static readonly int CellSize = FrontWarsIconFactory.PixelSize;
        private const int Columns = 8;

        /// <summary>글자 크기 대비 그림 크기 · 밑선에서 띄우는 높이 [미측정 — 글자와 나란히 보이게 눈으로 맞춘 값].</summary>
        private const float GlyphScale = 1f;
        private const float BearingY = 0.8f;

        public static void Bake()
        {
            List<string> emojis = CollectEmojis();

            if (emojis.Count == 0)
            {
                Log.Error("구울 이모지가 없다");
                return;
            }

            int rows = Mathf.CeilToInt(emojis.Count / (float)Columns);
            int width = Columns * CellSize;
            int height = rows * CellSize;

            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 0);

            var rects = new GlyphRect[emojis.Count];

            for (int i = 0; i < emojis.Count; i++)
            {
                Color32[] src = FrontWarsIconFactory.EmojiPixels(emojis[i]);

                int col = i % Columns;
                int row = i / Columns;
                // 텍스처는 아래가 0 이다 — 위 줄부터 채우도록 뒤집어 쓴다
                int x0 = col * CellSize;
                int y0 = height - (row + 1) * CellSize;

                for (int y = 0; y < CellSize; y++)
                {
                    for (int x = 0; x < CellSize; x++)
                        pixels[(y0 + y) * width + x0 + x] = src[y * CellSize + x];
                }

                rects[i] = new GlyphRect(x0, y0, CellSize, CellSize);
            }

            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);

            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            BuildSpriteAsset(texture, emojis, rects);
            RegisterInTmpSettings();

            Log.Success($"이모지 표식 {emojis.Count}개를 TMP 스프라이트 에셋으로 구웠다 ({width}×{height})");
        }

        /// <summary>구울 이모지 목록 — 시뮬이 쓰는 55개 + 튜토리얼 목표 아이콘 + 글 속에만 나오는 이모지 (중복 없이).</summary>
        private static List<string> CollectEmojis()
        {
            var seen = new HashSet<string>();
            var list = new List<string>();

            void AddOne(string emoji)
            {
                if (string.IsNullOrEmpty(emoji) || seen.Add(emoji) == false)
                    return;

                list.Add(emoji);
            }

            foreach (string emoji in FrontWarsEmojis.All)
                AddOne(emoji);

            foreach (string emoji in FrontWarsTutorialObjectives.AllIcons)
                AddOne(emoji);

            foreach (string emoji in FrontWarsIconFactory.ExtraTextEmojis)
                AddOne(emoji);

            return list;
        }

        private static void BuildSpriteAsset(Texture2D texture, List<string> emojis, GlyphRect[] rects)
        {
            var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            spriteAsset.name = Path.GetFileNameWithoutExtension(SpriteAssetPath);
            spriteAsset.spriteSheet = texture;
            spriteAsset.spriteCharacterTable.Clear();
            spriteAsset.spriteGlyphTable.Clear();

            for (int i = 0; i < emojis.Count; i++)
            {
                var metrics = new GlyphMetrics(CellSize, CellSize, 0f, CellSize * BearingY, CellSize);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, rects[i], GlyphScale, 0);
                spriteAsset.spriteGlyphTable.Add(glyph);

                // 여러 코드포인트로 된 이모지(모양 고르는 글자 · ZWJ)는 «첫 코드포인트» 로 찾는다 — TMP 도 그렇게 찾는다
                var character = new TMP_SpriteCharacter((uint)char.ConvertToUtf32(emojis[i], 0), glyph)
                {
                    name = $"emoji{i}",
                    scale = GlyphScale
                };

                spriteAsset.spriteCharacterTable.Add(character);
            }

            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = spriteAsset.name + " Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);

            Directory.CreateDirectory(Path.GetDirectoryName(SpriteAssetPath));

            if (AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath) != null)
                AssetDatabase.DeleteAsset(SpriteAssetPath);

            AssetDatabase.CreateAsset(spriteAsset, SpriteAssetPath);
            material.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(material, spriteAsset);
            spriteAsset.material = material;

            // ⚠ 판 번호가 비어 있으면 TMP 가 「옛 에셋」 으로 보고 우리 표를 지운다 — 번호를 먼저 박는다
            var serialized = new SerializedObject(spriteAsset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            spriteAsset.UpdateLookupTables();

            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TMP 설정의 「이모지 대체 에셋」 목록에 우리 에셋을 넣는다 (없으면 TMP 가 이모지를 안 찾는다).</summary>
        private static void RegisterInTmpSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);

            if (settings == null)
            {
                Log.Error($"TMP 설정을 못 찾았다: {SettingsPath}");
                return;
            }

            var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            var serialized = new SerializedObject(settings);
            SerializedProperty list = serialized.FindProperty("m_EmojiFallbackTextAssets");

            if (list == null)
            {
                Log.Error("TMP 설정에 이모지 대체 목록이 없다 (TMP 버전 확인)");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == spriteAsset)
                    return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = spriteAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
