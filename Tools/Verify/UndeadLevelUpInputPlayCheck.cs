using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JinHyung.UI;
using JinHyung.UI.Fx;
using JinHyung.UndeadSlayer;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 「레벨 업 카드를 눌러도 아무 일이 없다」를 <b>재생 안에서 실제 포인터 이벤트로</b> 재현한다.
    ///
    /// <para>
    /// ★★ 편집 모드로는 못 잰다 — 캔버스가 한 번도 렌더되지 않으면 모든 <c>Graphic</c> 의 depth 가 −1 로
    /// 남고 <c>GraphicRaycaster</c> 가 전부 건너뛴다 (공통절차 「입력 판정은 재생 안에서만」).
    /// </para>
    ///
    /// <para>
    /// ★ <b>대조군을 같이 쏜다</b> — 「시작」 버튼(사람이 눌러서 되는 것으로 아는 것)을 같은 경로로 때린다.
    /// 대조군까지 안 맞으면 게임이 아니라 검사 환경이다.
    /// </para>
    ///
    /// <para>실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.UndeadLevelUpInputPlayCheck.RunAll</c></para>
    /// </summary>
    public static class UndeadLevelUpInputPlayCheck
    {
        public const string ArmedKey = "JinHyung.UndeadLevelUpInputPlayCheck.Armed";
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";
        public const string ScenePath = "Assets/Undead-Slayer/Scenes/UndeadSlayer.unity";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arm()
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(ArmedKey, false) == false)
                return;

            SessionState.SetBool(ArmedKey, false);

            var go = new GameObject("UndeadLevelUpInputProbe");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<UndeadLevelUpInputProbe>();
#endif
        }
    }

    public sealed class UndeadLevelUpInputProbe : MonoBehaviour
    {
        private const string ReportPath = "Tools/Verify/out/UndeadLevelUpInputReport.txt";

        private readonly StringBuilder _report = new StringBuilder(1 << 14);
        private int _pass;
        private int _fail;

        private UndeadGameRoot _root;

        private IEnumerator Start()
        {
            // ⚠ 덤프는 «마우스 없는» 상태를 잰다 — 진짜 커서가 창 위에 있으면 호버가 얹힌다
            Screen.SetResolution(1920, 1080, false);
            yield return null;

            Line($"화면 {Screen.width}x{Screen.height}");

            // ── 초기화를 기다린다 (시간 기준 — 프레임 수로 기다리지 않는다)
            float t = 0f;
            while (t < 40f && (UndeadGameRoot.Instance == null || UndeadGameRoot.Instance.Game == null))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _root = UndeadGameRoot.Instance;
            Check("게임 루트가 섰다", _root != null && _root.Game != null, "");
            Check("EventSystem 이 있다", EventSystem.current != null, "");

            if (_root == null || _root.Game == null)
            {
                Finish();
                yield break;
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in canvases)
                Line($"  canvas {c.name} mode={c.renderMode} order={c.sortingOrder} raycaster={(c.GetComponent<GraphicRaycaster>() != null)}");

            // ── 대조군 — 「시작」 버튼을 실제 레이캐스트로 누른다 ───────────
            UndeadReadyWindow ready = FindObjectsByType<UndeadReadyWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0
                ? FindObjectsByType<UndeadReadyWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0]
                : null;

            t = 0f;
            while (t < 20f && (ready == null || ready.IsOpen() == false))
            {
                var found = FindObjectsByType<UndeadReadyWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                ready = found.Length > 0 ? found[0] : null;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            Check("대기 창이 떴다", ready != null && ready.IsOpen(), "");

            if (ready != null)
            {
                Button startButton = ready.GetComponentInChildren<Button>(true);
                Check("[대조군] 「시작」 버튼이 있다", startButton != null, "");

                if (startButton != null)
                {
                    yield return null;
                    Diagnose("[대조군] 시작", startButton.transform);
                    bool hit = RaycastHits(startButton.transform);
                    Check("[대조군] 「시작」이 레이캐스트에 맞는다", hit,
                          hit ? "" : $"맞은 것: {FirstRaycastName(ScreenPointOf(startButton.transform))}");

                    ClickAt(startButton.transform);
                    yield return new WaitForSecondsRealtime(0.5f);

                    Check("[대조군] 「시작」으로 전투가 시작됐다", _root.Game.Paused == false,
                          $"Paused={_root.Game.Paused}");
                }
            }

            // ── 레벨업까지 간다 — 보석을 향해 «실제 입력 소스»로 움직인다 ───
            // ⚠ 게임 코드에 문을 뚫지 않는다. MoveInputSource 는 원래 있는 공개 배선이다.
            System.Func<UndeadVec2> original = _root.Game.MoveInputSource;
            _root.Game.MoveInputSource = BotInput;

            UndeadLevelUpWindow levelUp = null;
            t = 0f;

            while (t < 120f)
            {
                var found = FindObjectsByType<UndeadLevelUpWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (found.Length > 0 && found[0].IsOpen())
                {
                    levelUp = found[0];
                    break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _root.Game.MoveInputSource = original;

            Check("레벨 업 창이 떴다", levelUp != null,
                  levelUp != null ? "" : $"{t:F1}초 안에 레벨 업이 안 왔다 (게이지 {_root.Game.Simulation?.Gauge}/{_root.Game.Simulation?.GaugeGoal})");

            if (levelUp == null)
            {
                Finish();
                yield break;
            }

            // 한 프레임 그려야 depth 가 선다
            yield return null;
            yield return null;

            Line($"레벨 업 시점 — 레벨 {_root.Game.Simulation.Level} · Paused={_root.Game.Paused} · timeScale={Time.timeScale}");

            var buttons = new List<Button>();
            CollectCardButtons(levelUp, buttons);
            Check("카드 버튼을 찾았다", buttons.Count >= 1, $"찾은 수 {buttons.Count}");

            Button confirm = FindConfirm(levelUp);
            Line($"「선택」 버튼 오브젝트 = {(confirm == null ? "없음" : confirm.name)} · 활성={(confirm != null && confirm.gameObject.activeInHierarchy)}");

            // ── ★ 여기가 본론 — 카드를 실제로 누른다 ──────────────────────
            for (int i = 0; i < buttons.Count; i++)
                Diagnose($"카드{i}", buttons[i].transform);

            if (buttons.Count > 0)
            {
                Transform card = buttons[0].transform;
                Vector2 point = ScreenPointOf(card);
                GameObject first = FirstRaycast(point);

                Line($"카드0 화면점 {point} · RaycastAll 첫 결과 = {(first == null ? "없음" : Path(first.transform))}");
                DumpRaycastAll(point);

                bool cardHit = RaycastHitsScreenPoint(point, card);
                Check("카드0 이 레이캐스트에 맞는다", cardHit,
                      cardHit ? "" : $"대신 맞은 것: {(first == null ? "없음" : Path(first.transform))}");

                var stateScale = card.GetComponent<UiStateScale>();
                bool selectedBefore = IsSelected(stateScale);

                ClickAt(card);
                yield return new WaitForSecondsRealtime(0.35f);

                bool selectedAfter = IsSelected(stateScale);
                Check("카드0 을 누르면 «고른» 상태가 된다", selectedAfter && selectedBefore == false,
                      $"이전 {selectedBefore} → 이후 {selectedAfter}");

                confirm = FindConfirm(levelUp);
                bool confirmShown = confirm != null && confirm.gameObject.activeInHierarchy;
                Check("카드를 고르면 「선택」이 나타난다", confirmShown,
                      confirm == null ? "「선택」 버튼 자체가 없다" : $"activeInHierarchy={confirm.gameObject.activeInHierarchy}");

                if (confirmShown)
                {
                    yield return null;
                    Diagnose("선택", confirm.transform);

                    Vector2 cp = ScreenPointOf(confirm.transform);
                    bool confirmHit = RaycastHitsScreenPoint(cp, confirm.transform);
                    Check("「선택」이 레이캐스트에 맞는다", confirmHit,
                          confirmHit ? "" : $"대신 맞은 것: {(FirstRaycast(cp) == null ? "없음" : Path(FirstRaycast(cp).transform))}");
                    DumpRaycastAll(cp);

                    double fireBefore = _root.Game.Simulation.FireRateStat;
                    double moveBefore = _root.Game.Simulation.MoveSpeedStat;
                    int damageBefore = _root.Game.Simulation.DamageStat;
                    double radiusBefore = _root.Game.Simulation.CollectRadiusStat;

                    ClickAt(confirm.transform);
                    yield return new WaitForSecondsRealtime(0.6f);

                    Check("「선택」으로 레벨 업 창이 닫힌다", levelUp.IsOpen() == false,
                          $"IsOpen={levelUp.IsOpen()}");
                    Check("「선택」으로 전투가 재개된다", _root.Game.Paused == false,
                          $"Paused={_root.Game.Paused}");

                    bool statChanged =
                        !Mathf.Approximately((float)fireBefore, (float)_root.Game.Simulation.FireRateStat) ||
                        !Mathf.Approximately((float)moveBefore, (float)_root.Game.Simulation.MoveSpeedStat) ||
                        damageBefore != _root.Game.Simulation.DamageStat ||
                        !Mathf.Approximately((float)radiusBefore, (float)_root.Game.Simulation.CollectRadiusStat) ||
                        _root.Game.Simulation.LightningCount > 0;

                    Check("고른 업그레이드가 실제로 적용된다", statChanged,
                          $"fire {fireBefore}→{_root.Game.Simulation.FireRateStat} · move {moveBefore}→{_root.Game.Simulation.MoveSpeedStat} · dmg {damageBefore}→{_root.Game.Simulation.DamageStat} · radius {radiusBefore}→{_root.Game.Simulation.CollectRadiusStat}");
                }
            }

            Finish();
        }

        /// <summary>보석 쪽으로 움직이는 봇 — 레벨 업을 «실제로 벌어» 창을 띄우기 위한 것이다.</summary>
        private UndeadVec2 BotInput()
        {
            UndeadSimulation sim = _root == null || _root.Game == null ? null : _root.Game.Simulation;

            if (sim == null)
                return new UndeadVec2(0.0, 0.0);

            double bestDistance = double.MaxValue;
            UndeadVec2 target = sim.HeroPosition;
            bool found = false;

            for (int i = 0; i < sim.Gems.Count; i++)
            {
                if (sim.Gems[i].Active == false)
                    continue;

                UndeadVec2 d = sim.Gems[i].Position - sim.HeroPosition;
                double len = d.X * d.X + d.Y * d.Y;

                if (len >= bestDistance)
                    continue;

                bestDistance = len;
                target = sim.Gems[i].Position;
                found = true;
            }

            if (found == false)
                return new UndeadVec2(0.0, 0.0);

            UndeadVec2 dir = target - sim.HeroPosition;
            double m = System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);

            return m < 0.001 ? new UndeadVec2(0.0, 0.0) : new UndeadVec2(dir.X / m, dir.Y / m);
        }

        // ── 진단 · 유틸 ────────────────────────────────────────────────
        private static bool IsSelected(UiStateScale state)
        {
            if (state == null)
                return false;

            return state.Selected;
        }

        private static void CollectCardButtons(UndeadLevelUpWindow window, List<Button> into)
        {
            var all = window.GetComponentsInChildren<Button>(true);

            foreach (Button b in all)
            {
                if (b.name.StartsWith("Card"))
                    into.Add(b);
            }
        }

        private static Button FindConfirm(UndeadLevelUpWindow window)
        {
            var all = window.GetComponentsInChildren<Button>(true);

            foreach (Button b in all)
            {
                if (b.name == "Confirm")
                    return b;
            }

            return null;
        }

        private void Diagnose(string title, Transform target)
        {
            var graphic = target.GetComponent<Graphic>();
            var canvas = target.GetComponentInParent<Canvas>();
            var group = target.GetComponentInParent<CanvasGroup>();
            Vector2 p = ScreenPointOf(target);

            Line($"  ▸ {title} {Path(target)}");
            Line($"      활성={target.gameObject.activeInHierarchy} 화면점={p} 월드={target.position}");
            Line($"      graphic={(graphic == null ? "없음" : graphic.GetType().Name)} raycastTarget={(graphic != null && graphic.raycastTarget)} depth={(graphic == null ? -99 : graphic.depth)} cull={(graphic != null && graphic.canvasRenderer.cull)} color.a={(graphic == null ? 0f : graphic.color.a)}");
            Line($"      canvas={(canvas == null ? "없음" : canvas.name)} camera={(canvas == null || canvas.worldCamera == null ? "없음" : canvas.worldCamera.name)} group.blocks={(group == null ? "없음" : group.blocksRaycasts.ToString())} group.alpha={(group == null ? "-" : group.alpha.ToString("F2"))} group.interactable={(group == null ? "-" : group.interactable.ToString())}");

            var rect = target as RectTransform;

            if (rect != null)
                Line($"      rect size={rect.rect.size} anchoredPos={rect.anchoredPosition} scale={rect.localScale}");
        }

        private void DumpRaycastAll(Vector2 point)
        {
            if (EventSystem.current == null)
            {
                Line("      RaycastAll: EventSystem 이 없다");
                return;
            }

            var data = new PointerEventData(EventSystem.current) { position = point };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);

            Line($"      RaycastAll {results.Count}개");

            for (int i = 0; i < results.Count && i < 8; i++)
                Line($"        [{i}] {Path(results[i].gameObject.transform)} depth={results[i].depth} sortingOrder={results[i].sortingOrder}");
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);

            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");

            return sb.ToString();
        }

        private static void ClickAt(Transform target)
        {
            if (target == null)
                return;

            Vector2 point = ScreenPointOf(target);
            GameObject hit = FirstRaycast(point);

            if (hit == null)
                return;

            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };

            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
        }

        private static bool RaycastHits(Transform target)
        {
            return RaycastHitsScreenPoint(ScreenPointOf(target), target);
        }

        private static bool RaycastHitsScreenPoint(Vector2 point, Transform target)
        {
            GameObject hit = FirstRaycast(point);

            if (hit == null || target == null)
                return false;

            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        private static GameObject FirstRaycast(Vector2 point)
        {
            if (EventSystem.current == null)
                return null;

            var data = new PointerEventData(EventSystem.current) { position = point };
            var results = new List<RaycastResult>();

            EventSystem.current.RaycastAll(data, results);
            return results.Count == 0 ? null : results[0].gameObject;
        }

        private static string FirstRaycastName(Vector2 point)
        {
            GameObject hit = FirstRaycast(point);
            return hit == null ? "없음" : Path(hit.transform);
        }

        private static Vector2 ScreenPointOf(Transform target)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas == null ? null : canvas.worldCamera;

            return RectTransformUtility.WorldToScreenPoint(camera, target.position);
        }

        private void Line(string text)
        {
            _report.AppendLine(text);
        }

        private void Check(string title, bool ok, string detail)
        {
            if (ok)
                _pass++;
            else
                _fail++;

            _report.AppendLine($"{(ok ? "  ✔" : "  ✘")} {title}{(ok || string.IsNullOrEmpty(detail) ? "" : "  — " + detail)}");
        }

        private void Finish()
        {
            string summary = $"═══ 레벨 업 입력 검사 {_pass}/{_pass + _fail} 통과 ═══";
            _report.AppendLine(summary);

            Directory.CreateDirectory("Tools/Verify/out");
            File.WriteAllText(ReportPath, _report.ToString());

            Debug.Log(_report.ToString());
            Debug.Log(summary);

#if UNITY_EDITOR
            // ⚠ 다리가 시킨 실행이면 사람의 에디터를 끄지 않는다 (재발방지 #140)
            if (SessionState.GetBool(UndeadLevelUpInputPlayCheck.BridgeDrivingKey, false))
                EditorApplication.ExitPlaymode();
            else
                EditorApplication.Exit(_fail == 0 ? 0 : 1);
#endif
        }
    }
}
