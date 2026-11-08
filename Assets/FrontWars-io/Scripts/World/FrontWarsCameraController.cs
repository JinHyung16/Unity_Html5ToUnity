using System;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지도 카메라 — 원본 변환 그대로 `[소스] BMmXchgl.js:cs=class{constructor(e,t,n){this._viewRef=e…zoomLevel=1.7,this._panX=-340,this._panY=-190`.
    ///
    /// <para>
    /// 원본 화면 좌표: 화면x = (칸x − 맵폭/2 − panX) × zoom + 맵폭/2. zoom = 칸 하나의 화면 px.
    /// ⚠ 화면 기준점이 «화면 가운데»가 아니라 «맵 크기의 절반 px» 이다 — 원본이 그렇다.
    /// </para>
    ///
    /// <para>유니티는 직교 카메라로 같은 변환을 만든다: 크기 = 화면높이/2/zoom, 위치 = 화면 가운데 px 가 가리키는 칸 좌표.</para>
    /// </summary>
    public sealed class FrontWarsCameraController : MonoBehaviour
    {
        public const float StartZoom = 1.7f;
        public const float StartPanX = -340f;
        public const float StartPanY = -190f;
        public const float MinZoom = 0.2f;
        public const float MaxZoom = 20f;

        /// <summary>휠 한 값이 줌을 나누는 비 `[소스] n=1+e.delta/500`.</summary>
        private const float WheelDivisor = 500f;

        /// <summary>브라우저 휠 한 칸 deltaY — 유니티 한 칸 120 을 이 값으로 맞춘다.</summary>
        public const float BrowserWheelNotch = 100f;

        /// <summary>따라가기 타이머 간격(ms) · 한 번 최대 이동(px) · 감쇠 `[소스] as=16.6,os=14,ss=.02`.</summary>
        private const float AnimIntervalMs = 16.6f;
        private const float AnimMaxStep = 14f;
        private const float AnimDecay = 0.02f;
        private const float AnimStopDistance = 2f;
        private const float AnimZoomEpsilon = 0.01f;

        /// <summary>내 나라로 · 좌표로 갈 때 목표 줌 `[소스] this._destZoom=5`.</summary>
        public const float FocusZoom = 5f;

        [SerializeField] private Camera _camera;

        private FrontWarsTerrainMap _map;
        private Vector2? _destPoint;
        private float? _destZoom;
        private bool _movementOnly;
        private float _animElapsedMs;

        public float Zoom { get; private set; } = StartZoom;
        public float PanX { get; private set; } = StartPanX;
        public float PanY { get; private set; } = StartPanY;

        public Camera Camera => _camera;

        public void Bind(Camera camera, FrontWarsTerrainMap map)
        {
            _camera = camera;
            _map = map;
            SetView(StartPanX, StartPanY, StartZoom);
        }

        private float ViewWidth => _map != null ? _map.Width : 0f;
        private float ViewHeight => _map != null ? _map.Height : 0f;

        // ── 좌표

        /// <summary>
        /// 화면 px(왼쪽 위 기준, y 아래) → 칸 좌표 (floor) `[소스] toWorldSpace(e,t){`.
        /// </summary>
        public Vector2Int ScreenToTile(Vector2 screenTopLeft)
        {
            float x = (screenTopLeft.x - ViewWidth / 2f) / Zoom + PanX;
            float y = (screenTopLeft.y - ViewHeight / 2f) / Zoom + PanY;
            return new Vector2Int(Mathf.FloorToInt(x + ViewWidth / 2f), Mathf.FloorToInt(y + ViewHeight / 2f));
        }

        /// <summary>유니티 화면 좌표(왼쪽 아래 기준) → 칸 좌표.</summary>
        public Vector2Int UnityScreenToTile(Vector2 unityScreen)
        {
            return ScreenToTile(new Vector2(unityScreen.x, Screen.height - unityScreen.y));
        }

        /// <summary>칸 좌표 → 화면 px(왼쪽 위 기준) `[소스] toScreenSpace(e){`.</summary>
        public Vector2 TileToScreen(float tileX, float tileY)
        {
            float sx = (tileX - ViewWidth / 2f - PanX) * Zoom + ViewWidth / 2f;
            float sy = (tileY - ViewHeight / 2f - PanY) * Zoom + ViewHeight / 2f;
            return new Vector2(sx, sy);
        }

        /// <summary>화면 가운데가 가리키는 칸 좌표 (floor 로 자른 보이는 범위의 가운데) `[소스] getViewportCenter(){`.</summary>
        private Vector2 ViewportCenter()
        {
            float x0 = Mathf.Floor(-ViewWidth / 2f / Zoom + PanX + ViewWidth / 2f);
            float y0 = Mathf.Floor(-ViewHeight / 2f / Zoom + PanY + ViewHeight / 2f);
            float x1 = Mathf.Floor((Screen.width - ViewWidth / 2f) / Zoom + PanX + ViewWidth / 2f);
            float y1 = Mathf.Floor((Screen.height - ViewHeight / 2f) / Zoom + PanY + ViewHeight / 2f);
            return new Vector2(x0 + Mathf.Floor((x1 - x0) / 2f), y0 + Mathf.Floor((y1 - y0) / 2f));
        }

        // ── 입력

        /// <summary>휠 줌 — 커서 아래 칸이 제자리에 남는다 `[소스] handleZoom(e){`.</summary>
        /// <param name="screenTopLeft">커서 화면 px (왼쪽 위 기준).</param>
        /// <param name="delta">브라우저 deltaY 단위 (아래로 굴리면 +, 줌 아웃).</param>
        public void HandleZoom(Vector2 screenTopLeft, float delta)
        {
            StopAnimations();
            float old = Zoom;
            Zoom = Mathf.Clamp(Zoom / (1f + delta / WheelDivisor), MinZoom, MaxZoom);
            RepinAround(screenTopLeft.x, screenTopLeft.y, old);
        }

        /// <summary>드래그 — 이동값 −= 마우스 이동 / zoom `[소스] handleDrag(e){`.</summary>
        public void HandleDrag(float deltaX, float deltaY)
        {
            StopAnimations();
            PanX -= deltaX / Zoom;
            PanY -= deltaY / Zoom;
        }

        private void RepinAround(float screenX, float screenY, float oldZoom)
        {
            float hw = ViewWidth / 2f, hh = ViewHeight / 2f;
            float wx = (screenX - hw) / oldZoom + PanX;
            float wy = (screenY - hh) / oldZoom + PanY;
            PanX = wx - (screenX - hw) / Zoom;
            PanY = wy - (screenY - hh) / Zoom;
        }

        public void SetView(float panX, float panY, float zoom)
        {
            StopAnimations();
            PanX = panX;
            PanY = panY;
            Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        // ── 따라가기 `[소스] startCentering(){ · trackPlayer(e){ · processZoomAndMove(){ · processMovement(){`

        /// <summary>그 칸 좌표로 가며 줌 5 로 맞춘다 (내 나라로 · 좌표로).</summary>
        public void FocusOn(float tileX, float tileY, bool zoomIn = true)
        {
            StopAnimations();
            _destPoint = new Vector2(tileX, tileY);
            _destZoom = zoomIn ? FocusZoom : (float?)null;
            _movementOnly = zoomIn == false;
            _animElapsedMs = 0;
        }

        public void StopAnimations()
        {
            _destPoint = null;
            _destZoom = null;
            _movementOnly = false;
            _animElapsedMs = 0;
        }

        private void Update()
        {
            if (_map == null)
                return;

            if (_destPoint.HasValue)
            {
                // 원본 setInterval(16.6ms) — 프레임 시간을 모아 그 간격마다 한 번씩 돈다
                _animElapsedMs += Time.unscaledDeltaTime * 1000f;

                while (_destPoint.HasValue && _animElapsedMs >= AnimIntervalMs)
                {
                    _animElapsedMs -= AnimIntervalMs;

                    if (_movementOnly)
                        ProcessMovement(AnimIntervalMs);
                    else
                        ProcessZoomAndMove(AnimIntervalMs);
                }
            }

            Apply();
        }

        private void ProcessMovement(float elapsedMs)
        {
            Vector2 center = ViewportCenter();
            Vector2 dest = _destPoint.Value;

            if (Mathf.Abs(dest.x - center.x) + Mathf.Abs(dest.y - center.y) < AnimStopDistance)
            {
                StopAnimations();
                return;
            }

            float ease = 1f - Mathf.Pow(AnimDecay, elapsedMs / 1000f);
            PanX += Mathf.Clamp((dest.x - center.x) * ease, -AnimMaxStep, AnimMaxStep);
            PanY += Mathf.Clamp((dest.y - center.y) * ease, -AnimMaxStep, AnimMaxStep);
        }

        private void ProcessZoomAndMove(float elapsedMs)
        {
            Vector2 center = ViewportCenter();
            Vector2 dest = _destPoint.Value;
            float ease = 1f - Mathf.Pow(AnimDecay, elapsedMs / 1000f);
            bool arrived = Mathf.Abs(dest.x - center.x) + Mathf.Abs(dest.y - center.y) < AnimStopDistance;

            if (arrived == false)
            {
                PanX += Mathf.Clamp((dest.x - center.x) * ease, -AnimMaxStep, AnimMaxStep);
                PanY += Mathf.Clamp((dest.y - center.y) * ease, -AnimMaxStep, AnimMaxStep);
            }

            if (_destZoom.HasValue)
            {
                float diff = _destZoom.Value - Zoom;
                bool zoomDone = Mathf.Abs(diff) < AnimZoomEpsilon;

                if (zoomDone == false)
                {
                    float old = Zoom;
                    Zoom = Mathf.Clamp(Zoom + diff * ease, MinZoom, MaxZoom);
                    RepinAround(Screen.width / 2f, Screen.height / 2f, old);
                }

                if (arrived && zoomDone)
                    StopAnimations();
            }
            else if (arrived)
            {
                StopAnimations();
            }
        }

        /// <summary>원본 변환을 직교 카메라에 옮긴다.</summary>
        private void Apply()
        {
            if (_camera == null)
                return;

            // 화면 가운데 px 가 가리키는 칸 좌표 (floor 전)
            float cx = (Screen.width / 2f - ViewWidth / 2f) / Zoom + PanX + ViewWidth / 2f;
            float cy = (Screen.height / 2f - ViewHeight / 2f) / Zoom + PanY + ViewHeight / 2f;

            Transform t = _camera.transform;
            Vector3 world = FrontWarsWorld.FromTileCoords(cx, cy);
            t.position = new Vector3(world.x, world.y, t.position.z);
            _camera.orthographicSize = Screen.height / 2f / Zoom;
        }
    }
}
