using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 로비 화면을 그린다 — <b>타일맵 · 히어로 · 과제 NPC 둘 · 포털 둘</b>.
    ///
    /// <para>
    /// ★ <b>레이어 순서가 그림 순서다</b> [소스 — TMX <c>layers</c> 순]. 한 타일맵에 다 그리면
    /// <c>walls_bottom</c> 이 <c>objects</c> 를 덮는다 — 레이어마다 타일맵을 따로 둔다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <c>$colliders</c> 는 <b>안 그린다</b> — 막힘 표시지 그림이 아니다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 원본 TMX 는 <b>y 가 아래로</b> 증가한다. 유니티 타일맵은 위로 증가하므로
    /// <c>y</c> 를 뒤집어 놓는다 — 전투 지형과 <b>같은 규칙</b>이다.
    /// </para>
    /// </summary>
    public sealed class UndeadLobbyView : MonoBehaviour
    {
        [SerializeField] private Transform _root;

        private UndeadLobbySimulation _sim;
        private UndeadSpriteSet _heroSet;
        private UndeadSpriteSet[] _npcSets;
        private UndeadSpriteSet _portalGraveyard;
        private UndeadSpriteSet _portalWinterLocked;
        private UndeadSpriteSet _portalWinterOpen;

        private SpriteRenderer _hero;
        private readonly List<SpriteRenderer> _npcs = new List<SpriteRenderer>(2);
        private readonly List<SpriteRenderer> _portals = new List<SpriteRenderer>(2);
        private readonly List<SpriteRenderer> _portalGlows = new List<SpriteRenderer>(2);
        private readonly List<Tilemap> _layers = new List<Tilemap>(4);

        /// <summary>포털마다 하나 [소스 <c>Ru.sparkles</c>] — 팔레트가 갈린다 (묘지 보라 · 겨울 파랑).</summary>
        private readonly List<UndeadSparkleEmitter> _portalSparkles = new List<UndeadSparkleEmitter>(2);
        private readonly List<SpriteRenderer> _sparklePool = new List<SpriteRenderer>(64);
        private readonly List<int> _sparkleDraw = new List<int>(64);
        private Transform _sparkleRoot;

        /// <summary>포털 이미터가 서는 높이 [소스 <c>sparkles.y = −30</c>].</summary>
        private const double PortalSparkleEmitterY = -30.0;

        /// <summary>열릴 때 그림이 부푸는 폭 [소스 <c>sprite.scale.set(1 + .12·sin(πe))</c>].</summary>
        private const double PortalOpenPunch = 0.12;

        // ── 열릴 때 도는 테두리 원 [소스 glow.circle(0, −72, 58)]
        private const double GlowCenterY = -72.0;
        private const double GlowRadius = 58.0;
        private const float GlowRadiusPixels = 58f;

        /// <summary>[소스 <c>stroke.color = 10214655</c>].</summary>
        private static readonly Color GlowColor = new Color(0x9B / 255f, 0xDC / 255f, 0xFF / 255f, 1f);

        /// <summary>[소스 <c>stroke.alpha = .9</c>] — 여기에 연출 알파 <c>1 − e</c> 가 또 곱해진다.</summary>
        private const float GlowAlpha = 0.9f;

        /// <summary>[소스 <c>glow.scale = .75 + .5e</c>].</summary>
        private const double GlowScaleBase = 0.75;
        private const double GlowScaleGrow = 0.5;

        /// <summary>바닥이 «맨 아래»다 — 개체 정렬과 겹치지 않게 훨씬 낮게 둔다 (전투와 같은 규약).</summary>
        private const int LayerSortingBase = -1000;

        /// <summary>NPC·포털·히어로가 서는 층 — 전투의 <c>SortingBase</c> 와 같은 값을 쓴다.</summary>
        private const int EntitySortingBase = 10000;

        public bool IsBound
        {
            get { return _sim != null && _hero != null; }
        }

        /// <summary>
        /// 로비를 세운다. <b>타일맵은 여기서 한 번만</b> 만든다 — 맵은 안 바뀐다.
        /// </summary>
        public void Bind(UndeadLobbySimulation sim, UndeadLobbyMapDataContainer maps, TileBase[] tiles,
                         UndeadSpriteSet hero, UndeadSpriteSet[] npcs,
                         UndeadSpriteSet portalGraveyard, UndeadSpriteSet portalWinterLocked,
                         UndeadSpriteSet portalWinterOpen)
        {
            _sim = sim;
            _heroSet = hero;
            _npcSets = npcs;
            _portalGraveyard = portalGraveyard;
            _portalWinterLocked = portalWinterLocked;
            _portalWinterOpen = portalWinterOpen;

            if (_root == null)
                _root = transform;

            BuildTilemaps(maps, tiles);
            BuildEntities(npcs);
        }

        private void BuildTilemaps(UndeadLobbyMapDataContainer maps, TileBase[] tiles)
        {
            UndeadLobbyMapData map = maps == null ? null : maps.Map;

            if (map == null || map.Layers == null || tiles == null)
            {
                Log.Error("로비 맵이나 타일이 없다 — 바닥이 통째로 빈다");
                return;
            }

            var grid = new GameObject("LobbyGrid", typeof(Grid));
            grid.transform.SetParent(_root, false);
            grid.GetComponent<Grid>().cellSize = Vector3.one;

            for (int i = 0; i < map.Layers.Count; i++)
            {
                UndeadLobbyLayer layer = map.Layers[i];

                // 막힘 레이어는 그리지 않는다
                if (layer.Name == UndeadLobbyMapData.ColliderLayer)
                    continue;

                var go = new GameObject(layer.Name, typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(grid.transform, false);

                var tilemap = go.GetComponent<Tilemap>();
                go.GetComponent<TilemapRenderer>().sortingOrder = LayerSortingBase + i;

                var positions = new List<Vector3Int>(layer.Cells.Count);
                var cells = new List<TileBase>(layer.Cells.Count);

                foreach (KeyValuePair<string, int> pair in layer.Cells)
                {
                    if (int.TryParse(pair.Key, out int index) == false)
                        continue;

                    int tx = index % map.Width;
                    int ty = index / map.Width;

                    if (pair.Value < 0 || pair.Value >= tiles.Length)
                    {
                        Log.Error($"로비 타일 번호 {pair.Value} 가 시트({tiles.Length}칸) 밖이다");
                        continue;
                    }

                    // ⚠ 원본은 y 가 아래로 증가한다 — 유니티는 위로 증가하므로 뒤집는다
                    positions.Add(new Vector3Int(tx, -ty, 0));
                    cells.Add(tiles[pair.Value]);
                }

                tilemap.SetTiles(positions.ToArray(), cells.ToArray());
                _layers.Add(tilemap);
            }
        }

        private void BuildEntities(UndeadSpriteSet[] npcs)
        {
            if (_heroSet != null)
                _hero = NewRenderer("hero", _heroSet);

            for (int i = 0; i < UndeadSimulation.TaskNpcSlots; i++)
            {
                UndeadSpriteSet set = npcs != null && i < npcs.Length ? npcs[i] : null;

                if (set == null)
                {
                    Log.Error($"로비 NPC {i} 스프라이트가 없다 — 과제를 주는 사람이 화면에 없다");
                    continue;
                }

                _npcs.Add(NewRenderer($"npc{i}", set));
            }

            if (_portalGraveyard != null)
                _portals.Add(NewRenderer("portal1", _portalGraveyard));

            if (_portalWinterOpen != null)
                _portals.Add(NewRenderer("portal2", _portalWinterOpen));

            _sparkleRoot = new GameObject("PortalSparkles").transform;
            _sparkleRoot.SetParent(_root, false);

            for (int i = 0; i < _portals.Count; i++)
            {
                int biome = i + 1;

                _portalSparkles.Add(new UndeadSparkleEmitter(
                    $"lobby_portal_{biome}_sparkles",
                    biome == 1 ? UndeadSparklePalette.Purple : UndeadSparklePalette.Winter));

                _portalGlows.Add(NewGlow($"portal{biome}_glow"));
            }
        }

        /// <summary>
        /// 열릴 때 도는 <b>테두리 원</b> [소스 <c>glow.circle(0, −72, 58)</c> + <c>stroke</c>].
        /// <para>⚠ 처음에는 <b>안 보인다</b> [소스 <c>glow.visible = false</c>] — 열리는 «순간»에만 켠다.</para>
        /// </summary>
        private SpriteRenderer NewGlow(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = UndeadShapes.Ring(GlowRadiusPixels);
            renderer.enabled = false;
            return renderer;
        }

        private SpriteRenderer NewRenderer(string name, UndeadSpriteSet set)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = set.Get(0, 0.0);
            go.transform.localScale = new Vector3((float)set.Data.DisplayScaleX, (float)set.Data.DisplayScaleY, 1f);
            return renderer;
        }

        /// <summary>매 프레임. <b>시뮬이 «상태»를 들고, 여기서는 그리기만</b> 한다.</summary>
        public void Render()
        {
            if (IsBound == false)
                return;

            // ── 히어로. 0행 대기 · 1행 이동 (전투와 같은 시트)
            _hero.sprite = _heroSet.Get(_sim.HeroMoving ? 1 : 0, _sim.HeroAnimFrame);
            Place(_hero.transform, _sim.HeroPosition);
            _hero.sortingOrder = SortOrder(_sim.HeroPosition.Y);
            ApplyFacing(_hero.transform, _sim.HeroFacingLeft);

            for (int i = 0; i < _npcs.Count && i < _sim.Npcs.Count; i++)
            {
                UndeadLobbySimulation.Npc npc = _sim.Npcs[i];
                UndeadSpriteSet set = _npcSets[i];

                // NPC 는 «시간으로» 도는 2컷이다 [소스 animationSpeed .05 = 3fps]
                _npcs[i].sprite = set.Get(0, Time.time * set.Data.Fps);
                Place(_npcs[i].transform, npc.Position);
                _npcs[i].sortingOrder = SortOrder(npc.Position.Y);
            }

            for (int i = 0; i < _portals.Count && i < _sim.Portals.Count; i++)
            {
                UndeadLobbySimulation.Portal portal = _sim.Portals[i];
                UndeadSpriteSet set = PortalSetOf(portal);

                // ★ 열리는 연출 [소스 updateOpeningFeedback] — 그림이 «한 번 부풀었다» 돌아온다
                double e = portal.OpeningProgress;
                double punch = 1.0 + PortalOpenPunch * System.Math.Sin(e * System.Math.PI);

                if (set != null)
                {
                    _portals[i].sprite = set.Get(0, 0.0);
                    _portals[i].transform.localScale =
                        new Vector3((float)(set.Data.DisplayScaleX * punch),
                                    (float)(set.Data.DisplayScaleY * punch), 1f);
                }

                Place(_portals[i].transform, portal.Position);
                _portals[i].sortingOrder = SortOrder(portal.Position.Y);
                RenderGlow(i, portal, e);
            }

            RenderPortalSparkles(Time.deltaTime);
        }

        /// <summary>
        /// 테두리 원 — <b>연출이 도는 동안만</b> 보인다.
        /// <para>
        /// ⚠ 원본은 원의 «중심»이 그래픽 안에서 <c>(0, −72)</c> 다 — 그래픽을 통째로 배율하면
        /// <b>중심 자체도 같이 올라간다</b>. 자리를 배율 밖에 두면 커질수록 아래로 처진다.
        /// </para>
        /// </summary>
        private void RenderGlow(int index, UndeadLobbySimulation.Portal portal, double progress)
        {
            if (index >= _portalGlows.Count || _portalGlows[index] == null)
                return;

            SpriteRenderer glow = _portalGlows[index];

            if (portal.IsOpeningFeedbackActive == false)
            {
                if (glow.enabled)
                    glow.enabled = false;

                return;
            }

            float scale = (float)(GlowScaleBase + GlowScaleGrow * progress);

            glow.enabled = true;
            glow.transform.position = UndeadUnits.ToPosition(portal.Position.X, portal.Position.Y + GlowCenterY * scale);
            glow.transform.localScale = new Vector3(scale, scale, 1f);
            glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, (float)(GlowAlpha * (1.0 - progress)));
            glow.sortingOrder = SortOrder(portal.Position.Y) - 1;
        }

        /// <summary>포털 반짝임 — <b>열린 포털만</b> 뿜는다 [소스 <c>isEmitterEnabled: () =&gt; this.isOpen</c>].</summary>
        private void RenderPortalSparkles(float dt)
        {
            _sparkleDraw.Clear();

            for (int i = 0; i < _portalSparkles.Count && i < _sim.Portals.Count; i++)
            {
                _portalSparkles[i].Step(dt * 1000.0, 0.0, _sim.Portals[i].Open);

                for (int p = 0; p < _portalSparkles[i].Capacity; p++)
                {
                    if (_portalSparkles[i][p].Active)
                        _sparkleDraw.Add(i * _portalSparkles[i].Capacity + p);
                }
            }

            int used = 0;

            for (int i = 0; i < _sparkleDraw.Count; i++)
            {
                SpriteRenderer renderer = used < _sparklePool.Count ? _sparklePool[used] : null;

                if (renderer == null)
                {
                    var go = new GameObject("particle");
                    go.transform.SetParent(_sparkleRoot, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = UndeadParticleView.White;
                    _sparklePool.Add(renderer);
                }

                int packed = _sparkleDraw[i];
                int owner = packed / UndeadSparkleEmitter.SparkleConfig.MaxParticles;
                int slot = packed % UndeadSparkleEmitter.SparkleConfig.MaxParticles;

                double worldY = UndeadParticleView.Draw(renderer, _portalSparkles[owner][slot],
                                                        _portalSparkles[owner].Config,
                                                        _sim.Portals[owner].Position, PortalSparkleEmitterY);
                renderer.sortingOrder = SortOrder(worldY) + 1;
                renderer.enabled = true;
                used++;
            }

            for (int i = used; i < _sparklePool.Count; i++)
            {
                if (_sparklePool[i].enabled)
                    _sparklePool[i].enabled = false;
            }
        }

        /// <summary>
        /// 포털 그림은 <b>열림/잠김으로 갈린다</b> [소스 <c>Bu</c>].
        /// <para>⚠ 잠김 그림이 없으면 열림을 쓴다 — 바이옴 1 은 잠기지 않는다.</para>
        /// </summary>
        private UndeadSpriteSet PortalSetOf(UndeadLobbySimulation.Portal portal)
        {
            if (portal.Biome == 1)
                return _portalGraveyard;

            return portal.Open ? _portalWinterOpen : _portalWinterLocked;
        }

        private static void Place(Transform target, UndeadVec2 world)
        {
            target.position = UndeadUnits.ToPosition(world.X, world.Y);
        }

        private static int SortOrder(double worldY)
        {
            return EntitySortingBase - (int)worldY;
        }

        private static void ApplyFacing(Transform target, bool facingLeft)
        {
            Vector3 scale = target.localScale;
            float magnitude = Mathf.Abs(scale.x);
            scale.x = facingLeft ? -magnitude : magnitude;
            target.localScale = scale;
        }

        /// <summary>로비를 보이거나 감춘다 — 전투 화면과 <b>같은 씬</b>에 있기 때문이다.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null && _root != transform)
                _root.gameObject.SetActive(visible);
            else
                gameObject.SetActive(visible);
        }
    }
}
