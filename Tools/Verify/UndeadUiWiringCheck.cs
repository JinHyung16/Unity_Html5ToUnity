using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// UI 프리팹 <b>전수 배선 검사</b> — 「컴포넌트는 다 붙었는데 포인터가 통과하는」 종류의 공백을 잡는다.
    ///
    /// <para>
    /// ★ 이것은 <b>속성 검사</b>라 편집 모드로도 정확하다 (레이캐스트를 «쏘는» 것이 아니라
    /// <c>raycastTarget</c> 이 켜져 있는지를 «본다»). 실제 포인터 판정은 재생 검사가 따로 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>[사고]</b> 「선택」·「확인」·「부활」·「아니요」·「로비로」·「계속」 여섯 버튼이
    /// <c>raycastTarget = false</c> 인 채로 나갔다. 화면은 정상이고 데이터·프리팹 검사도 전부 통과하는데
    /// <b>눌러도 아무 일이 없었다</b>. 스프라이트 헬퍼가 장식 기본값(false)으로 내는 것을
    /// 버튼 자리에서 되돌리지 않았기 때문이다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadUiWiringCheck.RunAll</c></para>
    /// </summary>
    public static class UndeadUiWiringCheck
    {
        private const string UiPrefabRoot = "Assets/Undead-Slayer/Resources/UI/Window";

#if UNITY_EDITOR
        public static void RunAll()
        {
            var report = new StringBuilder(1 << 13);
            int pass = 0;
            int fail = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabRoot });

            report.AppendLine($"UI 프리팹 {guids.Length}개 — {UiPrefabRoot}");

            void Check(string title, bool ok, string detail)
            {
                if (ok)
                    pass++;
                else
                    fail++;

                report.AppendLine($"  {(ok ? "✔" : "✘")} {title}{(ok || string.IsNullOrEmpty(detail) ? "" : "  — " + detail)}");
            }

            Check("UI 프리팹이 있다", guids.Length > 0, "한 장도 없다 — UndeadPrefabBuilder.BuildAll 을 먼저 돌린다");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                report.AppendLine($"── {prefab.name}");

                var buttons = prefab.GetComponentsInChildren<Button>(true);

                // ⚠ HUD 처럼 «원본에도» 버튼이 없는 창이 있다 — 0개인 것 자체는 결함이 아니다.
                if (buttons.Length == 0)
                {
                    report.AppendLine("  · 버튼 없음 (원본도 없다)");
                    continue;
                }

                foreach (Button b in buttons)
                {
                    Graphic target = b.targetGraphic;

                    Check($"{prefab.name}/{b.name}: targetGraphic 이 물려 있다",
                          target != null, "비어 있다 — 클릭 판정이 설 자리가 없다");

                    if (target == null)
                        continue;

                    // ★ 이 한 줄이 이번 사고의 전부다
                    Check($"{prefab.name}/{b.name}: raycastTarget 이 켜져 있다",
                          target.raycastTarget, "꺼져 있다 — 포인터가 통과해 뒤(디머)가 대신 맞는다");

                    Check($"{prefab.name}/{b.name}: 클릭을 받을 크기가 있다",
                          target.rectTransform.rect.width > 1f && target.rectTransform.rect.height > 1f,
                          $"{target.rectTransform.rect.size}");
                }

                // 디머가 버튼보다 «뒤»에 있어야 한다 — 앞에 있으면 전부 가린다
                Transform dimmer = prefab.transform.Find("Dimmer");

                if (dimmer != null && buttons.Length > 0)
                {
                    int dimmerIndex = dimmer.GetSiblingIndex();
                    var blocked = new List<string>();

                    foreach (Button b in buttons)
                    {
                        Transform top = b.transform;

                        while (top.parent != null && top.parent != prefab.transform)
                            top = top.parent;

                        if (top.parent == prefab.transform && top.GetSiblingIndex() < dimmerIndex)
                            blocked.Add(b.name);
                    }

                    Check($"{prefab.name}: 디머가 버튼을 가리지 않는다",
                          blocked.Count == 0, string.Join(",", blocked));
                }
            }

            string summary = $"═══ UI 배선 검사 {pass}/{pass + fail} 통과 ═══";
            report.AppendLine(summary);
            Debug.Log(report.ToString());
            Debug.Log(summary);

            if (fail > 0 && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
#endif
    }
}
