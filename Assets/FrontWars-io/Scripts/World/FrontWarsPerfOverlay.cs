using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 성능 창 — 게임 중 Shift+D 로 켜고 끈다 `[소스] QPQdQ6Sy.js:function ga(){` · `BMmXchgl.js:e.code===\`KeyD\`&&e.shiftKey`.
    /// <list type="bullet">
    /// <item>처음 자리 (20, 20) · 끌어서 옮긴다 · 오른쪽 위 × 로 닫는다. 검정 80% · 둥근 8 · 안쪽 16 · 테두리 gray-700 50% · 최대 폭 448.</item>
    /// <item>FPS (avg) = 끝난 최근 3초의 초당 프레임 평균 · Current Second = 방금 끝난 1초 · Frame Time = 최근 60프레임 평균.</item>
    /// <item>그래프 300×100 · 50ms 마다 다시 그림 · 최근 5초의 프레임별 FPS (제목은 원본 그대로 「last 10s」).</item>
    /// </list>
    /// <para>
    /// 의도된 차이: 원본 시뮬은 워커에서 돌아 「Blocked · Ages · Queries · Drain · Worker heap」 줄이 있다. 우리 시뮬은 같은 스레드라 그 줄은 없고,
    /// 「Tick · Flow」 만 게임 관리자(<see cref="FrontWarsGameManager.Stats"/>)에서 읽는다. Frame Time 은 원본의 그리기 시간 대신 프레임 간격이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrontWarsPerfOverlay : MonoBehaviour
    {
        public static readonly Vector2 StartPosition = new Vector2(20f, 20f);

        private const float PanelPadding = 16f;
        private const float PanelMaxWidth = 448f;
        private const float RowFont = 14f;
        private const float SmallFont = 12f;
        private const float CloseSize = 24f;
        private const int GraphWidth = 300;
        private const int GraphHeight = 100;
        private const float GraphRedrawSeconds = 0.05f;
        private const float GraphWindowSeconds = 5f;
        private const int FrameTimeSamples = 60;
        private const int FpsAverageSeconds = 3;
        private const float WorkerRefreshSeconds = 0.5f;

        /// <summary>다른 창 위에 뜨게 — UI 캔버스보다 높은 정렬 번호 `[소스] z-10002`.</summary>
        private const int CanvasOrder = 32000;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color BorderColor = new Color32(55, 65, 81, 128);
        private static readonly Color32 GraphLine = new Color32(0x4a, 0xde, 0x80, 255);

        private FrontWarsGame _game;
        private FrontWarsGameManager _manager;
        private FrontWarsCameraController _camera;
        private FrontWarsRailView _railView;
        private Canvas _canvas;
        private RectTransform _panel;
        private TextMeshProUGUI _text;
        private RawImage _graph;
        private Texture2D _graphTex;
        private Color32[] _graphPixels;
        private TextMeshProUGUI _graphMax;
        private TextMeshProUGUI _graphMin;
        private bool _visible;

        private readonly Queue<float> _frameTimes = new Queue<float>();
        private readonly List<int> _secondFps = new List<int>();
        private readonly List<(float At, int Fps)> _samples = new List<(float, int)>();
        private readonly StringBuilder _builder = new StringBuilder();
        private int _currentSecond = -1;
        private int _framesThisSecond;
        private int _avgFps;
        private int _lastSecondFps;
        private float _graphAt;
        private float _workerAt;
        private string _workerText = string.Empty;

        /// <summary>× 를 눌렀다 — 설정 값도 끈다.</summary>
        public event Action OnCloseRequested;

        /// <summary>켜면 화면에 뜬다 (기본 끔). 처음 켤 때 캔버스를 만든다. 켤 때마다 기록을 비운다 `[소스] d.current=0,f.current=[]…`.</summary>
        public bool Visible
        {
            get { return _visible; }
            set
            {
                if (_visible == value)
                    return;

                _visible = value;

                if (value)
                {
                    EnsureCanvas();
                    ResetSamples();
                }

                if (_canvas != null)
                    _canvas.gameObject.SetActive(value);
            }
        }

        /// <summary>판을 붙인다 — 시뮬 · 철도 · 카메라 값을 여기서 읽는다.</summary>
        public void Bind(FrontWarsGame game, FrontWarsGameManager manager, FrontWarsCameraController camera, FrontWarsRailView railView)
        {
            Unbind();
            _game = game;
            _manager = manager;
            _camera = camera;
            _railView = railView;
        }

        public void Unbind()
        {
            _game = null;
            _manager = null;
            _camera = null;
            _railView = null;
        }

        private void ResetSamples()
        {
            _frameTimes.Clear();
            _secondFps.Clear();
            _samples.Clear();
            _currentSecond = -1;
            _framesThisSecond = 0;
            _avgFps = 0;
            _lastSecondFps = 0;
            _workerAt = 0f;
        }

        private void Update()
        {
            if (_visible == false || _text == null)
                return;

            float now = Time.unscaledTime;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            RecordFrame(now, frameMs);

            if (now - _workerAt >= WorkerRefreshSeconds)
            {
                _workerAt = now;
                _workerText = WorkerRows();
            }

            _text.text = Rows();

            if (now - _graphAt >= GraphRedrawSeconds)
            {
                _graphAt = now;
                DrawGraph(now);
            }
        }

        /// <summary>프레임 기록 — 초가 넘어가면 그 초의 프레임 수를 남기고, 1초 안이면 70:30 으로 섞어 그래프 값을 낸다 `[소스] a=Math.round(t*.7+e*.3)`.</summary>
        private void RecordFrame(float now, float frameMs)
        {
            _frameTimes.Enqueue(frameMs);
            while (_frameTimes.Count > FrameTimeSamples)
                _frameTimes.Dequeue();

            int second = Mathf.FloorToInt(now);

            if (_currentSecond < 0)
            {
                _currentSecond = second;
                return;
            }

            _framesThisSecond++;

            if (second != _currentSecond)
            {
                _secondFps.Add(_framesThisSecond);
                if (_secondFps.Count > 60)
                    _secondFps.RemoveAt(0);

                int from = Mathf.Max(0, _secondFps.Count - FpsAverageSeconds);
                float sum = 0f;
                for (int i = from; i < _secondFps.Count; i++)
                    sum += _secondFps[i];
                _avgFps = Mathf.RoundToInt(sum / (_secondFps.Count - from));
                _lastSecondFps = _framesThisSecond;
                _framesThisSecond = 0;
                _currentSecond = second;
            }

            float part = now - second;
            int fps = _secondFps.Count > 0 ? _secondFps[_secondFps.Count - 1] : 0;

            if (part > 0.2f && _framesThisSecond > 0)
            {
                int estimate = Mathf.RoundToInt(_framesThisSecond / part);

                if (_secondFps.Count == 0)
                {
                    fps = estimate;
                    _avgFps = estimate;
                }
                else
                {
                    fps = Mathf.RoundToInt(fps * 0.7f + estimate * 0.3f);
                }
            }

            _samples.Add((now, fps));
            _samples.RemoveAll(s => s.At < now - GraphWindowSeconds);
        }

        private string Rows()
        {
            float frameAvg = 0f;
            foreach (float t in _frameTimes)
                frameAvg += t;
            frameAvg = _frameTimes.Count > 0 ? frameAvg / _frameTimes.Count : 0f;

            _builder.Clear();
            _builder.Append("FPS (avg): <color=#4ade80>").Append(_avgFps).Append("</color>\n");
            _builder.Append("Current Second: <color=#60a5fa>").Append(_lastSecondFps).Append("</color>").Append(Unit(" fps")).Append('\n');
            _builder.Append("Frame Time: <color=#facc15>").Append(Mathf.RoundToInt(frameAvg)).Append("</color>").Append(Unit(" ms")).Append('\n');
            _builder.Append(_workerText);
            _builder.Append(RailRows());
            _builder.Append(Section("Memory:"));
            _builder.Append("JS Heap: <color=#60a5fa>").Append((GC.GetTotalMemory(false) / (1024f * 1024f)).ToString("0.0")).Append("</color>").Append(Unit(" MB")).Append('\n');
            _builder.Append(Section("Performance Graph (last 10s):"));
            return _builder.ToString();
        }

        /// <summary>시뮬 줄 — 기준을 넘으면 노랑 · 빨강 `[소스] function ma(e,t,n){` (Tick 20/50 · queue 5/20).</summary>
        private string WorkerRows()
        {
            var b = new StringBuilder();
            b.Append(Section("Core Worker (sim):"));

            if (_manager == null || _game == null)
            {
                b.Append(Unit("waiting for worker report…")).Append('\n');
                return b.ToString();
            }

            FrontWarsSimStats stats = _manager.Stats;
            int tps = stats.TicksPerSecond;
            b.Append("Tick: ").Append(Level(stats.LastMs, 20, 50)).Append(Unit(" last ")).Append(Level(stats.AverageMs, 20, 50))
             .Append(Unit(" avg ")).Append(Level(stats.MaxMs, 20, 50)).Append(Unit(" max ms")).Append('\n');
            b.Append("Flow: <color=#60a5fa>").Append(tps).Append("</color>").Append(Unit(" tick/s "))
             .Append("<color=#60a5fa>").Append(tps).Append("</color>").Append(Unit(" turn/s · queue "))
             .Append(Level(_manager.PendingCount, 5, 20)).Append(Unit(" · turn #")).Append(_game.Ticks).Append('\n');
            return b.ToString();
        }

        /// <summary>철도 줄 — 철도 수 · 칸 수 · 화면 안 칸 수 `[소스] Rails: … / Tiles: … (~N/rail) · In viewport: … (N%)`.</summary>
        private string RailRows()
        {
            int rails = _railView != null ? _railView.RailCount : 0;
            int tiles = _railView != null ? _railView.TileCount : 0;
            int inView = _railView != null ? _railView.ViewportTileCount : 0;

            var b = new StringBuilder();
            b.Append(Section("Rail Network:"));
            b.Append("Rails: <color=#4ade80>").Append(rails).Append("</color>").Append(Unit(" / Tiles: ")).Append("<color=#4ade80>").Append(tiles).Append("</color>");
            if (rails > 0)
                b.Append(Unit($" (~{Mathf.RoundToInt(tiles / (float)rails)}/rail)"));
            b.Append('\n');
            b.Append("In viewport: <color=#60a5fa>").Append(inView).Append("</color>");
            if (tiles > 0)
                b.Append(Unit($" ({Mathf.RoundToInt(inView / (float)tiles * 100f)}%)"));
            b.Append('\n');
            return b.ToString();
        }

        private static string Unit(string text) => $"<size={SmallFont}><color=#9ca3af>{text}</color></size>";

        private static string Section(string title) => $"<size={SmallFont}><color=#d1d5db>{title}</color></size>\n";

        private static string Level(double value, double warn, double bad)
        {
            string color = value >= bad ? "#f87171" : value >= warn ? "#facc15" : "#4ade80";
            return $"<color={color}>{Math.Round(value, 1)}</color>";
        }

        /// <summary>그래프 — 배경 검정 30% · 가로 격자 11줄 흰색 10% · 선 #4ade80 두께 2 · 오른쪽 위 최댓값(최소 60) · 오른쪽 아래 최솟값(최대 0).</summary>
        private void DrawGraph(float now)
        {
            var background = new Color32(0, 0, 0, 77);
            for (int i = 0; i < _graphPixels.Length; i++)
                _graphPixels[i] = background;

            var grid = new Color32(255, 255, 255, 26);
            for (int g = 0; g <= 10; g++)
            {
                int y = Mathf.Clamp(GraphHeight - 1 - Mathf.RoundToInt(GraphHeight / 10f * g), 0, GraphHeight - 1);
                for (int x = 0; x < GraphWidth; x++)
                    _graphPixels[y * GraphWidth + x] = Blend(_graphPixels[y * GraphWidth + x], grid);
            }

            int count = _samples.Count;
            int max = 60;
            int min = 0;
            foreach (var s in _samples)
            {
                max = Mathf.Max(max, s.Fps);
                min = Mathf.Min(min, s.Fps);
            }

            if (count >= 2)
            {
                float range = max - min == 0 ? 60 : max - min;
                float step = GraphWidth / (float)(count - 1);
                Vector2 prev = default;

                for (int i = 0; i < count; i++)
                {
                    var point = new Vector2(i * step, (_samples[i].Fps - min) / range * GraphHeight);
                    if (i > 0)
                        DrawLine(prev, point);
                    prev = point;
                }
            }

            _graphTex.SetPixels32(_graphPixels);
            _graphTex.Apply(false);
            _graphMax.text = max.ToString();
            _graphMin.text = min.ToString();
        }

        private void DrawLine(Vector2 a, Vector2 b)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))) + 1;

            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                for (int dy = 0; dy < 2; dy++)
                {
                    int x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, GraphWidth - 1);
                    int y = Mathf.Clamp(Mathf.RoundToInt(p.y) + dy - 1, 0, GraphHeight - 1);
                    _graphPixels[y * GraphWidth + x] = GraphLine;
                }
            }
        }

        private static Color32 Blend(Color32 under, Color32 over)
        {
            float a = over.a / 255f;
            return new Color32((byte)(under.r + (over.r - under.r) * a), (byte)(under.g + (over.g - under.g) * a),
                               (byte)(under.b + (over.b - under.b) * a), (byte)Mathf.Max(under.a, over.a));
        }

        private void OnDestroy()
        {
            Unbind();

            if (_graphTex != null)
                Destroy(_graphTex);
        }

        /// <summary>화면 캔버스 + 창 — 처음 켤 때 한 번만 만든다. 기준 해상도는 게임 UI 와 같다 (1920×1080).</summary>
        private void EnsureCanvas()
        {
            if (_canvas != null)
                return;

            var go = new GameObject("PerfOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // 창 — 왼쪽 위 기준 자리 · 내용에 맞춘 크기
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(go.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(StartPosition.x, -StartPosition.y);
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelGo.AddComponent<FrontWarsRoundedImage>().Radius = 8f;
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            panelGo.AddComponent<Drag>().Target = _panel;

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fit = panelGo.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _text = NewText(_panel, "Rows", RowFont, TextAlignmentOptions.TopLeft);
            _text.richText = true;
            var textLayout = _text.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = Mathf.Min(PanelMaxWidth - PanelPadding * 2f, GraphWidth + 40f);

            // 그래프
            var graphGo = new GameObject("Graph", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            graphGo.transform.SetParent(_panel, false);
            _graph = graphGo.GetComponent<RawImage>();
            _graph.raycastTarget = false;
            var graphLayout = graphGo.GetComponent<LayoutElement>();
            graphLayout.preferredWidth = GraphWidth;
            graphLayout.preferredHeight = GraphHeight;
            _graphTex = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _graphPixels = new Color32[GraphWidth * GraphHeight];
            _graph.texture = _graphTex;
            _graphMax = GraphLabel(graphGo.transform, "Max", new Vector2(1f, 1f));
            _graphMin = GraphLabel(graphGo.transform, "Min", new Vector2(1f, 0f));

            // 닫기 × — 오른쪽 위 24×24
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            closeGo.transform.SetParent(_panel, false);
            closeGo.GetComponent<LayoutElement>().ignoreLayout = true;
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(CloseSize, CloseSize);
            closeRect.anchoredPosition = new Vector2(-8f, -8f);
            closeGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            TextMeshProUGUI x = NewText(closeRect, "Label", 18f, TextAlignmentOptions.Center);
            x.text = "×";
            x.color = new Color32(156, 163, 175, 255);
            RectTransform xr = x.rectTransform;
            xr.anchorMin = Vector2.zero;
            xr.anchorMax = Vector2.one;
            xr.offsetMin = xr.offsetMax = Vector2.zero;
            closeGo.GetComponent<Button>().onClick.AddListener(() => OnCloseRequested?.Invoke());

            _canvas.gameObject.SetActive(_visible);
        }

        private static TextMeshProUGUI NewText(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = FrontWarsFonts.Korean;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static TextMeshProUGUI GraphLabel(Transform parent, string name, Vector2 corner)
        {
            TextMeshProUGUI label = NewText(parent, name, 10f, corner.y > 0.5f ? TextAlignmentOptions.TopRight : TextAlignmentOptions.BottomRight);
            label.color = new Color(1f, 1f, 1f, 0.7f);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = new Vector2(60f, 12f);
            rect.anchoredPosition = new Vector2(-2f, corner.y > 0.5f ? -1f : 2f);
            return label;
        }

        /// <summary>창 끌기 — 누른 자리와 창 모서리 차이를 유지한다.</summary>
        private sealed class Drag : MonoBehaviour, IDragHandler
        {
            public RectTransform Target;

            public void OnDrag(PointerEventData eventData)
            {
                var canvas = Target.GetComponentInParent<Canvas>();
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                Target.anchoredPosition += eventData.delta / scale;
            }
        }
    }
}
