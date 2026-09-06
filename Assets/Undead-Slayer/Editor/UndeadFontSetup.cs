using System;
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
    /// ★ 지금 쓰는 것은 <b>「온글잎 박다현체」</b> — <b>사람이 골라 넣은 것</b>이다.
    /// <b>손글씨 서체라 원본의 «픽셀» 서체와는 결이 다르다</b> (알린 뒤 지시대로 넣었다).
    /// 바꾸려면 <see cref="SourceTtfPath"/> <b>한 줄</b>이다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>왜 한글 폰트가 «필수»인가</b> — 원본 문구 60키를 전부 옮겨 놨는데
    /// TMP 기본 폰트에 한글 글리프가 없어 <b>전부 두부(□)로 떴다</b>.
    /// 「옮겼다」와 「보인다」는 다른 말이다 (`ResourceRule.md` 「원본의 기호는 셋 중 하나로 갈린다」).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>둘이 사는 곳이 다르다.</b>
    /// <b>구운 폰트 에셋</b>은 <c>Resources/</c> 다 — UI 프리팹이 Resources 라서다
    /// (<c>Art/</c> 아래 <c>.asset</c> 은 어드레서블로 등록돼 거기 두면 <b>빌드에 중복 편입</b>된다).
    /// <b>원본 TTF</b> 는 <c>Editor/</c> 다 — 굽는 «입력»이라 빌드에 들어갈 이유가 없다
    /// (<see cref="FontFolder"/>).
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
        /// <summary>
        /// ⚠⚠ <b>여기 한 줄이 서체의 전부다.</b> 한글 «픽셀» 서체를 <see cref="FontFolder"/> 에 두고
        /// 이 경로만 바꾼 뒤 <c>Setup</c> 을 다시 돌리면 된다 — 다른 곳은 손댈 것이 없다.
        ///
        /// <para>
        /// ★ 원본은 픽셀 서체라 <b>거의 고정폭</b>이다. 대체 서체가 <b>비례폭</b>이면
        /// 폭을 맞추려고 <see cref="MakeMonospace"/> 가 자폭을 강제하고, 그 결과 <b>글자 사이가 벌어져 보인다</b>.
        /// 한글 <b>픽셀</b> 서체가 들어오면 <b>그 강제가 필요 없어진다</b> — 자형과 자간이 같이 맞는다.
        /// </para>
        ///
        /// <para>그럴 때 고를 만한 것 — <b>갈무리(Galmuri)</b> · <b>Neo둥근모</b> (둘 다 SIL Open Font License).
        /// ⚠ 시스템 서체(굴림체·바탕체)는 <b>재배포가 안 된다</b> — 게임에 넣으면 라이선스 위반이다.</para>
        /// </summary>
        // 사람이 고른 서체 — 원본 파일 이름은 「온글잎 박다현체.ttf」다 (경로에 공백·한글이 있어 ASCII 로 옮겨 두었다).
        // ⚠ 손글씨 서체라 원본의 «픽셀» 서체와는 결이 다르다 — 사람 판단으로 넣은 것이다.
        public const string SourceTtfPath = "Assets/Undead-Slayer/Editor/Font/Onglyph-ParkDahyeon.ttf";

        /// <summary>
        /// 원본 TTF 가 사는 곳 — <b><c>Editor/</c> 다. <c>Resources/</c> 가 아니다.</b>
        ///
        /// <para>
        /// ★ TTF 는 <b>굽는 «입력»</b>이지 게임이 읽는 것이 아니다 — 다 구운 폰트 에셋은
        /// <c>atlasPopulationMode = Static</c> 이라 <b>소스 폰트를 런타임에 참조하지 않는다</b>
        /// (에셋의 <c>m_SourceFontFile</c> 이 <c>fileID: 0</c> 이다 [실측]).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b><c>Resources/</c> 에 두면 «쓰지 않아도» 빌드에 들어간다.</b>
        /// 실측 사례로 안 쓰는 서체 하나가 <b>10.4 MB</b> 를 차지한 채 실려 있었다
        /// (CLAUDE.md 폴더 규칙 — <c>Editor/</c> 는 「굽는 «입력» 데이터(빌드에 안 들어간다)」).
        /// </para>
        /// </summary>
        public const string FontFolder = "Assets/Undead-Slayer/Editor/Font";

        /// <summary>
        /// 자폭을 <b>강제할까</b> — 원본이 «거의 고정폭» 픽셀 서체라서 넣었던 보정이다.
        /// <para>⚠ 대체 서체가 원본과 비슷한 폭이면 <b>끄는 편이 낫다</b> — 강제하면 글자가 «칸에 갇혀» 자간이 벌어져 보인다.</para>
        /// <para>★ 켤지 끌지는 <b>실측으로</b> 정한다 — 문구 폭이 원본 실측치와 ±10% 안이면 끈다 (재발방지 #157).</para>
        ///
        /// <para>
        /// ⚠ <b>실측해 보고 «켠» 채로 둔다</b> [회차 20]. 끄고 재니 문구 폭이 <b>원본의 46~54%</b> 였다 —
        /// <c>00:00</c> 75.0 → 34.7 · <c>레벨 업!</c> 114.2 → 62.1. 지금 서체도 원본보다 훨씬 좁다.
        /// 끄면 자간은 자연스러워지지만 <b>문구가 자리에 비해 너무 작아진다</b>.
        /// </para>
        /// </summary>
        private const bool ForceMonospaceAdvance = true;

        public const string FontAssetPath = "Assets/Undead-Slayer/Resources/Font/UndeadSlayer SDF.asset";

        public static void Setup()
        {
            WarnUnusedFonts();

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
            if (ForceMonospaceAdvance)
                MakeMonospace(fontAsset);
            else
                Log.Success("자폭 강제를 «끄고» 굽는다 — 서체 본래 자간을 쓴다");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"폰트 에셋 생성 — {FontAssetPath} · 서체 {Path.GetFileName(SourceTtfPath)}"
                        + " (⚠ 의도된 차이 — 원본의 «픽셀» 서체는 상용이라 확보 불가다. 대체 서체는 자형이 다르다)");
        }

        /// <summary>
        /// 폰트 폴더에 <b>쓰이지 않는 서체 파일</b>이 있으면 알린다.
        ///
        /// <para>
        /// ⚠ 사람이 픽셀 서체를 넣어 두고 <see cref="SourceTtfPath"/> 를 안 바꾸면
        /// <b>넣은 줄 알고 그대로 지나간다</b> — 「만든 것에는 «읽는 곳»이 있어야 한다」의 서체판이다.
        /// </para>
        /// </summary>
        private static void WarnUnusedFonts()
        {
            if (Directory.Exists(FontFolder) == false)
                return;

            string used = Path.GetFileName(SourceTtfPath);

            foreach (string file in Directory.GetFiles(FontFolder))
            {
                string name = Path.GetFileName(file);

                if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) == false
                    && name.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }

                if (string.Equals(name, used, StringComparison.OrdinalIgnoreCase))
                    continue;

                Log.Warning($"쓰이지 않는 서체가 폰트 폴더에 있다: {name}"
                            + $" → 쓰려면 {nameof(UndeadFontSetup)}.{nameof(SourceTtfPath)} 를 이 파일로 바꾸고 다시 돌린다."
                            + " (안 쓸 것이면 지운다)");
            }
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

            // ⚠ 원본은 «완전» 고정폭이 아니다 [실측] — 한글이 좁고 숫자·기호가 넓다.
            //   문구마다 (글리프 폭 ÷ 글자수 ÷ fontSize) 를 재니 한글 0.92~0.95 · 그 밖 1.04~1.11 이었다.
            //   전부 1em 으로 두면 한글 문구는 넓어지고 숫자열은 좁아진다.
            int changed = 0;

            foreach (Glyph glyph in fontAsset.glyphTable)
                changed++;

            foreach (TMP_Character ch in fontAsset.characterTable)
            {
                Glyph glyph = ch.glyph;

                if (glyph == null)
                    continue;

                float advance = em * AdvanceEmOf((char)ch.unicode);
                GlyphMetrics m = glyph.metrics;

                // 글리프를 그 칸 «가운데»에 놓는다 — 폭만 늘리면 글자가 왼쪽으로 쏠린다
                float bearingX = m.horizontalBearingX + (advance - m.horizontalAdvance) * 0.5f;

                glyph.metrics = new GlyphMetrics(m.width, m.height, bearingX, m.horizontalBearingY, advance);
            }

            Log.Success($"글리프 {changed}자의 advance 를 원본 비율로 맞췄다 — 한글 {HangulAdvanceEm}em · 그 밖 {LatinAdvanceEm}em [실측]");
        }

        /// <summary>한글 자폭 [실측 — 문구별 0.92~0.95em].</summary>
        private const float HangulAdvanceEm = 0.93f;

        /// <summary>숫자·라틴·기호 자폭 [실측 — 1.04~1.11em].</summary>
        private const float LatinAdvanceEm = 1.07f;

        /// <summary>
        /// 그 글자가 차지할 폭 (em 배수).
        /// <para>⚠ 「고정폭이다/아니다」의 이분법이 아니다 — <b>글자 갈래마다 다른 고정폭</b>이 원본이다.</para>
        /// </summary>
        private static float AdvanceEmOf(char c)
        {
            // 한글 음절 · 자모 · 호환 자모
            bool hangul = (c >= '가' && c <= '힣')
                          || (c >= 'ᄀ' && c <= 'ᇿ')
                          || (c >= '㄰' && c <= '㆏');

            return hangul ? HangulAdvanceEm : LatinAdvanceEm;
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
