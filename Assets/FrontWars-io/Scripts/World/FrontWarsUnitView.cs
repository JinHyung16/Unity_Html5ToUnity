using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>건물 아이콘을 그리는 방식 — 확대 배율과 「건물 그림」 설정이 고른다 `[소스] BMmXchgl.js:let r=t<=.7?0:t<=5||!n?t>=.8?1:2:3`.</summary>
    public enum EFrontWarsStructureDraw
    {
        /// <summary>모드 0 — zoom ≤ 0.7. 도형만 기본크기/2.5 로, 불투명.</summary>
        Dot,

        /// <summary>모드 2 — 0.7 &lt; zoom &lt; 0.8. 기본 크기 도형만, 기호 없음.</summary>
        Shape,

        /// <summary>모드 1 — 0.8 ≤ zoom ≤ 5 이거나 건물 그림 설정이 꺼짐. 도형 + 기호 + 레벨.</summary>
        Symbol,

        /// <summary>모드 3 — zoom &gt; 5 이고 건물 그림 설정 켬. 레벨 숫자만 화면에 쓰고 그림은 월드 겹이 그린다.</summary>
        World
    }

    /// <summary>
    /// 지도 위 유닛 · 건물 그리기 — 유닛 갱신 알림을 받아 바뀐 것만 고친다.
    ///
    /// <para>
    /// 건물은 두 겹이다. 화면 겹(도형 · 기호 · 레벨)은 <b>화면 px</b> 라 확대해도 크기가 식으로 정해지고,
    /// 월드 겹(그림)은 <b>월드 단위</b> 라 확대하면 같이 커진다 `[소스] BMmXchgl.js:if(t&lt;=5||!this._structureSprites)return`.
    /// 화면 px 를 월드로 바꿀 때는 zoom(칸 하나의 화면 px)으로 나눈다.
    /// </para>
    ///
    /// <para>움직이는 유닛은 원본 그림 1px = 칸 1개라 월드 단위로 고정 크기다 `[소스] BMmXchgl.js:t.drawImage(c,…,c.width,c.width)`.</para>
    /// </summary>
    public sealed class FrontWarsUnitView : MonoBehaviour
    {
        /// <summary>월드 그림 겹 — 32×32 그림의 절반 `[소스] BMmXchgl.js:t.drawW=o.width&gt;&gt;1`.</summary>
        private const float WorldPictureSize = 16f;

        /// <summary>월드 그림 · 모드 3 화면 겹을 올리는 높이 (칸) `[소스] BMmXchgl.js:t.drawY=t.tileY-(t.drawH&gt;&gt;1)-4` · `this._shiftY=a?e*4:0`.</summary>
        private const float WorldPictureRise = 4f;

        /// <summary>도형 채움 = 영토색 명도 +13 `[소스] BMmXchgl.js:r.territoryColor().lighten(.13)`.</summary>
        private const double FillLighten = 0.13;

        /// <summary>도형 채움 투명도 — 모드 1 · 2 `[소스] BMmXchgl.js:.alpha(s?.65:1)`.</summary>
        private const float FillAlpha = 0.65f;

        /// <summary>짓는 중 채움 `[소스] BMmXchgl.js:rgb(120, 144, 156) 투명도 .8`.</summary>
        private static readonly Color32 ConstructionFill = new Color32(120, 144, 156, 255);
        private const float ConstructionAlpha = 0.8f;

        /// <summary>도형 안 기호 색 `[소스] BMmXchgl.js:rgb(42, 42, 42)`.</summary>
        private static readonly Color32 SymbolColor = new Color32(42, 42, 42, 255);

        /// <summary>목표를 잡은 군함 `[소스] BMmXchgl.js:hs=O({r:190,b:0,g:0})`.</summary>
        private static readonly Color32 WarshipTargetColor = new Color32(190, 0, 0, 255);

        /// <summary>공격 못 하는(targetable 아님) 유닛 투명도 `[소스] BMmXchgl.js:공격 불가 유닛 0.5`.</summary>
        private const float NonTargetableAlpha = 0.5f;

        /// <summary>레벨 숫자 대체 글꼴 크기 = 16 × 배율 px `[소스] BMmXchgl.js:bold (16×배율)px monospace`.</summary>
        private const float LevelFontPx = 16f;

        /// <summary>모드 1 레벨 배율 `[소스] BMmXchgl.js:r=Math.max(.55,o*.72)`.</summary>
        private const float LevelScaleMin = 0.55f;
        private const float LevelScaleFactor = 0.72f;

        /// <summary>모드 3 레벨 배율 `[소스] BMmXchgl.js:let r=t.pad,i=Math.max(.75,n/3)`.</summary>
        private const float WorldLevelScaleMin = 0.75f;
        private const float WorldLevelZoomDivisor = 3f;

        /// <summary>레벨 숫자를 도형 위로 띄우는 여백 (배율 곱하기 전) `[소스] BMmXchgl.js:한변/2 + 2×배율`.</summary>
        private const float LevelGap = 2f;

        /// <summary>TMP 3D 글자 크기 → 월드 높이 비 (fontSize 10 ≈ 1 단위) — 이름표와 같은 약속이다.</summary>
        private const float TmpSizePerWorldUnit = 10f;

        private const int StructureOrder = 10;
        private const int SymbolOrder = 11;
        private const int LevelOrder = 12;
        private const int PictureOrder = 13;
        private const int ShipOrder = 20;
        private const int ProjectileOrder = 30;

        /// <summary>유닛 하나가 쓰는 그리기 조각들 — 건물만 기호 · 그림 · 레벨을 더 쓴다.</summary>
        private sealed class View
        {
            public FrontWarsUnit Unit;
            public SpriteRenderer Body;
            public SpriteRenderer Symbol;
            public SpriteRenderer Picture;

            /// <summary>포탄 직전 칸 점 `[소스] BMmXchgl.js:지금 칸 + 직전 칸 점 2개`.</summary>
            public SpriteRenderer Trail;

            public TextMeshPro Level;
            public bool IsStructure;
            public FrontWarsIconFactory.EStructureShape Shape;

            /// <summary>그림에 쓴 종류 — 짓는 중이면 무엇을 짓는지다 (정해지면 도형 · 기호가 바뀐다).</summary>
            public EFrontWarsUnit Shown;
        }

        private FrontWarsGame _game;
        private FrontWarsMapRenderer _renderer;
        private FrontWarsCameraController _camera;
        private readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly Stack<TextMeshPro> _textPool = new Stack<TextMeshPro>();
        private readonly Dictionary<int, FrontWarsUnit> _dirty = new Dictionary<int, FrontWarsUnit>();
        private bool _showStructures = true;
        private float _lastZoom = -1f;

        /// <summary>
        /// 설정 「건물 그림」 — 끄면 확대해도 월드 그림 겹을 안 그리고 화면 아이콘(도형 + 기호)으로 남는다
        /// `[소스] BMmXchgl.js:t&lt;=5||!n` · `structureSprites(){return this.get(\`settings.structureSprites\`,!0)}` (기본 켬).
        /// <para>⚠ 건물을 숨기는 설정이 아니다 — 끄면 오히려 아이콘이 계속 보인다.</para>
        /// </summary>
        public bool ShowStructures
        {
            get { return _showStructures; }
            set
            {
                if (_showStructures == value)
                    return;

                _showStructures = value;
                LayoutStructures();
            }
        }

        /// <summary>
        /// 확대 배율을 읽을 카메라 — 건물 화면 겹이 zoom 을 쓴다.
        /// <para>안 넣으면 같은 월드 묶음(<see cref="FrontWarsWorldView"/>)에서 찾는다.</para>
        /// </summary>
        public FrontWarsCameraController Camera
        {
            get
            {
                if (_camera == null)
                {
                    FrontWarsWorldView world = GetComponentInParent<FrontWarsWorldView>();
                    _camera = world != null ? world.CameraController : null;
                }

                return _camera;
            }
            set { _camera = value; }
        }

        public void Bind(FrontWarsGame game, FrontWarsMapRenderer renderer)
        {
            Unbind();
            _game = game;
            _renderer = renderer;
            _game.OnUnitUpdate += HandleUnitUpdate;
        }

        public void Unbind()
        {
            if (_game != null)
                _game.OnUnitUpdate -= HandleUnitUpdate;

            foreach (View view in _views.Values)
                Release(view);

            _views.Clear();
            _dirty.Clear();
            _game = null;
        }

        private void HandleUnitUpdate(FrontWarsUnit unit)
        {
            _dirty[unit.Id] = unit;
        }

        private void LateUpdate()
        {
            if (_game == null)
                return;

            if (_dirty.Count > 0)
            {
                foreach (FrontWarsUnit unit in _dirty.Values)
                    Apply(unit);

                _dirty.Clear();
            }

            float zoom = Camera != null ? Camera.Zoom : FrontWarsCameraController.StartZoom;

            // 건물 화면 겹은 화면 px 라 확대가 바뀔 때마다 월드 크기를 다시 잡는다
            if (Mathf.Approximately(zoom, _lastZoom) == false)
            {
                _lastZoom = zoom;
                LayoutStructures();
            }
        }

        private void Apply(FrontWarsUnit unit)
        {
            if (unit.IsActive == false)
            {
                if (_views.TryGetValue(unit.Id, out View dead))
                {
                    Release(dead);
                    _views.Remove(unit.Id);
                }

                return;
            }

            if (_views.TryGetValue(unit.Id, out View view) == false)
            {
                view = Create(unit);
                _views[unit.Id] = view;
            }

            view.Unit = unit;

            // 짓는 중이던 것이 무엇을 짓는지 정해지면 도형 · 기호가 바뀐다
            if (view.IsStructure && view.Shown != ShownType(unit))
                Dress(view);

            Paint(view);

            if (view.IsStructure)
                Layout(view, Camera != null ? Camera.Zoom : FrontWarsCameraController.StartZoom);
            else
                PlaceMoving(view);
        }

        // ── 만들기

        private View Create(FrontWarsUnit unit)
        {
            bool structure = IsStructure(unit.Type);

            var view = new View
            {
                Unit = unit,
                IsStructure = structure,
                Body = Acquire(structure ? StructureOrder : OrderOf(unit.Type)),
            };

            view.Body.gameObject.name = $"{unit.Type}#{unit.Id}";

            if (structure)
            {
                view.Symbol = Acquire(SymbolOrder);
                view.Picture = Acquire(PictureOrder);
                view.Level = AcquireText();
                Dress(view);
                return view;
            }

            view.Body.sprite = FrontWarsIconFactory.Get(FrontWarsIconFactory.ShapeOf(unit.Type));

            if (unit.Type == EFrontWarsUnit.Shell)
            {
                view.Trail = Acquire(ProjectileOrder);
                view.Trail.sprite = view.Body.sprite;
            }

            return view;
        }

        /// <summary>건물 도형 · 가운데 기호 · 월드 그림을 종류에 맞춰 입힌다.</summary>
        private static void Dress(View view)
        {
            view.Shown = ShownType(view.Unit);
            view.Shape = FrontWarsIconFactory.StructureShapeOf(view.Shown);
            view.Body.sprite = FrontWarsIconFactory.GetStructure(view.Shape);
            view.Symbol.sprite = FrontWarsIconFactory.Get(FrontWarsIconFactory.ShapeOf(view.Shown));
            view.Picture.sprite = view.Symbol.sprite;
        }

        /// <summary>짓는 중이면 무엇을 짓는지로 도형 · 기호를 고른다 (다 짓기 전에는 망치).</summary>
        private static EFrontWarsUnit ShownType(FrontWarsUnit unit)
        {
            if (unit.Type != EFrontWarsUnit.Construction)
                return unit.Type;

            return unit.ConstructionTypeValue ?? EFrontWarsUnit.Construction;
        }

        // ── 색

        private void Paint(View view)
        {
            FrontWarsUnit unit = view.Unit;
            Color32 territory = _renderer != null && _renderer.Colors != null
                ? _renderer.Colors.TerritoryColor(unit.Owner)
                : new Color32(255, 255, 255, 255);

            if (view.IsStructure)
            {
                Color32 fill = unit.Type == EFrontWarsUnit.Construction
                    ? ConstructionFill
                    : FrontWarsColorMath.DarkenRounded(territory, -FillLighten);

                view.Body.color = fill;
                view.Symbol.color = SymbolColor;
                view.Picture.color = Color.white;   // 월드 그림은 주인색으로 다시 칠하지 않는다
                view.Level.color = Color.white;
                return;
            }

            Color32 border = _renderer != null && _renderer.Colors != null
                ? _renderer.Colors.BorderColor(unit.Owner, false)
                : new Color32(255, 255, 255, 255);

            bool dot = unit.Type == EFrontWarsUnit.Shell || unit.Type == EFrontWarsUnit.MIRVWarhead;
            Color32 body = dot ? border : territory;

            if (unit.Type == EFrontWarsUnit.Warship && unit.TargetUnit != null)
                body = WarshipTargetColor;

            float alpha = unit.Targetable ? 1f : NonTargetableAlpha;
            view.Body.color = new Color(body.r / 255f, body.g / 255f, body.b / 255f, alpha);

            if (view.Trail != null)
                view.Trail.color = view.Body.color;
        }

        // ── 자리 · 크기

        private void LayoutStructures()
        {
            float zoom = Camera != null ? Camera.Zoom : FrontWarsCameraController.StartZoom;

            foreach (View view in _views.Values)
            {
                if (view.IsStructure)
                    Layout(view, zoom);
            }
        }

        /// <summary>확대 배율과 설정으로 그리기 방식을 고른다 `[소스] BMmXchgl.js:let r=t&lt;=.7?0:t&lt;=5||!n?t&gt;=.8?1:2:3`.</summary>
        public static EFrontWarsStructureDraw DrawModeOf(float zoom, bool structureSprites)
        {
            if (zoom <= 0.7f)
                return EFrontWarsStructureDraw.Dot;

            if (zoom <= 5f || structureSprites == false)
                return zoom >= 0.8f ? EFrontWarsStructureDraw.Symbol : EFrontWarsStructureDraw.Shape;

            return EFrontWarsStructureDraw.World;
        }

        private void Layout(View view, float zoom)
        {
            EFrontWarsStructureDraw mode = DrawModeOf(zoom, _showStructures);
            Vector3 center = FrontWarsWorld.TileCenter(_game.Map, view.Unit.Tile);
            float basePx = FrontWarsIconFactory.StructureBasePx(view.Shape);
            bool world = mode == EFrontWarsStructureDraw.World;

            // 모드 3 은 화면 겹을 월드 그림과 같은 높이로 올린다
            Vector3 anchor = center + new Vector3(0f, world ? WorldPictureRise : 0f, 0f);

            float sidePx = mode == EFrontWarsStructureDraw.Dot
                ? Mathf.Ceil(basePx / FrontWarsIconFactory.StructureDotDivisor)
                : FrontWarsIconFactory.StructureScreenPx(zoom);
            float side = sidePx / zoom;

            view.Body.enabled = world == false;
            view.Body.transform.localPosition = anchor + new Vector3(0f, 0f, -0.1f);
            view.Body.transform.localScale = new Vector3(side, side, 1f);

            Color bodyColor = view.Body.color;
            bodyColor.a = view.Unit.Type == EFrontWarsUnit.Construction ? ConstructionAlpha
                : mode == EFrontWarsStructureDraw.Dot ? 1f : FillAlpha;
            view.Body.color = bodyColor;

            // 기호 — 모드 1 에서만. 도형 안 크기 · 치우침은 원본 캔버스 px 비율 그대로
            bool symbol = mode == EFrontWarsStructureDraw.Symbol;
            view.Symbol.enabled = symbol;

            if (symbol)
            {
                float ratio = side / basePx;
                Vector2 offset = FrontWarsIconFactory.StructureSymbolOffsetPx(view.Shape);
                float symbolSide = FrontWarsIconFactory.StructureSymbolPx * ratio;
                view.Symbol.transform.localPosition = anchor + new Vector3(offset.x * ratio, -offset.y * ratio, -0.11f);
                view.Symbol.transform.localScale = new Vector3(symbolSide, symbolSide, 1f);
            }

            // 월드 그림 겹 — 확대가 5 를 넘고 건물 그림 설정이 켜졌을 때만
            view.Picture.enabled = world;

            if (world)
            {
                view.Picture.transform.localPosition = center + new Vector3(0f, WorldPictureRise, -0.12f);
                view.Picture.transform.localScale = new Vector3(WorldPictureSize, WorldPictureSize, 1f);
            }

            LayoutLevel(view, mode, zoom, anchor, sidePx);
        }

        private void LayoutLevel(View view, EFrontWarsStructureDraw mode, float zoom, Vector3 anchor, float sidePx)
        {
            bool show = view.Unit.Level > 1 && (mode == EFrontWarsStructureDraw.Symbol || mode == EFrontWarsStructureDraw.World);
            view.Level.enabled = show;

            if (show == false)
                return;

            float scale;
            float risePx;

            if (mode == EFrontWarsStructureDraw.World)
            {
                // 모드 3 — 배율 max(0.75, zoom/3), 월드 그림 위에 얹는다 [추론: 원본 pad 값을 못 읽어 그림 한 변을 쓴다]
                scale = Mathf.Max(WorldLevelScaleMin, zoom / WorldLevelZoomDivisor);
                risePx = WorldPictureSize * zoom * scale / 2f + LevelGap * scale;
            }
            else
            {
                scale = Mathf.Max(LevelScaleMin, sidePx / FrontWarsIconFactory.StructureBasePx(view.Shape) * LevelScaleFactor);
                risePx = sidePx / 2f + LevelGap * scale;
            }

            float fontWorld = LevelFontPx * scale / zoom;
            view.Level.fontSize = fontWorld * TmpSizePerWorldUnit;
            view.Level.text = view.Unit.Level.ToString();
            view.Level.transform.localPosition = anchor + new Vector3(0f, risePx / zoom, -0.13f);
        }

        private void PlaceMoving(View view)
        {
            float size = MovingSize(view.Unit.Type);
            Vector3 center = FrontWarsWorld.TileCenter(_game.Map, view.Unit.Tile);
            view.Body.transform.localScale = new Vector3(size, size, 1f);
            view.Body.transform.localPosition = center + new Vector3(0f, 0f, -0.1f);

            if (view.Trail == null)
                return;

            view.Trail.transform.localScale = new Vector3(size, size, 1f);
            view.Trail.transform.localPosition = FrontWarsWorld.TileCenter(_game.Map, view.Unit.LastTile) + new Vector3(0f, 0f, -0.1f);
        }

        /// <summary>건물인가 — 화면 px 로 그리는 두 겹짜리.</summary>
        private static bool IsStructure(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.City:
                case EFrontWarsUnit.Port:
                case EFrontWarsUnit.Factory:
                case EFrontWarsUnit.DefensePost:
                case EFrontWarsUnit.MissileSilo:
                case EFrontWarsUnit.SAMLauncher:
                case EFrontWarsUnit.Construction:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 움직이는 유닛 그림 한 변 (칸) `[소스] BMmXchgl.js:gn={[D.TransportShip]:pn,…}` 의 PNG 폭.
        /// </summary>
        public static float MovingSize(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.Warship: return 11f;
                case EFrontWarsUnit.AtomBomb: return 9f;
                case EFrontWarsUnit.HydrogenBomb: return 13f;
                case EFrontWarsUnit.MIRV: return 19f;
                case EFrontWarsUnit.MIRVWarhead: return 1f;
                case EFrontWarsUnit.Shell: return 1f;
                default: return 5f;   // 수송선 · 무역선 · SAM 미사일 · 기차
            }
        }

        private static int OrderOf(EFrontWarsUnit type)
        {
            return type == EFrontWarsUnit.Shell || type == EFrontWarsUnit.MIRVWarhead || type == EFrontWarsUnit.SAMMissile
                ? ProjectileOrder
                : ShipOrder;
        }

        // ── 풀

        private SpriteRenderer Acquire(int order)
        {
            SpriteRenderer view;

            if (_pool.Count > 0)
            {
                view = _pool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Unit");
                go.transform.SetParent(transform, false);
                view = go.AddComponent<SpriteRenderer>();
            }

            view.enabled = true;
            view.sortingOrder = order;
            return view;
        }

        private TextMeshPro AcquireText()
        {
            if (_textPool.Count > 0)
            {
                TextMeshPro pooled = _textPool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = new GameObject("Level");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = FrontWarsFonts.Korean;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = LevelOrder;
            return text;
        }

        private void Release(View view)
        {
            Release(view.Body);
            Release(view.Symbol);
            Release(view.Picture);
            Release(view.Trail);

            if (view.Level != null)
            {
                view.Level.gameObject.SetActive(false);
                _textPool.Push(view.Level);
            }
        }

        private void Release(SpriteRenderer view)
        {
            if (view == null)
                return;

            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
