using System.Collections.Generic;
using JinHyung.Core;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 아트 임포트 설정을 <b>코드로</b> 맞춘다.
    ///
    /// <para>
    /// 3D 템플릿에서 만든 프로젝트는 PNG 의 기본 텍스처 타입이 <c>Sprite</c> 가 아니다 —
    /// 그대로 두면 <c>Sprite</c> 로 로드가 안 되고, 사람이 인스펙터에서 하나씩 바꿔야 한다.
    /// <b>그건 절차가 아니다</b> (`CLAUDE.md` 「사람에게 유니티 조작을 요구하지 않는다」).
    /// </para>
    /// </summary>
    public static class ArtImportSetup
    {
        private const string ArtFolder = "Assets/Candy-Crush-Game/Art";

        /// <summary>
        /// PPU. 환산 배율 1.6 을 먹인 사탕이 112px 이고, 캔버스가 픽셀 기준(Reference 1080×1920)이라
        /// <b>1 유닛 = 1 픽셀</b>로 둔다 — UI 에서 스프라이트 크기가 곧 픽셀 크기가 된다.
        /// </summary>
        /// <summary>
        /// ⚠ <b>캔버스의 <c>referencePixelsPerUnit</c> 와 «같은 값»이어야 한다.</b>
        ///
        /// <para>
        /// 두 값이 어긋나면 <c>Image</c> 가 9-slice 경계를 그 비(比)로 나눠 쓴다 —
        /// 스프라이트 PPU 1 · 기준 PPU 100 이면 경계 11px 이 <b>0.11px</b> 이 되어
        /// <b>모서리가 통째로 늘어난다</b>(둥근 사각형이 타원이 된다). 실측으로 확인했다.
        /// </para>
        /// </summary>
        private const float PixelsPerUnit = 100f;

        public static void Setup()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });

            if (guids == null || guids.Length == 0)
            {
                Log.Warning($"아트가 없다: {ArtFolder}");
                return;
            }

            var changed = new List<string>();

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

                // 배경은 타일링한다 — Repeat 이 아니면 이음매에서 늘어난 픽셀이 보인다.
                bool isTile = path.Contains("bg_tile");
                TextureWrapMode wrap = isTile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

                if (importer.wrapMode != wrap)
                {
                    importer.wrapMode = wrap;
                    dirty = true;
                }

                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    dirty = true;
                }

                // 9-slice 경계 — 원본의 border-radius 를 옮긴 스프라이트만 해당한다.
                // 경계를 안 주면 늘렸을 때 «모서리가 같이 늘어난다».
                int border = GetSliceBorder(path);

                if (border > 0)
                {
                    var b = new Vector4(border, border, border, border);

                    if (importer.spriteBorder != b)
                    {
                        importer.spriteBorder = b;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed.Add(System.IO.Path.GetFileName(path));
                }
            }

            AssetDatabase.SaveAssets();
            Log.Success($"아트 임포트 설정 — 대상 {guids.Length}개 · 변경 {changed.Count}개 [{string.Join(", ", changed)}]");
        }

        /// <summary>
        /// 9-slice 경계. 굽는 쪽(스프라이트 생성기)과 <b>같은 값</b>이어야 한다 —
        /// 두 곳에 있으면 한쪽이 낡는다. 굽는 규격은 <c>06_리소스.md</c> ②.
        /// </summary>
        private static int GetSliceBorder(string path)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);

            switch (file)
            {
                case "ui_round_8": return 11;   // radius 8  + 3
                case "ui_round_16": return 19;  // radius 16 + 3
                case "ui_round_32": return 35;  // radius 32 + 3
                case "ui_board": return 44;     // radius 16 + inset 그림자 영역
                default: return 0;              // 사탕·배경은 9-slice 가 아니다
            }
        }
    }
}
