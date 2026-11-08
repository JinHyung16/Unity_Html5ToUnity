using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지도 입력 — 마우스 · 키보드 `[소스] BMmXchgl.js:Nh=class e{static{this.PAN_SPEED=6}…CLICK_THRESHOLD_PIXELS=10 · Lh … handleMouseClick(e){`.
    ///
    /// <list type="bullet">
    /// <item>왼쪽 누르고 10px 안에서 떼면 클릭, 넘으면 드래그로 지도를 민다. 휠은 커서 기준 줌.</item>
    /// <item>클릭: 스폰 단계면 빈 땅에 스폰, 그 뒤에는 칠 수 있으면 공격 · 아니면 100칸 안이면 배 공격(200ms 연타 방지).</item>
    /// <item>Ctrl 클릭 = 원형 메뉴(원본 건설 메뉴 자리) · 휠 클릭 = 가까운 건물 업그레이드 · Alt 클릭 = 그 나라에게 이모지 · 오른쪽 클릭 = 원형 메뉴.</item>
    /// <item>키: WASD/방향키 이동(프레임마다 6px) · Q/E(−/=) 줌(프레임마다 11) · Shift 누르는 동안 멈춤 · T/Y 공격 비율 ∓10 · B 배 공격 · G 땅 공격 · C 내 나라로 · H UI 숨기기 · 1~0 건설 · Space 누르는 동안 전술 보기 · Esc 취소.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsMapInput : MonoBehaviour
    {
        public const float PanSpeed = 6f;
        public const float ZoomSpeed = 11f;
        public const float ClickThresholdPixels = 10f;
        public const double BoatAttackDebounceMs = 200;

        /// <summary>배 공격을 허락하는 거리 (칸) `[소스] this.isBoatAttackAvailable(t,n,100)`.</summary>
        public const int BoatAttackRange = 100;

        /// <summary>원본 rAF 기준 프레임 시간 (ms) — 키 이동 · 줌을 프레임률과 떼어 놓는다.</summary>
        private const float ReferenceFrameMs = 1000f / 60f;

        /// <summary>유니티 휠 한 칸 값 (윈도 120) — 브라우저 100 으로 맞춘다.</summary>
        private const float UnityWheelNotch = 120f;

        /// <summary>공격 비율 키 한 번 변화 (%) `[source] new Se(-10) · new Se(10)`.</summary>
        public const int AttackRatioStep = 10;

        /// <summary>휠 클릭을 누르고 있으면 이만큼 뒤부터 업그레이드를 되풀이한다 `[소스] MIDDLE_MOUSE_MACRO_DELAY_MS=300`.</summary>
        public const double MiddleMouseMacroDelayMs = 300;

        private bool _middleDown;
        private double _middleDownMs;

        private FrontWarsGameManager _manager;
        private FrontWarsCameraController _camera;

        private bool _pointerDown;
        private Vector2 _pointerDownPosition;
        private Vector2 _lastPointerPosition;
        private double _lastBoatAttackMs = double.NegativeInfinity;

        /// <summary>공격 비율 0~1 (기본 0.2 `[소스] settings.attackRatio`).</summary>
        public float AttackRatio { get; private set; } = 0.2f;

        public float SendTroopsRatio { get; set; } = 0.1f;
        public float SendGoldRatio { get; set; } = 0.1f;

        /// <summary>건설 준비 중인 종류. null = 없음.</summary>
        public EFrontWarsUnit? PlacementUnit { get; private set; }

        public bool UiHidden { get; private set; }

        /// <summary>설정 「좌클릭으로 메뉴 열기」 — 켜면 왼쪽 클릭이 공격 대신 원형 메뉴를 연다 (건설 놓는 중엔 건설이 먼저다).</summary>
        public bool LeftClickOpensMenu { get; set; }

        public event Action<float> OnAttackRatioChanged;
        public event Action<EFrontWarsUnit?> OnPlacementChanged;
        public event Action<bool> OnUiHiddenChanged;
        public event Action<bool> OnAlternateView;

        /// <summary>오른쪽 클릭 — 화면 좌표(왼쪽 위 기준)와 칸.</summary>
        public event Action<Vector2, int> OnContextMenu;

        /// <summary>Alt 클릭 — 이모지를 보낼 나라.</summary>
        public event Action<FrontWarsPlayer> OnEmojiRequested;

        /// <summary>커서 아래 나라가 바뀌었다 (null = 없음).</summary>
        public event Action<FrontWarsPlayer> OnFocusedPlayer;

        /// <summary>Esc — 열린 메뉴 닫기.</summary>
        public event Action OnCancel;

        public void Bind(FrontWarsGameManager manager, FrontWarsCameraController camera)
        {
            _manager = manager;
            _camera = camera;
        }

        public void SetAttackRatio(float ratio)
        {
            AttackRatio = Mathf.Clamp01(ratio);
            OnAttackRatioChanged?.Invoke(AttackRatio);
        }

        public void SetPlacement(EFrontWarsUnit? unit)
        {
            PlacementUnit = unit;
            OnPlacementChanged?.Invoke(unit);
        }

        public void ToggleUi()
        {
            UiHidden = UiHidden == false;
            OnUiHiddenChanged?.Invoke(UiHidden);
        }

        private FrontWarsGame Game => _manager?.Game;

        private void Update()
        {
            if (Game == null || _camera == null)
                return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse != null)
                HandleMouse(mouse, keyboard);

            if (keyboard != null)
                HandleKeyboard(keyboard);
        }

        // ── 마우스

        private static Vector2 TopLeft(Vector2 unityScreen)
        {
            return new Vector2(unityScreen.x, Screen.height - unityScreen.y);
        }

        private bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void HandleMouse(Mouse mouse, Keyboard keyboard)
        {
            Vector2 position = TopLeft(mouse.position.ReadValue());

            bool shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Approximately(scroll, 0f) == false && PointerOverUi() == false)
            {
                // Shift + 휠 = 공격 비율 ±10, 줌은 안 한다 `[소스] handleShiftWheelScroll(e){if(e.shiftKey){let t=(e.deltaY===0?e.deltaX:e.deltaY)>0?-10:10`
                // 유니티 휠 위(+)가 브라우저 deltaY 음수 — 위로 굴리면 +10
                if (shiftHeld)
                    SetAttackRatio(AttackRatio + (scroll > 0f ? AttackRatioStep : -AttackRatioStep) / 100f);
                else
                    _camera.HandleZoom(position, -scroll / UnityWheelNotch * FrontWarsCameraController.BrowserWheelNotch);
            }

            // 휠 클릭 = 가까운 건물 업그레이드. 누르고 있으면 300ms 뒤부터 되풀이한다 `[소스] startMiddleMouseMacro(t){`
            // ⚠ 원본 반복 간격(middleMouseMacroFrequencyMs)은 기본 0 이고 바꾸는 곳이 없다 — 우리는 프레임마다 한 번
            if (mouse.middleButton.wasPressedThisFrame && PointerOverUi() == false)
            {
                _middleDown = true;
                _middleDownMs = Time.realtimeSinceStartupAsDouble * 1000;
                UpgradeNearestStructure(TileUnder(position));
            }
            else if (_middleDown && mouse.middleButton.isPressed)
            {
                if (Time.realtimeSinceStartupAsDouble * 1000 - _middleDownMs >= MiddleMouseMacroDelayMs)
                    UpgradeNearestStructure(TileUnder(position));
            }

            if (mouse.middleButton.wasReleasedThisFrame)
                _middleDown = false;

            if (mouse.leftButton.wasPressedThisFrame && PointerOverUi() == false)
            {
                _pointerDown = true;
                _pointerDownPosition = position;
                _lastPointerPosition = position;
            }

            if (_pointerDown && mouse.leftButton.isPressed)
            {
                Vector2 delta = position - _lastPointerPosition;
                if (delta != Vector2.zero)
                    _camera.HandleDrag(delta.x, delta.y);

                _lastPointerPosition = position;
            }

            if (_pointerDown && mouse.leftButton.wasReleasedThisFrame)
            {
                _pointerDown = false;
                bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
                bool alt = keyboard != null && keyboard.altKey.isPressed;

                if (Mathf.Abs(position.x - _pointerDownPosition.x) + Mathf.Abs(position.y - _pointerDownPosition.y) < ClickThresholdPixels)
                {
                    // Ctrl+클릭 = 보조 동작 `[소스] this.isModifierKeyActive(e)){this.eventBus.emit(new ai(` — 원본은 건설 메뉴, 우리 건설 메뉴는 원형 메뉴 「건설」 조각이라 원형 메뉴를 연다
                    // 설정 「좌클릭으로 메뉴 열기」 를 켜면 그냥 눌러도 원형 메뉴가 뜬다. 그때 Shift + 클릭이 공격이다 `[소스] !ot.leftClickOpensMenu()||t.shiftKey`
                    if (ctrl || (LeftClickOpensMenu && shiftHeld == false && PlacementUnit.HasValue == false))
                        OnContextMenu?.Invoke(position, TileUnder(position));
                    else if (alt)
                        RequestEmoji(TileUnder(position));
                    else
                        HandleClick(TileUnder(position));
                }
            }

            if (mouse.rightButton.wasReleasedThisFrame && PointerOverUi() == false)
                OnContextMenu?.Invoke(position, TileUnder(position));

            if (mouse.delta.ReadValue() != Vector2.zero)
                OnFocusedPlayer?.Invoke(PlayerAt(TileUnder(position)));
        }

        private int TileUnder(Vector2 topLeft)
        {
            Vector2Int t = _camera.ScreenToTile(topLeft);
            return Game.Map.IsValidCoord(t.x, t.y) ? Game.Map.Ref(t.x, t.y) : -1;
        }

        /// <summary>땅이면 주인, 물이면 50칸 안 가장 가까운 배의 주인 `[소스] getPlayerFromTile(e){`.</summary>
        private FrontWarsPlayer PlayerAt(int tile)
        {
            if (tile < 0)
                return null;

            if (Game.Map.IsLand(tile))
                return Game.Owner(tile);

            FrontWarsUnit nearest = null;
            int best = int.MaxValue;

            foreach (var (unit, dist) in Game.Units.NearbyUnits(tile, 50, new[] { EFrontWarsUnit.Warship, EFrontWarsUnit.TradeShip, EFrontWarsUnit.TransportShip }))
            {
                if (dist < best)
                {
                    best = dist;
                    nearest = unit;
                }
            }

            return nearest?.Owner;
        }

        /// <summary>
        /// 클릭 `[소스] handleMouseClick(e){`. 건설 준비 중이면 건설, 스폰 단계면 스폰, 아니면 공격 · 배 공격.
        /// </summary>
        public void HandleClick(int tile)
        {
            if (tile < 0)
                return;

            if (PlacementUnit.HasValue)
            {
                PlaceStructure(PlacementUnit.Value, tile);
                return;
            }

            FrontWarsGame game = Game;

            if (game.InSpawnPhase)
            {
                if (game.Settings.RandomSpawn == false && game.Map.IsLand(tile) && game.HasOwner(tile) == false)
                    _manager.Submit(new FrontWarsSpawnIntent { Tile = tile });

                return;
            }

            FrontWarsPlayer me = _manager.LocalPlayer;
            if (me == null)
                return;

            string targetId = game.Owner(tile)?.Id;
            double troops = Math.Floor(me.Troops * AttackRatio);

            if (me.CanAttack(tile))
            {
                _manager.Submit(new FrontWarsAttackIntent { TargetId = targetId, Troops = troops });
                return;
            }

            if (IsBoatAttackAvailable(me, tile, BoatAttackRange))
                ExecuteBoatAttack(me, tile, targetId, troops);
        }

        private bool IsBoatAttackAvailable(FrontWarsPlayer me, int tile, int range)
        {
            if (Game.Map.IsLand(tile) == false)
                return false;

            int spawn = me.CanBuild(EFrontWarsUnit.TransportShip, tile);
            if (spawn == FrontWarsPlayer.NoTile)
                return false;

            return range <= 0 || Game.Map.EuclideanDistanceSquared(tile, spawn) <= range * range;
        }

        private void ExecuteBoatAttack(FrontWarsPlayer me, int tile, string targetId, double troops)
        {
            double now = Time.realtimeSinceStartupAsDouble * 1000;
            if (now - _lastBoatAttackMs < BoatAttackDebounceMs)
                return;

            int source = FrontWarsTransportShipRules.BestTransportShipSpawn(Game, me, tile);
            _lastBoatAttackMs = now;
            _manager.Submit(new FrontWarsBoatIntent { TargetId = targetId, Destination = tile, Troops = troops, Source = source });
        }

        /// <summary>커서 칸 근처 건물 중 올릴 수 있는 가장 가까운 것 `[소스] upgradeNearestStructure(e){`.</summary>
        public void UpgradeNearestStructure(int tile)
        {
            FrontWarsPlayer me = _manager.LocalPlayer;

            if (tile < 0 || me == null || Game.InSpawnPhase)
                return;

            int bestId = -1;
            int bestDistance = int.MaxValue;

            foreach (FrontWarsBuildable b in me.BuildableUnits(tile))
            {
                if (b.UpgradeUnitId < 0)
                    continue;

                FrontWarsUnit unit = me.AllUnits is System.Collections.Generic.IReadOnlyList<FrontWarsUnit> list ? FindUnit(list, b.UpgradeUnitId) : null;
                if (unit == null)
                    continue;

                int d = Game.Map.ManhattanDistance(tile, unit.Tile);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestId = b.UpgradeUnitId;
                }
            }

            if (bestId >= 0)
                _manager.Submit(new FrontWarsUpgradeStructureIntent { UnitId = bestId });
        }

        private static FrontWarsUnit FindUnit(System.Collections.Generic.IReadOnlyList<FrontWarsUnit> units, int id)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Id == id)
                    return units[i];
            }

            return null;
        }

        /// <summary>건설 준비 중 클릭 — 올릴 수 있으면 업그레이드, 지을 수 있으면 건설 명령.</summary>
        private void PlaceStructure(EFrontWarsUnit type, int tile)
        {
            FrontWarsPlayer me = _manager.LocalPlayer;

            if (me == null || Game.InSpawnPhase)
                return;

            foreach (FrontWarsBuildable b in me.BuildableUnits(tile))
            {
                if (b.Type != type)
                    continue;

                if (b.UpgradeUnitId >= 0)
                    _manager.Submit(new FrontWarsUpgradeStructureIntent { UnitId = b.UpgradeUnitId });
                else if (b.BuildTile != FrontWarsPlayer.NoTile)
                    _manager.Submit(new FrontWarsBuildUnitIntent { Unit = type, Tile = tile });

                break;
            }

            SetPlacement(null);
        }

        private void RequestEmoji(int tile)
        {
            if (tile < 0 || Game.HasOwner(tile) == false)
                return;

            OnEmojiRequested?.Invoke(Game.Owner(tile));
        }

        // ── 키보드

        private void HandleKeyboard(Keyboard k)
        {
            bool shift = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            float frames = Time.unscaledDeltaTime * 1000f / ReferenceFrameMs;

            if (shift == false)
            {
                float dx = 0f, dy = 0f;

                if (k.wKey.isPressed || k.upArrowKey.isPressed) dy += PanSpeed;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) dy -= PanSpeed;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) dx += PanSpeed;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) dx -= PanSpeed;

                if (dx != 0f || dy != 0f)
                    _camera.HandleDrag(dx * frames, dy * frames);

                var center = new Vector2(Screen.width / 2f, Screen.height / 2f);

                if (k.qKey.isPressed || k.minusKey.isPressed)
                    _camera.HandleZoom(center, ZoomSpeed * frames);

                if (k.eKey.isPressed || k.equalsKey.isPressed)
                    _camera.HandleZoom(center, -ZoomSpeed * frames);
            }

            if (k.spaceKey.wasPressedThisFrame)
                OnAlternateView?.Invoke(true);

            if (k.spaceKey.wasReleasedThisFrame)
                OnAlternateView?.Invoke(false);

            if (k.escapeKey.wasPressedThisFrame)
            {
                SetPlacement(null);
                OnCancel?.Invoke();
            }

            if (k.bKey.wasReleasedThisFrame) BoatAttackAtCursor();
            if (k.gKey.wasReleasedThisFrame) GroundAttackAtCursor();
            if (k.tKey.wasReleasedThisFrame) SetAttackRatio(AttackRatio - AttackRatioStep / 100f);
            if (k.yKey.wasReleasedThisFrame) SetAttackRatio(AttackRatio + AttackRatioStep / 100f);
            if (k.cKey.wasReleasedThisFrame) CenterOnMe();
            if (k.hKey.wasReleasedThisFrame) ToggleUi();

            if (k.digit1Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.City);
            if (k.digit2Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.Factory);
            if (k.digit3Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.Port);
            if (k.digit4Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.DefensePost);
            if (k.digit5Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.MissileSilo);
            if (k.digit6Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.SAMLauncher);
            if (k.digit7Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.Warship);
            if (k.digit8Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.AtomBomb);
            if (k.digit9Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.HydrogenBomb);
            if (k.digit0Key.wasReleasedThisFrame) SetPlacement(EFrontWarsUnit.MIRV);
        }

        private int CursorTile()
        {
            if (Mouse.current == null || Game.InSpawnPhase)
                return -1;

            return TileUnder(TopLeft(Mouse.current.position.ReadValue()));
        }

        private void BoatAttackAtCursor()
        {
            int tile = CursorTile();
            FrontWarsPlayer me = _manager.LocalPlayer;

            if (tile < 0 || me == null || IsBoatAttackAvailable(me, tile, 0) == false)
                return;

            ExecuteBoatAttack(me, tile, Game.Owner(tile)?.Id, Math.Floor(me.Troops * AttackRatio));
        }

        private void GroundAttackAtCursor()
        {
            int tile = CursorTile();
            FrontWarsPlayer me = _manager.LocalPlayer;

            if (tile < 0 || me == null || me.CanAttack(tile) == false)
                return;

            _manager.Submit(new FrontWarsAttackIntent { TargetId = Game.Owner(tile)?.Id, Troops = Math.Floor(me.Troops * AttackRatio) });
        }

        /// <summary>내 나라 이름 자리로 줌 5 로 간다 `[소스] startCentering(){`.</summary>
        public void CenterOnMe()
        {
            FrontWarsPlayer me = _manager.LocalPlayer;

            if (me == null || me.IsAlive == false)
                return;

            FrontWarsNameLocation location = FrontWarsNamePlacement.Compute(Game, me);
            _camera.FocusOn(location.X, location.Y);
        }
    }
}
