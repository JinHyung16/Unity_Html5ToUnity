using System;
using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.FrontWarsIo;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이 게임의 TMP 한글 폰트 에셋을 <b>코드로</b> 굽는다 (결정 목록 F2 — Pretendard, SIL OFL).
    ///
    /// <para>
    /// ⚠ <b>원본은 시스템 글꼴</b>이다 (`Segoe UI` · 맑은 고딕 `[실측] computed fontFamily`) — 파일이 없어 그대로 못 쓴다.
    /// 그래서 «의도된 차이»로 재배포가 되는 Pretendard 를 쓴다. 사람 허락을 받아 받은 파일이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>둘이 사는 곳이 다르다.</b> 구운 폰트 에셋은 <c>Resources/</c> (UI 프리팹이 Resources 라서),
    /// 원본 서체 파일은 <c>Editor/</c> 다 (굽는 «입력» — 빌드에 들어갈 이유가 없다).
    /// </para>
    ///
    /// <para>
    /// ★★ <b>쓰는 글자를 미리 굽고 <see cref="AtlasPopulationMode.Static"/> 으로 고정한다.</b>
    /// 동적(Dynamic) 에셋은 재생에 들어갈 때 글자표를 비우고 원본 서체로 다시 채우는데,
    /// 원본 서체는 <c>Editor/</c> 라 빌드에 없다 — 그대로 두면 빌드에서 전부 두부(□)가 된다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsFontSetup.Setup</c></para>
    /// </summary>
    public static class FrontWarsFontSetup
    {
        /// <summary>받은 서체 — 바꾸려면 이 한 줄이다 (파일은 <see cref="FontFolder"/> 에 둔다).</summary>
        public const string SourceFontPath = "Assets/FrontWars-io/Editor/Font/Pretendard-Regular.otf";

        /// <summary>굽는 입력이 사는 곳 — 빌드 제외.</summary>
        public const string FontFolder = "Assets/FrontWars-io/Editor/Font";

        /// <summary>구운 에셋 — <c>FrontWarsFonts.BakedFontPath</c> 가 이 이름으로 읽는다.</summary>
        public const string FontAssetPath = "Assets/FrontWars-io/Resources/Font/Pretendard SDF.asset";

        /// <summary>데이터 표가 사는 곳 — 여기 JSON 의 모든 글자를 굽는다 (문구 · 봇 이름 · 국가 이름 · 지도 이름).</summary>
        private const string DataFolder = "Assets/FrontWars-io/Data";

        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 2048;

        /// <summary>표에 없어도 화면에 나오는 글자 — 숫자 · 라틴 · 기호 (창이 만드는 「%」 「/」 「+」 「×」 등).</summary>
        private const string ExtraCharacters =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
            + " .,:;!?()[]{}<>/\\|-_+=*&^%$#@~`'\""
            + "·…→←↑↓×÷−•●■✓✗°‹›«»";

        public static void Setup()
        {
            WarnUnusedFonts();

            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);

            if (font == null)
            {
                Log.Error($"서체 파일을 못 읽었다: {SourceFontPath}");
                return;
            }

            if (File.Exists(FontAssetPath))
                AssetDatabase.DeleteAsset(FontAssetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
            AssetDatabase.Refresh();

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Log.Error("TMP 폰트 에셋 생성 실패");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);

            // ★ 한글은 쓰는 글자만 해도 아틀라스 한 장을 넘긴다 — 여러 장을 허용하지 않으면 남은 글자가 조용히 두부가 된다
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

            string used = CollectUsedCharacters();

            if (fontAsset.TryAddCharacters(used, out string missing) == false || string.IsNullOrEmpty(missing) == false)
                Log.Error($"서체가 못 굽는 글자가 있다 ({missing?.Length ?? 0}자): {missing}");
            else
                Log.Success($"표에 나오는 글자 {used.Length}자를 전부 구웠다 (아틀라스 {fontAsset.atlasTextures.Length}장)");

            // ★★ 동적 → 정적. 이걸 안 하면 빌드에서 글자표가 비어 두부가 된다
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log.Success($"폰트 에셋 생성 — {FontAssetPath} · 서체 {Path.GetFileName(SourceFontPath)} (Pretendard · SIL OFL · 의도된 차이: 원본은 시스템 글꼴)");
        }

        /// <summary>폰트 폴더에 쓰이지 않는 서체가 있으면 알린다 — 넣어 두고 경로를 안 바꾸면 그대로 지나간다.</summary>
        private static void WarnUnusedFonts()
        {
            if (Directory.Exists(FontFolder) == false)
                return;

            string used = Path.GetFileName(SourceFontPath);

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

                Log.Warning($"쓰이지 않는 서체가 폰트 폴더에 있다: {name} → 쓰려면 {nameof(FrontWarsFontSetup)}.{nameof(SourceFontPath)} 를 바꾸고 다시 돌린다");
            }
        }

        /// <summary>
        /// 데이터 표 JSON 의 <b>모든 글자</b>를 모은다 — 문구 · 봇 이름 · 국가 이름 · 지도 이름이 다 표에 있다.
        /// <para>글자 목록을 손으로 적지 않는다. 표가 늘면 다시 구우면 된다.</para>
        /// </summary>
        private static string CollectUsedCharacters()
        {
            var set = new HashSet<char>();

            foreach (char c in ExtraCharacters)
                set.Add(c);

            if (Directory.Exists(DataFolder) == false)
            {
                Log.Warning($"데이터 폴더가 없다: {DataFolder} — 기본 글자만 굽는다");
                return ToText(set);
            }

            HashSet<char> emoji = EmojiCharacters();

            foreach (string path in Directory.GetFiles(DataFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                foreach (char c in File.ReadAllText(path))
                {
                    // JSON 문법 글자까지 넣어도 손해가 없다 — 어차피 기호는 몇 자 안 된다
                    // 이모지는 서체가 아니라 구운 이모지 스프라이트가 그린다 (`FrontWarsEmojiSpriteBake`)
                    if (char.IsControl(c) == false && emoji.Contains(c) == false)
                        set.Add(c);
                }
            }

            return ToText(set);
        }

        /// <summary>이모지 스프라이트가 맡는 글자 — 서로게이트(그림 이모지 반쪽) · 모양 고르개 U+FE0F · 이모지 목록에 든 기호.</summary>
        private static HashSet<char> EmojiCharacters()
        {
            var set = new HashSet<char> { (char)0xFE0F };
            var lists = new[] { FrontWarsEmojis.All, FrontWarsTutorialObjectives.AllIcons, FrontWarsIconFactory.ExtraTextEmojis };

            foreach (IEnumerable<string> list in lists)
                foreach (string emoji in list)
                    foreach (char c in emoji)
                        set.Add(c);

            for (int c = 0xD800; c <= 0xDFFF; c++)
                set.Add((char)c);

            return set;
        }

        private static string ToText(HashSet<char> set)
        {
            var chars = new char[set.Count];
            set.CopyTo(chars);
            Array.Sort(chars);
            return new string(chars);
        }
    }
}
