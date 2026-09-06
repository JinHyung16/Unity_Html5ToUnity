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

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private void LateUpdate()
        {
            if (IsReady == false || _worldView == null)
                return;

            UndeadSimulation simulation = UndeadGameRoot.Instance.Game.Simulation;

            // ★ 지형·카메라는 «카메라 피벗»을 따라간다 — 히어로가 아니다.
            //   원본 카메라는 히어로를 «뒤따르므로» 히어로를 그대로 쓰면 지형이 한 프레임 앞서 흐른다.
            UndeadVec2 pivot = simulation.CameraPivot;

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
            const string sheet = "biome_graveyard_tiles";
            const int cols = UndeadTerrainView.TilesetColumns;
            const int rows = 1;

            var tiles = new System.Collections.Generic.List<UnityEngine.Tilemaps.TileBase>(cols * rows);
            var addresses = new System.Collections.Generic.List<string>(cols * rows);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                    addresses.Add($"{sheet}[{sheet}_{row}_{col}]");
            }

            Sprite[] sprites = await ArtLoader.LoadSpritesAsync(addresses);   // 25칸을 «한꺼번에»

            if (cancellationToken.IsCancellationRequested)
                return;

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                tile.sprite = sprites[i];
                tiles.Add(tile);
            }

            if (tiles.Count == 0)
            {
                Log.Error("지형 타일을 하나도 못 읽었다 — 임포터의 격자 자르기를 본다");
                return;
            }

            _terrainView.Bind(_terrainView.GetComponent<UnityEngine.Tilemaps.Tilemap>(), tiles.ToArray());
            _terrainView.Follow(UndeadVec2.Zero);
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
            Task<UndeadSpriteSet> warriorTask = UndeadSpriteSet.LoadAsync(art.Get("warrior_lay"));   // ★ 과제 목표 [실측 · 회차 8]
            Task<UndeadSpriteSet> bubbleTask = UndeadSpriteSet.LoadAsync(art.Get("bubble"));
            Task<UndeadSpriteSet> bossTask = UndeadSpriteSet.LoadAsync(art.Get("darksoul"));
            Task<UndeadSpriteSet> fireballTask = UndeadSpriteSet.LoadAsync(art.Get("fireball"));
            Task<UndeadSpriteSet> familyTask = UndeadSpriteSet.LoadAsync(art.Get("family"));
            Task<UndeadSpriteSet> farmerTask = UndeadSpriteSet.LoadAsync(art.Get("farmer"));
            Task<UndeadSpriteSet> sheepTask = UndeadSpriteSet.LoadAsync(art.Get("sheep"));
            Task<UndeadSpriteSet> treeTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree"));
            Task<UndeadSpriteSet> treeOnTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree_activated"));
            Task<UndeadSpriteSet> treeOffTask = UndeadSpriteSet.LoadAsync(art.Get("graveyard_evil_tree_inactive"));
            Task<UndeadSpriteSet> meteorTask = UndeadSpriteSet.LoadAsync(art.Get("meteor"));
            Task<UndeadSpriteSet> fireTask = UndeadSpriteSet.LoadAsync(art.Get("fire"));
            Task<UndeadSpriteSet> fireOffTask = UndeadSpriteSet.LoadAsync(art.Get("fire_inactive"));
            Task<UndeadSpriteSet> heartTask = UndeadSpriteSet.LoadAsync(art.Get("heart"));
            Task<UndeadSpriteSet> lightningTask = UndeadSpriteSet.LoadAsync(art.Get("lightning"));
            Task<UndeadSpriteSet> hpTask = UndeadSpriteSet.LoadAsync(art.Get("hp_segment"));
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
                root.Game.MoveInputSource = _moveInput.Read;

            // ⑤ 월드 뷰 — 개체당 컴포넌트 0. 풀링된 SpriteRenderer 만 쓴다 (확정표 G).
            if (_worldView != null)
            {
                _worldView.Bind(root.Game.Simulation, hero, enemySets, projectile, gem);

                // ⚠ 말풍선 문구는 «문구 표»에서 온다 — 화면에 보이는 한국어를 코드에 박지 않는다.
                _worldView.BindBossQuest(await bossTask, await fireballTask, await familyTask, await farmerTask, await sheepTask);

                // 지형이 놓는 나무·모닥불과 나무의 유성 [소스 — 청크 노이즈]
                _worldView.BindWorldObjects(await treeTask, await treeOnTask, await treeOffTask, await meteorTask,
                                            await fireTask, await fireOffTask, await heartTask);

                _worldView.BindLightning(await lightningTask);
                _worldView.BindLifeBar(await hpTask, GameRoot.Instance.UndeadConfigDataContainer.Config.HeroMaxHp);

                _worldView.BindQuestTarget(warrior, bubble, await mageTask, await fragmentTask,
                                           GameRoot.Instance.UndeadTextDataContainer.Ko("helpMe"),
                                           UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Font/UndeadSlayer SDF"));
            }

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
