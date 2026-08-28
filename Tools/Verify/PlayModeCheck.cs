using System.Collections;
using System.Collections.Generic;
using System.Text;
using JinHyung.CandyCrush;
using JinHyung.Core;
using JinHyung.UI;
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
    /// <b>재생해서</b> 실제 포인터 경로로 눌러 보고 끌어 본다.
    ///
    /// <para>
    /// ⚠ <b>편집 모드 레이캐스트는 원리적으로 빈다.</b> 캔버스가 한 번도 그려지지 않아
    /// 모든 <c>Graphic.depth</c> 가 <c>-1</c> 로 남고 <c>GraphicRaycaster</c> 가 전부 건너뛴다.
    /// 입력은 <b>재생 안에서만</b> 잰다.
    /// </para>
    /// </summary>
    public static class PlayModeCheck
    {
        private const string Flag = "JinHyung.PlayModeCheck.Armed";
        private const string ScenePath = "Assets/Candy-Crush-Game/Scenes/CandyCrush.unity";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arm()
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(Flag, false) == false)
                return;

            SessionState.SetBool(Flag, false);

            var go = new GameObject("PlayModeProbe");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PlayModeProbe>();
#endif
        }
    }

    public class PlayModeProbe : MonoBehaviour
    {
        private const float Timeout = 90f;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass;
        private int _total;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;

            bool ok = false;
            IEnumerator body = Body();

            while (true)
            {
                bool moved;

                try
                {
                    moved = body.MoveNext();
                }
                catch (System.Exception e)
                {
                    _log.AppendLine($"  ❌ 검사 중 예외: {e}");
                    break;
                }

                if (moved == false)
                {
                    ok = _pass == _total && _total > 0;
                    break;
                }

                yield return body.Current;
            }

            _log.AppendLine();
            _log.AppendLine($"═══ 재생 검사 {_pass}/{_total} 통과 ═══");

            if (ok)
                Log.Success(_log.ToString());
            else
                Log.Error(_log.ToString());

            yield return null;

#if UNITY_EDITOR
            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }

        private IEnumerator Body()
        {
            _log.AppendLine($"── 재생 환경: 화면 {Screen.width}x{Screen.height} ──");

            ModeSelectWindow modeWindow = null;

            foreach (var step in WaitUntil(() =>
                     {
                         modeWindow = WindowManagement.HasInstance
                             ? WindowManagement.Instance.FindCreated(ModeSelectWindow.Key) as ModeSelectWindow
                             : null;

                         return modeWindow != null && modeWindow.IsOpen();
                     }))
                yield return step;

            Check("모드 선택 창이 열렸다", modeWindow != null && modeWindow.IsOpen());

            if (modeWindow == null)
                yield break;

            var buttons = modeWindow.GetComponentsInChildren<Button>(true);
            Check($"모드 버튼이 있다 (실측 {buttons.Length})", buttons.Length > 0);

            if (buttons.Length == 0)
                yield break;

            // ⚠ BG 가 SafeArea 앞 형제로 바뀌었다 — 버튼이 «배경보다 위»에서 포인터를 받아야 한다.
            Check("모드 버튼이 포인터를 받는다 (BG 가 안 가로막는다)",
                  ClickThroughPointer(buttons[0].transform as RectTransform));

            GameWindow gameWindow = null;

            foreach (var step in WaitUntil(() =>
                     {
                         gameWindow = WindowManagement.Instance.FindCreated(GameWindow.Key) as GameWindow;
                         return gameWindow != null && gameWindow.IsOpen();
                     }))
                yield return step;

            Check("버튼을 눌러 게임 창으로 넘어갔다", gameWindow != null && gameWindow.IsOpen());

            if (gameWindow == null)
                yield break;

            yield return null;

            var cells = new List<CellComponent>(gameWindow.GetComponentsInChildren<CellComponent>(true));
            Check($"칸이 64개다 (실측 {cells.Count})", cells.Count == 64);

            if (cells.Count < 64)
                yield break;

            foreach (int index in new[] { 0, 20, 63 })
                Check($"칸 {index} 이 포인터를 받는다", HitCell(cells[index]));

            BoardManager board = CandyGameManager.Instance.BoardMgr;

            int began = 0, dropped = 0, ended = 0;
            gameWindow.OnCellDragBegan += _ => began++;
            gameWindow.OnCellDropped += _ => dropped++;
            gameWindow.OnCellDragEnded += _ => ended++;

            // ⚠ «프레임 수»로 기다리지 않는다. 화면 없는 재생은 초당 수천 프레임이라
            //   60프레임이 틱 간격(0.1초)에도 못 미친다 — 틱이 안 도는 것으로 잘못 읽힌다.
            int idleStart = board.Score;
            int idleFrames = 0;
            float idleUntil = Time.realtimeSinceStartup + 1.5f;

            while (Time.realtimeSinceStartup < idleUntil)
            {
                idleFrames++;
                yield return null;
            }

            _log.AppendLine($"        틱 진단: IsRunning={board.IsRunning}"
                            + $" 점수 {idleStart} → {board.Score} (1.5초 · {idleFrames}프레임)");

            Check("드래그 없이도 초기 매치가 저절로 터진다 (원본 거동)", board.Score > idleStart);

            int scoreBefore = board.Score;
            bool swapped = false;

            for (int i = 0; i < cells.Count && swapped == false; i++)
            {
                int right = i + 1;

                if (right % 8 == 0 || right >= cells.Count)
                    continue;

                if (DragBetween(cells[i], cells[right]) == false)
                    continue;

                float until = Time.realtimeSinceStartup + 0.3f;

                while (Time.realtimeSinceStartup < until && swapped == false)
                {
                    yield return null;
                    swapped = board.Score > scoreBefore;
                }
            }

            _log.AppendLine($"        드래그 이벤트: 시작 {began} · 드롭 {dropped} · 종료 {ended}");
            Check("드래그 이벤트가 보드까지 닿았다", began > 0 && dropped > 0 && ended > 0);
            Check($"드래그로 실제 매치가 났다 (점수 {scoreBefore} → {board.Score})", swapped);
        }

        // ────────────────────────────── 포인터 경로

        private bool ClickThroughPointer(RectTransform target)
        {
            if (target == null || EventSystem.current == null)
                return false;

            var results = RaycastAt(ScreenPointOf(target), out Vector2 screen);

            if (results.Count == 0)
            {
                _log.AppendLine($"        아무것도 안 맞았다 ({screen})");
                return false;
            }

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screen,
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = results[0],
                pointerPressRaycast = results[0],
            };

            GameObject pressed = ExecuteEvents.ExecuteHierarchy(results[0].gameObject, pointer,
                                                               ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;

            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            GameObject clicked = ExecuteEvents.ExecuteHierarchy(results[0].gameObject, pointer,
                                                               ExecuteEvents.pointerClickHandler);

            _log.AppendLine($"        맞은 것: {results[0].gameObject.name} / 클릭 주인: "
                            + (clicked == null ? "없음" : clicked.name));

            return clicked != null;
        }

        private bool HitCell(CellComponent cell)
        {
            var results = RaycastAt(ScreenPointOf(cell.CachedRectTransform), out Vector2 screen);

            if (results.Count == 0)
            {
                _log.AppendLine($"        아무것도 안 맞았다 ({screen})");
                return false;
            }

            GameObject owner = ExecuteEvents.GetEventHandler<IBeginDragHandler>(results[0].gameObject);
            bool ok = owner != null && owner.GetComponent<CellComponent>() == cell;

            _log.AppendLine($"        맞은 것: {results[0].gameObject.name}"
                            + $" / 드래그 주인: {(owner == null ? "없음" : owner.name)}");

            return ok;
        }

        /// <summary>원본 <c>dragstart → drop → dragend</c> 순서를 그대로 흘린다.</summary>
        private bool DragBetween(CellComponent from, CellComponent to)
        {
            if (EventSystem.current == null)
                return false;

            var fromHits = RaycastAt(ScreenPointOf(from.CachedRectTransform), out Vector2 fromScreen);
            var toHits = RaycastAt(ScreenPointOf(to.CachedRectTransform), out Vector2 toScreen);

            if (fromHits.Count == 0 || toHits.Count == 0)
                return false;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = fromScreen,
                button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = fromHits[0],
                pointerPressRaycast = fromHits[0],
            };

            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(fromHits[0].gameObject, pointer,
                                                                 ExecuteEvents.pointerDownHandler);
            pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(fromHits[0].gameObject);

            if (pointer.pointerDrag == null)
                return false;

            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.beginDragHandler);

            pointer.position = toScreen;
            pointer.delta = toScreen - fromScreen;
            pointer.pointerCurrentRaycast = toHits[0];
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.dragHandler);

            ExecuteEvents.ExecuteHierarchy(toHits[0].gameObject, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);

            return true;
        }

        private static Vector2 ScreenPointOf(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rect.position);
        }

        private List<RaycastResult> RaycastAt(Vector2 screen, out Vector2 used)
        {
            used = screen;

            var pointer = new PointerEventData(EventSystem.current) { position = screen };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            return results;
        }

        private IEnumerable WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;

            while (condition() == false && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private void Check(string label, bool ok)
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
