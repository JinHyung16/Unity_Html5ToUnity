using JinHyung.Core;
using TMPro;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 화면 글꼴 — 결정 목록의 Pretendard 구운 에셋이 있으면 그것, 없으면 윈도 시스템 한글 글꼴을 실행 중에 불러 쓴다.
    /// <para>
    /// ⚠ Pretendard 는 받기 허락이 아직 없다 (작업 기록 남은 작업). 시스템 글꼴은 빌드에 넣지 않고 사용자 PC 의 것을 쓴다 —
    /// 원본 HUD 도 시스템 글꼴(Segoe UI · 맑은 고딕)이다 `[실측] computed fontFamily`.
    /// </para>
    /// </summary>
    public static class FrontWarsFonts
    {
        /// <summary>구운 Pretendard SDF 가 들어갈 Resources 경로.</summary>
        public const string BakedFontPath = "Font/Pretendard SDF";

        private static readonly string[] OsFallbacks = { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" };

        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 2048;

        private static readonly string[] OsStyles = { "Regular", "Normal" };

        private static TMP_FontAsset _korean;
        private static bool _searched;

        public static TMP_FontAsset Korean
        {
            get
            {
                if (_korean != null || _searched)
                    return _korean != null ? _korean : TMP_Settings.defaultFontAsset;

                _searched = true;
                _korean = Resources.Load<TMP_FontAsset>(BakedFontPath);

                if (_korean != null)
                    return _korean;

                foreach (string name in OsFallbacks)
                {
                    _korean = FromOsFamily(name);

                    if (_korean != null)
                    {
                        _korean.name = $"OS {name}";
                        Log.Warning($"[FrontWarsFonts] Pretendard 구운 에셋이 없어 시스템 글꼴 {name} 을 쓴다");
                        return _korean;
                    }
                }

                Log.Error("[FrontWarsFonts] 한글 글꼴을 못 찾았다 — 글자가 두부로 뜬다");
                return TMP_Settings.defaultFontAsset;
            }
        }

        /// <summary>
        /// 시스템 글꼴 → TMP 동적 글꼴.
        /// <para>⚠ <c>Font.CreateDynamicFontFromOSFont</c> 로 만든 글꼴은 파일 경로가 없어 TMP 가 글리프를 못 읽는다 (재생 검사에서 4번 실패) — 글꼴 가족 이름으로 찾는 TMP 경로를 먼저 쓴다.</para>
        /// </summary>
        private static TMP_FontAsset FromOsFamily(string family)
        {
            foreach (string style in OsStyles)
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(family, style, SamplingPointSize);
                if (asset != null)
                    return asset;
            }

            Font os = Font.CreateDynamicFontFromOSFont(family, SamplingPointSize);
            return os == null ? null : TMP_FontAsset.CreateFontAsset(os, SamplingPointSize, AtlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, AtlasSize, AtlasSize);
        }

        /// <summary>그 아래 모든 TMP 글자에 한글 글꼴을 준다.</summary>
        public static void ApplyTo(GameObject root)
        {
            TMP_FontAsset font = Korean;

            if (root == null || font == null)
                return;

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        }
    }
}
