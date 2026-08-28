using System.Collections.Generic;
using System.Text;
using JinHyung.CandyCrush;
using JinHyung.Core;
using JinHyung.UI;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>창 구조가 <c>Window → BG · SafeArea</c> 인가.</b>
    ///
    /// <para>
    /// 안전 영역은 <b>에디터에서 아무 일도 안 일어난다</b> (화면 전체가 안전 영역이라).
    /// 그래서 「제대로 걸렸는지」를 눈으로 못 본다 — <b>구조를 기계로 센다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ 배경이 <c>SafeArea</c> «안»에 들어가면 노치 기기에서 <b>가장자리에 빈 띠</b>가 생긴다.
    /// 그건 기기에서만 보이므로, 여기서 안 잡으면 <b>출시하고 나서 안다.</b>
    /// </para>
    /// </summary>
    public static class SafeAreaCheck
    {
        private static StringBuilder _log;
        private static int _pass;
        private static int _total;

        public static void RunAll()
        {
            _log = new StringBuilder();
            _pass = 0;
            _total = 0;

            foreach (string path in new[]
                     {
                         ModeSelectWindow.Key.Path, GameWindow.Key.Path, TimeUpPopup.Key.Path,
                     })
            {
                CheckWindow(path);
            }

            _log.AppendLine();
            _log.AppendLine($"═══ SafeArea 구조 {_pass}/{_total} 통과 ═══");

            if (_pass == _total)
                Log.Success(_log.ToString());
            else
                Log.Error(_log.ToString());
        }

        private static void CheckWindow(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            string shortName = System.IO.Path.GetFileName(path);

            _log.AppendLine($"── {shortName} ──");

            if (prefab == null)
            {
                Check($"{shortName} 를 읽었다", false);
                return;
            }

            Transform root = prefab.transform;
            var childNames = new List<string>();

            for (int i = 0; i < root.childCount; i++)
                childNames.Add(root.GetChild(i).name);

            _log.AppendLine($"        루트 자식 순서: {string.Join(" → ", childNames)}");

            // ① SafeArea 는 «루트의 직계 자식»이다
            Transform safe = root.Find("SafeArea");
            Check($"{shortName} SafeArea 가 루트 바로 아래 있다", safe != null);

            if (safe == null)
                return;

            // ② 컴포넌트가 실제로 붙어 있다 — 이름만 SafeArea 인 빈 껍데기이면 아무 일도 안 한다
            var component = safe.GetComponent<SafeArea>();
            Check($"{shortName} SafeArea 컴포넌트가 붙어 있다", component != null);

            // ③ 배경은 SafeArea «밖»이고 «먼저» 그려진다
            Transform bg = root.Find("BG");

            if (bg != null)
            {
                Check($"{shortName} BG 가 SafeArea 밖(루트 직계)이다", bg.parent == root);
                Check($"{shortName} BG 가 SafeArea 보다 먼저 그려진다",
                      bg.GetSiblingIndex() < safe.GetSiblingIndex());
                Check($"{shortName} BG 가 화면을 꽉 채운다 (앵커 0~1)",
                      IsStretched(bg as RectTransform));
            }
            else
            {
                // 게임 창은 배경이 «씬»에 있다 — 창에 깔면 원본에서 판 뒤로 보이던 하늘이 가려진다.
                _log.AppendLine("        BG 없음 — 배경이 씬에 있는 창이다");
            }

            // ④ SafeArea «안»에 배경 노릇을 하는 풀스크린 이미지가 없어야 한다
            Check($"{shortName} SafeArea 안에 풀스크린 배경이 없다", HasNoFullScreenImage(safe));

            // ⑤ 콘텐츠는 SafeArea 안에 있다
            Check($"{shortName} 콘텐츠가 SafeArea 안에 있다", safe.childCount > 0);
        }

        private static bool IsStretched(RectTransform rect)
        {
            if (rect == null)
                return false;

            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
        }

        /// <summary>SafeArea 직계 자식 중 «화면을 꽉 채우는 이미지»가 있으면 배경이 잘못 들어간 것이다.</summary>
        private static bool HasNoFullScreenImage(Transform safe)
        {
            for (int i = 0; i < safe.childCount; i++)
            {
                Transform child = safe.GetChild(i);

                if (child.GetComponent<Image>() == null)
                    continue;

                if (IsStretched(child as RectTransform))
                {
                    _log.AppendLine($"        ⚠ SafeArea 안에 꽉 찬 이미지: {child.name}");
                    return false;
                }
            }

            return true;
        }

        private static void Check(string label, bool ok)
        {
            _total++;

            if (ok)
            {
                _pass++;
                _log.AppendLine($"  ✅ {label}");
                return;
            }

            _log.AppendLine($"  ❌ {label}");
        }
    }
}
