using System.IO;
using JinHyung.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이 게임의 TMP 폰트 에셋을 <b>코드로</b> 만든다.
    ///
    /// <para>
    /// ⚠⚠ <b>서체 대체는 «의도된 차이» 로 등재된 항목이다.</b>
    /// 원본은 <c>font.ttf</c> 한 벌을 쓰는 <b>픽셀 서체</b>인데 [실측 · 네트워크 로그],
    /// <b>상용이라 확보 불가</b>다 — 파일을 가져다 쓰면 저작권 침해다.
    /// </para>
    ///
    /// <para>
    /// ★ 그래서 <b>Noto Sans KR</b>(SIL Open Font License · 재배포 가능)로 대체한다.
    /// <b>이것은 픽셀 서체가 아니다</b> — 자형 파리티가 안 맞는다.
    /// 한글 픽셀 서체(예: 갈무리 · 둥근모)를 받으면 <see cref="SourceTtfPath"/> 한 줄만 바꿔 다시 돌린다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>왜 한글 폰트가 «필수»인가</b> — 원본 문구 60키를 전부 옮겨 놨는데
    /// TMP 기본 폰트에 한글 글리프가 없어 <b>전부 두부(□)로 떴다</b>.
    /// 「옮겼다」와 「보인다」는 다른 말이다 (`ResourceRule.md` 「원본의 기호는 셋 중 하나로 갈린다」).
    /// </para>
    ///
    /// <para>
    /// ⚠ 폰트 에셋을 <b><c>Resources</c> 에 둔다</b> — UI 프리팹이 Resources 라서다.
    /// <c>Art/</c> 아래 <c>.asset</c> 은 어드레서블로 등록되므로 거기 두면 <b>빌드에 중복 편입</b>된다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>동적 아틀라스</b>다 — 한글은 완성형만 11 172자라 전부 구우면 아틀라스가 폭발한다.
    /// 쓰는 글자만 런타임에 채운다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadFontSetup.Setup</c></para>
    /// </summary>
    public static class UndeadFontSetup
    {
        /// <summary>⚠ <b>임시 대체</b>. 한글 «픽셀» 서체가 들어오면 이 한 줄을 바꾼다.</summary>
        public const string SourceTtfPath = "Assets/Undead-Slayer/Resources/Font/NotoSansKR-VF.ttf";

        public const string FontAssetPath = "Assets/Undead-Slayer/Resources/Font/UndeadSlayer SDF.asset";

        public static void Setup()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);

            if (font == null)
            {
                Log.Error($"TTF 를 못 읽었다: {SourceTtfPath}");
                return;
            }

            if (File.Exists(FontAssetPath))
                AssetDatabase.DeleteAsset(FontAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.Refresh();

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Log.Error("TMP 폰트 에셋 생성 실패");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);

            // ★★ 한글은 쓰는 글자만 해도 아틀라스 한 장을 넘긴다 —
            //   «여러 장»을 허용하지 않으면 아틀라스가 차는 순간 <b>남은 글자가 조용히 두부</b>가 된다.
            //   [사고] 실제로 60키 중 10키가 «폰트에 없다»로 잡혔는데, 원인은 폰트가 아니라 아틀라스였다.
            fontAsset.isMultiAtlasTexturesEnabled = true;
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

            // ★★ <b>쓰는 글자를 «미리» 굽는다.</b> 동적 폰트는 런타임에 채우지만,
            //   그때 아틀라스가 차면 <b>남은 글자가 조용히 두부</b>가 된다.
            //   원본 문구 60키에 나오는 글자를 여기서 전부 넣어 «되는지»를 지금 확인한다.
            string used = CollectUsedCharacters();

            if (fontAsset.TryAddCharacters(used, out string missing) == false || string.IsNullOrEmpty(missing) == false)
                Log.Error($"폰트가 못 굽는 글자가 있다 ({missing?.Length ?? 0}자): {missing}");
            else
                Log.Success($"원본 문구 글자 {used.Length}자 전부 구웠다 (아틀라스 {fontAsset.atlasTextures.Length}장)");

            // ★★★ <b>동적 → 정적으로 고정한다.</b>
            //   동적(Dynamic) 폰트는 <b>재생에 들어갈 때 글자표를 비운다</b> — 런타임에 다시 채우라는 뜻이다.
            //   그래서 「굽고 저장했는데 재생하면 두부」가 된다.
            //   [사고] 실제로 편집 모드에서는 184자가 다 구워졌는데 재생 검사는 51키가 빠졌다고 찍었다.
            //   ⇒ 쓰는 글자를 «미리» 구운 뒤 Static 으로 고정해 그 표를 에셋에 박는다.
            MakeMonospace(fontAsset);

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"폰트 에셋 생성 — {FontAssetPath} (⚠ 의도된 차이: 원본 픽셀 서체 확보 불가 → Noto Sans KR)");
        }

        /// <summary>
        /// 글자 하나가 <b>정확히 1em</b> 을 차지하게 만든다 — <b>원본이 «고정폭 픽셀 서체»</b>이기 때문이다 [실측].
        ///
        /// <para>
        /// 원본 글리프 폭 ÷ (글자수 × fontSize) 가 문구마다 0.95~1.05 였다 —
        /// <c>"3/40"</c> 4×12=48 → 50.0 · <c>"00:14"</c> 5×14=70 → 75.0 · <c>"레벨 업!"</c> 5×24=120 → 114.2.
        /// 대체 서체(Noto Sans KR)는 <b>비례폭</b>이라 숫자·공백·기호가 좁아, 같은 자리에 같은 크기로 놓아도
        /// 문구 폭이 원본의 <b>45~68%</b> 로 나온다. 글자 크기가 맞는데도 화면이 눈에 띄게 달라 보이는 원인이다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>폰트 에셋이 이 사실을 든다.</b> TMP 의 <c>m_monoSpacing</c> 은 <b>직렬화되지 않아</b>
        /// 프리팹에 굽히지 않고, <c>&lt;mspace&gt;</c> 태그는 문구를 갈아끼울 때마다 사라진다 —
        /// 둘 다 «호출부마다» 챙겨야 하는 규칙이 되어 언젠가 빠진다.
        /// </para>
        ///
        /// <para>⚠ <see cref="AtlasPopulationMode.Static"/> 으로 고정하기 «전»에 부른다. 그래야 표에 박힌다.</para>
        /// </summary>
        private static void MakeMonospace(TMP_FontAsset fontAsset)
        {
            float em = fontAsset.faceInfo.pointSize;

            if (em <= 0f)
            {
                Log.Error("폰트의 pointSize 가 0 이다 — 고정폭으로 못 바꾼다");
                return;
            }

            int changed = 0;

            foreach (Glyph glyph in fontAsset.glyphTable)
            {
                GlyphMetrics m = glyph.metrics;

                // 글리프를 1em 칸 «가운데»에 놓는다 — 폭만 늘리면 글자가 왼쪽으로 쏠린다
                float bearingX = m.horizontalBearingX + (em - m.horizontalAdvance) * 0.5f;

                glyph.metrics = new GlyphMetrics(m.width, m.height, bearingX, m.horizontalBearingY, em);
                changed++;
            }

            Log.Success($"글리프 {changed}자를 고정폭(1em = {em})으로 맞췄다 — 원본 서체가 고정폭이다 [실측]");
        }

        /// <summary>
        /// 원본 문구 60키에 실제로 나오는 글자 전수 + 숫자·기호.
        /// <b>표를 읽어서 만든다</b> — 여기에 글자 목록을 손으로 적지 않는다.
        /// </summary>
        private static string CollectUsedCharacters()
        {
            var set = new System.Collections.Generic.HashSet<char>();

            foreach (char c in "0123456789:/+-%.,!?()")
                set.Add(c);

            var texts = new JinHyung.Data.UndeadTextDataContainer();
            string path = "Assets/Undead-Slayer/Data/" + texts.Name + ".json";

            if (File.Exists(path))
            {
                texts.LoadJson(File.ReadAllText(path));

                for (int i = 0; i < texts.AllValues.Count; i++)
                {
                    string value = texts.AllValues[i].Ko;

                    if (string.IsNullOrEmpty(value))
                        continue;

                    foreach (char c in value)
                        set.Add(c);
                }
            }
            else
            {
                Log.Warning($"문구 표가 없다: {path} — 숫자·기호만 굽는다");
            }

            var chars = new char[set.Count];
            set.CopyTo(chars);
            return new string(chars);
        }
    }
}
