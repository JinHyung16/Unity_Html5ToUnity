using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JinHyung.UI;
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
    /// 우리 쪽 <b>사건 프레임 덤프</b> — 원본(PixiJS 씬 그래프)과 «표 대 표»로 맞추기 위한 것이다.
    ///
    /// <para>
    /// ★★ 좌표는 <b>원본 논리 좌표(세로 580 고정)</b>로 적는다. 엔진이 다르면 앵커·피벗의 뜻이 달라서
    /// 같은 그림도 좌표가 다르게 적히므로, 양쪽 다 <b>경계 상자의 중심(cx, cy)과 크기(w, h)</b>로 맞춘다
    /// (공통절차 「맞추는 값은 앵커 좌표가 아니라 경계 상자다」).
    /// </para>
    ///
    /// <para>
    /// ⚠ 화면비를 못 박고 돌린다 — 배치 재생 기본 게임 뷰는 4:3 이라 가장자리 HUD 가 다른 자리에 선다.
    /// 러너가 <c>-screen-width/-screen-height</c> 로 1920×1080 을 준다.
    /// </para>
    ///
    /// <para>실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.UndeadFrameDumpPlayCheck.RunAll</c></para>
    /// </summary>
    public static class UndeadFrameDumpPlayCheck
    {
        public const string ArmedKey = "JinHyung.UndeadFrameDumpPlayCheck.Armed";
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";
        public const string ScenePath = "Assets/Undead-Slayer/Scenes/UndeadSlayer.unity";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);
            ForceGameViewSize(1920, 1080);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// 게임 뷰를 <b>원본 화면비</b>로 못 박는다.
        ///
        /// <para>
        /// ⚠ 배치 재생의 기본 게임 뷰는 <b>4:3(640×480)</b> 이다. 원본은 16:9 라
        /// 가장자리에 붙는 HUD(보석·레벨)가 <b>통째로 다른 자리에 선다</b> —
        /// 그 상태로 대조하면 「좌표가 다르다」가 수십 개 나오고 진짜 결함이 묻힌다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <c>-screen-width/-screen-height</c> 는 «플레이어 빌드»에만 먹는다. 에디터 재생은
        /// 게임 뷰 크기 목록을 봐야 하고, 그 API 가 공개되어 있지 않아 리플렉션으로 간다.
        /// </para>
        /// </summary>
        public static void ForceGameViewSize(int width, int height)
        {
            System.Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            System.Type groupType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType");
            System.Type sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
            System.Type sizeKind = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");

            if (sizesType == null || groupType == null || sizeType == null || sizeKind == null)
            {
                Debug.LogWarning("게임 뷰 크기 API 를 못 찾았다 — 기본 크기로 돈다");
                return;
            }

            System.Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);

            object currentGroup = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.ToObject(groupType, currentGroup) });

            // GameViewSizeGroup 은 GetTotalCount() / GetGameViewSize(i) 로만 열린다 (리스트는 비공개 필드다)
            int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            System.Reflection.MethodInfo at = group.GetType().GetMethod("GetGameViewSize");

            int index = -1;

            for (int i = 0; i < total; i++)
            {
                object item = at.Invoke(group, new object[] { i });
                int w = (int)sizeType.GetProperty("width").GetValue(item);
                int h = (int)sizeType.GetProperty("height").GetValue(item);

                if (w != width || h != height)
                    continue;

                index = i;
                break;
            }

            if (index < 0)
            {
                object created = System.Activator.CreateInstance(sizeType,
                    System.Enum.ToObject(sizeKind, 0), width, height, $"Origin {width}x{height}");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { created });
                index = total;
            }

            System.Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(gameViewType, false, null, false);

            gameViewType.GetMethod("SizeSelectionCallback",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(view, new object[] { index, null });

            Debug.Log($"게임 뷰 크기를 {width}x{height} 로 못 박았다 (index {index})");
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arm()
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(ArmedKey, false) == false)
                return;

            SessionState.SetBool(ArmedKey, false);

            var go = new GameObject("UndeadFrameDumpProbe");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<UndeadFrameDumpProbe>();
#endif
        }
    }

    public sealed class UndeadFrameDumpProbe : MonoBehaviour
    {
        // ⚠ Temp/ 에 두지 않는다 — 유니티가 «시작할 때» 비운다. 러너가 끝에 에디터를 다시 열면
        //   방금 뜬 덤프가 통째로 사라진다 (재발방지 #38 과 같은 뿌리).
        private const string OutDir = "Tools/Verify/out/undead_frames";

        /// <summary>원본 불변식 — 세로 580 고정 (확정표 3-d).</summary>
        private const float OriginHeight = 580f;

        private readonly StringBuilder _log = new StringBuilder(1 << 12);
        private UndeadGameRoot _root;
        private Camera _worldCamera;
        private Camera _uiCamera;

        private IEnumerator Start()
        {
            Directory.CreateDirectory(OutDir);

            float t = 0f;
            while (t < 60f && (UndeadGameRoot.Instance == null || UndeadGameRoot.Instance.Game == null))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _root = UndeadGameRoot.Instance;

            if (_root == null || _root.Game == null)
            {
                Line("✘ 게임 루트가 안 섰다");
                Finish();
                yield break;
            }

            foreach (Camera c in Camera.allCameras)
            {
                if (c.name.Contains("UI"))
                    _uiCamera = c;
                else if (_worldCamera == null)
                    _worldCamera = c;
            }

            Line($"화면 {Screen.width}x{Screen.height} · 논리 {(Screen.width / LogicalScale()):F1}x{OriginHeight} · 배율 {LogicalScale():F5}");
            Line($"world camera={(_worldCamera == null ? "없음" : _worldCamera.name)} ortho={(_worldCamera == null ? 0f : _worldCamera.orthographicSize)} · ui camera={(_uiCamera == null ? "없음" : _uiCamera.name)}");

            // ── ready ─────────────────────────────────────────────
            UndeadReadyWindow ready = null;
            t = 0f;
            while (t < 30f)
            {
                var f = FindObjectsByType<UndeadReadyWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                ready = f.Length > 0 ? f[0] : null;

                if (ready != null && ready.IsOpen())
                    break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return null;
            yield return null;
            Dump("00_ready");

            // ── started ───────────────────────────────────────────
            if (ready != null)
            {
                Button start = ready.GetComponentInChildren<Button>(true);
                ClickAt(start == null ? null : start.transform);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            Dump("01_started");

            // ── battle 3s ─────────────────────────────────────────
            System.Func<UndeadVec2> original = _root.Game.MoveInputSource;
            _root.Game.MoveInputSource = BotInput;

            yield return new WaitForSecondsRealtime(3f);
            Dump("02_battle_3s");

            // ── level up ──────────────────────────────────────────
            UndeadLevelUpWindow levelUp = null;
            t = 0f;
            while (t < 120f)
            {
                var f = FindObjectsByType<UndeadLevelUpWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (f.Length > 0 && f[0].IsOpen())
                {
                    levelUp = f[0];
                    break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _root.Game.MoveInputSource = original;

            if (levelUp == null)
            {
                Line("✘ 레벨 업이 안 왔다");
                Finish();
                yield break;
            }

            yield return null;
            yield return null;
            Dump("10_levelup_open");

            var cards = new List<Button>();

            foreach (Button b in levelUp.GetComponentsInChildren<Button>(true))
            {
                if (b.name.StartsWith("Card"))
                    cards.Add(b);
            }

            if (cards.Count > 0)
            {
                ClickAt(cards[0].transform);
                yield return new WaitForSecondsRealtime(0.35f);
                Dump("11_levelup_selected");

                Button confirm = null;

                foreach (Button b in levelUp.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == "Confirm")
                        confirm = b;
                }

                if (confirm != null && confirm.gameObject.activeInHierarchy)
                {
                    ClickAt(confirm.transform);
                    yield return new WaitForSecondsRealtime(0.7f);
                    Dump("12_levelup_confirmed");
                    Line($"확정 뒤 — 레벨 업 열림={levelUp.IsOpen()} · Paused={_root.Game.Paused}");
                }
                else
                {
                    Line("✘ 「선택」이 안 나타났다");
                }
            }

            Finish();
        }

        private UndeadVec2 BotInput()
        {
            UndeadSimulation sim = _root == null || _root.Game == null ? null : _root.Game.Simulation;

            if (sim == null)
                return new UndeadVec2(0.0, 0.0);

            double best = double.MaxValue;
            UndeadVec2 target = sim.HeroPosition;
            bool found = false;

            for (int i = 0; i < sim.Gems.Count; i++)
            {
                if (sim.Gems[i].Active == false)
                    continue;

                UndeadVec2 d = sim.Gems[i].Position - sim.HeroPosition;
                double len = d.X * d.X + d.Y * d.Y;

                if (len >= best)
                    continue;

                best = len;
                target = sim.Gems[i].Position;
                found = true;
            }

            if (found == false)
                return new UndeadVec2(0.0, 0.0);

            UndeadVec2 dir = target - sim.HeroPosition;
            double m = System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            return m < 0.001 ? new UndeadVec2(0.0, 0.0) : new UndeadVec2(dir.X / m, dir.Y / m);
        }

        // ── 덤프 ───────────────────────────────────────────────────
        private static float LogicalScale()
        {
            return Screen.height / OriginHeight;
        }

        /// <summary>화면 좌표(좌하단 원점) → 원본 논리 좌표(좌상단 원점 · 세로 580).</summary>
        private static Vector2 ToLogical(Vector2 screenPoint)
        {
            float s = LogicalScale();
            return new Vector2(screenPoint.x / s, (Screen.height - screenPoint.y) / s);
        }

        private void Dump(string label)
        {
            var sb = new StringBuilder(1 << 15);
            sb.AppendLine($"# label: {label}");
            sb.AppendLine($"# screen: {Screen.width}x{Screen.height}  logical: {(Screen.width / LogicalScale()):F2}x{OriginHeight}");

            if (_root != null && _root.Game != null && _root.Game.Simulation != null)
            {
                UndeadSimulation sim = _root.Game.Simulation;
                sb.AppendLine($"# sim: level={sim.Level} gauge={sim.Gauge}/{sim.GaugeGoal} t={sim.ElapsedSeconds:F2} enemies={sim.ActiveEnemyCount} gems={sim.ActiveGemCount} paused={_root.Game.Paused}");
            }

            sb.AppendLine("kind\tpath\tvisible\talpha\tcx\tcy\tw\th\tsprite\ttext\tfontSize\tcolor");

            var rows = new List<string>(256);

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.transform.parent != null)
                    continue;

                CollectUi(canvas.transform, rows);
            }

            foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                CollectWorld(sr, rows);

            rows.Sort(string.CompareOrdinal);

            foreach (string r in rows)
                sb.AppendLine(r);

            string path = System.IO.Path.Combine(OutDir, $"ours_{label}.tsv");
            File.WriteAllText(path, sb.ToString());
            Line($"  덤프 {label} — {rows.Count}행 → {path}");
        }

        private void CollectUi(Transform node, List<string> rows)
        {
            var graphic = node.GetComponent<Graphic>();

            if (graphic != null)
            {
                bool visible = node.gameObject.activeInHierarchy && graphic.enabled && graphic.color.a > 0.004f && GroupAlpha(node) > 0.004f;

                var rect = node as RectTransform;

                if (rect != null)
                {
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);

                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                    Vector2 max = new Vector2(float.MinValue, float.MinValue);

                    // ⚠ 경계 상자는 «네 귀» 전부로 — 돌아간 것을 대각 두 귀로 재면 폭이 반으로 나온다
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 p = ToLogical(RectTransformUtility.WorldToScreenPoint(_uiCamera, corners[i]));
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }

                    var text = graphic as TMP_Text;
                    string content = text == null ? "" : text.text.Replace("\t", " ").Replace("\n", " ");
                    string sprite = "";

                    var image = graphic as Image;

                    if (image != null && image.sprite != null)
                        sprite = image.sprite.name;

                    // 글자는 상자가 아니라 «글리프 경계»로 잰다 (서체가 다르면 자폭이 달라진다)
                    if (text != null && text.textInfo != null && text.textInfo.characterCount > 0)
                    {
                        text.ForceMeshUpdate();
                        Bounds b = text.textBounds;
                        Vector3 c0 = text.transform.TransformPoint(new Vector3(b.min.x, b.min.y, 0f));
                        Vector3 c1 = text.transform.TransformPoint(new Vector3(b.max.x, b.max.y, 0f));
                        Vector2 p0 = ToLogical(RectTransformUtility.WorldToScreenPoint(_uiCamera, c0));
                        Vector2 p1 = ToLogical(RectTransformUtility.WorldToScreenPoint(_uiCamera, c1));
                        min = Vector2.Min(p0, p1);
                        max = Vector2.Max(p0, p1);
                    }

                    Vector2 size = max - min;
                    Vector2 center = (max + min) * 0.5f;

                    rows.Add(string.Join("\t",
                        "ui", NodePath(node), visible ? "1" : "0", F(graphic.color.a * GroupAlpha(node)),
                        F(center.x), F(center.y), F(size.x), F(size.y),
                        sprite, content,
                        text == null ? "" : F(text.fontSize / LogicalScale()),
                        ColorUtility.ToHtmlStringRGB(graphic.color)));
                }
            }

            for (int i = 0; i < node.childCount; i++)
                CollectUi(node.GetChild(i), rows);
        }

        private void CollectWorld(SpriteRenderer sr, List<string> rows)
        {
            if (_worldCamera == null)
                return;

            bool visible = sr.gameObject.activeInHierarchy && sr.enabled && sr.color.a > 0.004f && sr.sprite != null;

            if (visible == false)
                return;

            Bounds b = sr.bounds;
            var pts = new Vector3[4]
            {
                new Vector3(b.min.x, b.min.y, 0f),
                new Vector3(b.max.x, b.min.y, 0f),
                new Vector3(b.min.x, b.max.y, 0f),
                new Vector3(b.max.x, b.max.y, 0f),
            };

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector2 p = ToLogical(_worldCamera.WorldToScreenPoint(pts[i]));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Vector2 size = max - min;
            Vector2 center = (max + min) * 0.5f;

            // 화면 «밖»은 원본 덤프에도 나오므로 같이 적는다 (존재는 존재다)
            rows.Add(string.Join("\t",
                "world", NodePath(sr.transform), "1", F(sr.color.a),
                F(center.x), F(center.y), F(size.x), F(size.y),
                sr.sprite.name, "", "", ColorUtility.ToHtmlStringRGB(sr.color)));
        }

        private static float GroupAlpha(Transform node)
        {
            float a = 1f;

            for (Transform t = node; t != null; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();

                if (g != null)
                    a *= g.alpha;
            }

            return a;
        }

        private static string F(float v)
        {
            return v.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string NodePath(Transform t)
        {
            var sb = new StringBuilder(t.name);

            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");

            return sb.ToString();
        }

        private static void ClickAt(Transform target)
        {
            if (target == null || EventSystem.current == null)
                return;

            var canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas == null ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, target.position);

            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);

            if (results.Count == 0)
                return;

            GameObject hit = results[0].gameObject;
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
        }

        private void Line(string text)
        {
            _log.AppendLine(text);
        }

        private void Finish()
        {
            _log.AppendLine("═══ 사건 프레임 덤프 완료 ═══");
            Debug.Log(_log.ToString());

#if UNITY_EDITOR
            if (SessionState.GetBool(UndeadFrameDumpPlayCheck.BridgeDrivingKey, false))
                EditorApplication.ExitPlaymode();
            else
                EditorApplication.Exit(0);
#endif
        }
    }
}
