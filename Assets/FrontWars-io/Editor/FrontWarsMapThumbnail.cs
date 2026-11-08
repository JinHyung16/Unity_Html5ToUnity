using System.IO;
using JinHyung.Core;
using JinHyung.FrontWarsIo;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 맵 썸네일을 굽는다 — 원본 싱글 설정 「맵」 탭에 158×158 그림이 있다 `[실측] 06_리소스.md ①`.
    /// <para>원본 그림은 안 쓴다 (R11). 우리 지형 데이터를 줄여 우리 지형 색으로 새로 그린다.</para>
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsMapThumbnail.BakeAll</c> → 어드레서블 등록은 `FrontWarsAddressableSetup.Setup` 이 한다.</para>
    /// </summary>
    public static class FrontWarsMapThumbnail
    {
        private const string MapFolder = "Assets/FrontWars-io/Data/Maps";
        private const string OutFolder = "Assets/FrontWars-io/Art/UI";

        /// <summary>원본 썸네일 크기 [실측 158×158].</summary>
        public const int Size = 158;

        public static void BakeAll()
        {
            Directory.CreateDirectory(OutFolder);
            int made = 0;

            foreach (string dir in Directory.GetDirectories(MapFolder))
            {
                string name = Path.GetFileName(dir);
                string mapPath = Path.Combine(dir, "map.bytes");
                string manifestPath = Path.Combine(dir, "manifest.json");

                if (File.Exists(mapPath) == false || File.Exists(manifestPath) == false)
                    continue;

                if (Bake(name, mapPath, manifestPath))
                    made++;
            }

            AssetDatabase.Refresh();
            Log.Success($"맵 썸네일 {made}장 구웠다 ({Size}×{Size})");
        }

        private static bool Bake(string mapName, string mapPath, string manifestPath)
        {
            string manifest = File.ReadAllText(manifestPath);
            var json = Newtonsoft.Json.Linq.JObject.Parse(manifest);
            int width = (int)json["map"]["width"];
            int height = (int)json["map"]["height"];

            byte[] bytes = File.ReadAllBytes(mapPath);
            FrontWarsTerrainMap map = FrontWarsTerrainMap.FromRawTiles(bytes, width, height);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            // 칸을 성기게 뽑아 우리 지형 색으로 칠한다 (한 픽셀 = 여러 칸 중 하나 — 썸네일이라 대표값으로 충분하다)
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    int x = Mathf.Min(width - 1, px * width / Size);
                    int y = Mathf.Min(height - 1, py * height / Size);
                    int tile = y * width + x;

                    // 원본은 늘 Dark 지형 식을 쓴다 (`05_연출.md`) — 썸네일도 같은 색으로 그린다
                    Color32 color = FrontWarsTerrainColors.TerrainColor(map, tile, true, FrontWarsTerrainColors.WaterDarkPreset);
                    // 텍스처는 아래가 0 이라 세로를 뒤집는다
                    pixels[(Size - 1 - py) * Size + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            string outPath = $"{OutFolder}/MapThumbnail_{mapName}.png";
            File.WriteAllBytes(outPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return true;
        }
    }
}
