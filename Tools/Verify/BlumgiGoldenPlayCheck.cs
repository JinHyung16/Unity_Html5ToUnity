using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BlumgiVerify;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 골든 벡터 채점기 — <b>재생 모드에서 «엔진 물리로» 돌린다</b>.
    ///
    /// <para>
    /// ★★ <b>왜 헤드리스에서 옮겨왔나.</b> 확정 G(자체 적분기)가 4회차 실측으로 폐기되고
    /// 유니티 2D 물리(Box2D)로 갈렸다 — 엔진 물리는 <c>dotnet run</c> 으로 못 돈다.
    /// 채점 비용이 올라가지만 부수 효과로 <b>채점이 「실제 경로」를 지나게 된다</b>.
    /// </para>
    ///
    /// <para>
    /// ★ <b>사람이 쓰던 에디터를 끄지 않는다.</b> 다리(<c>EditorCommandBridge</c>)가 시킨 실행이면
    /// <c>EditorApplication.Exit</c> 대신 <c>ExitPlaymode</c> 로 끝낸다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>씬을 «새로 만들지» 않는다.</b> 사람이 열어 둔 씬을 갈아엎지 않기 위해
    /// <b>전용 로컬 물리 씬</b>(<see cref="LocalPhysicsMode.Physics2D"/>)을 하나 만들어 거기서만 돌린다 —
    /// 다른 씬의 콜라이더가 섞이지 않는다는 이점도 같이 온다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이관이 끝나면 이 폴더째로 지운다</b> (CLAUDE.md). 정답지는 <c>07_기대값.md</c> 의 표이고
    /// 채점기는 그 표를 읽어 주는 일회용이다.
    /// </para>
    ///
    /// <para>
    /// 실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiGoldenPlayCheck.RunAll</c>
    /// (에디터가 꺼져 있으면 <c>PLAYMODE=1</c> 을 붙인다)
    /// </para>
    /// </summary>
    public static class BlumgiGoldenPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiGoldenPlayCheck.Armed";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);

            // ⚠ 씬을 새로 열지 않는다 — 사람이 열어 둔 씬이 통째로 사라진다.
            //   검사는 재생 뒤에 «전용 로컬 물리 씬»을 만들어 거기서만 돈다.
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

            var go = new GameObject("BlumgiGoldenProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiGoldenProbe>();
#endif
        }
    }

    /// <summary>
    /// 실제 채점기. <b>한 프레임 안에서 전부 돈다</b> —
    /// 로컬 물리 씬을 <c>Simulate</c> 로 «직접» 밀기 때문에 프레임을 기다릴 필요가 없다.
    /// (재발방지: 화면 없는 재생은 초당 수천 프레임이라 «프레임 수로 기다리면» 시간 감각이 무너진다.)
    /// </summary>
    public sealed class BlumgiGoldenProbe : MonoBehaviour
    {
        private const string DataFolderUnderAssets = "Blumgi-Bounce/Data";
        private const string PrefabFolder = "Assets/Blumgi-Bounce/Prefabs";
        private const string ReportPath = "Temp/BlumgiGoldenReport.txt";

        // ── 채점 규칙 (`07 §6` · 패스 ①-b 그대로 유지)
        private const double MechanismSpeedTolerance = 0.015;   // 3회차 반복 실측 산포 ±1.3% 의 바로 위
        private const double AngleToleranceDeg = 0.5;           // 같은 입력 실측 편차 ±0.42° (W1L5)
        private const double LaunchPosTolerancePx = 5.0;        // §5-0 「같은 레벨 전 시행이 ±5px 안」

        // ★ 발사점 «함수» 정답지 전용 — 여기만 조인다 (`07 §8-5-b` · §13-a).
        //   ★ [14회차-b] 우리 절편(BallRest)이 «실측 bbox 중심 그대로»가 됐다 — 6회차 0.5 반올림이 사라졌다.
        //     남은 오차는 14회차 모델 잔차 ≤0.066px 뿐이다. ⚠ 그래도 «점수가 좋아지도록» 조이지 않는다 —
        //     허용치를 바꾸는 것은 값을 고르는 것과 같다 (#48). 1.0px 그대로 둔다.
        private const double LaunchModelTolerancePx = 1.0;

        /// <summary>
        /// 발사 각속도 허용오차(rad/s). 원본은 13·14회차 누적 <b>36샷 표준편차 0</b> 이고
        /// 우리는 한 스텝 각감쇠(0.01)가 이미 걸린 값을 읽으므로 그만큼(≈0.0003)의 위다.
        /// </summary>
        private const double LaunchOmegaToleranceRad = 0.01;

        /// <summary>첫 접촉 «직전» 각속도 허용오차(rad/s) — 원본은 각감쇠로만 −3.4907 → −3.432 로 준다.</summary>
        private const double FirstContactOmegaToleranceRad = 0.02;

        /// <summary>
        /// 첫 접촉 «시각» 허용오차(ms). <b>원본 접촉 검출이 rAF 프레임(7.4~9.1 ms) 단위</b>라
        /// 원리적으로 ±1 프레임이 뜨고, 우리 격자(8.33 ms)와의 위상차로 한 칸이 더 뜬다 ⇒ <b>±2 프레임</b>.
        /// </summary>
        private const double FirstContactTimeToleranceMs = 20.0;

        /// <summary>첫 접촉을 찾는 창(초) — 원본 채록이 6 s 였다. 「접촉 없음」의 정의가 이 창이다.</summary>
        private const double FirstContactWindowSeconds = 6.0;

        // ★ |v0| 계수의 «정체»를 가르는 눈금 — 옛 상수 20.95 는 정확값보다 0.37% 낮아 여기서 «걸린다».
        //   느슨하게 두면 틀린 상수가 되돌아와도 통과한다.
        private const double LaunchSpeedTolerance = 0.003;
        private const double PointRelTolerance = 0.02;          // ±2%
        private const double PointAbsTolerance = 10.0;          // 또는 ±10px 중 큰 값
        private const double ApexTimeToleranceMs = 50.0;

        /// <summary>감지기 좌표 허용오차(px). 7회차가 소수 둘째 자리까지 떴으므로 «거의 0»으로 잡는다.</summary>
        private const double DetectorTolerancePx = 0.01;

        /// <summary>한 발을 끝까지 굴리는 상한(초). 넘으면 «미클리어»로 닫는다.</summary>
        private const double ShotTimeLimitSeconds = 20.0;

        private readonly StringBuilder _report = new StringBuilder(1 << 16);
        private readonly List<string> _failLines = new List<string>();
        private readonly List<string> _contradictions = new List<string>();

        private int _pass;
        private int _fail;

        private int _clearPass;
        private int _clearTotal;

        private BlumgiConfigData _config;
        private Scene _scene;
        private PhysicsScene2D _physics;
        private BlumgiPhysicsWorld _world;
        private double _step;
        private string _builtLevel;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;

            bool ok = false;

            try
            {
                ok = Body();
            }
            catch (Exception e)
            {
                Line($"  ❌ 검사 중 예외: {e}");
            }

            Line(string.Empty);
            Line("========================================");
            Line($"골든 대조 결과: {_pass}/{_pass + _fail}");
            Line($"클리어 판정: {_clearPass}/{_clearTotal}");

            if (_failLines.Count > 0)
            {
                Line(string.Empty);
                Line("-- 못 맞춘 항목 --");

                for (int i = 0; i < _failLines.Count; i++)
                    Line("  " + _failLines[i]);
            }

            if (_contradictions.Count > 0)
            {
                Line(string.Empty);
                Line($"-- 정답지 «자기모순» {_contradictions.Count}건 (우리 결함이 아니다 · 원본 문서 쪽 문제) --");

                for (int i = 0; i < _contradictions.Count; i++)
                    Line("  " + _contradictions[i]);
            }

            WriteReport();

            string summary = $"═══ 골든 재생 채점 {_pass}/{_pass + _fail} 통과 · 클리어 {_clearPass}/{_clearTotal} " +
                             $"· 전문 {ReportPath} ═══";

            if (ok && _fail == 0)
                Log.Success(summary);
            else
                Log.Error(summary + $"  (실패 {_fail}건 · 모순 {_contradictions.Count}건)");

            yield return null;

            Finish(ok && _fail == 0);
        }

        private bool Body()
        {
            if (LoadData() == false)
                return false;

            CreateScene();

            Section("0. 물리 월드 설정 — 확정 E 개정 (1 유닛 = 1 m = 원본 50 px)");
            ScoreWorldSetup();

            Section("0-b. 프리팹에 구워진 콜라이더 — «격자와 다른» 크기가 실제로 들어갔나");
            ScorePrefabColliders();

            Section("0-c. ★ 반발 «혼합 규칙» 실측 — 엔진이 정말 max(e₁,e₂) 인가");
            ScoreRestitutionMixing();

            Section("0-d. ★★ 공의 질량·관성 — 9회차가 원본 Box2D 에서 «직접 읽은» 값과 대조");
            ScoreBallMassInertia();

            Section("0-d2. ★★★ «공 ↔ 발사대» 쌍 배제 — 엔진에서 되읽어 판정  [16회차 실측 반영]");
            ScoreLauncherPairExclusion();

            Section("0-d3. ★★★ 발사대 «정착 자리»는 «유도»된다 — 5레벨 전수  [20회차 실측 · 재발방지 #102]");
            ScoreLauncherRestPosition();

            Section("0-e. ★ 블록 밭의 기하 — 병합 금지 · 같은 행 윗면 높이 일치  [9회차 §2-a · §5-b]");
            ScoreBlockFieldGeometry();

            Section("0-f. ★★★ 접촉점 정답지 — 같은 (v_n, v_t, ω, n) 에서 우리 엔진이 같은 ω' 를 내는가  [9회차 35건]");
            ScoreContactGolden();

            Section("1. 데이터 게이트");
            ScoreDataGate();

            Section("2. 파워 기전 — |v0| = k · min(100, 10 + 55·홀드초)  [3회차 실측 · 축은 «우리 시계» 홀드]");
            Line("  ⚠ 이 절의 홀드는 3회차가 «벽시계»로 잰 값이다 — 게임 내부 값과 −18~+3ms 어긋난다 [7회차].");
            Line("     ★ [정정 · 12회차] k 는 20.95 가 «아니다» — 정확히 21.0272 = 1/(m·worldScale) 이고");
            Line("       상수가 아니라 «질량에서 유도»된다. 20.95 는 궤적 피팅 잔재로 0.37% 낮았다.");
            ScoreMechanism();

            Section("3. 발사 파라미터 — 조준각 · 발사 위치  [3회차 실측 + ★12회차 «발사점 = 홀드의 함수»]");
            ScoreLaunchParameters();

            Section("3-c. ★★★ 발사 프레임 정답지 — 14회차-b «4레벨 20샷»  [발사점 트윈이 «전역»인가]");
            ScoreLaunchFrames14b();

            Section("3-d. ★★★ 첫 접촉 «직전» 정답지 — 14회차-b 20점  [기준점 = «발사 프레임»]");
            ScoreFirstContacts();

            Section("4. 최고점  [3회차 실측 · 정본 · 자유비행에서만]");
            ScoreApexes();

            Section("5. 궤적  [1·2회차 · 정본 · ★ 첫 반발 «이전»에만 허용오차를 적용한다]");
            ScoreTrajectories();

            Section("5-b. ★ 골인 감지기 자리 — 7회차 W1L1~L5 «전수 실측»과 대조  [채점한다]");
            ReportDetectors();

            Section("6. 클리어 여부  [합격 기준 5 — 레벨당 ≥3점 · W1L5 만 실패下1+성공2 · ★ 입력 축 = force]");
            ScoreClears();

            Section("6-b. ★ 남은 실패 벡터의 «반발 지점» 추적 — 어디서 갈리는가  [채점 아님]");
            TraceRemainingFailures();

            Section("6-c. ★★ 쪼개짐 «지문» — 같은 블록 재접촉 · 접촉 구간  [9회차 §5-c 전수 0건]");
            ScoreSplitFingerprint();

            Section("7. 정답지 «자기모순» 리포트 — 1·2회차 |v0| 컬럼 vs 3회차 기전  [채점 아님]");
            ReportLegacySpeeds();

            return true;
        }

        // ────────────────────────────────────────────────────────── 준비

        private bool LoadData()
        {
            string dataDir = Path.Combine(Application.dataPath, DataFolderUnderAssets);

            if (Directory.Exists(dataDir) == false)
            {
                Line($"데이터 폴더가 없다: {dataDir}");
                return false;
            }

            BlumgiContainerRegister.RegisterAll();

            var map = new Dictionary<string, string>();

            foreach (string path in Directory.GetFiles(dataDir, "*.json"))
                map[Path.GetFileNameWithoutExtension(path)] = File.ReadAllText(path);

            Line($"JSON {map.Count}개 적재 — {dataDir}");

            DataManager.Instance.InitializeFromJson(map);
            _config = GameRoot.Instance.BlumgiConfigDataContainer?.Config;

            if (_config == null)
            {
                Line("설정 행(Id 1)을 못 읽었다");
                return false;
            }

            _step = _config.PhysicsStepSeconds;
            BlumgiPhysicsSetup.Apply(_config);
            return true;
        }

        /// <summary>
        /// 전용 «로컬 물리 씬». 사람이 열어 둔 씬을 건드리지 않고, 다른 씬의 콜라이더도 섞이지 않는다.
        /// 로컬 물리 씬은 <b>자동으로 돌지 않으므로</b> 우리가 <c>Simulate</c> 로 민다 — 그래서 결정적이다.
        /// </summary>
        private void CreateScene()
        {
            _scene = SceneManager.CreateScene("BlumgiGolden" + (_sceneSerial++).ToString(CultureInfo.InvariantCulture),
                                              new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physics = _scene.GetPhysicsScene2D();

            var worldGo = new GameObject("BlumgiPhysicsWorld");
            SceneManager.MoveGameObjectToScene(worldGo, _scene);
            _world = worldGo.AddComponent<BlumgiPhysicsWorld>();
        }

        private int _sceneSerial;

        /// <summary>
        /// ★★★★ <b>레벨을 다시 세울 때는 물리 «월드»째로 새로 만든다</b> — 오브젝트만 헐면 부족하다.
        ///
        /// <para>
        /// <b>[실측 · 패스 ①-q · 재발방지 #106]</b> <c>Teardown</c> → <c>Build</c> 만 하면 <b>같은 입력이
        /// 다른 답</b>을 낸다 — <c>W1L3 21.4565</c> 5회 연속이 골인/미골인/골인/미골인/골인 이었다.
        /// Box2D 가 지운 바디의 프록시·블록을 <b>자유목록에서 역순으로</b> 재사용해 바디 순서가 바뀌고,
        /// 솔버 누적 임펄스의 더하는 순서가 바뀌어(부동소수 비결합) 접촉 여섯 번째쯤에서 사슬이 갈린다.
        /// <b>새 씬 = 새 <c>b2World</c></b> 라 그 자유목록이 처음부터다 — 60발을 쏴도 «전부 같다» [실측].
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>점수를 보고 고른 것이 아니다</b> (#48) — 「같은 입력에 같은 답이 나오는가」로 골랐다.
        /// </para>
        /// </summary>
        private void RecreateScene()
        {
            if (_world != null)
            {
                _world.Teardown();
                DestroyImmediate(_world.gameObject);
                _world = null;
            }

            if (_scene.IsValid())
                SceneManager.UnloadSceneAsync(_scene);

            CreateScene();
            _builtLevel = null;
        }

        private static GameObject LoadPrefab(string prefabName)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{prefabName}.prefab");
#else
            return null;
#endif
        }

        // ────────────────────────────────────────────────────────── 0. 물리 세팅

        private void ScoreWorldSetup()
        {
            string audit = BlumgiPhysicsSetup.Audit(_config);
            Check("전역 물리 설정이 데이터대로 들어갔다", audit.Length == 0, audit);

            CheckAbs("중력 (유닛/s²) = 원본 1500 px/s² ÷ 50", -Physics2D.gravity.y, 30.0, 1e-4, "");
            CheckAbs("스프라이트 PPU", BlumgiUnits.WorldPixelsPerUnit, 50.0, 1e-6, "");
            CheckAbs("카메라 직교 크기 (1280 px ÷ 2 ÷ 50)", BlumgiUnits.CameraOrthographicSize, 12.8, 1e-4, "");

            // ★ 스케일을 맞춘 «이유» 그 자체를 검사한다 —
            //   반발이 죽는 속도 임계가 원본 눈금으로 50 px/s 여야 원본과 «같은 지점»에서 안 튀기 시작한다.
            CheckAbs("반발 임계가 원본 눈금으로 (Box2D 1 m/s)",
                     BlumgiUnits.ToWorldLength(Physics2D.bounceThreshold), _config.BounceThresholdSpeed, 1e-6, "px/s");

            CheckAbs("접촉 오프셋이 원본 폴리곤 스킨과 같다 (0.01 m = 0.5 px)",
                     BlumgiUnits.ToWorldLength(Physics2D.defaultContactOffset), 0.5, 1e-6, "px");
        }

        // ────────────────────────────────────────────────────────── 0-b. 프리팹 콜라이더

        private void ScorePrefabColliders()
        {
            GameObject ballPrefab = LoadPrefab(BlumgiPhysicsWorld.BallPrefabName);
            GameObject blockPrefab = LoadPrefab(BlumgiPhysicsWorld.BlockPrefabName);
            GameObject hoopPrefab = LoadPrefab(BlumgiPhysicsWorld.HoopPrefabName);
            GameObject blobPrefab = LoadPrefab(BlumgiPhysicsWorld.BlobPrefabName);

            if (ballPrefab == null || blockPrefab == null || hoopPrefab == null || blobPrefab == null)
            {
                Fail("프리팹을 못 읽었다 — BlumgiPrefabBuilder.BuildAll 을 먼저 돌린다");
                return;
            }

            var ball = ballPrefab.GetComponent<BlumgiBallBody>();
            Check($"공에 {nameof(BlumgiBallBody)} 가 있다", ball != null, "없다");

            var ballCircle = ballPrefab.GetComponent<CircleCollider2D>();
            var ballBody = ballPrefab.GetComponent<Rigidbody2D>();

            if (ballCircle != null)
            {
                CheckAbs("공 콜라이더 반지름", BlumgiUnits.ToWorldLength(ballCircle.radius), _config.BallRadius, 0.01, "px");
                CheckAbs("공 밀도", ballCircle.density, _config.BallDensity, 1e-4, "");
                CheckMaterial("공", ballCircle.sharedMaterial, _config.BallRestitution, _config.BallFriction);
            }
            else
            {
                Fail("공에 CircleCollider2D 가 없다");
            }

            if (ballBody != null)
            {
                Check("공은 회전한다 (preventRotation = false)", ballBody.freezeRotation == false, "회전이 잠겨 있다");
                CheckAbs("공 각감쇠", ballBody.angularDamping, _config.BallAngularDamping, 1e-5, "");
                // ★★ [9회차 정정] 원본은 «월드» CCD 가 켜져 있다 (`world.GetContinuousPhysics() = true`).
                //    Box2D 에서 그 값이 참이면 총알이 아닌 동적 바디도 «정적 바디»에는 CCD 가 걸린다 —
                //    블록·림이 전부 정적이라 원본 공은 CCD 로 막힌다. 유니티는 월드 스위치가 없어
                //    바디 단위 Continuous 가 그 자리다. Discrete 는 «원본에 없는 파고듦»을 만든다.
                Check("공에 연속 충돌 검출이 걸려 있다 — 원본 world CCD = true [9회차 §1-d]",
                      ballBody.collisionDetectionMode == CollisionDetectionMode2D.Continuous,
                      ballBody.collisionDetectionMode.ToString());
            }
            else
            {
                Fail("공에 Rigidbody2D 가 없다");
            }

            var box = blockPrefab.GetComponent<BoxCollider2D>();

            if (box != null)
            {
                double width = BlumgiUnits.ToWorldLength(box.size.x);
                double height = BlumgiUnits.ToWorldLength(box.size.y);

                CheckAbs("블록 콜라이더 폭", width, _config.BlockCollisionWidth, 0.01, "px");
                CheckAbs("블록 콜라이더 높이", height, _config.BlockCollisionHeight, 0.01, "px");

                // ★★ 이 검사가 이번 패스의 핵심이다 — 「격자 크기로 깔면 공이 샌다」.
                Check($"블록 콜라이더가 격자 피치({_config.BlockGridPitch})보다 «크다» — 이웃끼리 겹치는 것이 원본이다",
                      width > _config.BlockGridPitch && height > _config.BlockGridPitch,
                      $"{width:0.###} × {height:0.###}");

                CheckMaterial("블록", box.sharedMaterial, _config.BlockRestitution, _config.BlockFriction);
                Check("블록은 정적이다 (Rigidbody2D 없음)", blockPrefab.GetComponent<Rigidbody2D>() == null, "동적이다");
            }
            else
            {
                Fail("블록에 BoxCollider2D 가 없다");
            }

            var posts = hoopPrefab.GetComponents<CircleCollider2D>();
            Check("골대 판정체가 2개다 (spr_BasketCollision ×2)", posts.Length == 2, $"{posts.Length}개");

            for (int i = 0; i < posts.Length; i++)
            {
                CheckAbs($"골대 판정체{i} 반지름", BlumgiUnits.ToWorldLength(posts[i].radius), _config.HoopPostRadius, 0.01, "px");
                CheckMaterial($"골대 판정체{i}", posts[i].sharedMaterial, _config.HoopRestitution, _config.HoopFriction);

                // ★ 7회차 실측 — 기둥은 골대 중심에서 ±(75, 0). «5레벨 전부 같다» 가 확정이므로
                //   프리팹이 그 오프셋을 실제로 들고 있는지 본다 (데이터에만 있고 프리팹이 다르면 조용히 갈린다).
                CheckAbs($"골대 판정체{i} x 오프셋 |75|",
                         Math.Abs(BlumgiUnits.ToWorldLength(posts[i].offset.x)), _config.GoalPostOffsetX, 0.02, "px");
                CheckAbs($"골대 판정체{i} y 오프셋",
                         -BlumgiUnits.ToWorldLength(posts[i].offset.y), _config.GoalPostOffsetY, 0.02, "px");
            }

            Transform net = hoopPrefab.transform.Find("Net");

            if (net != null)
            {
                // 그물 그림은 골대 중심 +(0, 88) [7회차 실측 5/5]. y 부호는 ToPosition 이 뒤집는다.
                CheckAbs("골대 그물 y 오프셋 (+88)",
                         -BlumgiUnits.ToWorldLength(net.localPosition.y), _config.GoalNetOffsetY, 0.02, "px");
                CheckAbs("골대 그물 x 오프셋 (0)",
                         BlumgiUnits.ToWorldLength(net.localPosition.x), _config.GoalNetOffsetX, 0.02, "px");
            }
            else
            {
                Fail("골대 프리팹에 Net 자식이 없다");
            }

            var blobBox = blobPrefab.GetComponent<BoxCollider2D>();
            var blobBody = blobPrefab.GetComponent<Rigidbody2D>();

            if (blobBox != null)
            {
                CheckAbs("블롭 콜라이더 폭", BlumgiUnits.ToWorldLength(blobBox.size.x), _config.BlobCollisionWidth, 0.01, "px");
                CheckAbs("블롭 콜라이더 높이", BlumgiUnits.ToWorldLength(blobBox.size.y), _config.BlobCollisionHeight, 0.01, "px");
                CheckAbs("블롭 밀도", blobBox.density, _config.BlobDensity, 1e-3, "");
                CheckMaterial("블롭", blobBox.sharedMaterial, _config.BlobRestitution, _config.BlobFriction);

                // ★★ [16회차 실측] Box2D body 원점이 «bbox 아래변 중앙»이다 —
                //    상자는 원점보다 «높이/2» 만큼 «위»에 있어야 한다. 오프셋 0(= 중심 정렬)이면
                //    데이터의 y 가 «중심»을 뜻하게 되어 기전이 갈린다 (재발방지 #53).
                CheckAbs("블롭 콜라이더 x 오프셋 (0 — 좌우 대칭)",
                         BlumgiUnits.ToWorldLength(blobBox.offset.x), 0.0, 0.01, "px");
                CheckAbs("블롭 콜라이더 y 오프셋 = −높이/2 (body 원점이 bbox «아래변»이라 상자가 위로 선다)",
                         -BlumgiUnits.ToWorldLength(blobBox.offset.y), -_config.BlobCollisionHeight * 0.5, 0.01, "px");

                // ★ 16회차 fixture AABB 88.136 × 51.005 = 상자 + 폴리곤 스킨 0.5 px 사방 [실측].
                Line($"  [실측·원본] 블롭 fixture AABB 88.136 × 51.005 = 상자 " +
                     $"{_config.BlobCollisionWidth:0.###} × {_config.BlobCollisionHeight:0.###} + 스킨 0.5 px 사방 " +
                     $"[16회차 · 4레벨 전부 동일]");
            }
            else
            {
                Fail("블롭에 BoxCollider2D 가 없다");
            }

            if (blobBody != null)
            {
                Check("블롭은 회전이 잠겨 있다 (preventRotation = true)", blobBody.freezeRotation, "안 잠겨 있다");
                Check("블롭도 연속 충돌 검출이다 — 월드 CCD 는 바디를 가리지 않는다",
                      blobBody.collisionDetectionMode == CollisionDetectionMode2D.Continuous,
                      blobBody.collisionDetectionMode.ToString());
            }
            else
            {
                Fail("블롭에 Rigidbody2D 가 없다");
            }
        }

        /// <summary>
        /// ★★ <b>질량·관성을 «유도값»이 아니라 «직접 읽은 값»과 댄다.</b>
        ///
        /// <para>
        /// 9회차가 원본 Box2D 바디에서 <c>GetMass()</c> · <c>GetInertia()</c> 를 그대로 읽었다 —
        /// <b>2.377871513366699 kg</b> · <b>0.8999055027961731 kg·m²</b> (= 정확히 <c>½mR²</c>, 균일 원판).
        /// 우리는 «밀도 1 + 반지름 0.87 유닛 + auto-mass» 로 그 값이 «나오게» 해 두었을 뿐이라,
        /// <b>정말 나왔는지는 엔진에서 되읽어야 안다</b> — 프리팹 인스펙터에는 auto-mass 결과가 안 적힌다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 관성이 어긋나면 반발 «횟수»가 아니라 <b>ω 가 통째로 갈린다</b> (9회차 §2-d #4) —
        /// 그러면 §8 의 접촉점 정답지가 전부 어긋난다. 그래서 이 절이 §8 «앞»에 있다.
        /// </para>
        /// </summary>
        private void ScoreBallMassInertia()
        {
            if (EnsureLevel("W1L1") == null || _world.Ball == null || _world.Ball.Body == null)
            {
                Fail("공 바디를 못 세웠다 — 질량·관성을 못 잰다");
                return;
            }

            Rigidbody2D body = _world.Ball.Body;

            double radiusUnits = _config.BallRadius / BlumgiUnits.WorldPixelsPerUnit;
            double expectedMass = _config.BallDensity * Math.PI * radiusUnits * radiusUnits;
            double expectedInertia = 0.5 * expectedMass * radiusUnits * radiusUnits;

            Line($"  [실측·우리] m = {body.mass:0.000000} kg · I = {body.inertia:0.000000} kg·m²");
            Line($"  [실측·원본] m = 2.377871513366699 kg · I = 0.8999055027961731 kg·m²  [9회차 §1-d 직접 읽음]");

            CheckRel("공 질량 = 원본 직접 읽은 2.377872 kg", body.mass, 2.377871513366699, 1e-4);
            CheckRel("공 관성 = 원본 직접 읽은 0.899906 kg·m²", body.inertia, 0.8999055027961731, 1e-4);
            CheckRel("공 관성이 «균일 원판» ½mR² 다 (회전 잠금·질량중심 오프셋이 없다는 뜻)",
                     body.inertia, expectedInertia, 1e-4);

            // 우리 데이터로 «계산한» 값과 원본 «직접 읽은» 값이 같은지도 본다 — 데이터가 흔들리면 여기서 먼저 터진다.
            CheckRel("우리 데이터(밀도 1 · r 43.5px)가 원본 질량을 «낸다»", expectedMass, 2.377871513366699, 1e-4);

            ScoreBlobMass();
        }

        /// <summary>
        /// ★★ <b>블롭 질량도 «되읽어» 댄다</b> [16회차 실측 — 원본 <c>GetMass() = 174.272</c>].
        ///
        /// <para>
        /// 이 값은 <b>폭이 맞는지</b>의 검산이기도 하다 — 밀도 100 × (폭/50 m × 높이/50 m) 이므로
        /// 4회차의 반올림 <c>87.14</c> 로는 <b>174.28</b> 이 나오고 시트 원문 <c>87.13590823980422</c> 로는
        /// <b>174.2718</b> 이 나온다. 원본은 후자다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>관성(원본 43.568)은 채점하지 않는다 — 회전이 잠겨 있어 «쓰이지 않는» 값</b>이라
        /// 엔진마다 계산 규약이 다를 수 있다. 「안 쓰이는 값을 맞추려고 쓰이는 값을 흔드는 것」이
        /// 재발방지 #48 의 전형이다. <b>읽어서 찍기만</b> 한다.
        /// </para>
        /// </summary>
        private void ScoreBlobMass()
        {
            if (_world.Blob == null)
            {
                Fail("블롭 바디를 못 세웠다 — 질량을 못 잰다");
                return;
            }

            Rigidbody2D blob = _world.Blob;

            double widthUnits = _config.BlobCollisionWidth / BlumgiUnits.WorldPixelsPerUnit;
            double heightUnits = _config.BlobCollisionHeight / BlumgiUnits.WorldPixelsPerUnit;
            double expectedMass = _config.BlobDensity * widthUnits * heightUnits;

            Line($"  [실측·우리] 블롭 m = {blob.mass:0.000000} kg · I = {blob.inertia:0.000000} kg·m² (회전 잠금 — I 는 안 쓰인다)");
            Line("  [실측·원본] 블롭 m = 174.272 kg · I = 43.568 kg·m²  [16회차 · 4레벨 전부 동일]");

            CheckRel("블롭 질량 = 원본 직접 읽은 174.272 kg", blob.mass, 174.272, 1e-4);
            CheckRel("우리 데이터(밀도 100 · 87.1359 × 50 px)가 원본 질량을 «낸다»", expectedMass, 174.272, 1e-4);
        }

        /// <summary>
        /// ★★★ <b>«공 ↔ 발사대 블롭» 쌍만 충돌에서 빠졌는지 «되읽어» 잰다</b> [16회차 실측 · 재발방지 #78].
        ///
        /// <para>
        /// <b>원본이 낸 사실</b> — 공 중심이 블롭 콜라이더 한가운데인 프레임에 <c>b2Contact</c> 가
        /// «닿은 것도 안 닿은 broadphase 쌍도» <b>0 건</b>인데, <b>같은 프레임에 블롭은 깔고 앉은 블록
        /// 3개와 접촉 중</b>이다. ⇒ 빠지는 것은 <b>«쌍 하나»</b>이지 «블롭»이 아니다.
        /// </para>
        ///
        /// <para>
        /// 그래서 <b>세 항목을 «같이»</b> 본다 — 하나만 보면 「블롭을 통째로 껐다」와 구분되지 않는다.
        /// 그리고 <b>시뮬 재진입 경로까지 실제로 태운다</b>(재발방지 #77) — 골인하면 공은
        /// <c>Freeze</c> 로 <c>simulated = false</c> 가 되었다가 <c>Park</c> 로 돌아오는데,
        /// 그 자리에서 Box2D fixture 가 다시 만들어져 <b>배제가 조용히 지워질 수 있다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>배제 «기전»은 미측정이다</b> — 원본이 어느 값으로 그 쌍을 거르는지는 못 읽었다.
        /// 우리가 재현하는 것은 <b>«결과»</b>다.
        /// </para>
        /// </summary>
        /// <summary>
        /// ★★★ <b>발사대 y 는 «레벨 데이터»가 아니라 «물리가 앉힌 자리»다</b>
        /// [20회차 실측 · 5레벨 전수 · 재발방지 #102 · #53].
        ///
        /// <para>
        /// 16~19회차는 정착 위치를 레벨 표에 다섯 벌 적어 두었고, <b>못 잰 W1L5 한 칸을 반올림
        /// <c>345.0</c> 으로 채워 0.241 px 어긋나 있었다.</b> 20회차가 다섯 레벨의 잔차를 닫아
        /// <b>「유도값」</b>임을 확증했고, 이관은 <b>표에서 빼고 기전을 옮겼다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ 여기서 <b>「유도가 맞는가」와 「엔진에 실제로 들어갔는가」를 «따로» 센다</b> (재발방지 #55) —
        /// 유도만 맞고 스폰이 옛 값을 쓰면 데이터는 통과하고 화면만 어긋난다.
        /// </para>
        /// </summary>
        private void ScoreLauncherRestPosition()
        {
            // [실측 20회차 · gap20/dump_N.json] 레벨 → (블록 행 y, b2Body 원점 y)
            string[] codes = { "W1L1", "W1L2", "W1L3", "W1L4", "W1L5" };
            double[] rows = { 627.0, 975.0, 375.0, 425.0, 375.0 };
            double[] bodyY = { 596.759, 944.759, 344.759, 394.759, 344.759 };
            double[] bodyX = { 150.0, 225.0, 1000.0, 1050.0, 100.0 };

            // 원본 실측이 소수 3자리다 — 그 아래는 «분해할 수 있는 값»이 아니다.
            // ⚠ 이 허용치를 «넓혀» 통과시키지 마라 (#48). 넓히는 근거는 새 실측뿐이다.
            const double Tolerance = 0.01;

            Line($"     허용치 {Tolerance:0.###} px — 원본 실측 자체가 소수 3자리다 (넓히지 마라 · #48)");

            for (int i = 0; i < codes.Length; i++)
            {
                BlumgiLevelRuntime runtime = EnsureLevel(codes[i]);

                if (runtime == null)
                {
                    Fail($"{codes[i]}: 레벨을 못 세웠다 — 발사대 정착 자리를 판정할 수 없다");
                    continue;
                }

                CheckAbs($"{codes[i]} 발사대가 깔고 앉은 «블록 행 y»", runtime.LauncherSupportRowY, rows[i], 1e-9, "px");

                CheckAbs($"{codes[i]} 발사대 body x  (= authored iv[0] · 수평 접촉이 없어 안 움직인다)",
                         runtime.LauncherBodyPosition.X, bodyX[i], Tolerance, "px");

                CheckAbs($"{codes[i]} 발사대 body y  «유도» = 행y − 블록반높이 − 정착간격",
                         runtime.LauncherBodyPosition.Y, bodyY[i], Tolerance, "px");

                // ★ #55 — 「유도했다」가 아니라 «엔진에 들어갔다»를 본다. 스폰된 강체를 되읽는다.
                if (_world == null || _world.Blob == null)
                {
                    Fail($"{codes[i]}: 블롭 강체를 못 잡았다 — 스폰 위치를 되읽을 수 없다");
                    continue;
                }

                BlumgiUnits.ToWorld(_world.Blob.transform.position, out double gotX, out double gotY);

                CheckAbs($"{codes[i]} 스폰된 블롭을 «되읽은» body y  [#55 — 넣었다가 아니라 들어갔다]",
                         gotY, bodyY[i], Tolerance, "px");
            }

            Line("     ⇒ 레벨 표에는 이제 authored LauncherHomeY (602 · 950 · 350 · 400 · 350) 만 남았다.");
            Line("       ★ W1L1 의 행 627(= 625 + 2)이 «발사대 하나»로 원본이상 #3 을 독립 재확증한다 —");
            Line("         다른 넷은 전부 25 배수인데 L1 만 627 이고 발사대 y 596.759 가 그 +2 를 물고 있다.");
        }

        private void ScoreLauncherPairExclusion()
        {
            BlumgiLevelRuntime runtime = EnsureLevel("W1L3");

            if (runtime == null || _world.Ball == null || _world.Ball.Collider == null || _world.BlobCollider == null)
            {
                Fail("공/블롭 콜라이더를 못 잡았다 — 쌍 배제를 판정할 수 없다");
                return;
            }

            Collider2D ball = _world.Ball.Collider;
            Collider2D blob = _world.BlobCollider;
            Collider2D block = FirstBlockCollider();

            if (block == null)
            {
                Fail("블록 콜라이더를 못 잡았다 — 대조군이 없다");
                return;
            }

            Line($"  대조군 — 세워진 블록 {_world.BlockCount}개 중 첫 번째");

            Check("① 공 ↔ 발사대 블롭 : «배제»돼 있다  [원본 16회차 — 쌍이 만들어지지도 않는다]",
                  Physics2D.GetIgnoreCollision(ball, blob), "배제되지 않았다");

            Check("② 발사대 블롭 ↔ 블록 : «살아 있다»  [원본 — 블롭은 깔고 앉은 블록 3개와 접촉 중]",
                  Physics2D.GetIgnoreCollision(blob, block) == false,
                  "블롭이 블록과도 안 부딪힌다 — 쌍이 아니라 블롭을 통째로 껐다는 뜻이다");

            Check("③ 공 ↔ 블록 : «살아 있다»  [원본 — 관통 구간의 접촉 상대가 전부 블록이다]",
                  Physics2D.GetIgnoreCollision(ball, block) == false, "공이 블록과 안 부딪힌다");

            // ★★ #77 — 「고쳤다」가 아니라 «발동한다»를 본다. 시뮬에서 빠졌다 돌아오는 경로를 실제로 태운다.
            _world.Ball.Freeze();
            _world.Ball.Park(BlumgiUnits.ToVector(runtime.BallParkPosition.X, runtime.BallParkPosition.Y));

            Check("④ Freeze → Park (시뮬 «재진입») 뒤에도 공 ↔ 블롭 배제가 남아 있다",
                  Physics2D.GetIgnoreCollision(ball, blob),
                  "재진입에서 배제가 지워졌다 — 다시 세우는 자리가 빠졌다");

            _world.Ball.Launch(BlumgiUnits.ToVector(runtime.LauncherBodyPosition.X, runtime.LauncherBodyPosition.Y),
                               Vector2.zero, 0f);

            Check("⑤ Launch (시뮬 «재진입») 뒤에도 공 ↔ 블롭 배제가 남아 있다",
                  Physics2D.GetIgnoreCollision(ball, blob), "발사에서 배제가 지워졌다");

            Check("⑥ 그 뒤에도 블롭 ↔ 블록은 살아 있다 — 배제가 «번지지» 않았다",
                  Physics2D.GetIgnoreCollision(blob, block) == false, "블롭이 블록과 안 부딪히게 됐다");

            // 다음 절이 쓰는 상태를 어지럽히지 않는다 — 대기 자리로 되돌린다.
            _world.Ball.Park(BlumgiUnits.ToVector(runtime.BallParkPosition.X, runtime.BallParkPosition.Y));
        }

        /// <summary>세워진 블록 중 첫 번째의 콜라이더 — 배제가 «번졌는지»를 보는 대조군이다.</summary>
        private Collider2D FirstBlockCollider()
        {
            if (_world == null)
                return null;

            Transform blocks = _world.transform.Find("Blocks");

            if (blocks == null || blocks.childCount == 0)
                return null;

            return blocks.GetChild(0).GetComponent<Collider2D>();
        }

        private void CheckMaterial(string what, PhysicsMaterial2D material, double bounciness, double friction)
        {
            if (material == null)
            {
                Fail($"{what} 물리 머티리얼이 비었다 — 반발·마찰이 프로젝트 기본값으로 돈다");
                return;
            }

            CheckAbs($"{what} 반발 (머티리얼)", material.bounciness, bounciness, 1e-5, "");
            CheckAbs($"{what} 마찰 (머티리얼)", material.friction, friction, 1e-5, "");
        }

        // ────────────────────────────────────────────────────────── 0-c. 반발 혼합 규칙

        /// <summary>
        /// ★★ <b>혼합 규칙을 «가정»하지 않고 잰다.</b>
        ///
        /// <para>
        /// 4회차가 원본에서 실측한 사실 — 공 0.7 · 블록 0 인데 <b>실효 반발이 0.70</b> — 은
        /// Box2D 표준 <c>max(e₁, e₂)</c> 를 함의한다. 유니티 2D 도 Box2D 지만 <b>그렇다고 «믿고»
        /// 넘어가면, 아니었을 때 조용히 틀린다.</b> 그래서 실제 프리팹 두 개를 부딪혀 본다.
        /// </para>
        ///
        /// <para>
        /// 만약 <c>max</c> 가 아니라고 나오면 <b>머티리얼로 실효값을 맞춰야 한다</b> —
        /// 그때 무엇을 얼마나 고칠지 알 수 있도록 평균·곱 값도 같이 찍는다.
        /// </para>
        /// </summary>
        private void ScoreRestitutionMixing()
        {
            double measured = MeasureNormalRestitution();

            if (double.IsNaN(measured))
            {
                Fail("반발 혼합 측정 실패 — 낙하 시험에서 충돌을 못 잡았다");
                return;
            }

            double max = Math.Max(_config.BallRestitution, _config.BlockRestitution);
            double average = (_config.BallRestitution + _config.BlockRestitution) * 0.5;
            double product = _config.BallRestitution * _config.BlockRestitution;

            Line($"  [실측] 공(e={_config.BallRestitution}) × 블록(e={_config.BlockRestitution}) 실효 반발 = {measured:0.0000}");
            Line($"         후보 — max {max:0.00} · 평균 {average:0.00} · 곱 {product:0.00}   (원본 실측 = 0.70)");

            CheckRel("반발 혼합이 max(e₁,e₂) 다 — 원본 실측 실효 0.70 과 같다", measured, max, 0.03);
        }

        /// <summary>
        /// 공을 블록 위로 «수직으로» 떨어뜨려 법선 반발을 잰다.
        /// 중력이 섞이지 않도록 <b>충돌 직전·직후 한 스텝</b>만 보고 중력분(<c>g·h</c>)을 빼 준다.
        /// </summary>
        private double MeasureNormalRestitution()
        {
            GameObject ballPrefab = LoadPrefab(BlumgiPhysicsWorld.BallPrefabName);
            GameObject blockPrefab = LoadPrefab(BlumgiPhysicsWorld.BlockPrefabName);

            if (ballPrefab == null || blockPrefab == null)
                return double.NaN;

            var rig = new GameObject("MixingRig");
            SceneManager.MoveGameObjectToScene(rig, _scene);

            GameObject block = Instantiate(blockPrefab, rig.transform);
            block.transform.localPosition = Vector3.zero;

            GameObject ballGo = Instantiate(ballPrefab, rig.transform);
            var ball = ballGo.GetComponent<BlumgiBallBody>();

            double result = double.NaN;

            if (ball != null && ball.Body != null)
            {
                // 블록 위 3 유닛에서 아래로 12 유닛/s (= 600 px/s) — 4회차 검산 구간과 같은 자릿수다.
                // ⚠ 이 리그는 «법선 반발 혼합»만 잰다 — 회전은 결과에 안 들어가므로 0 으로 세운다.
                //   (게임의 실제 발사는 −200 °/s 를 붙인다 [13회차 실측] — 여기는 그 경로가 아니다.)
                ball.Launch(new Vector2(0f, 3f), new Vector2(0f, -12f), 0f);

                float previous = ball.Body.linearVelocity.y;

                for (int i = 0; i < 2000; i++)
                {
                    _physics.Simulate((float)_step);
                    float now = ball.Body.linearVelocity.y;

                    if (ball.CollisionCount > 0 && now > 0f)
                    {
                        // 접촉 스텝에서도 중력은 걸렸다 — 들어간 속도에서 그만큼 되돌린다.
                        double incoming = Math.Abs(previous) + (Math.Abs(Physics2D.gravity.y) * _step);
                        result = now / incoming;
                        break;
                    }

                    previous = now;
                }
            }

            DestroyImmediate(rig);
            return result;
        }

        // ────────────────────────────────────────────────────────── 0-e. 블록 밭 기하

        /// <summary>
        /// ★ <b>「합치지 않는다」와 「같은 행 윗면 높이가 같다」를 기계로 확인한다</b> [9회차 §2-a · §5-b].
        ///
        /// <para>
        /// 9회차가 원본에서 실측한 것 — 블록 34개가 <b>독립 정적 바디 · fixture 1개 · 필터 없음</b>이고,
        /// 겹친 이웃을 동시에 쳐도 <b>매니폴드 법선이 사실상 같아 한 번의 반발로 풀린다.</b>
        /// 그 «법선이 같음»의 근거가 <b>같은 행이면 윗면 y 가 정확히 같다</b>는 것이다 —
        /// 행마다 y 가 어긋나면 이음매마다 <b>턱</b>이 생겨 옆으로 차인다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <c>CompositeCollider2D</c> 로 묶으면 겹침이 <b>외곽선 하나로 병합</b>돼 «다른 도형»이 된다.
        /// 「이음매가 걸리니 합치자」는 원본을 버리는 처방이다 — 그래서 <b>없다는 것을 검사한다</b>.
        /// </para>
        /// </summary>
        private void ScoreBlockFieldGeometry()
        {
            if (EnsureLevel("W1L1") == null)
            {
                Fail("W1L1 을 못 세웠다 — 블록 밭 기하를 못 잰다");
                return;
            }

            var composites = _world.GetComponentsInChildren<CompositeCollider2D>(true);
            Check("블록을 합성 콜라이더로 묶지 않았다 — 원본은 34개를 «겹친 채로 따로» 둔다",
                  composites.Length == 0, $"{composites.Length}개 있다");

            var boxes = new List<BoxCollider2D>(_world.GetComponentsInChildren<BoxCollider2D>(true));
            var blockBoxes = new List<BoxCollider2D>(boxes.Count);

            for (int i = 0; i < boxes.Count; i++)
            {
                // 블롭도 BoxCollider2D 라 «정적인 것»만 고른다 (블록은 Rigidbody2D 가 없다).
                if (boxes[i].attachedRigidbody == null)
                    blockBoxes.Add(boxes[i]);
            }

            Check("블록 콜라이더가 데이터 수(34)만큼 있고 «전부 따로»다",
                  blockBoxes.Count == 34, $"{blockBoxes.Count}개");

            // ── 같은 행(같은 y)끼리 윗면이 정확히 같은 높이인가
            var rowTop = new Dictionary<long, float>();
            double worstRowGap = 0.0;
            int rows = 0;

            for (int i = 0; i < blockBoxes.Count; i++)
            {
                Bounds b = blockBoxes[i].bounds;
                long key = (long)Math.Round(blockBoxes[i].transform.position.y * 1000.0);

                if (rowTop.TryGetValue(key, out float top) == false)
                {
                    rowTop[key] = b.max.y;
                    rows++;
                    continue;
                }

                double gapPx = BlumgiUnits.ToWorldLength(Math.Abs(b.max.y - top));

                if (gapPx > worstRowGap)
                    worstRowGap = gapPx;
            }

            Line($"  [실측] 행 {rows}개 · 같은 행 윗면 높이 최대 편차 {worstRowGap:0.####} px  (원본: 0 — 턱이 없다)");
            CheckAbs("같은 행 블록의 윗면 y 가 정확히 같다 (이음매에 «턱»이 없다)", worstRowGap, 0.0, 1e-4, "px");

            // ── 겹침이 실제로 34 px 인가 (격자 피치 50 · 콜라이더 폭 84)
            double expectedOverlap = _config.BlockCollisionWidth - _config.BlockGridPitch;
            double measuredOverlap = double.NaN;

            for (int i = 0; i < blockBoxes.Count && double.IsNaN(measuredOverlap); i++)
            {
                for (int j = 0; j < blockBoxes.Count; j++)
                {
                    if (i == j)
                        continue;

                    Vector3 a = blockBoxes[i].transform.position;
                    Vector3 c = blockBoxes[j].transform.position;

                    if (Math.Abs(a.y - c.y) > 1e-4f)
                        continue;

                    double dx = BlumgiUnits.ToWorldLength(Math.Abs(a.x - c.x));

                    if (Math.Abs(dx - _config.BlockGridPitch) > 1e-3)
                        continue;

                    measuredOverlap = _config.BlockCollisionWidth - dx;
                    break;
                }
            }

            if (double.IsNaN(measuredOverlap))
            {
                Fail("가로로 이웃한 블록쌍을 못 찾았다 — 겹침을 못 잰다");
                return;
            }

            CheckAbs("가로 이웃끼리 34px 겹친다 (원본 그대로 — 겹침이 사라지면 세로면이 노출된다)",
                     measuredOverlap, expectedOverlap, 1e-3, "px");
        }

        // ────────────────────────────────────────────────────────── 0-f. 접촉점 정답지

        /// <summary>리그를 세우는 자리 — 레벨에서 한참 떨어뜨린다 (같은 물리 씬을 함께 밀기 때문).</summary>
        private static readonly Vector2 ContactRigCenter = new Vector2(0f, 100f);

        private const float ContactSlabHalfWidth = 20f;
        private const float ContactSlabHalfHeight = 2f;
        private const float ContactGapUnits = 0.05f;

        /// <summary>
        /// ω' 허용오차 (rad/s). <b>고른 값이 아니라 «표본 지연»에서 유도한 값이다.</b>
        ///
        /// <para>
        /// 9회차의 「전」·「후」는 접촉 임펄스에서 <b>rAF 1프레임(8.3 ms)</b> 떨어진 표본이다.
        /// 그 사이 중력이 <c>30 m/s² × 8.3 ms = 0.25 m/s = 12.5 px/s</c> 를 속도에 더한다.
        /// 스틱이면 <c>ω' = v_t'/R = (2v_t + ωR)/(3R)</c> 이므로 <c>v_t</c> 오차 12.5 px/s 는
        /// <c>(2/3)·12.5/43.5 = 0.19 rad/s</c> 로 옮는다. <b>전·후 양쪽</b>이 지연돼 있으니 ×2 ≈ 0.38,
        /// 올려서 <b>0.50</b>. 9회차 자신의 예측 오차(중앙값 0.16 · 최대 0.66 rad/s)와 같은 자릿수다.
        /// </para>
        /// </summary>
        private const double ContactOmegaToleranceRad = 0.50;

        private const double ContactOmegaRelTolerance = 0.05;

        /// <summary>스틱 판정 문턱 (px/s). 9회차 실측 <c>|v_c'| ≤ 22.1</c> (중앙값 10.5) 의 바로 위다.</summary>
        private const double ContactStickThresholdPx = 30.0;

        /// <summary>
        /// ★★★ <b>접촉점 정답지 채점.</b> 같은 <c>(v_n, v_t, ω, n)</c> 를 넣고 <b>우리 엔진의 <c>ω'</c></b> 를 잰다.
        ///
        /// <para>
        /// ★ <b>법선을 Δv 로 «유도»하지 않는다</b> — 정답지가 매니폴드에서 읽은 법선을 그대로 들고 있고,
        /// 우리는 그 법선을 <b>기하로 세운다</b>(면을 그 각도로 돌려 놓는다). 4회차 산포의 절반이
        /// 「읽을 수 있는 것을 추정한 것」이었다 (9회차).
        /// </para>
        ///
        /// <para>
        /// ★ <b>림 충돌도 같은 리그로 잰다.</b> 상대가 정적이면 충돌 응답은 «법선 · 질량 · 관성 · 접촉점»만으로
        /// 정해지고 곡률은 안 들어간다. 마찰 혼합 <c>√(0.5×0.5) = 0.5</c> · 반발 혼합 <c>max</c> 도
        /// 블록(0)·림(0.7) 어느 쪽이든 <b>실효 0.5 / 0.7 로 같다</b> — 그래서 면 하나로 충분하다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이 절 동안만 중력을 0 으로 둔다.</b> 재는 것은 «충돌 임펄스»고 중력은 그 위에 얹히는
        /// 잡음이다. 끝나면 <c>finally</c> 로 되돌리고, 되돌아왔는지 <b>여기서 다시 검사</b>한다.
        /// </para>
        /// </summary>
        private void ScoreContactGolden()
        {
            GameObject ballPrefab = LoadPrefab(BlumgiPhysicsWorld.BallPrefabName);
            GameObject blockPrefab = LoadPrefab(BlumgiPhysicsWorld.BlockPrefabName);

            if (ballPrefab == null || blockPrefab == null)
            {
                Fail("접촉 리그를 못 세웠다 — 프리팹이 없다");
                return;
            }

            var blockBox = blockPrefab.GetComponent<BoxCollider2D>();
            PhysicsMaterial2D surface = blockBox == null ? null : blockBox.sharedMaterial;

            if (surface == null)
            {
                Fail("블록 물리 머티리얼이 없다 — 접촉 리그의 마찰이 프로젝트 기본값이 된다");
                return;
            }

            Vector2 savedGravity = Physics2D.gravity;
            var omegaErrors = new List<double>();
            var verdictMiss = new List<string>();

            int scored = 0;
            int verdictHit = 0;
            int omegaHit = 0;
            int missed = 0;

            try
            {
                Physics2D.gravity = Vector2.zero;

                foreach (GoldenVectors.ContactEvent e in GoldenVectors.Contacts())
                {
                    // ★★ 법선 «무효» 처리 — 원본에서 상대가 «원(골대 림)»인 접촉의 localNormal 이
                    //   [18회차 정정 · #96 — 조건은 「저속·깊은겹침」이 아니라 「상대가 원」이다]
                    //   [0,0] 으로 나온다(한 샷 19건 중 5건). 그 건을 리그에 그대로 먹이면
                    //   «면이 없는 리그»를 세우게 되고, 결과가 0/NaN 인데 「맞았다」로 보인다.
                    //   ⚠ 조용히 건너뛰지 않는다 — 이유를 찍는다.
                    if (e.NormalValid == false)
                    {
                        Line($"  ·#{e.Id,-3} {e.Shot,-11} ⚠ [법선 무효] 정답지 법선이 [0,0] 이다 — " +
                             "원본 e_circles 매니폴드(상대가 원 = 골대 림)라 «면 법선»이 아니다. 리그를 못 세우므로 채점에서 뺀다 " +
                             "[14회차-b §4-a · 새로 발견]");
                        continue;
                    }

                    bool ok = MeasureContact(ballPrefab, surface, e,
                                             out double vnAfter, out double vtAfter, out double omegaAfter,
                                             out int contactSteps);

                    if (ok == false)
                    {
                        missed++;
                        Line($"  #{e.Id,-3} {e.Shot,-11} ⚠ 리그에서 접촉을 못 잡았다");

                        if (e.Scored)
                            Fail($"접촉 정답지 #{e.Id}: 리그가 충돌을 못 만들었다");

                        continue;
                    }

                    double vc = vtAfter - (omegaAfter * GoldenVectors.ContactBallRadiusPx);
                    string ourVerdict = Math.Abs(vc) <= ContactStickThresholdPx ? "스틱" : "슬립";
                    double omegaDelta = omegaAfter - e.OmegaAfter;
                    double enOurs = e.VnBefore == 0.0 ? double.NaN : Math.Abs(vnAfter / e.VnBefore);

                    string mark = e.Scored ? " " : "·";

                    Line($"  {mark}#{e.Id,-3} {e.Shot,-11} n({e.Nx,6:0.000},{e.Ny,6:0.000})  " +
                         $"ω' 우리 {omegaAfter,8:0.000} / 원본 {e.OmegaAfter,8:0.000} (차 {omegaDelta,7:+0.000;-0.000})  " +
                         $"v_t' {vtAfter,8:0.0}/{e.VtAfter,8:0.0}  v_n' {vnAfter,8:0.0}/{e.VnAfter,8:0.0}  " +
                         $"e_n {enOurs:0.0000}  v_c' {vc,7:0.0} → {ourVerdict}/{e.Verdict}  접촉 {contactSteps}스텝");

                    if (e.Scored == false)
                    {
                        Line($"        └ 채점 제외: {e.ExcludeReason}");
                        continue;
                    }

                    scored++;
                    omegaErrors.Add(Math.Abs(omegaDelta));

                    double tol = Math.Max(ContactOmegaToleranceRad, Math.Abs(e.OmegaAfter) * ContactOmegaRelTolerance);

                    if (Math.Abs(omegaDelta) <= tol)
                        omegaHit++;

                    CheckAbs($"접촉 #{e.Id} ω' (원본 {e.OmegaAfter:0.000} rad/s)", omegaAfter, e.OmegaAfter, tol, " rad/s");

                    // ⚠ 스틱/슬립 «판정»은 채점하지 않는다 — 그 컬럼은 9회차 «모형의 출력»이지
                    //   원본에서 잰 값이 아니다. 모형 출력을 정답지로 쓰면 「우리 엔진이 그 모형과 같은가」를
                    //   재게 되고, 그건 «원본과 같은가»가 아니다. 잰 값(ω' · v_c')만 채점한다.
                    if (ourVerdict == e.Verdict)
                        verdictHit++;
                    else
                        verdictMiss.Add($"#{e.Id}: 정답지 판정 «{e.Verdict}»(모형 예측) · 우리 v_c' {vc:0.0} · 원본 실측 v_c' {e.VcAfter:0.0}");
                }
            }
            finally
            {
                Physics2D.gravity = savedGravity;
            }

            // ★ 되돌렸다는 «주장»이 아니라 «확인»이다 — 여기가 안 돌아오면 뒤 절이 통째로 어긋난다.
            CheckAbs("리그가 끝난 뒤 중력이 되돌아왔다", -Physics2D.gravity.y, 30.0, 1e-4, "");

            omegaErrors.Sort();

            double median = omegaErrors.Count == 0 ? double.NaN : omegaErrors[omegaErrors.Count / 2];
            double worst = omegaErrors.Count == 0 ? double.NaN : omegaErrors[omegaErrors.Count - 1];

            // ★★ 「법선 무효」 장치는 0건이어도 «찍는다» (재발방지 #77 — 장치가 조용히 죽어 있는 것을 막는다).
            int invalidNormals = 0;

            foreach (GoldenVectors.ContactEvent e in GoldenVectors.Contacts())
            {
                if (e.NormalValid == false)
                    invalidNormals++;
            }

            Line(string.Empty);
            Line($"  → 채점 {scored}건 / 전체 {GoldenVectors.Contacts().Count}건 · " +
                 $"ω' 적중 {omegaHit}/{scored} · 접촉 못 잡음 {missed}건");
            Line($"     [법선 무효] 정답지 {invalidNormals}건" +
                 (invalidNormals == 0
                      ? " — 9회차 35건에는 «없다»(그 35건의 상대가 전부 블록이라서다). 장치는 살아 있고, 상대가 원(골대 림)인 접촉을 " +
                        "정답지에 넣는 회차에 발동한다"
                      : " — 리그를 못 세우므로 채점에서 뺐다 [14회차-b §13-d]"));
            Line($"     ω' 절대오차 — 중앙값 {median:0.000} · 최대 {worst:0.000} rad/s  " +
                 $"(9회차 «공식 대 원본» 은 중앙값 0.160 · 최대 0.657)");
            Line($"     [채점 아님] 스틱/슬립 «모형 판정»과 같은 건 {verdictHit}/{scored}");

            for (int i = 0; i < verdictMiss.Count; i++)
                Line($"        └ {verdictMiss[i]}");

            // ★ 정답지 자기모순 — 9회차 §1-e 의 제외 규칙이 자기 표 전체에 적용되지 않았다.
            Contradiction("9회차 §1-e 는 「e_n 이 0.70 에서 벗어난 5건」이라 적고 «여섯 값»(0.644·0.683·0.717·" +
                          "0.743·0.804·0.833)을 나열한다. 게다가 그 목록에 §1-b 의 #19(0.6700)·#10(0.6891) 과 " +
                          "§5-a 의 D1(0.6662)·D4(0.6538)·D7(0.6800)·D2(0.8320) 이 빠져 있다 — " +
                          "제외 규칙이 «표 전체»에 적용되지 않았다. 여기서는 규칙을 명시(|e_n−0.70| ≤ 0.01)하고 " +
                          "빠진 건을 «이유와 함께» 뺐다");

            Contradiction("9회차 D11 의 판정 컬럼은 «슬립»(모형 예측 v_c' −25)인데 같은 행의 «실측» v_c' 는 " +
                          "−5.3 이다 — 그 값은 어느 문턱으로 봐도 «스틱»이다. 즉 그 컬럼은 «잰 것»이 아니라 " +
                          "«모형이 낸 것»이라 정답지로 쓸 수 없다. 참고로 우리 엔진은 −26.4 로 «모형 쪽»에 붙는다 " +
                          "— 원본의 −5.3 은 «후» 표본이 1 rAF 뒤라 잔류가 이미 털린 것으로 보인다 (추정)");

            // ★ 리그가 «같은 물리 씬»을 함께 밀었다 — 레벨을 헐어 뒤 절이 깨끗한 상태에서 시작하게 한다.
            _world.Teardown();
            _builtLevel = null;
        }

        /// <summary>
        /// 충돌 한 건을 리그에서 재현하고 <b>충돌 후</b> 값을 원본 좌표계(px/s · rad/s)로 돌려준다.
        ///
        /// <para>
        /// 좌표 변환은 <see cref="BlumgiUnits"/> 한 곳만 지난다. y 뒤집기 하나가
        /// <b>접선 부호와 각속도 부호를 함께</b> 뒤집으므로, 여기서는 <b>원본 벡터를 먼저 만들고</b>
        /// 그걸 통째로 환산한다 — 축마다 부호를 손으로 맞추면 반드시 하나를 놓친다.
        /// </para>
        /// </summary>
        private bool MeasureContact(GameObject ballPrefab, PhysicsMaterial2D surface, GoldenVectors.ContactEvent e,
                                    out double vnAfter, out double vtAfter, out double omegaAfter, out int contactSteps)
        {
            vnAfter = 0.0;
            vtAfter = 0.0;
            omegaAfter = 0.0;
            contactSteps = 0;

            double norm = Math.Sqrt((e.Nx * e.Nx) + (e.Ny * e.Ny));

            if (norm <= 0.0)
                return false;

            // 원본 좌표계 — 법선 n 과 접선 t = (−n.y, n.x). 이 규약에서 v_c = v_t − ωR 이 성립한다.
            double nx = e.Nx / norm;
            double ny = e.Ny / norm;
            double tx = -ny;
            double ty = nx;

            double vx = (e.VnBefore * nx) + (e.VtBefore * tx);
            double vy = (e.VnBefore * ny) + (e.VtBefore * ty);

            var rig = new GameObject($"ContactRig_{e.Id}");
            SceneManager.MoveGameObjectToScene(rig, _scene);
            rig.transform.position = ContactRigCenter;

            // 유니티 법선 — 위치·속도와 «같은» 뒤집기를 탄다.
            var normalUnity = new Vector2((float)nx, (float)-ny);

            var slab = new GameObject("Slab");
            slab.transform.SetParent(rig.transform, false);
            slab.transform.localPosition = Vector3.zero;
            slab.transform.rotation = Quaternion.Euler(0f, 0f,
                (Mathf.Atan2(normalUnity.y, normalUnity.x) * Mathf.Rad2Deg) - 90f);

            var box = slab.AddComponent<BoxCollider2D>();
            box.size = new Vector2(ContactSlabHalfWidth * 2f, ContactSlabHalfHeight * 2f);
            box.sharedMaterial = surface;

            GameObject ballGo = Instantiate(ballPrefab, rig.transform);
            var ball = ballGo.GetComponent<BlumgiBallBody>();

            if (ball == null || ball.Body == null || ball.Collider == null)
            {
                DestroyImmediate(rig);
                return false;
            }

            var probe = ballGo.AddComponent<BlumgiContactProbe>();

            float standoff = ContactSlabHalfHeight + ball.Collider.radius + ContactGapUnits;
            Vector2 start = ContactRigCenter + (normalUnity * standoff);

            // ★ 정답지 ω 는 «원본 축»(rad/s)이다 — 축 뒤집기(부호 반전)는 BlumgiUnits «한 곳»이 한다.
            //   게임의 발사 경로와 «같은 규약»을 지나야 채점기와 게임이 같은 축을 쓴다.
            ball.Launch(start, BlumgiUnits.ToVector(vx, vy),
                        BlumgiUnits.ToAngularVelocityDegrees(e.OmegaBefore * Mathf.Rad2Deg));

            bool touched = false;

            for (int i = 0; i < 600; i++)
            {
                probe.Step = i;
                _physics.Simulate((float)_step);

                if (probe.EnterCount > 0)
                    touched = true;

                if (touched && probe.TouchingCount <= 0)
                    break;
            }

            if (touched)
            {
                contactSteps = probe.LastExitStep - probe.FirstEnterStep + 1;

                BlumgiUnits.ToWorld(ball.Body.linearVelocity, out double avx, out double avy);

                vnAfter = (avx * nx) + (avy * ny);
                vtAfter = (avx * tx) + (avy * ty);
                omegaAfter = -ball.Body.angularVelocity * Mathf.Deg2Rad;
            }

            DestroyImmediate(rig);
            return touched;
        }

        // ────────────────────────────────────────────────────────── 1. 데이터 게이트

        private void ScoreDataGate()
        {
            var config = GameRoot.Instance.BlumgiConfigDataContainer;
            var levels = GameRoot.Instance.BlumgiLevelDataContainer;
            var blocks = GameRoot.Instance.BlumgiBlockDataContainer;

            Check("설정 행 수 1", config.Count == 1, $"{config.Count}");
            Check("레벨 행 수 5", levels.Count == 5, $"{levels.Count}");
            Check("블록 행 수 462", blocks.Count == 462, $"{blocks.Count}");

            // ★ 파워 곡선 표가 «없어야» 한다. 되살아나면 진실이 두 곳으로 갈린다.
            Check("파워 곡선 표가 없다 (기전으로 대체)",
                  File.Exists(Path.Combine(Application.dataPath, DataFolderUnderAssets, "BlumgiPowerCurveTable.json")) == false,
                  "BlumgiPowerCurveTable.json 이 되살아났다");

            Check("블록 W1L1 34", blocks.GetGroupCount("W1L1") == 34, $"{blocks.GetGroupCount("W1L1")}");
            Check("블록 W1L2 82", blocks.GetGroupCount("W1L2") == 82, $"{blocks.GetGroupCount("W1L2")}");
            Check("블록 W1L3 47", blocks.GetGroupCount("W1L3") == 47, $"{blocks.GetGroupCount("W1L3")}");
            Check("블록 W1L4 112", blocks.GetGroupCount("W1L4") == 112, $"{blocks.GetGroupCount("W1L4")}");
            Check("블록 W1L5 187", blocks.GetGroupCount("W1L5") == 187, $"{blocks.GetGroupCount("W1L5")}");

            Check("컨테이너 Validate 통과 (설정 · 콜라이더≠격자 검사 포함)", config.Validate(out string e1), e1);
            Check("컨테이너 Validate 통과 (레벨)", levels.Validate(out string e2), e2);
            Check("컨테이너 Validate 통과 (블록 · 원본 이상 #3 #4 보존 포함)", blocks.Validate(out string e3), e3);

            // 원본 이상이 «그대로» 있는지 — 고치면 결함이다.
            Check("원본 이상 #3 보존 (W1L1 y=627 무리)", CountBlock(blocks, "W1L1", 100, 627) == 1, "없다");
            Check("원본 이상 #4 보존 (W1L2 (1225,375) 2개)", CountBlock(blocks, "W1L2", 1225, 375) == 2, "개수 불일치");
            Check("W1L5 안전영역 밖 보존 (x=-25 열)", CountBlock(blocks, "W1L5", -25, -125) == 1, "잘려 나갔다");
            Check("W1L5 안전영역 밖 보존 (y=-175 행)", CountBlock(blocks, "W1L5", 1225, -175) == 1, "잘려 나갔다");

            // 기전 상수 — 원본 전역변수와 «같은 눈금»인지
            BlumgiConfigData c = config.Config;
            Check("forceShootStart = 10", Math.Abs(c.ForceShootStart - 10.0) < 1e-9, $"{c.ForceShootStart}");
            Check("forceShootOffset = 55 /초", Math.Abs(c.ForceShootRatePerSecond - 55.0) < 1e-9, $"{c.ForceShootRatePerSecond}");
            Check("force 클램프 = 100", Math.Abs(c.ForceShootMax - 100.0) < 1e-9, $"{c.ForceShootMax}");

            var model = new BlumgiPowerModel(c);
            CheckAbs("포화 홀드 (실측 1633ms)", model.SaturationHoldMs, 1633.0, 10.0, "ms");
            CheckRel("파워 상한 |v0| (실측 2097~2098)", model.MaxSpeed, 2097.5, MechanismSpeedTolerance);

            Line($"  [참고] 원본 1프레임 양자 = |v0| {model.OriginFrameQuantumSpeed:0.0} px/s " +
                 $"(상한 대비 {model.OriginFrameQuantumSpeed / model.MaxSpeed * 100.0:0.00}%) — 우리는 양자화하지 않는다");

            // ★ W1L1 의 블록 34개를 실제로 세워 보고 «데이터 행 수»와 같은지 본다.
            //   프리팹 하나가 안 나와도 화면은 멀쩡하고 공만 그 자리를 통과한다.
            BlumgiLevelRuntime runtime = EnsureLevel("W1L1");
            Check("W1L1 물리 블록이 데이터 수만큼 세워졌다",
                  runtime != null && _world.BlockCount == 34, $"{_world.BlockCount}");
            Check("W1L1 공 물리 바디가 세워졌다", _world.Ball != null && _world.Ball.Body != null, "없다");
        }

        private static int CountBlock(BlumgiBlockDataContainer c, string level, double x, double y)
        {
            IReadOnlyList<BlumgiBlockData> list = c.GetGroup(level);

            if (list == null)
                return 0;

            int n = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].X == x && list[i].Y == y)
                    n++;
            }

            return n;
        }

        // ────────────────────────────────────────────────────────── 2. 기전

        private void ScoreMechanism()
        {
            foreach (GoldenVectors.MechanismPoint p in GoldenVectors.Mechanism())
            {
                if (p.ExcludeReason != null)
                {
                    Line($"  [채점 제외] {p.Level} 실홀드 {p.RealHoldMs}ms |v0| — {p.ExcludeReason}");
                    continue;
                }

                BlumgiShotSimulation sim = Fire(p.Level, p.RealHoldMs);

                if (sim == null)
                {
                    Fail($"{p.Level} 실홀드 {p.RealHoldMs}ms: 레벨을 못 올렸다");
                    continue;
                }

                CheckRel($"{p.Level} 실홀드 {p.RealHoldMs}ms |v0|", sim.LaunchVelocity.Magnitude, p.Speed, MechanismSpeedTolerance);
            }

            ReportCapShotSpread();
        }

        /// <summary>
        /// ★ <b>3회차 표 «안»의 모순도 찍는다.</b> 포화(≥1636ms) 위의 샷은 <b>완전히 같은 샷</b>이어야 한다 —
        /// 같은 레벨의 캡 샷끼리 최고점이 벌어지면 <b>그만큼이 3회차 측정 자신의 산포</b>다.
        /// </summary>
        private void ReportCapShotSpread()
        {
            double saturation = new BlumgiPowerModel(_config).SaturationHoldMs;
            var byLevel = new SortedDictionary<string, List<GoldenVectors.MechanismPoint>>(StringComparer.Ordinal);

            foreach (GoldenVectors.MechanismPoint p in GoldenVectors.Mechanism())
            {
                if (p.ExcludeReason != null || p.UngradableReason != null ||
                    p.ApexTimeMs <= 0.0 || p.RealHoldMs < saturation)
                    continue;

                if (byLevel.TryGetValue(p.Level, out var bucket) == false)
                {
                    bucket = new List<GoldenVectors.MechanismPoint>();
                    byLevel[p.Level] = bucket;
                }

                bucket.Add(p);
            }

            foreach (var pair in byLevel)
            {
                List<GoldenVectors.MechanismPoint> pts = pair.Value;
                double minX = double.MaxValue, maxX = double.MinValue;
                double minT = double.MaxValue, maxT = double.MinValue;

                for (int i = 0; i < pts.Count; i++)
                {
                    minX = Math.Min(minX, pts[i].ApexX);
                    maxX = Math.Max(maxX, pts[i].ApexX);
                    minT = Math.Min(minT, pts[i].ApexTimeMs);
                    maxT = Math.Max(maxT, pts[i].ApexTimeMs);
                }

                if (maxX - minX <= PointAbsTolerance && maxT - minT <= ApexTimeToleranceMs / 2.0)
                    continue;

                Contradiction($"{pair.Key} 캡 구간 {pts.Count}샷은 «같은 샷»이어야 하는데 3회차 실측 최고점이 " +
                              $"x {minX:0}~{maxX:0} ({maxX - minX:0}px) · 시각 {minT:0}~{maxT:0}ms ({maxT - minT:0}ms) 로 벌어진다 " +
                              "— 이 폭 위에서는 ±10px 채점이 성립하지 않는다");
            }
        }

        // ────────────────────────────────────────────────────────── 3. 발사 파라미터

        private void ScoreLaunchParameters()
        {
            var levels = GameRoot.Instance.BlumgiLevelDataContainer;
            var byLevel = new SortedDictionary<string, List<GoldenVectors.MechanismPoint>>(StringComparer.Ordinal);

            foreach (GoldenVectors.MechanismPoint p in GoldenVectors.Mechanism())
            {
                if (p.ExcludeReason != null)
                    continue;

                if (byLevel.TryGetValue(p.Level, out var bucket) == false)
                {
                    bucket = new List<GoldenVectors.MechanismPoint>();
                    byLevel[p.Level] = bucket;
                }

                bucket.Add(p);
            }

            // ★★★ [패스 ①-h] 발사 위치는 «중앙값 상수»로 채점하지 않는다 — 홀드의 «함수»다.
            //   12회차: 발사점 = 정지 머리공 중심 + (0.264, 9.721)·sin(π/2·min(1, 홀드초/1.5))
            //   그래서 «점마다» 그 점의 홀드로 모델을 세워 대조하고, 레벨당 «최악 잔차»를 채점한다.
            //   ⚠ 검사 «개수»는 바꾸지 않았다(레벨당 x·y 두 건) — 옛 성적과 그대로 비교되어야 한다.
            foreach (var pair in byLevel)
            {
                BlumgiLevelData level = levels.GetByCode(pair.Key);
                List<GoldenVectors.MechanismPoint> pts = pair.Value;
                BlumgiLevelRuntime runtime = EnsureLevel(pair.Key);

                double angle = Median(pts, p => p.AngleDeg);

                CheckAbs($"{pair.Key} 발사각 (rotator {level.AimRotatorValue:0} → {level.LaunchAngleDeg:0.0}°)",
                         level.LaunchAngleDeg, angle, AngleToleranceDeg, "°");

                if (runtime == null)
                {
                    Fail($"{pair.Key} 발사 위치: 레벨을 못 올렸다");
                    Fail($"{pair.Key} 발사 위치: 레벨을 못 올렸다");
                    continue;
                }

                double worstX = 0.0;
                double worstY = 0.0;
                int worstXHold = 0;
                int worstYHold = 0;

                for (int i = 0; i < pts.Count; i++)
                {
                    BlumgiVec2 modeled = runtime.LaunchPositionAt(pts[i].RealHoldMs);
                    double dx = Math.Abs(modeled.X - pts[i].PosX);
                    double dy = Math.Abs(modeled.Y - pts[i].PosY);

                    if (dx > worstX) { worstX = dx; worstXHold = pts[i].RealHoldMs; }
                    if (dy > worstY) { worstY = dy; worstYHold = pts[i].RealHoldMs; }
                }

                CheckAbs($"{pair.Key} 발사 x 모델 최악 잔차 (홀드 {worstXHold}ms · n={pts.Count})",
                         worstX, 0.0, LaunchPosTolerancePx, "px");
                CheckAbs($"{pair.Key} 발사 y 모델 최악 잔차 (홀드 {worstYHold}ms · n={pts.Count})",
                         worstY, 0.0, LaunchPosTolerancePx, "px");
            }

            // W1L3 은 3회차의 «깨끗한» 시행이 없다 — 조준각만 2회차 궤적 실측과 대조한다.
            BlumgiLevelData l3 = levels.GetByCode("W1L3");
            CheckAbs("W1L3 발사각 (rotator 300 → 60.0°) · 대조는 2회차 실측 59.8°",
                     l3.LaunchAngleDeg, 59.8, AngleToleranceDeg, "°");

            // ★★★ [정정 · 12회차] 「W1L3 은 미측정」이 «해소»됐다.
            //   3회차는 «발사 직후 충돌»에 막혀 못 쟀는데, 12회차가 «중력이 한 틱도 안 걸린
            //   진짜 발사 프레임»을 직독해 5점을 떴다 (각도가 5/5 정확히 60.000° 라는 것이 근거다).
            Line("  ★ [해소 · 12회차] W1L3 발사 위치·|v0| 은 이제 «실측»이다 — 아래 3-b 에서 5점 전수 채점한다.");
            ScoreLaunchPointModelW1L3();

            // ★ W1L5 조준값 330 을 4회차가 읽고 7회차가 «5레벨 전수»로 다시 읽었다 (_a = iv[0] 5/5).
            //   추정칸이 남아 있으면 안 된다.
            int estimated = 0;

            for (int i = 0; i < levels.AllValues.Count; i++)
            {
                if (levels.AllValues[i].AimRotatorEstimated)
                    estimated++;
            }

            Check("조준각 «추정» 칸 0 (4·7회차가 W1L5 330 을 독립으로 읽어 해소)", estimated == 0, $"{estimated}칸 남아 있다");

            // ★ 7회차 전수표 — 조준 인스턴스 변수값 자체를 5레벨 다 대조한다.
            //   각도(°)가 아니라 «원본 변수값»으로 비교해야 원본과 대조가 끊기지 않는다.
            double[] rotators = { 300.0, 290.0, 300.0, 230.0, 330.0 };

            for (int i = 0; i < rotators.Length; i++)
            {
                string code = $"W1L{i + 1}";
                BlumgiLevelData data = levels.GetByCode(code);

                CheckAbs($"{code} 조준 인스턴스변수 `_a` (7회차 전수 실측)",
                         data == null ? double.NaN : data.AimRotatorValue, rotators[i], 1e-9, "");
            }
        }

        /// <summary>
        /// ★★★ <b>3-b. 발사점 «함수» 정답지 — 12회차 W1L3 5점</b> (`07 §8-5-b`).
        ///
        /// <para>
        /// <b>이 절이 패스 ①-h 의 직접 판정이다.</b> 발사점을 상수로 두면 여기 5점이
        /// «한 점에서만» 맞는다 — 옛 상수 233.0 은 force 66 짜리 값이라
        /// force 15.06 에서 <b>7.6 px</b> 어긋난다. 모델이면 5점 전부 맞아야 한다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>|v0| 도 여기서 «계수의 정체»를 판정한다.</b> 허용오차를 <b>0.3 %</b> 로 조인 것은
        /// 옛 상수 20.95 가 정확값보다 <b>0.37 %</b> 낮기 때문이다 — 느슨하게 두면
        /// <b>틀린 상수가 되돌아와도 통과한다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ force 15.0563 은 «내부 홀드» 91.9 ms 로 <b>발사 최소 홀드(124 ms) 아래</b>다 —
        /// 그 점만 실제 발사 경로를 못 지난다(원본 시행 자체의 이상 · 12회차 §5). 모델 함수를
        /// 직접 부르고 <b>그 사실을 찍는다</b> — 조용히 빼지 않는다.
        /// </para>
        /// </summary>
        private void ScoreLaunchPointModelW1L3()
        {
            BlumgiLevelRuntime runtime = EnsureLevel("W1L3");

            if (runtime == null)
            {
                Fail("W1L3 을 못 올렸다 — 발사점 모델을 못 잰다");
                return;
            }

            var model = new BlumgiPowerModel(_config);

            Line($"  [유도] |v0| 계수 k = 1/(m·worldScale) = {model.SpeedPerForce:0.0000} " +
                 $"(질량 {model.BallMassKg:0.000000000} kg · 옛 상수 20.95 는 0.37% 낮았다)");

            foreach (GoldenVectors.LaunchFramePoint p in GoldenVectors.LaunchFramesW1L3())
            {
                double holdMs = model.HoldSecondsAtForce(p.Force) * 1000.0;
                BlumgiVec2 pos;
                double speed;

                if (holdMs >= _config.LaunchHoldThresholdMs)
                {
                    BlumgiShotSimulation sim = Fire("W1L3", holdMs);

                    if (sim == null || sim.State != EBlumgiShotState.Flying)
                    {
                        Fail($"W1L3 force {p.Force:0.###}: 발사가 안 걸렸다 (홀드 {holdMs:0.0}ms)");
                        continue;
                    }

                    pos = sim.LaunchPosition;
                    speed = sim.LaunchVelocity.Magnitude;
                }
                else
                {
                    Line($"  [경로 주의] W1L3 force {p.Force:0.###} — 내부 홀드 {holdMs:0.0}ms 가 " +
                         $"발사 최소({_config.LaunchHoldThresholdMs:0}ms) 아래라 실제 발사 경로를 못 지난다. " +
                         "모델 함수를 직접 대조한다 [원본 시행 자체의 이상 · 12회차 §5].");

                    pos = runtime.LaunchPositionAt(holdMs);
                    speed = model.Speed(holdMs);
                }

                CheckAbs($"W1L3 force {p.Force:0.###} 발사 x", pos.X, p.PosX, LaunchModelTolerancePx, "px");
                CheckAbs($"W1L3 force {p.Force:0.###} 발사 y", pos.Y, p.PosY, LaunchModelTolerancePx, "px");
                CheckRel($"W1L3 force {p.Force:0.###} |v0| (k 는 질량에서 유도)", speed, p.Speed, LaunchSpeedTolerance);
            }
        }

        /// <summary>
        /// ★★★ <b>3-c. 발사 프레임 정답지 — 14회차-b 4레벨 20샷</b> (`07 §13-a`).
        ///
        /// <para>
        /// <b>12회차는 W1L3 «한 레벨»만 봤다.</b> 그래서 「발사점 트윈이 전역인가 레벨별인가」가
        /// 원리적으로 안 갈렸다(재발방지 #39). 14회차-b 가 4레벨을 force 5점씩 떠서 닫았다 —
        /// 절편을 각 레벨 정지 머리공 중심으로 <b>고정</b>하고 진폭 하나로 맞추니 5레벨 38점이
        /// <b>잔차 y ≤ 0.066 px</b> 안이었다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>이 절은 우리 «데이터»가 실측으로 갈렸는지를 직접 잰다</b> —
        /// <c>BallRestX/Y</c> 5개와 트윈 진폭 <c>(0.2665, 9.8386)</c> 이 그 답이다.
        /// 반올림된 옛 값(0.5 단위)으로 되돌리면 여기서 먼저 깨진다.
        /// </para>
        /// </summary>
        private void ScoreLaunchFrames14b()
        {
            var model = new BlumgiPowerModel(_config);
            int excluded = 0;

            Line("  축 = 릴리즈 직전 forceShoot_P1 · 위치는 «중력이 한 틱도 안 걸린» 발사 프레임 [실측].");
            Line("  근거: 20샷 전부 atan2(−vy, vx) 가 조준각과 소수 3자리까지 같다 (60/70/130/30°).");
            Line(string.Empty);

            foreach (GoldenVectors.LaunchFramePoint p in GoldenVectors.LaunchFrames14b())
            {
                double holdMs = model.HoldSecondsAtForce(p.Force) * 1000.0;

                if (p.ExcludeReason != null)
                {
                    excluded++;
                    BlumgiLevelRuntime skipRuntime = EnsureLevel(p.Level);
                    string modeled = "레벨을 못 올렸다";

                    if (skipRuntime != null)
                    {
                        BlumgiVec2 m = skipRuntime.LaunchPositionAt(holdMs);
                        modeled = $"우리 모델 ({m.X:0.000}, {m.Y:0.000}) vs 원본 ({p.PosX:0.000}, {p.PosY:0.000}) " +
                                  $"→ Δy {m.Y - p.PosY:+0.000;-0.000} px";
                    }

                    Line($"  [채점 제외] {p.Level} force {p.Force:0.####} (holdSec {holdMs / 1000.0:0.0000}) — {p.ExcludeReason}");
                    Line($"              {modeled}   ← 참고로만 찍는다");
                    continue;
                }

                BlumgiShotSimulation sim = Fire(p.Level, holdMs);

                if (sim == null || sim.State != EBlumgiShotState.Flying)
                {
                    Fail($"{p.Level} force {p.Force:0.####}: 발사가 안 걸렸다 (내부홀드 {holdMs:0.0}ms)");
                    continue;
                }

                CheckAbs($"{p.Level} force {p.Force:0.####} 발사 x", sim.LaunchPosition.X, p.PosX, LaunchModelTolerancePx, "px");
                CheckAbs($"{p.Level} force {p.Force:0.####} 발사 y", sim.LaunchPosition.Y, p.PosY, LaunchModelTolerancePx, "px");
                CheckRel($"{p.Level} force {p.Force:0.####} |v0|", sim.LaunchVelocity.Magnitude, p.Speed, LaunchSpeedTolerance);

                // ★ 각속도 — 13·14회차 누적 36샷 표준편차 0. 부호가 좌표계에 딸린 값이라 «정답지로 판정»한다 (#81).
                double omegaRad = -sim.BallAngularVelocityDeg * Math.PI / 180.0;
                CheckAbs($"{p.Level} force {p.Force:0.####} 발사 ω", omegaRad, p.OmegaRad, LaunchOmegaToleranceRad, " rad/s");
            }

            Line(string.Empty);
            Line($"  ⇒ 채점 제외 {excluded}건 (전부 holdSec > 1.5 — 원본이 그 구간에서 «두 갈래»로 튄다).");
            Line("     ⚠ 조용히 빼지 않는다. 이관 창 밖이라 결과에는 영향이 없다 (창 상한이 5레벨 전부 force 77 이하).");
        }

        /// <summary>
        /// ★★★ <b>3-d. 첫 접촉 «직전» 정답지</b> (`07 §13-b` · 14회차-b 20점).
        ///
        /// <para>
        /// <b>왜 이 갈래가 필요한가.</b> 지금까지의 대조는 «접촉 이후»(반발 사슬·클리어 여부)와
        /// «발사 프레임» 둘뿐이었다. 그 사이가 비어 있으면 <b>「어디서부터 어긋나는지」를 못 가른다</b> —
        /// 발사가 맞고 결과가 다르면 원인이 물리인지 궤적인지 알 길이 없었다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>시각 기준점이 «발사 프레임»이다</b> — 9·13회차 정답지의 «홀드 시작» 기준과 다르다.
        /// 섞으면 첫 접촉부터 수백 ms 어긋나는 가짜 결론이 나온다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>첫 접촉 «전»은 순수 포물선이다</b>(v_x 가 16/16 발사값 그대로 · 선감쇠 0).
        /// 그래서 우리 쪽도 <b>골든의 t 에 맞춰 대조</b>한다 — 프레임 격자 차이를 판정에서 뺀다.
        /// 접촉 «시각»은 따로 잰다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>[검사기 사고 · 고침] «접촉 프레임»을 보간에 섞으면 안 된다.</b>
        /// 첫 판에서는 골든 <c>PreTimeMs</c> 를 감싸는 두 프레임을 그냥 보간했는데,
        /// 골든의 「직전」이 접촉보다 <b>1 프레임 앞</b>이라 그 두 프레임 중 «뒤»가 곧 <b>접촉 프레임</b>이었다.
        /// 충돌로 <c>v</c>·<c>ω</c> 가 통째로 바뀐 값을 섞어 <b>12건이 「어긋났다」로 나왔다</b> —
        /// 실제 직전 프레임 값은 골든과 <b>소수 1자리까지 같았다.</b>
        /// ⇒ <b>충돌 «전» 프레임만 쓰고, 그 뒤는 포물선으로 «외삽»한다</b>(원본이 순수 포물선임이 실측이므로 정당하다).
        /// <b>허용오차는 손대지 않았다</b> — 값을 고르는 것이 아니라 «잘못 잰 것»을 고친 것이다.
        /// </para>
        /// </summary>
        private void ScoreFirstContacts()
        {
            var model = new BlumgiPowerModel(_config);

            Line("  기준점 = «발사 프레임» (홀드 시작이 아니다). 부호 규약은 원본 좌표계 · y 아래가 +.");
            Line("  판정 — ① 직전 위치 ② 직전 v ③ 직전 ω ④ 첫 접촉 «비행 ms».");
            Line("  ⚠ 원본 접촉 검출은 rAF 프레임(7.4~9.1 ms) 단위라 ④의 허용은 ±2 프레임 = ±20 ms 다.");
            Line(string.Empty);

            foreach (GoldenVectors.FirstContactPoint g in GoldenVectors.FirstContacts())
            {
                double holdMs = model.HoldSecondsAtForce(g.Force) * 1000.0;
                BlumgiShotSimulation sim = Fire(g.Level, holdMs);

                if (sim == null || sim.State != EBlumgiShotState.Flying)
                {
                    Fail($"첫접촉 {g.Level} force {g.Force:0.####}: 발사가 안 걸렸다");
                    continue;
                }

                // ── 접촉 직전 프레임과 접촉 프레임을 «둘 다» 잡는다.
                int limit = (int)(FirstContactWindowSeconds / _step);
                double prevT = 0.0;
                BlumgiVec2 prevP = sim.BallPosition;
                BlumgiVec2 prevV = sim.BallVelocity;
                double prevOmega = -sim.BallAngularVelocityDeg * Math.PI / 180.0;

                double preT = double.NaN;
                BlumgiVec2 preP = prevP;
                BlumgiVec2 preV = prevV;
                double preOmega = prevOmega;

                double hitT = double.NaN;
                BlumgiVec2 hitP = BlumgiVec2.Zero;

                // ★ 보간용 — 골든 t 를 감싸는 «충돌 전» 두 프레임. 접촉 프레임은 절대 안 넣는다.
                bool spanned = false;
                double loT = 0.0;
                BlumgiVec2 loP = prevP;
                BlumgiVec2 loV = prevV;
                double loOmega = prevOmega;
                double hiT = 0.0;
                BlumgiVec2 hiP = prevP;
                BlumgiVec2 hiV = prevV;
                double hiOmega = prevOmega;

                for (int i = 0; i < limit; i++)
                {
                    StepOnce(sim);

                    double t = sim.FlightSeconds * 1000.0;
                    BlumgiVec2 p = sim.BallPosition;
                    BlumgiVec2 v = sim.BallVelocity;
                    double omega = -sim.BallAngularVelocityDeg * Math.PI / 180.0;

                    if (sim.CollisionCount > 0 && double.IsNaN(hitT))
                    {
                        hitT = t;
                        hitP = p;
                        preT = prevT;
                        preP = prevP;
                        preV = prevV;
                        preOmega = prevOmega;
                        break;      // ⚠ 여기서 끊으므로 이 프레임은 «절대» 보간에 안 들어간다
                    }

                    if (g.HasContact && spanned == false && prevT <= g.PreTimeMs && t >= g.PreTimeMs)
                    {
                        spanned = true;
                        loT = prevT; loP = prevP; loV = prevV; loOmega = prevOmega;
                        hiT = t; hiP = p; hiV = v; hiOmega = omega;
                    }

                    prevT = t; prevP = p; prevV = v; prevOmega = omega;

                    if (sim.IsBallActive == false || sim.State == EBlumgiShotState.Scored)
                        break;
                }

                // ── 원본에 «접촉이 없는» 샷 — 부존재가 정답이다.
                if (g.HasContact == false)
                {
                    bool none = double.IsNaN(hitT);

                    Check($"첫접촉 {g.Level} force {g.Force:0.####} — 원본은 {FirstContactWindowSeconds:0}s 안에 접촉이 «없다»",
                          none,
                          none ? "우리도 접촉 0건" : $"우리는 +{hitT:0.0}ms 에 접촉했다 ({hitP.X:0.0}, {hitP.Y:0.0})");
                    continue;
                }

                if (double.IsNaN(hitT))
                {
                    Fail($"첫접촉 {g.Level} force {g.Force:0.####}: 우리 쪽에 {FirstContactWindowSeconds:0}s 안에 접촉이 없다 " +
                         $"(원본은 +{g.FlightMs:0.0}ms 에 ({g.OtherX:0}, {g.OtherY:0}) 을 친다)");
                    continue;
                }

                // ── ①②③ 골든 t 에서 대조. «충돌 전» 프레임만 쓴다.
                //   ⓐ 골든 t 가 충돌 «전» 두 프레임 사이면 → 보간
                //   ⓑ 골든 t 가 마지막 충돌 전 프레임과 우리 접촉 사이면 → 포물선 «외삽»
                //      (첫 접촉 전이 순수 포물선임이 실측 16/16 이라 정당하다. ω 는 8 ms 감쇠가 1e-4 라 그대로 둔다)
                //   ⓒ 골든 t 가 우리 접촉보다 «뒤»면 → 우리가 먼저 부딪힌 것이다. 진짜 어긋남.
                bool extrapolated = false;

                if (spanned == false && g.PreTimeMs <= hitT + 1e-9)
                {
                    double dt = (g.PreTimeMs - preT) / 1000.0;

                    if (dt >= -1e-9)
                    {
                        extrapolated = true;
                        loT = preT;
                        loOmega = preOmega;
                        hiT = g.PreTimeMs;
                        hiOmega = preOmega;
                        loP = preP;
                        loV = preV;
                        hiP = new BlumgiVec2(preP.X + (preV.X * dt),
                                             preP.Y + (preV.Y * dt) + (0.5 * _config.GravityY * dt * dt));
                        hiV = new BlumgiVec2(preV.X, preV.Y + (_config.GravityY * dt));
                        spanned = true;
                    }
                }

                if (spanned == false)
                {
                    Fail($"첫접촉 {g.Level} force {g.Force:0.####}: 우리 첫 접촉(+{hitT:0.0}ms)이 " +
                         $"골든 «직전 t»(+{g.PreTimeMs:0.0}ms)보다 «먼저» 왔다 — 그 자체가 어긋남이다");
                }
                else
                {
                    double span = hiT - loT;
                    double a = span <= 1e-9 ? 1.0 : (g.PreTimeMs - loT) / span;

                    if (extrapolated)
                        a = 1.0;    // hi 가 곧 «골든 t 의 포물선 외삽값»이다

                    double ix = loP.X + ((hiP.X - loP.X) * a);
                    double iy = loP.Y + ((hiP.Y - loP.Y) * a);
                    double ivx = loV.X + ((hiV.X - loV.X) * a);
                    double ivy = loV.Y + ((hiV.Y - loV.Y) * a);
                    double iom = loOmega + ((hiOmega - loOmega) * a);

                    if (extrapolated)
                    {
                        Line($"     [외삽] {g.Level} f{g.Force:0.####} — 골든 직전 t(+{g.PreTimeMs:0.0}ms)가 " +
                             $"우리 마지막 «충돌 전» 프레임(+{preT:0.0}ms)과 접촉(+{hitT:0.0}ms) 사이다. " +
                             $"포물선으로 {(g.PreTimeMs - preT):0.0}ms 외삽했다 — 접촉 프레임을 섞지 않는다.");
                    }

                    CheckPoint($"첫접촉 {g.Level} f{g.Force:0.####} 직전 x @+{g.PreTimeMs:0}ms", ix, g.PreX);
                    CheckPoint($"첫접촉 {g.Level} f{g.Force:0.####} 직전 y @+{g.PreTimeMs:0}ms", iy, g.PreY);
                    CheckRel($"첫접촉 {g.Level} f{g.Force:0.####} 직전 v_x", ivx, g.PreVx, PointRelTolerance);
                    CheckAbs($"첫접촉 {g.Level} f{g.Force:0.####} 직전 v_y", ivy, g.PreVy,
                             Math.Max(PointAbsTolerance, Math.Abs(g.PreVy) * PointRelTolerance), " px/s");
                    CheckAbs($"첫접촉 {g.Level} f{g.Force:0.####} 직전 ω", iom, g.PreOmega, FirstContactOmegaToleranceRad, " rad/s");
                }

                // ── ④ 접촉 «시각»
                CheckAbs($"첫접촉 {g.Level} f{g.Force:0.####} 비행 ms", hitT, g.FlightMs, FirstContactTimeToleranceMs, "ms");

                string normalNote = g.NormalValid
                    ? $"n({g.Nx:0.000},{g.Ny:0.000})"
                    : "n «무효»([0,0] — e_circles 매니폴드 · 상대가 원(골대 림))";

                Line($"     {g.Level} f{g.Force,8:0.0000}  원본 접촉 +{g.FlightMs,7:0.0}ms ({g.HitX,8:0.0},{g.HitY,7:0.0}) " +
                     $"{normalNote} 상대({g.OtherX:0},{g.OtherY:0})");
                Line($"     {"",5}{"",9}  우리 접촉 +{hitT,7:0.0}ms ({hitP.X,8:0.0},{hitP.Y,7:0.0}) " +
                     $"· 직전 +{preT:0.0}ms ({preP.X:0.0},{preP.Y:0.0}) v({preV.X:0.0},{preV.Y:0.0}) ω {preOmega:0.000}");
            }
        }

        private static double Median(List<GoldenVectors.MechanismPoint> pts, Func<GoldenVectors.MechanismPoint, double> get)
        {
            var values = new List<double>(pts.Count);

            for (int i = 0; i < pts.Count; i++)
                values.Add(get(pts[i]));

            values.Sort();
            return values[values.Count / 2];
        }

        // ────────────────────────────────────────────────────────── 4. 최고점

        private void ScoreApexes()
        {
            Line("  ★★ [15회차 · `07 §8-3a`] 최고점을 «어떻게» 재는지가 바뀌었다 —");
            Line("     「프레임 샘플 중 최댓값」 → **「보간 꼭짓점」**(v_y 부호 전환을 선형보간).");
            Line("     극값을 «샘플 중 최댓값»으로 잡으면 폭이 «샘플 간격 × 속도»만큼 «반드시» 생긴다.");
            Line("     원본은 가변 rAF · 우리는 고정 1/120 s 라 «같은 정의»로 재야 비교가 성립한다.");
            Line("     ⚠ 아래 두 값을 «둘 다» 찍는다. 정답지가 어느 정의로 잰 것인지에 맞춰 채점한다.");
            Line(string.Empty);

            int ungradable = 0;

            foreach (GoldenVectors.MechanismPoint p in GoldenVectors.Mechanism())
            {
                if (p.ApexTimeMs <= 0.0)
                    continue;

                if (p.ExcludeReason != null)
                {
                    Line($"  [채점 제외] {p.Level} 실홀드 {p.RealHoldMs}ms 최고점 — {p.ExcludeReason}");
                    continue;
                }

                // ⛔ 「채점 불가(원리)」는 «비결정»과 다른 줄에 찍는다 (`07 §8-3b` · 재발방지 #74).
                if (p.UngradableReason != null)
                {
                    ungradable++;
                    Line($"  ⛔ [채점 «불가»(원리)] {p.Level} 실홀드 {p.RealHoldMs}ms 최고점 — {p.UngradableReason}");
                    continue;
                }

                BlumgiShotSimulation sim = Fire(p.Level, p.RealHoldMs);

                if (sim == null || sim.State != EBlumgiShotState.Flying)
                {
                    Fail($"{p.Level} 실홀드 {p.RealHoldMs}ms 최고점: 발사되지 않았다 ({sim?.State})");
                    continue;
                }

                if (FindApex(sim, out ApexReading reading) == false)
                {
                    Line($"  [채점 제외] {p.Level} 실홀드 {p.RealHoldMs}ms 최고점 — " +
                         "우리 쪽에서 최고점 «전»에 충돌했다. 자유비행 검사가 아니다");
                    continue;
                }

                bool vertex = p.Measure == GoldenVectors.EApexMeasure.InterpolatedVertex;

                double ourX = vertex ? reading.VertexX : reading.SampleX;
                double ourY = vertex ? reading.VertexY : reading.SampleY;
                double ourT = vertex ? reading.VertexMs : reading.SampleMs;
                string tag = vertex ? "보간꼭짓점" : "rAF샘플";

                CheckPoint($"{p.Level} 실홀드 {p.RealHoldMs}ms 최고점 x [{tag}]", ourX, p.ApexX);
                CheckPoint($"{p.Level} 실홀드 {p.RealHoldMs}ms 최고점 y [{tag}]", ourY, p.ApexY);
                CheckAbs($"{p.Level} 실홀드 {p.RealHoldMs}ms 최고점 시각 [{tag}]", ourT, p.ApexTimeMs, ApexTimeToleranceMs, "ms");

                // ★ 두 정의를 «둘 다» 남긴다 — 다음 회차가 정의를 바꿀 때 재측정 없이 갈아탈 수 있다.
                Line($"     {p.Level} h{p.RealHoldMs,-5} 우리  샘플({reading.SampleX,8:0.00}, {reading.SampleY,8:0.00}) @{reading.SampleMs,7:0.0}ms " +
                     $"·  꼭짓점({reading.VertexX,8:0.00}, {reading.VertexY,8:0.00}) @{reading.VertexMs,7:0.0}ms " +
                     $"|  정답지[{tag}] ({p.ApexX:0.00}, {p.ApexY:0.00}) @{p.ApexTimeMs:0}ms");
            }

            // ★★ 0 건이어도 찍는다 — 장치가 있다는 것과 이번에 발동했는지를 둘 다 남긴다.
            Line(string.Empty);
            Line(ungradable > 0
                     ? $"  ⛔ [채점 «불가»(원리) {ungradable}건] — 「비결정」이 «아니다». 원본은 결정적인데 " +
                       "«입력 분해능 1칸»이 허용치보다 큰 변화를 만드는 자리다 (`07 §8-3b`). " +
                       "처방은 「더 쏴 보라」가 아니라 «이 벡터를 골든에서 버리고 캡 구간을 써라» 다."
                     : "  [채점 «불가»(원리)] 0건.");
        }

        /// <summary>
        /// 최고점 한 번의 관측 — <b>두 정의를 «둘 다»</b> 담는다 (`07 §8-3a`).
        /// </summary>
        private struct ApexReading
        {
            /// <summary>프레임 샘플 중 가장 높은 점 (3회차 정답지의 옛 정의).</summary>
            public double SampleX;
            public double SampleY;
            public double SampleMs;

            /// <summary>★ <c>v_y</c> 부호 전환을 선형보간한 «진짜 꼭짓점». <b>샘플링 위상에 독립</b>이다.</summary>
            public double VertexX;
            public double VertexY;
            public double VertexMs;
        }

        /// <summary>
        /// ★★ 최고점을 <b>두 정의로 동시에</b> 잰다.
        ///
        /// <para>
        /// <b>왜 보간이 필요한가</b> — 극값을 «샘플 중 최댓값»으로 잡으면 폭이
        /// <b>«샘플 간격 × 속도»</b>만큼 «반드시» 생긴다. W1L4 캡 구간은 <c>|v_x| = 1351 px/s</c> 라
        /// 한 프레임(8.33 ms)이 <b>11.3 px</b> 이고, 그것이 3회차 정답지의 <c>−456 ↔ −444</c> 다.
        /// 15회차가 원본을 16회 다시 쏘니 <b>보간 꼭짓점이 0.05 px 안에서 같았다</b>.
        /// </para>
        ///
        /// <para>
        /// <b>어떻게</b> — <c>v_y</c> 가 부호를 바꾸는 두 샘플 사이를 선형보간해 <c>t*</c> 를 내고
        /// 그 시각의 <c>x*</c> 를 등속으로, <c>y*</c> 를 <c>y − v_y²/(2g)</c> 로 낸다.
        /// ⚠ <b>y 는 «아래가 +»</b> 인 원본 좌표계라 «위로 갈수록 작아진다».
        /// </para>
        /// </summary>
        private bool FindApex(BlumgiShotSimulation sim, out ApexReading reading)
        {
            reading = new ApexReading
            {
                SampleX = sim.BallPosition.X,
                SampleY = sim.BallPosition.Y,
                SampleMs = 0.0,
                VertexX = sim.BallPosition.X,
                VertexY = sim.BallPosition.Y,
                VertexMs = 0.0,
            };

            double bestY = double.MaxValue;
            bool turned = false;
            bool vertexFound = false;

            // «직전» 프레임 — 보간에 쓴다.
            BlumgiVec2 prevPos = sim.BallPosition;
            BlumgiVec2 prevVel = sim.BallVelocity;
            double prevMs = 0.0;

            for (int i = 0; i < 6000; i++)
            {
                StepOnce(sim);

                if (sim.CollisionCount > 0)
                    return turned && vertexFound;

                double nowMs = sim.FlightSeconds * 1000.0;
                BlumgiVec2 pos = sim.BallPosition;
                BlumgiVec2 vel = sim.BallVelocity;

                // ── ① rAF 샘플 정의 — 가장 높은(= y 가 가장 작은) 샘플
                if (pos.Y < bestY)
                {
                    bestY = pos.Y;
                    reading.SampleX = pos.X;
                    reading.SampleY = pos.Y;
                    reading.SampleMs = nowMs;
                }
                else
                {
                    turned = true;
                }

                // ── ② 보간 꼭짓점 — v_y 가 «위로(−)» 에서 «아래로(+)» 로 바뀌는 자리
                if (vertexFound == false && prevVel.Y < 0.0 && vel.Y >= 0.0)
                {
                    double span = vel.Y - prevVel.Y;
                    double frac = Math.Abs(span) < 1e-12 ? 0.0 : (0.0 - prevVel.Y) / span;

                    frac = Math.Max(0.0, Math.Min(1.0, frac));

                    double dtMs = nowMs - prevMs;

                    reading.VertexMs = prevMs + (dtMs * frac);
                    reading.VertexX = prevPos.X + ((pos.X - prevPos.X) * frac);

                    // y* = y − v_y²/(2g)   (y 아래 +  ⇒  꼭짓점은 «더 작은» y)
                    double g = Math.Abs(span) < 1e-12 ? 0.0 : span / (dtMs / 1000.0);
                    double vyAtVertexSource = prevVel.Y;

                    reading.VertexY = Math.Abs(g) < 1e-9
                        ? prevPos.Y
                        : prevPos.Y - ((vyAtVertexSource * vyAtVertexSource) / (2.0 * g));

                    vertexFound = true;
                }

                prevPos = pos;
                prevVel = vel;
                prevMs = nowMs;

                if (turned && vertexFound && nowMs > reading.SampleMs + 200.0)
                    return true;
            }

            return turned && vertexFound;
        }

        // ────────────────────────────────────────────────────────── 5. 궤적

        /// <summary>
        /// 궤적 채점. 축 보정 둘(홀드 축 · 시간 원점)을 먼저 하고 <b>첫 반발까지만</b> 채점한다
        /// — 패스 ①-b 규칙 그대로다.
        /// </summary>
        private void ScoreTrajectories()
        {
            var levels = GameRoot.Instance.BlumgiLevelDataContainer;
            var model = new BlumgiPowerModel(_config);

            foreach (GoldenVectors.Trajectory t in GoldenVectors.Trajectories())
            {
                BlumgiLevelData level = levels.GetByCode(t.Level);

                if (t.LabelNote != null)
                    Line($"  [라벨 정정] {t.Level} h{t.LabelHoldMs} — {t.LabelNote}");

                // ① 궤적 자신의 vx → 실홀드
                double dt = (t.TimeMs[1] - t.TimeMs[0]) / 1000.0;
                double vx = (t.X[1] - t.X[0]) / dt;
                double cos = Math.Cos(level.LaunchAngleDeg * Math.PI / 180.0);

                if (Math.Abs(cos) < 1e-6)
                {
                    Line($"  [채점 제외] {t.Level} h{t.LabelHoldMs} 궤적 — 발사각이 수직이라 vx 로 역산할 수 없다");
                    continue;
                }

                double impliedSpeed = vx / cos;
                // ★ k 는 데이터 컬럼이 아니라 «질량에서 유도»된다 [12회차] — 모델에게 묻는다.
                double impliedHoldMs = (impliedSpeed / model.SpeedPerForce - _config.ForceShootStart)
                                       / _config.ForceShootRatePerSecond * 1000.0;

                bool saturated = impliedSpeed >= model.MaxSpeed * (1.0 - MechanismSpeedTolerance);
                double feedHoldMs = saturated ? model.SaturationHoldMs + 200.0 : impliedHoldMs;

                Line($"  [축 보정] {t.Level} h{t.LabelHoldMs}: 궤적 vx {vx:0} → |v0| {impliedSpeed:0} → " +
                     (saturated
                         ? "캡 구간(포화) — 홀드가 결과를 안 바꾼다"
                         : $"실홀드 {impliedHoldMs:0}ms (라벨 대비 {impliedHoldMs - t.LabelHoldMs:+0;-0}ms)"));

                if (saturated == false && (impliedHoldMs < 0.0 || impliedHoldMs - t.LabelHoldMs > 400.0))
                {
                    Contradiction($"{t.Level} h{t.LabelHoldMs} 궤적: 역산 실홀드 {impliedHoldMs:0}ms 가 라벨과 " +
                                  $"{impliedHoldMs - t.LabelHoldMs:+0;-0}ms 어긋난다 — 라벨 자체를 의심할 자리다");
                }

                BlumgiShotSimulation sim = Fire(t.Level, feedHoldMs);

                if (sim == null || sim.State != EBlumgiShotState.Flying)
                {
                    Fail($"{t.Level} h{t.LabelHoldMs} 궤적: 발사되지 않았다");
                    continue;
                }

                // ② 검출 지연
                double lagMs = (t.X[0] - sim.BallPosition.X) / sim.LaunchVelocity.X * 1000.0;

                if (lagMs < -5.0 || lagMs > 120.0)
                {
                    Contradiction($"{t.Level} h{t.LabelHoldMs} 궤적: 검출 지연이 {lagMs:0}ms 로 나온다 " +
                                  "(정상 0~60ms) — 발사 위치나 각도 중 하나가 어긋났다");
                }

                Line($"           검출 지연 {lagMs:0}ms — 골든의 t=0 은 진짜 발사보다 그만큼 늦다");

                int goldenBounceIndex = FindGoldenBounce(t);

                if (goldenBounceIndex < t.TimeMs.Length)
                {
                    Line($"           골든 자신의 첫 반발 = +{t.TimeMs[goldenBounceIndex]:0}ms " +
                         "(그 구간에서 vx 가 꺾인다). 여기까지만 «자유비행»이다");
                }

                double simMs = 0.0;
                int scored = 0;

                for (int i = 0; i < goldenBounceIndex; i++)
                {
                    double targetMs = t.TimeMs[i] + lagMs;
                    Advance(sim, (targetMs - simMs) / 1000.0);
                    simMs = targetMs;

                    if (sim.CollisionCount > 0)
                    {
                        Line($"           [자유비행 끝] 우리 쪽이 +{t.TimeMs[i]:0}ms 에서 먼저 반발했다 — " +
                             $"여기까지 {scored}점 채점. 반발 «뒤»는 채점하지 않는다 (PD 확정 5-b)");
                        break;
                    }

                    CheckPoint($"{t.Level} h{t.LabelHoldMs} +{t.TimeMs[i]:0}ms x", sim.BallPosition.X, t.X[i]);
                    CheckPoint($"{t.Level} h{t.LabelHoldMs} +{t.TimeMs[i]:0}ms y", sim.BallPosition.Y, t.Y[i]);
                    scored++;
                }

                Line($"           자유비행 {scored}점 채점 (전체 {t.TimeMs.Length}점 중)");
            }
        }

        /// <summary>
        /// 골든 점열이 «스스로» 말하는 첫 반발 지점.
        /// 수평 가속이 0 이라 자유비행이면 <c>vx</c> 가 상수다 — 5% 넘게 꺾이면 그 구간에서 부딪힌 것이다.
        /// </summary>
        private static int FindGoldenBounce(GoldenVectors.Trajectory t)
        {
            double reference = (t.X[1] - t.X[0]) / ((t.TimeMs[1] - t.TimeMs[0]) / 1000.0);

            if (Math.Abs(reference) < 1e-6)
                return t.TimeMs.Length;

            for (int i = 1; i < t.TimeMs.Length - 1; i++)
            {
                double dt = (t.TimeMs[i + 1] - t.TimeMs[i]) / 1000.0;

                if (dt <= 0.0)
                    continue;

                double vx = (t.X[i + 1] - t.X[i]) / dt;

                if (Math.Abs(vx - reference) > Math.Abs(reference) * 0.05)
                    return i + 1;
            }

            return t.TimeMs.Length;
        }

        // ────────────────────────────────────────────────────────── 5-b. 감지기 자리 리포트

        /// <summary>
        /// ★★ <b>이제 «채점»한다.</b> 패스 ①-d 까지는 정답이 W1L1 하나뿐이라 리포트만 했는데,
        /// <b>7회차가 W1L1~L5 다섯 레벨의 bbox 를 전수로 떠</b> 정답지가 생겼다 (`07 §9-a`).
        ///
        /// <para>
        /// 기대값은 <b>7회차 실측표를 그대로 옮긴 것</b>이고, 우리 쪽 값은 «골대 중심 + 상수 오프셋»으로
        /// 편 결과다. 둘이 맞으면 <b>「골대는 중심 하나면 된다」가 기계로 확인</b>된다.
        /// </para>
        /// </summary>
        private void ReportDetectors()
        {
            Line($"  공의 «감지» 사각 한 변 {_config.BallDetectionBoxSize} (물리 원 r {_config.BallRadius} 과 «다르다» — 실측)");
            Line("  기대값 = 7회차 W1L1~L5 전수 bbox. 우리 값 = «골대 중심 + 상수 오프셋» 으로 편 것.");
            Line(string.Empty);

            // `07 §9-a` — 레벨, 윗감지기 중심(x, y), 아랫감지기 중심(x, y). [7회차 실측 · 5레벨 전수]
            double[][] measured =
            {
                new[] { 1.0, 824.30,  966.47,  825.0, 1077.0 },
                new[] { 2.0, 874.30, 1039.47,  875.0, 1150.0 },
                new[] { 3.0, 449.30,  764.47,  450.0,  875.0 },
                new[] { 4.0, 474.30,  914.47,  475.0, 1025.0 },
                new[] { 5.0, 949.30, 1039.47,  950.0, 1150.0 },
            };

            for (int i = 0; i < measured.Length; i++)
            {
                string code = $"W1L{(int)measured[i][0]}";
                BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(code);

                if (runtime == null)
                {
                    Fail($"{code} 레벨을 못 세웠다 — 감지기 대조 불가");
                    continue;
                }

                BlumgiAabb top = runtime.DetectorTop;
                BlumgiAabb bottom = runtime.DetectorBottom;

                Line($"  {code}  위 {top.MinX:0.00},{top.MinY:0.00}–{top.MaxX:0.00},{top.MaxY:0.00}" +
                     $" · 아래 {bottom.MinX:0.00},{bottom.MinY:0.00}–{bottom.MaxX:0.00},{bottom.MaxY:0.00}");

                CheckAbs($"{code} 윗감지기 중심 x", (top.MinX + top.MaxX) * 0.5, measured[i][1], DetectorTolerancePx, "px");
                CheckAbs($"{code} 윗감지기 중심 y", (top.MinY + top.MaxY) * 0.5, measured[i][2], DetectorTolerancePx, "px");
                CheckAbs($"{code} 아랫감지기 중심 x", (bottom.MinX + bottom.MaxX) * 0.5, measured[i][3], DetectorTolerancePx, "px");
                CheckAbs($"{code} 아랫감지기 중심 y", (bottom.MinY + bottom.MaxY) * 0.5, measured[i][4], DetectorTolerancePx, "px");

                CheckAbs($"{code} 윗감지기 한 변", top.MaxX - top.MinX, 50.0, DetectorTolerancePx, "px");
                CheckAbs($"{code} 아랫감지기 한 변", bottom.MaxY - bottom.MinY, 50.0, DetectorTolerancePx, "px");

                Check($"{code} 감지기 좌표가 «유도값»이 아니다 (7회차 5레벨 전수 실측)",
                      runtime.DetectorOffsetDerived == false, "아직 유도값으로 표시돼 있다");
            }

            Line(string.Empty);
            Line("  → 두 감지기의 세로 간격은 5레벨 전부 110.53px 다 (= 100 − (−10.53)) [7회차 실측].");
        }

        /// <summary>그 레벨의 감지기 좌표가 유도값인가. 실패 줄에 같이 찍어 «미측정 탓일 수 있음»을 남긴다.</summary>
        private bool CurrentLevelDetectorDerived(string levelCode)
        {
            BlumgiLevelData level = GameRoot.Instance.BlumgiLevelDataContainer?.GetByCode(levelCode);
            return level != null && level.DetectorOffsetDerived;
        }

        // ────────────────────────────────────────────────────────── 6. 클리어

        /// <summary>
        /// ★★ <b>이제 반발 «이후»도 볼 수 있다.</b> 패스 ①-b 까지는 반발 계수가 미측정이라
        /// 클리어 판정이 「우리가 고른 값」에 좌우됐다 — 4회차가 그 값을 «읽어» 왔으므로
        /// 클리어는 이제 <b>정당한 채점 항목</b>이다.
        /// </summary>
        private void ScoreClears()
        {
            var byLevel = new SortedDictionary<string, int[]>(StringComparer.Ordinal);

            Line("  ★ 입력 축이 둘이다 — «force» 는 게임이 «본» 값(정본)이고 «홀드ms» 는 우리 시계다 [7회차].");
            Line(string.Empty);

            foreach (GoldenVectors.ClearVector v in GoldenVectors.Clears())
            {
                List<double> feeds = v.FeedHoldMs();
                bool allMatch = true;
                var detail = new StringBuilder();

                for (int i = 0; i < feeds.Count; i++)
                {
                    RunToEnd(v.Level, feeds[i], out EBlumgiShotState state, out int bounces, out double seconds,
                             out string goalTrace);

                    if ((state == EBlumgiShotState.Scored) != v.ExpectScored)
                        allMatch = false;

                    if (detail.Length > 0)
                        detail.Append("   /   ");

                    detail.Append($"{feeds[i]:0}ms → {state} (반발 {bounces}회 · {seconds:0.00}s · {goalTrace})");
                }

                if (byLevel.TryGetValue(v.Level, out int[] tally) == false)
                {
                    tally = new int[2];
                    byLevel[v.Level] = tally;
                }

                tally[0]++;
                _clearTotal++;

                if (allMatch)
                {
                    tally[1]++;
                    _clearPass++;
                }

                string expect = v.ExpectScored ? "클리어" : "미클리어";
                string note = v.AxisNote == null ? " [force 축 · 정본]" : $" · 축: {v.AxisNote}";

                // ★ 유도값이 남아 있으면 실패 줄에 같이 찍는다 — 7회차 뒤로는 월드1 에 하나도 없어야 한다.
                string derived = CurrentLevelDetectorDerived(v.Level) ? " · ⚠ 감지기 좌표 «유도값»" : string.Empty;

                Check($"{v.Level} {v.AxisLabel()} → {expect}  ({v.Source}){note}",
                      allMatch,
                      $"{detail}{derived}");
            }

            Line(string.Empty);

            foreach (var pair in byLevel)
            {
                string extra = pair.Key == "W1L5"
                    ? "  ★ 실패上은 «원리적으로» 없다 — 실패下 1 + 성공 2 로 닫는다"
                    : string.Empty;

                Line($"  [레벨 요약] {pair.Key} {pair.Value[1]}/{pair.Value[0]}{extra}");
            }

            // ★ 정답지 정정의 «부수 결과»를 조용히 넘기지 않는다.
            //   W1L3 의 실패下였던 「실홀드 530」이 «클리어»로 뒤집혔으므로 그 레벨엔 실패下가 없다.
            //   합격 기준 5 는 「실패下-성공-실패上」을 요구하는데 이 레벨은 창이 «이어져 있지도» 않다.
            Line(string.Empty);
            Contradiction("W1L3 는 «실패下» 벡터가 없다 — 7회차가 옛 실패下(「실홀드 530」)를 «클리어»로 " +
                          "정정했기 때문이다 (`07 §9-d`). 이 레벨은 클리어 창이 이어져 있지 않고(150 O / 400 X / 600 O) " +
                          "«아래쪽 실패»가 어느 force 인지 미측정이다 — 8회차 격자 스윕 대기");
        }

        /// <summary>
        /// 한 발을 끝까지. <b>골 규칙의 두 사건을 «채점기 쪽에서도 따로» 센다</b> —
        /// 실패한 벡터가 「위를 못 지났나 / 아래에 못 닿았나 / 규칙이 아니라 궤적이 다른가」를 가르기 위해서다.
        /// ⚠ 게임 코드에 문을 뚫지 않는다 — 공 위치와 감지기 사각은 <b>이미 공개된 값</b>이라 여기서 다시 잰다.
        /// </summary>
        private void RunToEnd(string level, double holdMs, out EBlumgiShotState state, out int bounces, out double seconds,
                              out string goalTrace)
        {
            BlumgiShotSimulation sim = Fire(level, holdMs);

            if (sim == null)
            {
                state = EBlumgiShotState.Ready;
                bounces = 0;
                seconds = 0.0;
                goalTrace = string.Empty;
                return;
            }

            int limit = (int)(ShotTimeLimitSeconds / _step);
            double half = _config.BallDetectionBoxSize * 0.5;

            BlumgiAabb top = sim.Level.DetectorTop;
            BlumgiAabb bottom = sim.Level.DetectorBottom;

            int topOverlaps = 0;
            int bottomStarts = 0;
            bool wasBottom = false;

            for (int i = 0; i < limit; i++)
            {
                StepOnce(sim);

                BlumgiVec2 p = sim.BallPosition;

                if (top.OverlapsBox(p, half))
                    topOverlaps++;

                bool nowBottom = bottom.OverlapsBox(p, half);

                if (nowBottom && wasBottom == false)
                    bottomStarts++;

                wasBottom = nowBottom;

                if (sim.State == EBlumgiShotState.Scored)
                    break;

                if (sim.IsBallActive == false)
                    break;
            }

            state = sim.State;
            bounces = sim.CollisionCount;
            seconds = sim.FlightSeconds;
            goalTrace = $"위겹침 {topOverlaps}틱 · 아래닿기시작 {bottomStarts}회";
        }

        // ────────────────────────────────────────────────────────── 6-b. 남은 실패 추적

        /// <summary>
        /// ★ <b>「원인은 물리다」를 «추측»으로 남기지 않는다.</b>
        ///
        /// <para>
        /// 남은 클리어 실패 벡터를 다시 쏘면서 <b>발사 속도 · 반발마다의 시각과 자리</b>를 찍는다.
        /// 7회차가 원본 궤적을 <c>gap7/rec_H2*_*.json</c> 에 25ms 간격으로 남겼으므로
        /// <b>「몇 번째 반발부터 갈리는가」를 사람이 눈으로 대조</b>할 수 있다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>채점하지 않는다</b> — 반발 «이후»는 미측정(접선 반발·회전 결합)이 지배하는 구간이라
        /// 여기에 허용오차를 걸면 판정을 우리 품질이 아니라 미측정이 결정한다 (PD 확정 5-b).
        /// </para>
        /// </summary>
        private void TraceRemainingFailures()
        {
            TraceOne("W1L4", 25.153, "원본: 클리어 3/3 (`07 §9-4`)");
            TraceOne("W1L4", 24.635, "원본: 클리어 3/3 (`07 §9-5`) · 원본 v0 (−333, −397) · " +
                                     "①-e 반발 7회 / 원본 10회");
            TraceOne("W1L4", 39.859, "원본: 미클리어 0/3 · 아래 감지기 접촉 0회 (`07 §9-6`) · 원본 v0 (−539, −630) · " +
                                     "★ ①-e 반발 22회 / 원본 5회 — CCD 를 켠 뒤 여기가 어떻게 되는지 본다");
        }

        private void TraceOne(string level, double force, string originNote)
        {
            double holdMs = GoldenVectors.HoldMsFromForce(force);
            BlumgiShotSimulation sim = Fire(level, holdMs);

            if (sim == null || sim.State != EBlumgiShotState.Flying)
            {
                Line($"  {level} force {force:0.000}: 발사되지 않았다 ({sim?.State})");
                return;
            }

            Line($"  ── {level} force {force:0.000} (내부홀드 {holdMs:0}ms) — {originNote}");
            Line($"     우리 v0 ({sim.LaunchVelocity.X:0.0}, {sim.LaunchVelocity.Y:0.0}) " +
                 $"|v0| {sim.LaunchVelocity.Magnitude:0.0} · 발사 ({sim.BallPosition.X:0.0}, {sim.BallPosition.Y:0.0})");

            int limit = (int)(ShotTimeLimitSeconds / _step);
            int seen = 0;

            for (int i = 0; i < limit; i++)
            {
                StepOnce(sim);

                if (sim.CollisionCount > seen)
                {
                    seen = sim.CollisionCount;

                    if (seen <= 8)
                    {
                        Line($"     반발 #{seen} @ +{sim.FlightSeconds * 1000.0:0}ms " +
                             $"({sim.BallPosition.X:0.0}, {sim.BallPosition.Y:0.0})");
                    }
                }

                if (sim.State == EBlumgiShotState.Scored || sim.IsBallActive == false)
                    break;
            }

            Line($"     끝: {sim.State} · 반발 {sim.CollisionCount}회 · {sim.FlightSeconds:0.00}s · " +
                 $"({sim.BallPosition.X:0.0}, {sim.BallPosition.Y:0.0})");
        }

        // ────────────────────────────────────────────────────────── 6-c. 쪼개짐 지문

        /// <summary>원본 «샷당 반발 횟수» 한 줄 [9회차 §5-c 실측].</summary>
        private sealed class SplitShot
        {
            public string Level;
            public double HoldMs;
            public int OriginBlockBounces;
            public int OriginRimBounces;
            public string Note;
        }

        /// <summary>
        /// ★★ <b>「우리가 22회로 쪼갠다」를 «수치»로 잡는다.</b>
        ///
        /// <para>
        /// 9회차가 준 <b>지문</b>은 이것이다 — 원본은 35건 전수에서 <b>같은 블록 재접촉 0건</b>,
        /// 접촉 구간 <b>1~2 프레임</b>, 샷당 블록 반발 <b>1~7회</b>. 그래서 반발 «총수»가 아니라
        /// <b>「같은 블록을 다시 치는가」</b>를 본다 — 그게 쪼개짐과 «궤적이 다른 것»을 가르는 축이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>반발 «횟수»는 채점하지 않는다.</b> 홀드 축이 9회차의 «주입 홀드»(벽시계)라
        /// 내부 force 와 −18~+3ms 어긋나고, W1L4 는 혼돈적이라 한 프레임 차이가 궤적을 바꾼다.
        /// 채점하는 것은 <b>축이 흔들려도 성립해야 하는 구조적 성질</b>(같은 블록 재접촉) 하나뿐이다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>게임 코드에 문을 뚫지 않았다</b> — 접촉 기록기는 채점기가 «생성된 인스턴스»에
        /// 붙이는 <see cref="BlumgiContactProbe"/> 이고 <c>Tools\Verify</c> 안에서만 산다.
        /// </para>
        /// </summary>
        private void ScoreSplitFingerprint()
        {
            var shots = new List<SplitShot>
            {
                new SplitShot { Level = "W1L1", HoldMs = 300,  OriginBlockBounces = 1, OriginRimBounces = 0, Note = "미클리어" },
                new SplitShot { Level = "W1L1", HoldMs = 450,  OriginBlockBounces = 1, OriginRimBounces = 0, Note = "클리어" },
                new SplitShot { Level = "W1L1", HoldMs = 700,  OriginBlockBounces = 5, OriginRimBounces = 4, Note = "클리어" },
                new SplitShot { Level = "W1L1", HoldMs = 900,  OriginBlockBounces = 7, OriginRimBounces = 6, Note = "미클리어 (림에서 오래 튕긴다)" },
                new SplitShot { Level = "W1L1", HoldMs = 1200, OriginBlockBounces = 0, OriginRimBounces = 0, Note = "미클리어 (아무것도 안 맞고 날아간다)" },
                new SplitShot { Level = "W1L4", HoldMs = 254,  OriginBlockBounces = 2, OriginRimBounces = 7, Note = "클리어" },
                new SplitShot { Level = "W1L4", HoldMs = 530,  OriginBlockBounces = 2, OriginRimBounces = 0, Note = "⚠ 9회차 1시행이 클리어 — 7회차와 어긋난다" },
                new SplitShot { Level = "W1L4", HoldMs = 900,  OriginBlockBounces = 0, OriginRimBounces = 0, Note = "미클리어" },

                // ★★ 「22회로 쪼갠다」가 «기록된» 두 벡터다 (`07 §9-g` · force 축이라 축 어긋남이 없다).
                //    원본 반발 총수는 §9-g 가 적은 값이고 블록/림 내역은 안 적혀 있어 총수만 쓴다.
                new SplitShot
                {
                    Level = "W1L4", HoldMs = GoldenVectors.HoldMsFromForce(24.635),
                    OriginBlockBounces = 10, OriginRimBounces = 0,
                    Note = "force 24.635 · 원본 클리어 · 원본 반발 «총» 10회 (`07 §9-g`) · ①-e 우리 7회",
                },
                new SplitShot
                {
                    Level = "W1L4", HoldMs = GoldenVectors.HoldMsFromForce(39.859),
                    OriginBlockBounces = 5, OriginRimBounces = 0,
                    Note = "★ force 39.859 · 원본 미클리어 · 원본 반발 «총» 5회 · ①-e 우리 **22회** — 쪼개짐이 기록된 자리",
                },
            };

            int totalRecontact = 0;
            int totalNearRecontact = 0;
            int worstContactSteps = 0;
            int oldTotalNear = 0;
            int oldTotalBounce = 0;
            int newTotalBounce = 0;
            int originTotalBounce = 0;

            Line("  ⚠ 홀드 축은 9회차가 «주입»한 벽시계 홀드다 — 내부 force 와 −18~+3ms 어긋난다.");
            Line("     그래서 «반발 횟수»는 리포트고, 채점하는 것은 «같은 블록 재접촉» 하나뿐이다.");
            Line("  ★ 같은 샷을 «Discrete(패스 ①-e 까지의 설정)»로도 한 번 돌려 나란히 찍는다 —");
            Line("     「쪼개짐이 줄었다」를 주장이 아니라 «두 줄의 차»로 남기기 위해서다.");
            Line(string.Empty);

            for (int s = 0; s < shots.Count; s++)
            {
                SplitShot shot = shots[s];

                bool oldOk = RunFingerprint(shot, CollisionDetectionMode2D.Discrete,
                                            out int oldBounce, out int oldBlocks, out int oldRims,
                                            out int oldRecontact, out int oldNear, out int oldRimRe,
                                            out int oldSteps, out EBlumgiShotState oldState);

                bool ok = RunFingerprint(shot, CollisionDetectionMode2D.Continuous,
                                         out int bounce, out int blocks, out int rims,
                                         out int recontact, out int near, out int rimRe,
                                         out int steps, out EBlumgiShotState state);

                if (ok == false || oldOk == false)
                {
                    Line($"  {shot.Level} h{shot.HoldMs:0}ms — 발사되지 않았다 · 원본도 반발 " +
                         $"{shot.OriginBlockBounces + shot.OriginRimBounces}회");
                    continue;
                }

                totalRecontact += recontact;
                totalNearRecontact += near;
                oldTotalNear += oldNear;
                oldTotalBounce += oldBounce;
                newTotalBounce += bounce;
                originTotalBounce += shot.OriginBlockBounces + shot.OriginRimBounces;

                if (steps > worstContactSteps)
                    worstContactSteps = steps;

                Line($"  {shot.Level} h{shot.HoldMs,4:0}ms  원본 반발 {shot.OriginBlockBounces + shot.OriginRimBounces,2} " +
                     $"(블록 {shot.OriginBlockBounces} · 림 {shot.OriginRimBounces})   [원본 {shot.Note}]");
                Line($"       옛(Discrete) 반발 {oldBounce,3} (블록 {oldBlocks,2} · 림 {oldRims,2}) · " +
                     $"같은 «블록» 재접촉 {oldRecontact} (0.25초 내 {oldNear}) · 같은 림 재접촉 {oldRimRe} · " +
                     $"접촉 최대 {oldSteps}스텝 · {oldState}");
                Line($"     지금(Continuous) 반발 {bounce,3} (블록 {blocks,2} · 림 {rims,2}) · " +
                     $"같은 «블록» 재접촉 {recontact} (0.25초 내 {near}) · 같은 림 재접촉 {rimRe} · " +
                     $"접촉 최대 {steps}스텝 · {state}");

                Check($"{shot.Level} h{shot.HoldMs:0}ms — 같은 «블록»을 0.25초 안에 다시 치지 않는다 " +
                      "(원본 35건 전수 0건 [9회차 §5-c])",
                      near == 0, $"{near}건");
            }

            Line(string.Empty);
            Line($"  → 같은 «블록» 재접촉 합계 — 옛(Discrete) 0.25초 내 {oldTotalNear}건 → " +
                 $"지금(Continuous) {totalNearRecontact}건 (전 구간 {totalRecontact}건) · " +
                 $"접촉 구간 최대 {worstContactSteps}스텝  (원본: 블록 재접촉 0건 · 접촉 구간 1~2프레임)");
            Line($"     반발 총수 8샷 합계 — 옛 {oldTotalBounce} → 지금 {newTotalBounce} · **원본 {originTotalBounce}**");
        }

        /// <summary>
        /// 한 샷을 «지정한 충돌 검출 방식»으로 끝까지 굴리고 접촉 지문을 돌려준다.
        /// ⚠ 프리팹은 손대지 않는다 — <b>세워진 인스턴스</b>의 값만 잠깐 바꾼다 (다음 샷이 다시 세운다).
        /// </summary>
        private bool RunFingerprint(SplitShot shot, CollisionDetectionMode2D mode,
                                    out int bounces, out int blocks, out int rims,
                                    out int recontact, out int nearRecontact, out int rimRecontact,
                                    out int maxContactSteps, out EBlumgiShotState state)
        {
            bounces = 0;
            blocks = 0;
            rims = 0;
            recontact = 0;
            nearRecontact = 0;
            rimRecontact = 0;
            maxContactSteps = 0;
            state = EBlumgiShotState.Ready;

            // ★★ A/B 를 «짝지어» 잰다 — 레벨을 통째로 다시 세우고 시작한다.
            //    블롭은 동적이라 앞 샷의 여파가 남는다. 그대로 두면 「Discrete 라서 달랐다」와
            //    「앞 샷이 블롭을 밀어 놨다」가 섞여 두 줄의 차를 못 읽는다.
            _world.Teardown();
            _builtLevel = null;

            BlumgiShotSimulation sim = Fire(shot.Level, shot.HoldMs);

            if (sim == null || _world.Ball == null || _world.Ball.Body == null)
                return false;

            _world.Ball.Body.collisionDetectionMode = mode;
            state = sim.State;

            if (sim.State != EBlumgiShotState.Flying)
                return false;

            var probe = _world.Ball.GetComponent<BlumgiContactProbe>();

            if (probe == null)
                probe = _world.Ball.gameObject.AddComponent<BlumgiContactProbe>();

            probe.Restart();

            int limit = (int)(ShotTimeLimitSeconds / _step);

            for (int i = 0; i < limit; i++)
            {
                probe.Step = i;
                StepOnce(sim);

                if (sim.State == EBlumgiShotState.Scored || sim.IsBallActive == false)
                    break;
            }

            bounces = probe.EnterCount;
            blocks = probe.CountBy(BlumgiPhysicsWorld.BlockPrefabName);
            rims = probe.CountBy(BlumgiPhysicsWorld.HoopPrefabName);

            // ★ 지문은 «블록» 재접촉이다 — 림은 원본도 같은 기둥을 다시 친다 (기둥 2개 · 반발 6회).
            recontact = probe.RecontactCount(int.MaxValue, BlumgiPhysicsWorld.BlockPrefabName);
            nearRecontact = probe.RecontactCount(30, BlumgiPhysicsWorld.BlockPrefabName);   // 0.25초 = 30 스텝
            rimRecontact = probe.RecontactCount(30, BlumgiPhysicsWorld.HoopPrefabName);
            maxContactSteps = probe.MaxContactSteps;
            state = sim.State;
            return true;
        }

        // ────────────────────────────────────────────────────────── 7. 자기모순 리포트

        private void ReportLegacySpeeds()
        {
            var model = new BlumgiPowerModel(_config);
            var levels = GameRoot.Instance.BlumgiLevelDataContainer;

            int over = 0;

            // ⚠ 밴드의 위쪽 끝이 «추정 +130ms» 에서 «실측 +24ms» 로 좁아졌다 [7회차].
            //   밴드가 좁아지면 어긋나는 점이 늘어난다 — 그게 맞다. 넓은 밴드는 «추정으로 봐주던 것»이었다.
            foreach (GoldenVectors.LegacySpeed s in GoldenVectors.LegacySpeeds())
            {
                double atNominal = model.Speed(s.NominalHoldMs + GoldenVectors.NominalToInternalBiasMinMs);
                double atBiased = model.Speed(s.NominalHoldMs + GoldenVectors.NominalToInternalBiasMaxMs);

                bool insideBand = s.Speed >= Math.Min(atNominal, atBiased) - (s.Speed * 0.03)
                                  && s.Speed <= Math.Max(atNominal, atBiased) + (s.Speed * 0.03);

                double angleDelta = s.AngleDeg - levels.GetByCode(s.Level).LaunchAngleDeg;

                if (insideBand && Math.Abs(angleDelta) <= 1.0)
                    continue;

                over++;

                string why = insideBand == false
                    ? $"|v0| {s.Speed:0} 이 기전 밴드 [{atNominal:0}, {atBiased:0}] 밖"
                    : $"발사각 {s.AngleDeg:0.00}° 가 레벨 상수와 {angleDelta:+0.00;-0.00}° 차";

                Line($"  [{s.Round}회차 컬럼] {s.Level} h{s.NominalHoldMs}: {why}");
            }

            Line(string.Empty);
            Line($"  → 1·2회차 |v0|/각도 컬럼 {GoldenVectors.LegacySpeeds().Count}점 중 {over}점이 기전과 어긋난다.");
            Line("     저홀드일수록 «낮게» 어긋나는 것이 규칙적이다 — 발사 검출이 늦어 vy 가 이미 깎인 뒤 피팅됐기 때문이다.");
        }

        // ────────────────────────────────────────────────────────── 구동

        /// <summary>
        /// 실제 경로로 한 발 쏜다 — <c>BeginHold</c> → <c>Tick</c> ×n → <c>EndHold</c>.
        /// <b>홀드 중에는 물리를 밀지 않는다</b> — 원본도 발사 전 공은 물리 밖에 대기하고,
        /// 블롭·블록은 정지 상태로 실측된 좌표에 있어야 한다.
        /// </summary>
        private BlumgiShotSimulation Fire(string levelCode, double holdMs)
        {
            // ★★★ 샷마다 물리 «월드»를 새로 만든다 (RecreateScene 주석 · #106).
            //   예전 판은 «같은 레벨이면 다시 안 세웠다» — 그러면 앞 샷이 밀어 놓은 블롭과
            //   재사용된 바디 순서가 다음 샷에 실린다. 「같은 입력에 같은 답」이 먼저다.
            RecreateScene();

            BlumgiLevelRuntime runtime = EnsureLevel(levelCode);

            if (runtime == null || _world.Ball == null)
                return null;

            var sim = new BlumgiShotSimulation(runtime, _world.Ball);
            sim.BeginHold();

            double remain = holdMs / 1000.0;

            while (remain > 1e-12)
            {
                double dt = remain > _step ? _step : remain;
                sim.Tick(dt);
                remain -= dt;
            }

            sim.EndHold();
            return sim;
        }

        /// <summary>
        /// 레벨의 물리 실체를 세운다. <b>같은 레벨이면 다시 세우지 않는다</b> — 187칸짜리 레벨을
        /// 샷마다 다시 세우면 채점이 몇 분 단위로 늘어난다. 블록은 정적이라 재사용해도 상태가 안 남는다.
        /// ⚠ 블롭은 동적이라 앞 샷의 여파가 남을 수 있어 <b>레벨이 바뀔 때 통째로 다시 세운다</b>.
        /// </summary>
        private BlumgiLevelRuntime EnsureLevel(string levelCode)
        {
            if (_builtLevel == levelCode && _world.Level != null)
                return _world.Level;

            BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(levelCode);

            if (runtime == null)
            {
                Fail($"레벨 데이터를 못 읽었다: {levelCode}");
                return null;
            }

            _world.Build(runtime, LoadPrefab);
            _builtLevel = levelCode;
            return _world.Level;
        }

        /// <summary>물리 한 스텝 + 상태 한 스텝. <b>둘은 반드시 같은 폭으로 움직인다.</b></summary>
        private void StepOnce(BlumgiShotSimulation sim)
        {
            _physics.Simulate((float)_step);
            sim.Tick(_step);
        }

        /// <summary>
        /// 정확히 <paramref name="seconds"/> 만큼 민다. 골든 표본 시각에 «정확히» 서야 하므로
        /// 스텝을 균등 분할한다 — 남는 조각을 마지막에 몰면 그 스텝만 폭이 달라져 반발이 흔들린다.
        /// </summary>
        private void Advance(BlumgiShotSimulation sim, double seconds)
        {
            if (seconds <= 1e-12)
                return;

            int count = (int)Math.Ceiling(seconds / _step);

            if (count < 1)
                count = 1;

            double h = seconds / count;

            for (int i = 0; i < count; i++)
            {
                _physics.Simulate((float)h);
                sim.Tick(h);

                if (sim.State == EBlumgiShotState.Scored || sim.IsBallActive == false)
                    return;

                if (sim.CollisionCount > 0)
                    return;
            }
        }

        // ────────────────────────────────────────────────────────── 판정 · 출력

        private void Section(string title)
        {
            Line(string.Empty);
            Line($"== {title} ==");
        }

        private void Check(string name, bool ok, string actual)
        {
            if (ok)
            {
                _pass++;
                Line($"  OK   {name}");
                return;
            }

            Fail($"{name} — {actual}");
        }

        private void CheckRel(string name, double actual, double expected, double tolerance)
        {
            double diff = Math.Abs(actual - expected);
            bool ok = diff <= Math.Abs(expected) * tolerance;

            Report(name, ok, actual, expected, $"{(diff / Math.Abs(expected)) * 100.0:0.00}%");
        }

        private void CheckAbs(string name, double actual, double expected, double tolerance, string unit)
        {
            double diff = Math.Abs(actual - expected);
            Report(name, diff <= tolerance, actual, expected, $"{diff:0.####}{unit}");
        }

        private void CheckPoint(string name, double actual, double expected)
        {
            double tol = Math.Max(PointAbsTolerance, Math.Abs(expected) * PointRelTolerance);
            double diff = Math.Abs(actual - expected);

            Report(name, diff <= tol, actual, expected, $"{diff:0.0}px / 허용 {tol:0.0}px");
        }

        private void Report(string name, bool ok, double actual, double expected, string detail)
        {
            if (ok)
            {
                _pass++;
                return;
            }

            Fail($"{name}: {actual.ToString("0.00", CultureInfo.InvariantCulture)} vs 골든 " +
                 $"{expected.ToString("0.00", CultureInfo.InvariantCulture)} (차 {detail})");
        }

        private void Fail(string line)
        {
            _fail++;
            _failLines.Add(line);
            Line("  FAIL " + line);
        }

        private void Contradiction(string line)
        {
            _contradictions.Add(line);
            Line("  [골든 내부 모순] " + line);
        }

        private void Line(string text)
        {
            _report.AppendLine(text);
        }

        /// <summary>
        /// ⚠ <b>콘솔에 통째로 찍지 않는다.</b> 채점 전문이 수백 줄이라 러너가 앞 140줄만 보여 주면
        /// 정작 «못 맞춘 항목»이 잘린다. 전문은 파일로 남기고 콘솔에는 요약만 낸다.
        /// </summary>
        private void WriteReport()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, _report.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"채점 전문을 못 썼다: {e.Message}");
            }
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            // ★ 다리가 시킨 실행이면 «에디터를 끄지 않는다» — 사람이 쓰던 창이 사라진다.
            if (SessionState.GetBool(BlumgiGoldenPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }

    /// <summary>
    /// ★ <b>접촉 기록기</b> — 채점기가 «생성한 인스턴스»에 붙인다.
    ///
    /// <para>
    /// 왜 필요한가: <see cref="BlumgiBallBody.CollisionCount"/> 는 «몇 번 닿았나»만 세고
    /// <b>«누구를» 닿았나</b>를 안 남긴다. 9회차가 준 지문 — <b>같은 블록 재접촉 0건</b> ·
    /// <b>접촉 구간 1~2프레임</b> — 은 상대의 «정체»가 있어야 잴 수 있다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>게임 코드에는 손대지 않는다.</b> 이 컴포넌트는 <c>Tools\Verify</c> 안에서만 살고
    /// 채점기가 <c>AddComponent</c> 로 붙였다 뗀다 — 이관 결과물에 남지 않는다
    /// (CLAUDE.md 「게임 코드에 검증용 진입점을 두지 않는다」).
    /// </para>
    /// </summary>
    public sealed class BlumgiContactProbe : MonoBehaviour
    {
        private readonly List<int> _enterStep = new List<int>(64);
        private readonly List<int> _enterId = new List<int>(64);
        private readonly List<string> _enterName = new List<string>(64);
        private readonly Dictionary<int, int> _openStep = new Dictionary<int, int>(16);

        /// <summary>바깥(채점기)이 스텝마다 세워 주는 시각. 물리 스텝 한 번 = 1 이다.</summary>
        public int Step;

        /// <summary>지금 «닿아 있는» 상대 수.</summary>
        public int TouchingCount { get; private set; }

        /// <summary>「닿기 시작」 총 횟수.</summary>
        public int EnterCount
        {
            get { return _enterStep.Count; }
        }

        public int FirstEnterStep
        {
            get { return _enterStep.Count == 0 ? -1 : _enterStep[0]; }
        }

        public int LastExitStep { get; private set; } = -1;

        /// <summary>가장 긴 접촉 구간 (스텝). <b>원본은 1~2 프레임</b>이다 [9회차 §2-c].</summary>
        public int MaxContactSteps { get; private set; }

        public void Restart()
        {
            _enterStep.Clear();
            _enterId.Clear();
            _enterName.Clear();
            _openStep.Clear();
            TouchingCount = 0;
            LastExitStep = -1;
            MaxContactSteps = 0;
            Step = 0;
        }

        /// <summary>이름에 <paramref name="prefabName"/> 이 들어간 상대와의 접촉 수.</summary>
        public int CountBy(string prefabName)
        {
            int n = 0;

            for (int i = 0; i < _enterName.Count; i++)
            {
                if (_enterName[i] != null && _enterName[i].Contains(prefabName))
                    n++;
            }

            return n;
        }

        /// <summary>
        /// <b>같은 상대를 다시 친 횟수</b> — <paramref name="withinSteps"/> 스텝 안의 재접촉만 센다.
        /// 이 수가 0 이 아니면 <b>한 번의 스침이 여러 접촉으로 쪼개졌다는 뜻</b>이다 [9회차 §5-c].
        ///
        /// <para>
        /// ⚠ <paramref name="prefabName"/> 로 <b>상대를 가려서</b> 센다. 9회차의 지문은
        /// <b>「같은 «블록» 재접촉 0건」</b>이고 <b>림은 해당하지 않는다</b> —
        /// 원본 W1L1 h900 은 <b>기둥 2개에 림 반발 6회</b>라 같은 기둥을 반드시 다시 친다 [9회차 §5-c].
        /// 림까지 넣고 세면 <b>원본 자신도 0 이 아니다.</b>
        /// </para>
        /// </summary>
        public int RecontactCount(int withinSteps, string prefabName)
        {
            var last = new Dictionary<int, int>(32);
            int n = 0;

            for (int i = 0; i < _enterId.Count; i++)
            {
                if (prefabName != null && (_enterName[i] == null || _enterName[i].Contains(prefabName) == false))
                    continue;

                int id = _enterId[i];

                if (last.TryGetValue(id, out int previous) && _enterStep[i] - previous <= withinSteps)
                    n++;

                last[id] = _enterStep[i];
            }

            return n;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TouchingCount++;

            int id = collision.collider == null ? 0 : collision.collider.GetInstanceID();
            string name = collision.collider == null ? null : collision.collider.gameObject.name;

            _enterStep.Add(Step);
            _enterId.Add(id);
            _enterName.Add(name);
            _openStep[id] = Step;
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            TouchingCount--;
            LastExitStep = Step;

            int id = collision.collider == null ? 0 : collision.collider.GetInstanceID();

            if (_openStep.TryGetValue(id, out int began) == false)
                return;

            int length = Step - began + 1;

            if (length > MaxContactSteps)
                MaxContactSteps = length;

            _openStep.Remove(id);
        }
    }
}
