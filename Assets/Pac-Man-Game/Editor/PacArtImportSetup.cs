using System.Collections.Generic;
using JinHyung.Core;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이 게임의 아트 임포트 설정.
    ///
    /// <para>
    /// ★★ <b>PPU 는 «쓰임»과 짝이다.</b> 공용 <c>ArtImportSetup</c> 은 UI 스프라이트를 전제해
    /// 캔버스 기준 PPU(100)에 맞춘다 — 그건 <b>UI 에 맞는 값</b>이다.
    /// 여기 스프라이트는 <b>월드 격자</b>에 놓이므로 기준이 다르다.
    /// </para>
    ///
    /// <para>
    /// 칸 하나를 <b>1 유닛</b>으로 두었으므로 <b>PPU = 칸 픽셀(36)</b> 이어야
    /// 36px 타일이 정확히 한 칸을 채운다. 100 으로 두면 <b>0.36칸</b>이 되어 격자에 구멍이 뚫린다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>공용 설정 «뒤에» 돌린다.</b> 공용이 전부 100 으로 맞춘 다음 여기서 덮는다 —
    /// 순서가 바뀌면 이 값이 도로 100 이 된다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.PacArtImportSetup.Setup</c></para>
    /// </summary>
    public static class PacArtImportSetup
    {
        private const string ArtRoot = "Assets/Pac-Man-Game/Art";

        /// <summary>칸 하나 = 1 유닛. <c>PacSpriteBuilder</c> 의 칸 크기와 <b>같은 값</b>이어야 한다.</summary>
        private const float PixelsPerUnit = 36f;

        public static void Setup()
        {
            var changed = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                    continue;

                bool dirty = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }

                // ⚠ `textureType` 만 바꾸면 «스프라이트가 만들어지지 않는다».
                //   임포터는 「Sprite 다」라고 답하는데 LoadAssetAtPath<Sprite> 는 null 을 준다 —
                //   씬은 멀쩡히 구워지고 화면만 빈다 (실측: 타일 3종이 통째로 안 그려졌다).
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }

                if (Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit) == false)
                {
                    importer.spritePixelsPerUnit = PixelsPerUnit;
                    dirty = true;
                }

                // 원본은 브라우저가 그대로 그린다 — 밉맵을 만들면 축소 시 원본보다 흐려진다.
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

                // ⚠ 격자에 딱 붙는 타일이라 «가장자리를 이웃에서 빨아오면» 이음매가 보인다.
                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    dirty = true;
                }

                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed.Add(System.IO.Path.GetFileName(path));
                }
            }

            SetupAudio(changed);

            AssetDatabase.SaveAssets();
            Log.Success($"Pac-Man 아트 임포트 — 대상 {guids.Length}개 · 변경 {changed.Count}개 " +
                        $"(PPU {PixelsPerUnit} = 칸 1유닛)");
        }

        /// <summary>
        /// 오디오. <b>짧은 효과음과 긴 배경음은 로드 방식이 다르다</b> —
        /// 효과음을 스트리밍하면 첫 재생이 늦고, 배경음을 통째로 풀면 메모리를 먹는다.
        /// </summary>
        private static void SetupAudio(List<string> changed)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ArtRoot });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                if (importer == null)
                    continue;

                bool isBgm = System.IO.Path.GetFileName(path).StartsWith("bgm_");

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = isBgm
                    ? AudioClipLoadType.Streaming
                    : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isBgm ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;

                importer.SaveAndReimport();
                changed.Add(System.IO.Path.GetFileName(path));
            }
        }
    }
}
