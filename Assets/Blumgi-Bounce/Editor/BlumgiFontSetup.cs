using System.IO;
using JinHyung.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이 게임의 TMP 폰트 에셋을 <b>코드로</b> 만든다.
    ///
    /// <para>
    /// ⚠⚠ <b>서체 대체는 «의도된 차이 #5» 로 등재된 항목이다.</b>
    /// 원본 서체는 <c>Rounds Black</c> — <b>상용이라 확보 불가</b>다 [실측 <c>sdkInst._faceName</c>].
    /// UIUX 가 권한 대체 후보는 <b>둥근 Black 계열</b>(<c>Baloo 2 ExtraBold</c> · <c>Fredoka One</c> ·
    /// <c>Nunito Black</c> · <c>Rowdies Bold</c>)인데 <b>저장소에 그 폰트 파일이 없다</b>.
    /// </para>
    ///
    /// <para>
    /// ★ 그래서 지금은 <b>저장소에 이미 있는 <c>Montserrat-Regular</c> 로 «임시» 대체</b>한다.
    /// <b>이것은 「둥근 Black」이 아니다</b> — 자형 파리티가 아직 안 맞는다.
    /// <b>폰트 파일이 들어오면 <see cref="SourceTtfPath"/> 한 줄만 바꿔 다시 돌린다.</b>
    /// (원장 「재측정 대기」에 올려야 하는 항목이다 — 담당자가 스스로 다운로드하지 않는다.)
    /// </para>
    ///
    /// <para>
    /// ⚠ 폰트 에셋을 <b><c>Resources</c> 에 둔다</b> — UI 프리팹이 Resources 라서 그렇다.
    /// <c>Art/</c> 아래 <c>.asset</c> 은 <c>AddressableSetup</c> 이 어드레서블로 등록하므로
    /// 거기 두면 <b>Resources 프리팹이 어드레서블 에셋을 물어 빌드에 중복 편입</b>된다 (ResourceRule).
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiFontSetup.Setup</c></para>
    /// </summary>
    public static class BlumgiFontSetup
    {
        /// <summary>⚠ <b>임시 대체</b>. 「둥근 Black」 폰트가 들어오면 이 한 줄을 바꾼다.</summary>
        public const string SourceTtfPath = "Assets/Candy-Crush-Game/Art/Font/Montserrat-Regular.ttf";

        public const string FontAssetPath = "Assets/Blumgi-Bounce/Resources/Font/Blumgi SDF.asset";

        public static void Setup()
        {
            if (File.Exists(FontAssetPath))
            {
                Log.Success($"폰트 에셋 이미 있음 — {FontAssetPath}");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);

            if (font == null)
            {
                Log.Error($"TTF 를 못 읽었다: {SourceTtfPath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.Refresh();

            // 표시 문자가 «ASCII 대문자 · 숫자 · ! · %» 뿐이라 [04 §4-d] 동적 아틀라스로 넉넉하다.
            // 한글·이모지가 없으므로 «폴백 0개»로 갈 수 있다 (UIUX.md 「폰트는 하나가 기본값」).
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Log.Error("TMP 폰트 에셋 생성 실패");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTextures != null)
            {
                for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                {
                    if (fontAsset.atlasTextures[i] == null)
                        continue;

                    fontAsset.atlasTextures[i].name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"폰트 에셋 생성 — {FontAssetPath} (⚠ 임시 대체: 원본 Rounds Black 확보 불가)");
        }
    }
}
