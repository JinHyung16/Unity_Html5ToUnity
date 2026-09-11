using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 씬에서 <b>유일하게 <c>Awake</c> 를 가진 것</b>. 초기화 «순서»를 한 곳에 못 박는다.
    ///
    /// <para>
    /// ⚠ <b>순서가 사양이다</b> — UI 루트 → 데이터 → 아트 → 매니저 → 월드 뷰 → 배선.
    /// 데이터보다 매니저가 먼저 서면 <b>폴백값으로 초기화</b>되고, 그건 에러도 안 난다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>매 프레임 «시뮬 → 렌더» 순서로 돈다.</b> 반대로 하면 화면이 한 프레임 늦는다.
    /// 시뮬은 <c>BaseGameManager.Update</c> 가 돌리고, 렌더는 여기 <c>LateUpdate</c> 다.
    /// </para>
    /// </summary>
    public sealed class UndeadGameInitialize : MonoBehaviour
    {
        /// <summary>적 피격 팝업은 <b>적 자리에서 20 위</b>다 [소스 <c>new $l(s.x, s.y − 20, …)</c>].</summary>
        private const double EnemyPopupOffsetY = 20.0;

        /// <summary>모닥불 팝업은 <b>스프라이트 중점에서 30 위</b>다 [소스 <c>y − .5·height − 30</c> · height = 24×2.5].</summary>
        private const double FirePopupOffsetY = 24.0 * 2.5 * 0.5 + 30.0;

        /// <summary>히어로 팝업은 <b>스프라이트 중점에서 70 위</b>다 [소스 <c>getEffectTextY</c> · height = 16×3].</summary>
        private const double HeroPopupOffsetY = 16.0 * 3.0 * 0.5 + 70.0;

        private static UndeadVec2 HeroPopupAt(UndeadSimulation sim)
        {
            return new UndeadVec2(sim.HeroPosition.X, sim.HeroPosition.Y - HeroPopupOffsetY);
        }

        [Header("Camera")]
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Camera _uiCamera;

        [Header("Root")]
        [SerializeField] private Transform _uiRoot;
        [SerializeField] private Transform _managerRoot;
        [SerializeField] private Transform _managementRoot;
        [SerializeField] private Transform _worldRoot;

        [Header("View")]
        [SerializeField] private UndeadWorldView _worldView;
        [SerializeField] private UndeadTerrainView _terrainView;

        /// <summary>로비 — <b>전투와 같은 씬</b>이라 여기서 같이 세운다.</summary>
        [SerializeField] private UndeadLobbyView _lobbyView;
        [SerializeField] private UndeadFloatingTextView _floatingText;
        [SerializeField] private UndeadCameraDirector _cameraDirector;
        [SerializeField] private UndeadMoveInput _moveInput;

        /// <summary>초기화가 끝났나. 재생 검사가 «기다릴» 지점이다 (게임 코드가 쓰는 상태 그대로다).</summary>
        public bool IsReady { get; private set; }

        public UndeadWorldView WorldView
        {
            get { return _worldView; }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private async void Awake()
        {
            await InitAsync(_cts.Token);
        }

        /// <summary>
        /// 전사가 하는 말 <b>다섯 마디</b> [소스 <c>helpMe</c> · <c>thanksPal</c> · <c>introduction.bubbleTextKey</c> 셋].
        ///
        /// <para>
        /// ⚠ 순서가 <see cref="UndeadWorldView"/> 의 규약이다 —
        /// <b>도와줘 · 고마워 · 마법사 예고 · 첫 보스 예고 · 농부 예고</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ [사고] 뒤의 네 키는 <b>문구 표에 있었는데 읽는 곳이 없었다</b> — 전사가 한 마디밖에 못 했다
        /// (재발방지 #131 · #159 — 「표에 넣고 읽는 곳이 없다」).
        /// </para>
        /// </summary>
        private static string[] WarriorSpeechTexts()
        {
            JinHyung.Data.UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;

            return new[]
            {
                texts.Ko("helpMe"),
                texts.Ko("thanksPal"),
                texts.Ko("iHearSomebodyScreaming"),
                texts.Ko("iFeelSomeDarkEnergy"),
                texts.Ko("whatTheAnimals"),
            };
        }

        /// <summary>
        /// 마법사·가족·농부의 문구 — <b>순서가 <see cref="EUndeadNpcSpeech"/> 와 «한 쌍»</b>이다
        /// (<c>None</c> 을 뺀 나머지).
        /// <para>⚠ 순서를 바꾸면 다른 NPC 의 말이 나온다 — 그런데 오류는 안 난다.</para>
        /// </summary>
        private static string[] NpcSpeechTexts()
        {
            JinHyung.Data.UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;

            return new[]
            {
                texts.Ko("iLostTwoArcaneFragments"),
                texts.Ko("youSavedMeReward"),
                texts.Ko("weCantPassDarkSoul"),
                texts.Ko("thanksNowWeCanContinue"),
                texts.Ko("mySheepAreInDanger"),
                texts.Ko("myHeroThankYou"),
            };
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        /// <summary>지금 화면에 물려 둔 바이옴 — <b>매 프레임 «지금 값»과 비교한다</b>.</summary>
        private int _boundBiome = 1;

        private void LateUpdate()
        {
            if (IsReady == false || _worldView == null)
                return;

            UndeadSimulation simulation = UndeadGameRoot.Instance.Game.Simulation;

            // ★ 지형·카메라는 «카메라 피벗»을 따라간다 — 히어로가 아니다.
            //   원본 카메라는 히어로를 «뒤따르므로» 히어로를 그대로 쓰면 지형이 한 프레임 앞서 흐른다.
            UndeadVec2 pivot = simulation.CameraPivot;

            // ★ 바이옴이 바뀌었으면 «그림 벌»을 갈아 끼운다.
            //   ⚠ 「바꾼 쪽이 뷰를 부른다」로 두지 않는다 — 부르는 자리가 늘 때마다 하나씩 빠뜨린다.
            //     매 프레임 한 번 비교하는 쪽이 «순서와 무관»하다.
            int biome = UndeadGameRoot.Instance.Game.Biome;

            if (biome != _boundBiome)
            {
                _boundBiome = biome;
                _terrainView?.SetBiome(biome);
                _worldView.SetBiome(biome);
            }

            // ⚠ 지형을 «먼저» 따라가게 한다 — 카메라가 먼저 움직이면 한 프레임 동안 가장자리가 빈다.
            if (_terrainView != null)
                _terrainView.Follow(pivot);

            _worldView.Render();

            if (_cameraDirector != null)
                _cameraDirector.Follow(pivot);
        }

        /// <summary>
        /// 지형 타일셋을 읽어 <see cref="UndeadTerrainView"/> 에 붙인다.
        /// <para>주소는 <c>biome_graveyard_tiles[biome_graveyard_tiles_&lt;행&gt;_&lt;열&gt;]</c> — 임포터 규약과 한 쌍이다.</para>
        /// </summary>
        private async Task BindTerrainAsync(CancellationToken cancellationToken)
        {
            UndeadBiomeDataContainer biomes = GameRoot.Instance.UndeadBiomeDataContainer;

            if (biomes == null || biomes.Count == 0)
            {
                Log.Error("바이옴 표가 없다 — 지형 타일셋을 못 고른다");
                return;
            }

            // ★ 바이옴마다 한 벌 — 표가 이름을 준다. 여기서 시트 이름을 만들지 않는다.
            UnityEngine.Tilemaps.TileBase[] graveyard = await LoadTilesetAsync(biomes.Get(1), cancellationToken);
            UnityEngine.Tilemaps.TileBase[] winter = await LoadTilesetAsync(biomes.Get(2), cancellationToken);

            if (cancellationToken.IsCancellationRequested || graveyard == null)
                return;

            _terrainView.Bind(_terrainView.GetComponent<UnityEngine.Tilemaps.Tilemap>(), graveyard, winter);
            _terrainView.Follow(UndeadVec2.Zero);
        }

        /// <summary>
        /// 타일셋 한 벌 — <b>격자로 잘린 칸을 한꺼번에</b> 읽는다.
        /// <para>주소는 <c>&lt;시트&gt;[&lt;시트&gt;_0_&lt;열&gt;]</c> — 임포터 규약과 한 쌍이다.</para>
        /// </summary>
        private static async Task<UnityEngine.Tilemaps.TileBase[]> LoadTilesetAsync(
            UndeadBiomeData biome, CancellationToken cancellationToken)
        {
            if (biome == null)
            {
                Log.Error("바이옴 행이 없다 — 그 바이옴의 지형이 통째로 빈다");
                return null;
            }

            string sheet = biome.TilesetCode;
            const int cols = UndeadTerrainView.TilesetColumns;
            var addresses = new System.Collections.Generic.List<string>(cols);

            for (int col = 0; col < cols; col++)
                addresses.Add($"{sheet}[{sheet}_0_{col}]");

            Sprite[] sprites = await ArtLoader.LoadSpritesAsync(addresses);   // 46칸을 «한꺼번에»

            if (cancellationToken.IsCancellationRequested)
                return null;

            var tiles = new System.Collections.Generic.List<UnityEngine.Tilemaps.TileBase>(cols);

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                tile.sprite = sprites[i];
                tiles.Add(tile);
            }

            if (tiles.Count != cols)
            {
                Log.Error($"{sheet}: 타일 {tiles.Count}칸만 읽혔다 — {cols}칸이어야 한다 (임포터의 격자 자르기를 본다)");
                return tiles.Count == 0 ? null : tiles.ToArray();
            }

            return tiles.ToArray();
        }

        /// <summary>
        /// 로비 타일 — 전투 타일셋과 <b>같은 규약</b>(격자로 잘린 스프라이트 주소)이다.
        /// <para>⚠ 칸 수는 <b>맵이 정한다</b> — 여기서 숫자를 만들지 않는다.</para>
        /// </summary>
        /// <summary>
        /// 로비를 세운다 — 맵·타일·아트·시뮬을 물린다.
        ///
        /// <para>
        /// ⚠ <b>로비 뷰가 없으면 조용히 넘어가지 않는다</b> — 씬에 안 붙어 있으면
        /// 로비 화면이 «빈 화면»이 되는데 아무 오류도 안 난다.
        /// </para>
        /// </summary>
        private async Task SetupLobbyAsync(UndeadGameRoot root, CancellationToken cancellationToken)
        {
            if (_lobbyView == null)
                _lobbyView = FindFirstObjectByType<UndeadLobbyView>(FindObjectsInactive.Include);

            if (_lobbyView == null)
            {
                Log.Error("로비 뷰가 씬에 없다 — 로비가 빈 화면이 된다");
                return;
            }

            UndeadLobbyMapDataContainer maps = GameRoot.Instance.UndeadLobbyMapDataContainer;
            UndeadLobbyMapData map = maps == null ? null : maps.Map;

            if (map == null)
            {
                Log.Error("로비 맵 표가 없다 — 로비를 못 세운다");
                return;
            }

            UndeadArtDataContainer art = GameRoot.Instance.UndeadArtDataContainer;

            Task<UndeadSpriteSet> heroTask = UndeadSpriteSet.LoadAsync(art.Get("hero"));
            Task<UndeadSpriteSet> npc1Task = UndeadSpriteSet.LoadAsync(art.Get("lobby_task_npc_groundskeeper"));
            Task<UndeadSpriteSet> npc2Task = UndeadSpriteSet.LoadAsync(art.Get("lobby_task_npc_herbalist"));
            Task<UndeadSpriteSet> portal1Task = UndeadSpriteSet.LoadAsync(art.Get("lobby_portal_graveyard_open"));
            Task<UndeadSpriteSet> lockedTask = UndeadSpriteSet.LoadAsync(art.Get("lobby_portal_winter_locked"));
            Task<UndeadSpriteSet> openTask = UndeadSpriteSet.LoadAsync(art.Get("lobby_portal_winter_open"));

            UnityEngine.Tilemaps.TileBase[] tiles = await LoadLobbyTilesAsync(map.SourceTiles.Count);

            if (cancellationToken.IsCancellationRequested)
                return;

            // ★ 시뮬을 «먼저» 세우고 뷰를 한 번만 물린다 — 두 번 물리면 타일맵을 두 번 짓는다
            root.Lobby.Setup(root.Game.Simulation, _lobbyView, maps,
                             GameRoot.Instance.UndeadConfigDataContainer.Config.HeroMoveSpeedWorld);

            _lobbyView.Bind(root.Lobby.Simulation, maps, tiles, await heroTask,
                            new[] { await npc1Task, await npc2Task },
                            await portal1Task, await lockedTask, await openTask);

            if (_moveInput != null)
                root.Lobby.MoveInputSource = _moveInput.Read;

            _lobbyView.SetVisible(false);
        }

        private static async Task<UnityEngine.Tilemaps.TileBase[]> LoadLobbyTilesAsync(int cols)
        {
            const string sheet = "biome_lobby_tiles";
            var addresses = new System.Collections.Generic.List<string>(cols);

            for (int col = 0; col < cols; col++)
                addresses.Add($"{sheet}[{sheet}_0_{col}]");

            Sprite[] sprites = await ArtLoader.LoadSpritesAsync(addresses);
            var tiles = new UnityEngine.Tilemaps.TileBase[sprites.Length];
            int missing = 0;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    missing++;
                    continue;
                }

                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                tile.sprite = sprites[i];
                tiles[i] = tile;
            }

            if (missing > 0)
                Log.Error($"로비 타일 {missing}칸을 못 읽었다 — 그 자리는 빈다");

            return tiles;
        }

        private async Task InitAsync(CancellationToken cancellationToken)
        {
            // ★ 단계별 시간을 찍는다 — 「캐릭터가 늦게 뜬다」는 지적은 «어디서» 늦는지 재야 답이 된다 [회차 11].
            var clock = System.Diagnostics.Stopwatch.StartNew();
            long tData = 0, tArt = 0, tTerrain = 0, tIcons = 0;

            // ① UI 루트·카메라. 이게 안 되면 창을 만들 자리가 없다.
            WindowManagement.Instance.BindEnvironment(_uiRoot, _uiCamera);

            // ② 데이터. 등록 목록은 사람이 들고 있는 UndeadContainerRegister 하나다.
            //    ⚠ 여기서 등록하지 않으면 JSON 이 있어도 «아무도 안 읽고 에러도 안 난다».
            UndeadContainerRegister.RegisterAll();
            await DataManager.Instance.InitializeAsync(cancellationToken);
            tData = clock.ElapsedMilliseconds;

            if (cancellationToken.IsCancellationRequested)
                return;

            // ③ 아트. 표가 «어느 시트를 몇 컷으로 자르는지»를 안다.
            UndeadArtDataContainer art = GameRoot.Instance.UndeadArtDataContainer;
            // ★ 세트 전부를 «먼저 띄우고» 나중에 받는다 — 하나씩 await 하면 세트 수만큼 왕복이 늘어난다 [회차 11].
            // ★ 적 그림은 «종류마다» 있다 — 스폰 우선순위 순서가 시뮬의 TypeId 순서다 [회차 9].
            System.Collections.Generic.IReadOnlyList<UndeadEnemyData> enemyRows =
                GameRoot.Instance.UndeadEnemyDataContainer.ByPriority;
            var enemyTasks = new System.Collections.Generic.List<Task<UndeadSpriteSet>>(enemyRows.Count);

            Task<UndeadSpriteSet> heroTask = UndeadSpriteSet.LoadAsync(art.Get("hero"));

            for (int i = 0; i < enemyRows.Count; i++)
                enemyTasks.Add(UndeadSpriteSet.LoadAsync(art.Get(enemyRows[i].TextureCode)));

            Task<UndeadSpriteSet> projectileTask = UndeadSpriteSet.LoadAsync(art.Get("projectile1"));
            Task<UndeadSpriteSet> gemTask = UndeadSpriteSet.LoadAsync(art.Get("gem"));
            // ★ 전사는 «모드마다 시트»다 [소스 loadFrames] — 누움 · 서기 · 달리기
            Task<UndeadSpriteSet> warriorTask = UndeadSpriteSet.LoadAsync(art.Get("warrior_lay"));
            Task<UndeadSpriteSet> warriorIdleTask = UndeadSpriteSet.LoadAsync(art.Get("warrior_idle"));
            Task<UndeadSpriteSet> warriorRunTask = UndeadSpriteSet.LoadAsync(art.Get("warrior_run"));
            // ★ 구조된 전사가 던지는 쿠나이 [소스 class Pd]
            Task<UndeadSpriteSet> kunaiTask = UndeadSpriteSet.LoadAsync(art.Get("kunai"));
            Task<UndeadSpriteSet> bubbleTask = UndeadSpriteSet.LoadAsync(art.Get("bubble"));
            // ★ 전사가 «다음 과제를 예고»할 때 쓰는 큰 말풍선 [소스 variant "medium"]
            Task<UndeadSpriteSet> bubbleMediumTask = UndeadSpriteSet.LoadAsync(art.Get("bubble_medium"));
            // ★ 쓰러진 전사 곁에서 차오르는 구조 진행 링 [소스 class Ad]
            Task<UndeadSpriteSet> questProgressTask = UndeadSpriteSet.LoadAsync(art.Get("quest_progress"));
            Task<UndeadSpriteSet> bossTask = UndeadSpriteSet.LoadAsync(art.Get("darksoul"));
            Task<UndeadSpriteSet> fireballTask = UndeadSpriteSet.LoadAsync(art.Get("fireball"));
            // ★ 가족은 «세 명»이고 각기 다른 시트다 [소스 createMembers]
            Task<UndeadSpriteSet> family1Task = UndeadSpriteSet.LoadAsync(art.Get("family_npc_1"));
            Task<UndeadSpriteSet> family2Task = UndeadSpriteSet.LoadAsync(art.Get("family_npc_2"));
            Task<UndeadSpriteSet> family3Task = UndeadSpriteSet.LoadAsync(art.Get("family_npc_3"));
            Task<UndeadSpriteSet> farmerTask = UndeadSpriteSet.LoadAsync(art.Get("farmer"));
            Task<UndeadSpriteSet> sheepTask = UndeadSpriteSet.LoadAsync(art.Get("sheep"));
            Task<UndeadSpriteSet> treeTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree"));
            Task<UndeadSpriteSet> treeOnTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree_activated"));
            Task<UndeadSpriteSet> treeOffTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree_inactive"));
            Task<UndeadSpriteSet> snowTask = UndeadSpriteSet.LoadAsync(art.Get("snowman"));
            Task<UndeadSpriteSet> snowOnTask = UndeadSpriteSet.LoadAsync(art.Get("snowman_activated"));
            Task<UndeadSpriteSet> snowOffTask = UndeadSpriteSet.LoadAsync(art.Get("snowman_inactive"));
            Task<UndeadSpriteSet> meteorTask = UndeadSpriteSet.LoadAsync(art.Get("meteor"));
            Task<UndeadSpriteSet> fireTask = UndeadSpriteSet.LoadAsync(art.Get("fire"));
            Task<UndeadSpriteSet> fireOffTask = UndeadSpriteSet.LoadAsync(art.Get("fire_inactive"));
            Task<UndeadSpriteSet> heartTask = UndeadSpriteSet.LoadAsync(art.Get("heart"));
            Task<UndeadSpriteSet> lightningTask = UndeadSpriteSet.LoadAsync(art.Get("lightning"));
            Task<UndeadSpriteSet> trailTask = UndeadSpriteSet.LoadAsync(art.Get("skill_effect_blazing_trail"));
            Task<UndeadSpriteSet> hpTask = UndeadSpriteSet.LoadAsync(art.Get("hp_segment_bg"));
            // ★ 체력 칸은 «두 겹»이다 — 어두운 바탕은 늘 있고 빨간 채움만 줄었다 늘었다 한다 [소스 rebuildSegments]
            Task<UndeadSpriteSet> hpFillTask = UndeadSpriteSet.LoadAsync(art.Get("hp_segment_fill"));
            Task<UndeadSpriteSet> mageTask = UndeadSpriteSet.LoadAsync(art.Get("mage"));
            Task<UndeadSpriteSet> fragmentTask = UndeadSpriteSet.LoadAsync(art.Get("arcane_fragment"));

            UndeadSpriteSet hero = await heroTask;
            var enemySets = new System.Collections.Generic.List<UndeadSpriteSet>(enemyTasks.Count);

            for (int i = 0; i < enemyTasks.Count; i++)
                enemySets.Add(await enemyTasks[i]);

            UndeadSpriteSet projectile = await projectileTask;
            UndeadSpriteSet gem = await gemTask;
            UndeadSpriteSet warrior = await warriorTask;
            UndeadSpriteSet bubble = await bubbleTask;
            tArt = clock.ElapsedMilliseconds;

            if (cancellationToken.IsCancellationRequested)
                return;

            // ④ 로직. 데이터가 선 뒤여야 한다.
            UndeadGameRoot root = UndeadGameRoot.Instance;

            if (_managerRoot != null)
                root.transform.SetParent(_managerRoot, false);

            root.Bootstrap();

            // ⚠ 주입은 Bootstrap «뒤»다 — 매니저 실체가 그때 생긴다.
            if (_moveInput != null)
            {
                root.Game.MoveInputSource = _moveInput.Read;
                root.Game.SkillInputSource = _moveInput.ReadSkillPress;
            }

            // ⑤ 월드 뷰 — 개체당 컴포넌트 0. 풀링된 SpriteRenderer 만 쓴다 (확정표 G).
            if (_worldView != null)
            {
                _worldView.Bind(root.Game.Simulation, hero, enemySets, projectile, gem);

                // ⚠ 말풍선 문구는 «문구 표»에서 온다 — 화면에 보이는 한국어를 코드에 박지 않는다.
                _worldView.BindBossQuest(await bossTask, await fireballTask,
                                         new[] { await family1Task, await family2Task, await family3Task },
                                         await farmerTask, await sheepTask);

                // 지형이 놓는 나무·모닥불과 나무의 유성 [소스 — 청크 노이즈]
                _worldView.BindWorldObjects(await treeTask, await treeOnTask, await treeOffTask, await meteorTask,
                                            await fireTask, await fireOffTask, await heartTask);
                _worldView.BindWinterObjects(await snowTask, await snowOnTask, await snowOffTask);

                _worldView.BindLightning(await lightningTask);
                _worldView.BindTrail(await trailTask);
                _worldView.BindLifeBar(await hpTask, await hpFillTask, GameRoot.Instance.UndeadConfigDataContainer.Config.HeroMaxHp);

                _worldView.BindQuestTarget(warrior, await warriorIdleTask, await warriorRunTask,
                                           bubble, await mageTask, await fragmentTask,
                                           await kunaiTask, await bubbleMediumTask,
                                           await questProgressTask,
                                           WarriorSpeechTexts(),
                                           NpcSpeechTexts(),
                                           UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Font/UndeadSlayer SDF"));
            }

            // ══════════════════════════════ 로비 — 전투와 «같은 씬»이라 여기서 같이 세운다
            await SetupLobbyAsync(root, cancellationToken);

            if (_cameraDirector != null)
            {
                UndeadConfigData cfg = GameRoot.Instance.UndeadConfigDataContainer.Config;
                _cameraDirector.SetOffsetY(cfg.CameraOffsetY);

                // 나무가 터지면 카메라가 흔들린다 [소스 triggerShake(10, 300)]
                root.Game.Simulation.OnTreeBurst += _ => _cameraDirector.Shake(cfg.CameraShakeAmplitude, cfg.CameraShakeSeconds);
            }

            // ⑤-b 지형 — 타일 에셋은 어드레서블 스프라이트에서 «런타임에» 만든다.
            //     ⚠ 타일 «에셋 파일»을 굽지 않는 이유: 스프라이트 하나에 Tile 하나라 파일만 늘고,
            //       바뀌는 것은 스프라이트뿐이다.
            if (_terrainView != null)
                await BindTerrainAsync(cancellationToken);

            tTerrain = clock.ElapsedMilliseconds;

            // ⑤-c 연출 — 시뮬이 «사건»을 알리고 뷰가 그린다.
            //     ⚠ 시뮬 안에서 그리지 않는다 — 그러면 골든 채점이 렌더에 묶인다.
            if (_floatingText != null)
            {
                _floatingText.SetFont(UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Font/UndeadSlayer SDF"));
                _floatingText.SetTable(GameRoot.Instance.UndeadPopupDataContainer);
                _floatingText.SetHeart(await heartTask);

                UndeadSimulation sim = root.Game.Simulation;
                UndeadTextDataContainer texts = GameRoot.Instance.UndeadTextDataContainer;

                // ★ 자리는 «부르는 쪽»이 정한다 — 원본이 개체마다 다른 높이를 쓴다.
                //   보석 = 보석 자리 · 적 = 적 y −20 · 히어로 = 스프라이트 중점 −70 · 모닥불 = 스프라이트 중점 −30
                sim.OnGemCollected += (at, xp) => _floatingText.Spawn("xp", at, $"+{xp} XP");

                sim.OnEnemyDamaged += (at, damage, lightning, boss) =>
                    _floatingText.Spawn(lightning ? "lightningDamage" : "enemyDamage",
                                        new UndeadVec2(at.X, at.Y - EnemyPopupOffsetY), $"-{damage}");

                sim.OnHeroDamaged += damage =>
                    _floatingText.Spawn("heroDamage", HeroPopupAt(sim), $"-{damage}");

                sim.OnHeroHealed += (at, amount) =>
                    _floatingText.Spawn("heroHeal", HeroPopupAt(sim), $"+{amount}");

                sim.OnFireplaceFullHealth += at =>
                    _floatingText.Spawn("fullHealth", new UndeadVec2(at.X, at.Y - FirePopupOffsetY), texts.Ko("fullHealth"));

                sim.OnFireplaceOut += at =>
                    _floatingText.Spawn("fireOut", new UndeadVec2(at.X, at.Y - FirePopupOffsetY), texts.Ko("fireIsOut"));
            }

            // ⑤-d 업그레이드 «축별 아이콘» — 카드가 뜨는 순간에 받으면 첫 프레임이 빈다.
            //     ⚠ 표에 <c>IconAddress</c> 가 있어도 «받아서 넣는 줄»이 없으면
            //       카드 세 장이 전부 프리팹 기본 그림으로 뜬다 [회차 8 — 캡처로 잡았다].
            var upgradeIcons = new System.Collections.Generic.Dictionary<string, Sprite>();

            var iconAddresses = new System.Collections.Generic.List<string>(8);

            foreach (UndeadUpgradeData upgrade in GameRoot.Instance.UndeadUpgradeDataContainer.AllValues)
            {
                if (string.IsNullOrEmpty(upgrade.IconAddress) || iconAddresses.Contains(upgrade.IconAddress))
                    continue;

                iconAddresses.Add(upgrade.IconAddress);
            }

            // ★ 스킬 아이콘도 같이 받는다 — 로비의 «보상» 자리와 보상 팝업이 쓴다.
            //   ⚠ 따로 받으면 그 자리만 «비어 있는데» 아무 오류도 안 난다.
            UndeadSkillDataContainer skillTable = GameRoot.Instance.UndeadSkillDataContainer;

            if (skillTable != null)
            {
                for (int i = 0; i < skillTable.AllValues.Count; i++)
                {
                    string address = skillTable.AllValues[i].IconAddress;

                    if (iconAddresses.Contains(address) == false)
                        iconAddresses.Add(address);
                }
            }

            Sprite[] iconSprites = await ArtLoader.LoadSpritesAsync(iconAddresses);

            for (int i = 0; i < iconAddresses.Count; i++)
                upgradeIcons[iconAddresses[i]] = iconSprites[i];

            tIcons = clock.ElapsedMilliseconds;

            if (cancellationToken.IsCancellationRequested)
                return;

            // ⑥ 창 조종자. 매니저를 곧바로 읽으므로 ④ 뒤여야 한다.
            var management = AddManagement<UndeadManagement>();
            management.BindUpgradeIcons(upgradeIcons);
            management.Bind(root);

            // ★ 로비 UI 는 «문구·아이콘 창구»를 통해서만 표를 본다
            management.BindLobbyTexts(new UndeadLobbyTexts(
                GameRoot.Instance.UndeadTextDataContainer,
                GameRoot.Instance.UndeadTaskDataContainer,
                GameRoot.Instance.UndeadSkillDataContainer,
                upgradeIcons));

            management.BindLobbyCamera(_gameCamera);
            management.Initialize();

            // ⑦ 첫 화면 — 원본은 바이옴에 들어오면 <b>「시작」 버튼이 뜬 대기 상태</b>다 [실측].
            root.Game.Paused = true;
            root.GameFlow.ChangeScreen(EUndeadScreenType.Ready);

            IsReady = true;
            Log.Success("Undead Slayer 초기화 완료 — 단위 0·1·2·3·4·5·6 (토대 · 데이터 · 시뮬 · 렌더 · 지형 · HUD · 모달)");
            Log.Success($"[초기화 시간] 데이터 {tData}ms · 아트 +{tArt - tData}ms · 월드/지형 +{tTerrain - tArt}ms · 아이콘 +{tIcons - tTerrain}ms · 합계 {clock.ElapsedMilliseconds}ms");
        }

        private T AddManagement<T>() where T : BaseManagement
        {
            var go = new GameObject(typeof(T).Name);

            if (_managementRoot != null)
                go.transform.SetParent(_managementRoot, false);

            return go.AddComponent<T>();
        }
    }
}
