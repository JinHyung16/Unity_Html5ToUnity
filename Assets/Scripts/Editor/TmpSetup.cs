using System.IO;
using JinHyung.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// TMP 환경을 <b>코드로</b> 세운다 — 필수 리소스 임포트 + 폰트 에셋 생성.
    ///
    /// <para>
    /// 보통은 사람이 <c>Window &gt; TextMeshPro &gt; Import TMP Essential Resources</c> 를 눌러야 한다.
    /// <b>그건 절차가 아니다</b> (`CLAUDE.md` 「사람에게 유니티 조작을 요구하지 않는다」).
    /// </para>
    /// </summary>
    public static class TmpSetup
    {
        private const string FontTtfPath = "Assets/Candy-Crush-Game/Art/Font/Montserrat-Regular.ttf";
        private const string FontAssetPath = "Assets/Candy-Crush-Game/Art/Font/Montserrat-Regular SDF.asset";
        private const string TmpEssentialMarker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static void Setup()
        {
            ImportEssentials();
            CreateFontAsset();
        }

        /// <summary>TMP 필수 리소스(셰이더·기본 폰트·설정)를 임포트한다.</summary>
        public static void ImportEssentials()
        {
            if (File.Exists(TmpEssentialMarker))
            {
                Log.Success("TMP Essentials 이미 있음");
                return;
            }

            string packagePath = FindEssentialPackage();

            if (string.IsNullOrEmpty(packagePath))
            {
                Log.Error("TMP Essential Resources.unitypackage 를 못 찾았다.");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Log.Success($"TMP Essentials 임포트 요청 — {packagePath}");
        }

        /// <summary>
        /// Montserrat TTF 로 TMP 폰트 에셋을 만든다.
        ///
        /// <para>
        /// ⚠ <b>Regular(400) 하나만 굽는다.</b> 원본이 받아오는 것이 <c>Montserrat:300,400</c> 뿐이라
        /// 굵은 글씨가 <b>브라우저 합성 볼드</b>다 (<c>HtmlToUnityLogic/06_리소스.md</c>).
        /// 진짜 Bold 폰트를 넣으면 <b>원본보다 굵고 자간이 달라진다.</b>
        /// </para>
        /// </summary>
        public static void CreateFontAsset()
        {
            if (File.Exists(FontAssetPath))
            {
                Log.Success("폰트 에셋 이미 있음");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);

            if (font == null)
            {
                Log.Error($"TTF 를 못 읽었다: {FontTtfPath}");
                return;
            }

            // 동적 아틀라스 — 쓰는 글리프만 런타임에 채운다. 원본 문구가 전부 ASCII 라 넉넉하다.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Log.Error("TMP_FontAsset 생성 실패");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"폰트 에셋 생성 — {FontAssetPath}");
        }

        private static string FindEssentialPackage()
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");

            if (Directory.Exists(root) == false)
                return null;

            string[] found = Directory.GetFiles(root, "TMP Essential Resources.unitypackage",
                                                SearchOption.AllDirectories);

            return found.Length > 0 ? found[0] : null;
        }
    }
}
