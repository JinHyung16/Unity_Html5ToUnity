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
    /// ★★★ <b>«밸런싱 창» 대조기</b> — 합격 기준 5-c (`07_기대값.md §12`).
    ///
    /// <para>
    /// <b>왜 이 검사가 따로 필요한가.</b> 골든 채점기는 «점»을 잰다 — 「이 force 는 클리어」.
    /// 그런데 이 게임의 난이도는 <b>«창의 폭과 위치»</b>다. 점 몇 개가 맞아도
    /// <b>창이 다르면 체감 난이도가 다르다.</b> 그래서 원본이 훑은 <b>같은 force 격자</b>를
    /// 우리 쪽에 그대로 돌려 <b>창 대 창</b>으로 잰다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>격자는 원본 것 그대로다</b> — 11회차가 실제로 쏜 <b>141 시행의 force 값</b>을 그대로 먹인다.
    /// 우리가 임의로 촘촘한 격자를 만들면 「원본이 못 본 자리」를 우리만 보게 되어 창이 «달라 보인다».
    /// 원자료는 <c>gap11/progress_W1L*.md</c>, 정본 표는 <c>07_기대값.md §12-b</c> 다.
    /// </para>
    ///
    /// <para>
    /// ⚠⚠ <b>바늘은 채점에서 뺀다 — 그리고 «뺐다는 것을 찍는다»</b> (`07 §12-c`).
    /// 폭 force 0.0055(홀드 0.1 ms)짜리는 품질이 아니라 우연을 재는 것이다.
    /// <b>조용히 빼면 다음 회차가 다시 넣는다</b> — 그래서 뺀 자리와 우리 결과를 같이 출력한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>창을 맞추려고 물리·데이터 값을 «고르지» 않는다</b> (재발방지 #48).
    /// 어긋나면 이 검사는 <b>어긋난 자리를 찍을 뿐</b>이고, 원인 규명은 사람이 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>이관이 끝나면 이 폴더째로 지운다</b> (CLAUDE.md). 정답지는 <c>07 §12</c> 의 표다.
    /// </para>
    ///
    /// <para>
    /// 실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.BlumgiWindowPlayCheck.RunAll</c>
    /// </para>
    /// </summary>
    public static class BlumgiWindowPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiWindowPlayCheck.Armed";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);

            // ⚠ 씬을 새로 열지 않는다 — 사람이 열어 둔 씬이 통째로 사라진다.
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

            var go = new GameObject("BlumgiWindowProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiWindowProbe>();
#endif
        }
    }

    /// <summary>
    /// 창 대조 실행기. 골든 채점기와 <b>같은 방식</b>으로 «전용 로컬 물리 씬»을 만들고
    /// <c>Simulate</c> 로 직접 밀어 결정적으로 돌린다.
    /// </summary>
    public sealed class BlumgiWindowProbe : MonoBehaviour
    {
        private const string DataFolderUnderAssets = "Blumgi-Bounce/Data";
        private const string PrefabFolder = "Assets/Blumgi-Bounce/Prefabs";
        private const string ReportPath = "Temp/BlumgiWindowReport.txt";

        /// <summary>한 발을 끝까지 굴리는 상한(초). 넘으면 «미클리어»로 닫는다.</summary>
        private const double ShotTimeLimitSeconds = 20.0;

        /// <summary>창 경계 오차의 «참고» 기준(force). 홀드 36 ms 상당 — `07 §12-f`.</summary>
        private const double BoundaryReferenceTolerance = 2.0;

        /// <summary>W1L4 창B 는 «폭»이 아니라 «존재»만 본다 — `07 §12-f`.</summary>
        private const double IslandCenter = 51.25;

        private const double IslandTolerance = 1.6;

        /// <summary>경계 미세화(참고용) 이분 반복 횟수. <b>채점하지 않는다.</b></summary>
        private const int RefineIterations = 6;

        /// <summary>
        /// 결정성 자기검산 시행 수. <b>3이면 충분하다</b> — 관측된 비결정은 <b>2-주기</b>였고
        /// 3회면 «두 상태»가 반드시 한 번은 섞인다.
        /// </summary>
        private const int DeterminismTrials = 3;

        private readonly StringBuilder _report = new StringBuilder(1 << 16);
        private readonly List<string> _failLines = new List<string>();

        private int _pass;
        private int _fail;

        private int _shots;

        /// <summary>비결정으로 «뺀» 격자점 수 — 요약 줄에 «반드시» 같이 찍는다 (재발방지 #74).</summary>
        private int _nondetExcluded;

        /// <summary>
        /// ★ <b>미측정으로 «뺀» 격자점 수</b> — 비결정과 <b>따로</b> 찍는다 (`07 §14-h`).
        /// 합쳐서 「제외 N건」으로 내면 조용한 상한이 된다 (#74).
        /// </summary>
        private int _unmeasuredExcluded;

        /// <summary>
        /// ★★★★ <b>도달 불가로 «뺀» 격자점 수</b> — 22회차 신설. 비결정·미측정과 <b>따로</b> 찍는다
        /// (`07 §20-b` · 재발방지 #112 · #74).
        /// </summary>
        private int _unreachableExcluded;

        private BlumgiConfigData _config;
        private Scene _scene;
        private PhysicsScene2D _physics;
        private BlumgiPhysicsWorld _world;
        private double _step;

        // ────────────────────────────────────────────────────────── 자료형

        /// <summary>격자 한 점 — 원본이 실제로 쏜 force 하나.</summary>
        private sealed class SweepPoint
        {
            public double Force;
            public bool OriginScored;

            /// <summary>바늘 자리인가 — <b>채점에서 뺀다</b> (`07 §12-c`).</summary>
            public bool Needle;

            public string NeedleNote;

            /// <summary>
            /// ★★★ <b>«비결정» 격자점인가 — 채점에서 뺀다</b> (`07 §14` · 14회차-b).
            ///
            /// <para>
            /// <b>원본이 스스로도 재현하지 못하는 입력</b>이다. 같은 원본을 11회차와 14회차-b 가
            /// <b>반대로 쟀고</b>, 그 샷의 원본 접촉 사슬이 <b>5회 이상</b>이라 모서리 스침이 섞인다.
            /// 원본에 정답이 없는 입력을 정답지로 쓰면 «맞출 수 없는 것을 맞추라»는 것이 된다.
            /// </para>
            ///
            /// <para>
            /// ⚠⚠ <b>이것을 면죄부로 쓰지 마라</b> — <b>원본의 골인«율»은 아직 미측정</b>이고,
            /// 제외 범위를 넓히면 그게 바로 재발방지 #48 이다. 등재는 <b>실측 증거가 있는 점만</b>이다.
            /// </para>
            /// </summary>
            public bool Nondeterministic;

            public string NondeterministicNote;

            /// <summary>
            /// ★★ <b>«미측정» 격자점인가 — 채점에서 뺀다</b> (`07 §14-g2`).
            ///
            /// <para>
            /// <b>「비결정」과 다르다.</b> 비결정은 «원본이 같은 입력에 다른 답을 낸다»는 관측이고,
            /// 미측정은 <b>«그 격자값의 원본 답이 아직 없다»</b>는 부재다.
            /// 처방도 다르다 — 비결정은 「더 쏴 보라」, 미측정은 <b>「가서 재라」</b>다.
            /// </para>
            /// </summary>
            public bool Unmeasured;

            public string UnmeasuredNote;

            /// <summary>
            /// ★★★★ <b>«도달 불가» 격자점인가 — 채점에서 뺀다</b> (22회차 신설 · `07 §20-b` · 재발방지 #112).
            ///
            /// <para>
            /// <b>「미측정」과 «다르다».</b> 미측정은 <b>아직 «안» 잰 것</b>이고 처방은 「가서 재라」다.
            /// 도달 불가는 <b>원본이 그 값을 «원리적으로 못 내는 것»</b> — 가서 재도 안 나온다.
            /// 처방은 <b>「격자 위의 값으로 정답지를 옮겨라, 그게 안 되면 이 벡터를 버려라」</b>다.
            /// </para>
            ///
            /// <para>
            /// ⚠⚠ <b>세 줄을 한 줄로 합치지 마라</b> — 합치면 「제외 N건」이라는 <b>조용한 상한</b>이 된다 (#74).
            /// </para>
            /// </summary>
            public bool Unreachable;

            public string UnreachableNote;

            /// <summary>채점 대상인가 — 바늘도 비결정도 미측정도 «도달 불가»도 아닌 점.</summary>
            public bool Graded
            {
                get
                {
                    return Needle == false && Nondeterministic == false &&
                           Unmeasured == false && Unreachable == false;
                }
            }

            public bool OurScored;
        }

        /// <summary>연속한 클리어 구간 하나.</summary>
        private sealed class Window
        {
            public double Lower;
            public double Upper;
            public int Count;

            /// <summary>창 «바로 아래»의 실패 force. 없으면 <see cref="HasPrevFail"/> 가 false.</summary>
            public double PrevFail;

            public bool HasPrevFail;

            public double NextFail;
            public bool HasNextFail;

            public double Width
            {
                get { return Upper - Lower; }
            }
        }

        /// <summary>정답지(원본) 창 하나 — `07 §12-b`.</summary>
        private sealed class ExpectedWindow
        {
            public string Label;
            public double Lower;
            public double Upper;

            /// <summary>하한 «상자» — 우리 하한이 이 안에 들어야 한다. (아래 실패값 ~ 원본 하한)</summary>
            public double LowerBoxMin;

            public double LowerBoxMax;

            /// <summary>상한 «상자» — (원본 상한 ~ 위 실패값)</summary>
            public double UpperBoxMin;

            public double UpperBoxMax;

            /// <summary>하한이 «입력 도메인의 끝»이라 그 아래가 원리적으로 없다 (`07 §12-g`).</summary>
            public bool LowerIsDomainEdge;

            public bool UpperIsDomainEdge;

            /// <summary>「폭」이 아니라 「존재하는가」만 본다 — W1L4 창B.</summary>
            public bool ExistenceOnly;

            public double ConfirmedWidth
            {
                get { return Upper - Lower; }
            }

            public double MaxWidth
            {
                get { return UpperBoxMax - LowerBoxMin; }
            }

            public double MinWidth
            {
                get { return UpperBoxMin - LowerBoxMax; }
            }
        }

        /// <summary>레벨 하나의 정답지 + 격자.</summary>
        private sealed class LevelSpec
        {
            public string Level;
            public List<SweepPoint> Grid = new List<SweepPoint>(48);
            public List<ExpectedWindow> Expected = new List<ExpectedWindow>(2);

            /// <summary>구멍 구간 (하한, 상한) — 이 사이 격자점이 <b>전부 미클리어</b>여야 한다. 없으면 null.</summary>
            public double[] HoleRange;

            public string Note;
        }

        // ────────────────────────────────────────────────────────── 진입

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
            Line($"창 대조 결과: {_pass}/{_pass + _fail}   (총 {_shots} 발 · " +
                 $"★ 비결정 {_nondetExcluded}건 · 도달불가 {_unreachableExcluded}건 · " +
                 $"미측정 {_unmeasuredExcluded}건 제외)");

            if (_failLines.Count > 0)
            {
                Line(string.Empty);
                Line("-- 못 맞춘 항목 --");

                for (int i = 0; i < _failLines.Count; i++)
                    Line("  " + _failLines[i]);
            }

            WriteReport();

            string summary = $"═══ 창 대조 {_pass}/{_pass + _fail} 통과 · {_shots}발 · " +
                             $"비결정 {_nondetExcluded}건 · 도달불가 {_unreachableExcluded}건 · " +
                             $"미측정 {_unmeasuredExcluded}건 제외 · 전문 {ReportPath} ═══";

            if (ok && _fail == 0)
                Log.Success(summary);
            else
                Log.Error(summary + $"  (실패 {_fail}건)");

            yield return null;

            Finish(ok && _fail == 0);
        }

        private bool Body()
        {
            if (LoadData() == false)
                return false;

            CreateScene();

            List<LevelSpec> specs = BuildSpecs();

            Section("0. 이 검사가 무엇을 재는가 — 합격 기준 5-c (`07 §12`)");
            Line("  원본이 실제로 쏜 force 격자(11회차 n=141)를 «그대로» 우리 쪽에 먹여 창을 낸다.");
            Line("  판정 4항목 — ① 창 개수 · ② 창 경계 · ③ 창 폭 · ④ 구멍 위치.");
            Line("  원자료: scratchpad/gap11/progress_W1L*.md · 정본 표: 07_기대값.md §12-b");
            Line(string.Empty);
            ReportNeedleExclusion(specs);
            Line(string.Empty);
            ReportNondeterministicExclusion(specs);

            Section("1. 정답지 «자기검산» — 원본 격자(바늘·비결정 제외)가 §12-b 표를 정말 만들어 내는가");
            Line("  ⚠ 이 절이 재는 것은 «우리 구현»이 아니라 «내가 옮겨 적은 격자»다.");
            Line("     여기가 깨지면 아래 전부가 무의미하므로 먼저 본다.");

            for (int i = 0; i < specs.Count; i++)
                SelfCheckGolden(specs[i]);

            Section("1-b. ★★★★ 검사기 «자기검산» — 같은 입력이 같은 답을 내는가  [게이트 · 재발방지 #106·#109]");
            CheckDeterminism();

            Section("2. ★★ 우리 쪽 스윕 — 원본과 «같은 격자»로 레벨1~5 전수");

            for (int i = 0; i < specs.Count; i++)
                SweepLevel(specs[i]);

            Section("3. ★★★ 창 대 창 판정 — ① 개수 · ② 경계 · ③ 폭 · ④ 구멍");

            for (int i = 0; i < specs.Count; i++)
                JudgeLevel(specs[i]);

            Section("4. 바늘 자리에서 우리는 무엇이 나왔나  [채점 아님 · 곡선 고정용]");
            ReportNeedleResults(specs);

            Section("5. 경계 «미세화» — 우리 창의 실제 경계는 어디인가  [채점 아님 · 참고]");
            Line("  ⚠ 원본 경계는 격자 해상도(1.4~6.0)까지만 좁혀져 있다 (`07 §12-g`).");
            Line("     그래서 이 숫자를 원본과 직접 빼면 «해상도가 다른 것»을 비교하게 된다 — 채점하지 않는다.");

            for (int i = 0; i < specs.Count; i++)
                RefineBoundaries(specs[i]);

            Section("6. 난이도 스펙트럼 — 원본 vs 우리  [`07 §12-d`]");
            ReportSpectrum(specs);

            Section("7. ★★ 어긋난 점의 «모양» — 원인을 어디서 찾을지 정해 준다  [채점 아님 · `07 §9-e` 와 같은 도구]");
            Line("  ⚠ 「원인은 물리다」를 «추측»으로 남기지 않는다. 갈린 점만 다시 쏘면서");
            Line("     발사속도 · 반발 횟수 · 첫 반발 시각/자리 · 감지기 겹침 · 종료 사유를 찍는다.");
            Line("  ⚠ 여기서 나온 숫자로 물리·데이터 값을 «고르지» 않는다 (재발방지 #48). 자리를 «가리킬» 뿐이다.");

            for (int i = 0; i < specs.Count; i++)
                DiagnoseMismatches(specs[i]);

            Section("7-b. ★★★ W1L3 «첫 반발» 정답지 대조 — 발사점 모델이 실제로 들어갔는가  [채점 아님 · 12회차 5점]");
            ReportFirstBounceW1L3();

            Section("8. 이미 등재된 격차인가, 새로 나온 것인가  [읽는 표 · ★ 기계가 만든다]");
            ReportGapRegistry(specs);

            return true;
        }

        // ────────────────────────────────────────────────────────── 자기검산

        /// <summary>
        /// ★★★★ <b>채점하기 «전»에 검사기 자신이 결정적인지 본다</b> (재발방지 #106 · #109).
        ///
        /// <para>
        /// <b>[사고 · 패스 ①-q]</b> 이 검사기는 <b>같은 입력에 두 가지 답</b>을 내고 있었다 —
        /// 한 보고서 안에서 스윕 · §7 재현 · §8 재현이 서로 어긋났고, 사슬 추적기와도 갈렸다.
        /// 그 상태로 점수를 읽으면 <b>개선·악화 판정이 통째로 근거를 잃는다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「대개 재현된다」로 넘기지 마라</b> — 흔들리는 것은 <b>모서리 스침이 있는 샷뿐</b>이다.
        /// 그래서 <b>실제로 갈렸던 자리</b>와 <b>안 갈렸던 자리</b>를 «둘 다» 태운다 (#77 — 참·거짓 양쪽).
        /// </para>
        /// </summary>
        private void CheckDeterminism()
        {
            (string, double, string)[] probes =
            {
                ("W1L3", 21.4565, "★ 패스 ①-q 이전에 «실제로 갈리던» 자리 (골인 ↔ 미골인 2-주기)"),
                ("W1L1", 66.3860, "★ 패스 ①-q 이전에 «실제로 갈리던» 자리 (반발 37 ↔ 31)"),
                ("W1L3", 54.0000, "대조군 — 그때도 5/5 로 안 갈리던 자리"),
            };

            Line("  ⚠ 이 절이 실패하면 «아래 모든 숫자»가 근거를 잃는다 — 점수보다 먼저 읽는다.");
            Line(string.Empty);

            for (int i = 0; i < probes.Length; i++)
            {
                string level = probes[i].Item1;
                double force = probes[i].Item2;

                bool first = false;
                int firstBounces = 0;
                double firstSeconds = 0.0;
                bool same = true;

                for (int k = 0; k < DeterminismTrials; k++)
                {
                    ShotTrace t;
                    bool scored = ShootForce(level, force, out t);

                    if (k == 0)
                    {
                        first = scored;
                        firstBounces = t.Bounces;
                        firstSeconds = t.Seconds;
                        continue;
                    }

                    if (scored != first || t.Bounces != firstBounces ||
                        Math.Abs(t.Seconds - firstSeconds) > 1e-9)
                    {
                        same = false;
                    }
                }

                Check($"{level} force {force:0.0000} — 같은 입력 {DeterminismTrials}회가 «같은 답»인가  [{probes[i].Item3}]",
                      same,
                      same
                          ? $"{DeterminismTrials}/{DeterminismTrials} 동일 ({(first ? "골인" : "미골인")} · " +
                            $"반발 {firstBounces}회 · {firstSeconds:0.000}s)"
                          : $"★★ 갈린다 — 첫 시행은 {(first ? "골인" : "미골인")} · 반발 {firstBounces}회 · " +
                            $"{firstSeconds:0.000}s 였는데 뒤 시행이 다르다. " +
                            "월드 재사용을 의심한다 (RecreateScene 주석)");
            }
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

        private void CreateScene()
        {
            _scene = SceneManager.CreateScene("BlumgiWindow" + (_sceneSerial++).ToString(CultureInfo.InvariantCulture),
                                              new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physics = _scene.GetPhysicsScene2D();

            var worldGo = new GameObject("BlumgiPhysicsWorld");
            SceneManager.MoveGameObjectToScene(worldGo, _scene);
            _world = worldGo.AddComponent<BlumgiPhysicsWorld>();
        }

        private int _sceneSerial;

        /// <summary>
        /// ★★★★ <b>샷마다 물리 «월드»를 통째로 새로 만든다</b> — 오브젝트만 헐면 부족하다.
        ///
        /// <para>
        /// <b>[실측 · 패스 ①-q · 재발방지 #106]</b> 이 검사기는 <b>같은 입력에 다른 답</b>을 내고 있었다.
        /// <c>W1L3 21.4565</c> 를 «같은 호출»로 5회 연속 쏘면 <b>골인/미골인/골인/미골인/골인</b> 이고,
        /// 0·2·4 는 서로 <b>비트까지 같다</b>. 그래서 §2 스윕(미골인) · §7 재현(골인 18반발 4.33 s) ·
        /// §8 재현(미골인 40반발 15.75 s) 이 <b>한 보고서 안에서 서로 어긋났고</b>,
        /// 사슬 추적기(골인)와도 갈렸다.
        /// </para>
        ///
        /// <para>
        /// 원인은 <b>Box2D 월드의 내부 재사용</b>이다 — <c>Teardown</c> 이 바디를 지우면 프록시·할당자가
        /// 자유목록으로 돌아가고, 다음 <c>Build</c> 가 그 자리를 <b>역순으로</b> 집어 간다.
        /// 바디 순서가 바뀌면 솔버 누적 임펄스의 «더하는 순서»가 바뀌고(부동소수 비결합)
        /// 그 1e−7 이 접촉 6 (스텝 260)에서 <b>0.024 px</b> 로 벌어져 사슬이 통째로 갈린다.
        /// <b>새 씬 = 새 <c>b2World</c></b> 라 그 자유목록이 처음부터다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>씬을 새로 만들면 60발을 쏴도 «전부 같다»</b> [실측] — 앞선 샷 수를 1·2·3발로 바꿔도 같다.
        /// 값도 싸다(60발 218 ms). 그리고 이것이 <b>원본 절차</b>다 — 원본도 시행마다 ↺ 로 레이아웃을 다시 깐다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>점수를 보고 고른 것이 아니다</b> (재발방지 #48) — 「같은 입력에 같은 답이 나오는가」로 골랐다.
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
        }

        private static GameObject LoadPrefab(string prefabName)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{prefabName}.prefab");
#else
            return null;
#endif
        }

        // ────────────────────────────────────────────────────────── 정답지 (07 §12)

        /// <summary>
        /// 11회차 원자료(`gap11/progress_W1L*.md` · `07 §12`) 그대로. <b>force 오름차순</b>이다.
        /// ⚠ <b>이 배열을 «정돈»하지 마라</b> — 중복 force 는 원본이 실제로 그 자리를 여러 번 쏜 기록이고,
        /// 「몇 점으로 잰 창인가」가 신뢰도이기 때문에 시행 수가 줄면 안 된다.
        /// </summary>
        private static List<LevelSpec> BuildSpecs()
        {
            var list = new List<LevelSpec>(5);

            // ── W1L1 — n = 23 · 클리어 12 · 창 1개 [실측]
            var l1 = new LevelSpec { Level = "W1L1", Note = "연속 1개 · 구멍 없음" };
            Add(l1, 21.9240, false); Add(l1, 26.9565, false); Add(l1, 27.4130, false);
            Add(l1, 27.4185, false); Add(l1, 27.4240, false);
            Add(l1, 28.7880, true); Add(l1, 30.6250, true); Add(l1, 32.4565, true);
            Add(l1, 37.9675, true); Add(l1, 42.9670, true); Add(l1, 48.9565, true);
            Add(l1, 54.4565, true); Add(l1, 59.9455, true); Add(l1, 65.0110, true);
            Add(l1, 66.3695, true); Add(l1, 66.3750, true); Add(l1, 66.3860, true);
            Add(l1, 68.2010, false); Add(l1, 70.5055, false); Add(l1, 73.7065, false);
            Add(l1, 76.0055, false); Add(l1, 87.9075, false); Add(l1, 98.9130, false);
            l1.Expected.Add(new ExpectedWindow
            {
                Label = "W1L1 창",
                Lower = 28.79, Upper = 66.39,
                LowerBoxMin = 27.4240, LowerBoxMax = 28.7880,
                UpperBoxMin = 66.3860, UpperBoxMax = 68.2010
            });
            list.Add(l1);

            // ── W1L2 — n = 27 · 클리어 11 · 창 1개 [실측 · 「두 창」 의심은 반증됨]
            var l2 = new LevelSpec { Level = "W1L2", Note = "연속 1개 · 구멍 없음 (「두 창」 의심 반증)" };
            Add(l2, 21.0055, false); Add(l2, 26.5055, false); Add(l2, 32.4620, false);
            Add(l2, 38.4295, false); Add(l2, 39.3315, false); Add(l2, 39.3315, false);
            Add(l2, 39.3370, false); Add(l2, 39.7935, false); Add(l2, 40.7065, false);
            Add(l2, 40.7120, false); Add(l2, 40.7175, false); Add(l2, 41.6250, false);
            Add(l2, 42.5380, true); Add(l2, 43.9130, true); Add(l2, 49.4185, true);
            Add(l2, 54.9295, true); Add(l2, 59.9675, true); Add(l2, 65.4565, true);
            Add(l2, 68.2120, true); Add(l2, 70.5000, true); Add(l2, 70.5055, true);
            Add(l2, 70.5055, true); Add(l2, 71.4075, true);
            Add(l2, 73.6845, false); Add(l2, 76.9185, false); Add(l2, 87.9185, false);
            Add(l2, 98.9185, false);
            l2.Expected.Add(new ExpectedWindow
            {
                Label = "W1L2 창",
                Lower = 42.54, Upper = 71.41,
                LowerBoxMin = 41.6250, LowerBoxMax = 42.5380,
                UpperBoxMin = 71.4075, UpperBoxMax = 73.6845
            });
            list.Add(l2);

            // ── W1L3 — n = 21 · 클리어 15 · 창 1개 · ★ 하한이 «없다» (발사 최소에서 이미 클리어)
            //   ★ [18회차] 59.5275 라벨이 클리어 → 미클리어로 정정됐다(클리어 16 → 15).
            //     다만 그 점은 «비결정 등재»라 채점·창 추출에서 «없는 셈» 치므로 창 모양은 그대로 1개다.
            var l3 = new LevelSpec { Level = "W1L3", Note = "연속 1개 · 하한이 «입력 도메인의 끝»이다" };
            Add(l3, 17.3425, true); Add(l3, 17.7880, true); Add(l3, 18.2500, true);
            Add(l3, 18.2500, true); Add(l3, 19.6250, true); Add(l3, 20.5490, true);
            Add(l3, 21.4565, true); Add(l3, 26.9620, true); Add(l3, 32.4455, true);
            Add(l3, 38.4185, true); Add(l3, 43.0275, true); Add(l3, 48.9620, true);
            Add(l3, 54.0000, true);

            // ★★★ [18회차 정정 · `07 §14-i`] 11회차는 이 자리를 «클리어»로 적었는데 —
            //   18회차가 «force 축 러너»로 실 force 59.478~59.511 을 여덟 발 짚어 보니 «1/8 (13 %)» 다.
            //   ⇒ 원본은 이 자리에서 87 % «못 넣는다». 라벨을 미클리어로 고친다.
            //   ⚠ 근거는 «점수»가 아니라 원본 재측정이다 (재발방지 #48).
            //   ⚠ 다만 같은 실 force 59.5000 세 발이 «2 미골인 ↔ 1 골인»으로 갈렸다 ⇒ «비결정» 등재이기도 하다.
            //     그래서 이 점은 Add() 안에서 자동으로 «채점 제외»가 붙는다 — 라벨은 «표에 남기는 사실»이다.
            Add(l3, 59.5275, false);

            Add(l3, 65.9845, true);
            Add(l3, 76.9295, true);
            Add(l3, 81.4890, false); Add(l3, 81.5000, false); Add(l3, 81.5000, false);
            Add(l3, 87.9405, false); Add(l3, 98.8525, false);
            l3.Expected.Add(new ExpectedWindow
            {
                Label = "W1L3 창",
                Lower = 17.3425, Upper = 76.93,
                LowerBoxMin = 17.3425, LowerBoxMax = 17.3425, LowerIsDomainEdge = true,
                UpperBoxMin = 76.9295, UpperBoxMax = 81.4890
            });
            list.Add(l3);

            // ── W1L4 — n = 37 · 클리어 9 · ★★ 유일한 «파편» 레벨 (창A + 큰 구멍 + 창B(섬))
            //   ★ [18회차] 36.5870 라벨이 미클리어 → 클리어로 정정됐다(클리어 8 → 9).
            var l4 = new LevelSpec
            {
                Level = "W1L4",
                Note = "★ 파편 — 창A + 큰 구멍(37.50~49.88) + 창B(섬)",

                // ★ [18회차 정정] 구멍의 «아래 끝»이 34.7610 → 36.5870 으로 내려온다.
                //   36.5870 자리가 «원본 골인»으로 밝혀져 창A 안으로 들어왔기 때문이다.
                HoleRange = new[] { 36.5870, 51.2500 }
            };
            Add(l4, 18.2390, false);
            Add(l4, 24.1900, true); Add(l4, 30.1685, true); Add(l4, 34.7610, true);

            // ★★★ [18회차 정정 · `07 §15-k7` 해소] 11·16회차는 이 자리를 «미클리어»로 두었는데 —
            //   16회차 6샷이 35.73 / 37.04 / 37.50 / 37.96 «위아래»에만 떨어져 격자값을 못 짚은 것이었다.
            //   18회차 force 축 러너가 실 force 36.5760~36.5925 «여섯 발»을 짚어 «5/6 (83 %) 골인» 이다.
            //   ★ 한 발은 실 force 가 36.5870 «그 자체»(소수 4자리 일치)이고 ★골인(+4149.9 ms) 이다.
            //   ⇒ 라벨을 클리어로 고친다. ⚠ 근거는 «점수»가 아니라 원본 재측정이다 (#48) —
            //     이 정정은 우리 쪽 결과(골인)와 «같아지는» 방향이라 특히 조심해서 근거를 적어 둔다.
            //   ⚠ 비결정 여지는 남는다 — 36.5925 한 발만 미골인이다(한 양자의 1/40 차이가 뒤집는다).
            //     그러나 «같은 값 두 시행이 갈리는 것»은 못 봤으므로 비결정으로 «등재하지 않는다» (#83).
            Add(l4, 36.5870, true);

            Add(l4, 37.5000, false); Add(l4, 38.4185, false);
            Add(l4, 39.3260, false); Add(l4, 39.3260, false); Add(l4, 39.3645, false);
            Add(l4, 40.2390, false); Add(l4, 40.2500, false); Add(l4, 40.2610, false);
            Add(l4, 41.1630, false);
            AddNeedle(l4, 45.7445, true, "성공 바늘 — 옆값 45.7500 X ×2 와 0.0055 차 (구멍 «안»)");
            Add(l4, 45.7500, false); Add(l4, 45.7500, false); Add(l4, 46.2175, false);
            Add(l4, 48.5110, false); Add(l4, 49.8750, false);
            Add(l4, 51.2500, true); Add(l4, 51.2555, true); Add(l4, 51.2555, true);
            Add(l4, 51.2940, true);
            Add(l4, 53.0870, false); Add(l4, 53.9945, false); Add(l4, 56.7390, false);
            Add(l4, 56.7500, false); Add(l4, 56.7500, false); Add(l4, 57.2065, false);
            Add(l4, 62.2500, false); Add(l4, 62.2555, false); Add(l4, 62.6845, false);
            Add(l4, 62.7065, false); Add(l4, 74.1575, false); Add(l4, 85.1575, false);
            Add(l4, 100.0000, false);
            l4.Expected.Add(new ExpectedWindow
            {
                Label = "W1L4 창A",

                // ★ [18회차 정정] 상한이 34.76 → 36.59 로 «올라간다» — 36.5870 이 원본 골인이라
                //   창A 안으로 들어왔다. 상한 상자도 한 칸 밀린다: [34.7610~36.5870] → [36.5870~37.5000].
                Lower = 24.19, Upper = 36.59,
                LowerBoxMin = 18.2390, LowerBoxMax = 24.1900,
                UpperBoxMin = 36.5870, UpperBoxMax = 37.5000
            });
            l4.Expected.Add(new ExpectedWindow
            {
                Label = "W1L4 창B(섬)",
                Lower = 51.25, Upper = 51.29,
                LowerBoxMin = 49.8750, LowerBoxMax = 51.2500,
                UpperBoxMin = 51.2940, UpperBoxMax = 53.0870,
                ExistenceOnly = true
            });
            list.Add(l4);

            // ── W1L5 — n = 33 · 클리어 31 · 창 1개 (양끝이 «입력 도메인의 끝») + 실패 바늘 2
            var l5 = new LevelSpec { Level = "W1L5", Note = "사실상 전 구간 · 양끝이 도메인의 끝" };
            Add(l5, 17.7935, true); Add(l5, 19.6305, true); Add(l5, 23.7445, true);
            Add(l5, 23.7500, true); Add(l5, 23.7500, true);
            AddNeedle(l5, 24.6520, false, "실패 바늘 — 옆값 24.6630 O ×3 과 0.011 차 (창 «안»)");
            Add(l5, 24.6630, true); Add(l5, 24.6630, true); Add(l5, 24.6630, true);
            Add(l5, 29.7120, true); Add(l5, 35.6630, true); Add(l5, 40.7010, true);
            Add(l5, 45.7555, true); Add(l5, 51.7065, true); Add(l5, 57.2065, true);
            Add(l5, 62.2170, true); Add(l5, 62.2500, true); Add(l5, 62.2885, true);
            AddNeedle(l5, 63.1410, false, "실패 바늘 — 옆값 63.1575 O 와 0.017 차 (창 «안»)");
            Add(l5, 63.1575, true); Add(l5, 63.1685, true); Add(l5, 63.1740, true);
            Add(l5, 67.7500, true); Add(l5, 73.2500, true); Add(l5, 73.2500, true);
            Add(l5, 73.2555, true); Add(l5, 74.1465, true); Add(l5, 78.7500, true);
            Add(l5, 84.2445, true); Add(l5, 89.7500, true); Add(l5, 95.2500, true);
            Add(l5, 100.0000, true); Add(l5, 100.0000, true);
            l5.Expected.Add(new ExpectedWindow
            {
                Label = "W1L5 창",
                Lower = 17.7935, Upper = 100.0000,
                LowerBoxMin = 17.7935, LowerBoxMax = 17.7935, LowerIsDomainEdge = true,
                UpperBoxMin = 100.0000, UpperBoxMax = 100.0000, UpperIsDomainEdge = true
            });
            list.Add(l5);

            return list;
        }

        /// <summary>
        /// 격자점 하나. ★ <b>비결정 등재(`07 §14`)는 여기서 «자동으로» 붙는다</b> —
        /// 손으로 표시하면 등재 목록과 격자가 갈린다.
        /// </summary>
        private static void Add(LevelSpec spec, double force, bool scored)
        {
            var point = new SweepPoint { Force = force, OriginScored = scored };
            GoldenVectors.NondeterministicPoint nd = GoldenVectors.FindNondeterministic(spec.Level, force);

            if (nd != null)
            {
                point.Nondeterministic = true;
                point.NondeterministicNote = nd.Evidence;
            }

            // ★ 「미측정」은 «비결정»과 따로 붙는다 (`07 §14-g2`).
            GoldenVectors.UnmeasuredPoint um = GoldenVectors.FindUnmeasured(spec.Level, force);

            if (um != null)
            {
                point.Unmeasured = true;
                point.UnmeasuredNote = um.Reason;
            }

            // ★★★★ 「도달 불가」는 «미측정»과 또 따로 붙는다 (22회차 신설 · `07 §20-b` · #112).
            GoldenVectors.UnreachablePoint ur = GoldenVectors.FindUnreachable(spec.Level, force);

            if (ur != null)
            {
                point.Unreachable = true;
                point.UnreachableNote = ur.Evidence;
            }

            spec.Grid.Add(point);
        }

        private static void AddNeedle(LevelSpec spec, double force, bool scored, string note)
        {
            spec.Grid.Add(new SweepPoint
            {
                Force = force,
                OriginScored = scored,
                Needle = true,
                NeedleNote = note
            });
        }

        // ────────────────────────────────────────────────────────── 0. 바늘 고지

        /// <summary>
        /// ⚠ <b>「뺐다」를 반드시 화면에 남긴다</b> (`07 §12-c`). 조용히 빼면 다음 회차가 다시 넣는다.
        /// </summary>
        private void ReportNeedleExclusion(List<LevelSpec> specs)
        {
            int needles = 0;

            Line("  ── ⚠⚠ 채점에서 «뺀» 바늘 자리 (`07 §12-c`) ──");

            for (int i = 0; i < specs.Count; i++)
            {
                for (int k = 0; k < specs[i].Grid.Count; k++)
                {
                    SweepPoint p = specs[i].Grid[k];

                    if (p.Needle == false)
                        continue;

                    needles++;
                    Line($"     [제외] {specs[i].Level} force {p.Force:0.0000} " +
                         $"(원본 {(p.OriginScored ? "클리어" : "미클리어")}) — {p.NeedleNote}");
                }
            }

            Line($"     [제외] W1L4 force 39.8155 — 성공 바늘 [10회차 2/2] · " +
                 "11회차 격자에 «없어서» 아래 스윕에도 없다");
            Line($"  ⇒ 격자에서 뺀 바늘 {needles}자리 + 격자 밖 바늘 1자리. " +
                 "폭이 force 0.0055~0.03(홀드 0.1~0.5ms)이라 «품질이 아니라 우연»을 재게 된다.");
            Line("     지우지는 않는다 — 곡선 고정용으로 §4 에서 우리 결과를 같이 찍는다.");
        }

        /// <summary>
        /// ★★★ <b>「비결정 N건 제외」를 «반드시» 화면에 남긴다</b> (`07 §14` · 재발방지 #74 「조용한 상한 금지」).
        ///
        /// <para>
        /// ⚠⚠ <b>이 절은 «면죄부»가 아니다.</b> 원본의 골인«율»은 아직 미측정이고,
        /// 지금 하는 일은 <b>「원본이 갈리는 자리를 표시하는 것」까지</b>다.
        /// <b>창 점수를 좋게 보이려고 제외 범위를 넓히면 그게 바로 재발방지 #48 이다</b> —
        /// 그래서 등재 기준을 <b>「두 회차가 같은 원본을 반대로 쟀다」 + 「원본 접촉 사슬 ≥ 5」</b> 로 못 박았다.
        /// </para>
        /// </summary>
        private void ReportNondeterministicExclusion(List<LevelSpec> specs)
        {
            Line("  ── ⚠⚠ 채점에서 «뺀» 비결정 격자점 (`07 §14` · 15회차 정정 `§14-g` · ★ 18회차 신규 `§14-i`) ──");
            Line("     ★★ [15회차 정정] 등재를 «W1L3 force 36.5870 하나»로 줄였었다.");
            Line("        14회차-b 는 5자리(격자 6자리)를 「11회차 O / 14회차 X」로 반대로 재서 올렸는데,");
            Line("        그 판정은 «자리마다 n = 1» 위에 서 있었다. 15회차가 원본에서 «124 발»을 다시 쏘니 —");
            Line("           force 26.9620 → 8/8 골인   ·   32.4455 → 3/4   ·   21.4565 → 1/1   ·   18.2500 → 1/2");
            Line("        ⇒ 「원본이 스스로도 못 한다」의 근거가 무너졌다. 그 자리들은 «우리 결함»이다.");
            Line("        ⚠ 근거는 «점수»가 아니라 «원본 재측정»이다 (#48). 제외를 «줄이는» 방향이라 점수는 나빠진다.");
            Line(string.Empty);
            Line("     ① W1L3 force 36.587 — 두 시행이 «발사 0.001 px 이내로 같은데 한쪽만 골인»했다.");
            Line("        접촉 4까지 같다가 접촉 5(모서리 스침 · 법선 [−0.167,−0.986])에서 33 ms 어긋나고 뒤가 갈린다.");
            Line("        씨앗은 원본의 «가변 rAF dt»(7.4~9.1 ms) — +858 ms 에 0.594 px 이 벌어진다.");
            Line(string.Empty);
            Line("     ★★★ [18회차 신규 등재] ② W1L3 force 59.5275 — «두 번째» 직접 관측 사례다.");
            Line("        15·16회차는 «홀드 ms» 축이라 한 양자(force 0.46) 아래를 못 겨눴다 —");
            Line("        892 ms 는 59.94(위) · 880 ms 는 59.05(아래)로 떨어졌다. 그래서 «미측정»이었다.");
            Line("        18회차가 «홀드 중 forceShoot_P1 폴링 → 목표에서 릴리즈» 러너로 실 force 59.478~59.511 을");
            Line("        여덟 발 짚었고, ★ 실 force 59.5000 «세 발»이 2 미골인 ↔ 1 골인으로 «직접» 갈렸다.");
            Line(string.Empty);
            Line("     ⚠⚠ 「비결정」과 「원본이 거의 안 넣는다」는 «다른 정보»다 — 표에서 가른다.");
            Line("        59.5275 의 원본 골인율은 1/8 (13 %) 다. 13 % 는 0 % 가 «아니고», 반반도 «아니다».");
            Line("        ⇒ 11회차의 「클리어」와 14회차-b 의 「미클리어」는 서로를 반증한 것이 아니라");
            Line("          «같은 분포에서 다른 표본을 뽑은 것»이다. 이 자리는 채점에서 뺀다.");
            Line(string.Empty);

            List<GoldenVectors.NondeterministicPoint> registry = GoldenVectors.Nondeterministic();

            for (int i = 0; i < registry.Count; i++)
            {
                Line($"     [등재] {registry[i].Level} force {registry[i].GridForce:0.0000} " +
                     $"({registry[i].Rate}) — {registry[i].Evidence}");
            }

            int hit = 0;

            for (int i = 0; i < specs.Count; i++)
            {
                for (int k = 0; k < specs[i].Grid.Count; k++)
                {
                    SweepPoint p = specs[i].Grid[k];

                    if (p.Nondeterministic == false)
                        continue;

                    hit++;
                    Line($"     [제외] {specs[i].Level} force {p.Force:0.0000} — {p.NondeterministicNote}");
                }
            }

            Line(string.Empty);
            Line($"  ⇒ 비결정 «등재» {registry.Count}자리 / 그 중 11회차 격자에 «걸리는» 것 {hit}자리");
            Line($"     ⇒ 실제로 채점에서 빠지는 격자점 = «{hit}자리»");

            if (hit == 0)
            {
                Line("     ★ 0 자리인 것을 «조용히» 넘기지 않는다 — W1L3 격자는 32.4455 다음이 38.4185 라");
                Line("       force 36.5870 «그 자리»가 애초에 격자에 없다. 즉 「비결정이라 뺐다」로 가려지는 점이 하나도 없다.");
            }
            else
            {
                Line("     ★ 「뺐다」를 «조용히» 넘기지 않는다 — 등재 2자리 중 W1L3 36.5870 은 격자에 «없고»,");
                Line("       실제로 빠지는 것은 W1L3 59.5275 «한 자리»다 (18회차 신규). 그 자리는 원본이 87 % 못 넣는다 —");
                Line("       즉 «우리 미골인»과 방향이 같다. 그래도 «갈리는 것을 직접 봤으므로» 채점에서 뺀다.");
            }

            Line("     ⚠ 다른 4레벨은 «결정적»이 아니라 «미측정»이다 — 그 레벨들의 원본 접촉 사슬 길이를 아무도 안 쟀다.");
            Line("       미측정을 「제외 안 함」으로 두는 쪽이 안전하다. 넓히려면 «새 실측»이 있어야 한다.");
            Line(string.Empty);

            // ★★ 「미측정」은 «비결정»과 «따로» 찍는다 — 합치면 조용한 상한이 된다 (#74 · `07 §14-h`).
            Line("  ── ⚠ 채점에서 «뺀» 미측정 격자점 (`07 §14-g2`) — 비결정과 «다르다» ──");
            Line("     비결정 = 원본이 같은 입력에 «다른 답»을 낸다(관측)   ·   처방: 「더 쏴 보라」");
            Line("     미측정 = 그 격자값의 «원본 답이 아직 없다»(부재)     ·   처방: 「가서 재라」");
            Line(string.Empty);

            int unmeasured = 0;

            for (int i = 0; i < specs.Count; i++)
            {
                for (int k = 0; k < specs[i].Grid.Count; k++)
                {
                    SweepPoint p = specs[i].Grid[k];

                    if (p.Unmeasured == false)
                        continue;

                    unmeasured++;
                    Line($"     [제외] {specs[i].Level} force {p.Force:0.0000} — {p.UnmeasuredNote}");
                }
            }

            Line(string.Empty);
            Line($"  ⇒ 미측정으로 뺀 격자점 «{unmeasured}자리». **재측정 대기**에 올라 있다 (`07 §7`).");

            if (unmeasured == 0)
            {
                Line("     ★★ [18회차] 0 자리인 것을 «조용히» 넘기지 않는다 — 유일한 등재였던");
                Line("        W1L3 59.5275 가 «실제로 측정»돼 「미측정」에서 내려왔다(→ 비결정 등재).");
                Line("        ⇒ 지금 「아직 안 쟀다」로 빠지는 격자점은 «없다». 목록은 지우지 않고 비워 둔다.");
            }

            ReportUnreachableExclusion(specs);


            // ★ 정답지 자체의 «자기모순»을 여기서 못 박는다 — 조용히 한쪽 편을 들지 않는다.
            Line(string.Empty);
            Line("  ── 정답지 자기모순 (양쪽 다 원본 실측이다 · 어느 쪽도 «틀린 것»이 아니다) ──");
            Line("     11회차: GetMainRunningLayout()._name 폴링 (최대 9 s · 득점 래치 시 +8 s)");
            Line("     14회차-b: 공 인스턴스 변수 iv[2] (hasScored) 를 rAF 마다 «직독»  ← 방법은 이쪽이 낫다");
            Line("     ⚠ 어긋남 방향이 «11회차 O · 14회차 X» 라 폴링 누락으로는 설명이 안 된다.");
            Line("       ⇒ 14회차-b 는 여기서 «원본이 갈린다»로 결론냈다. 15회차가 그 자리들을 3~8회 다시 쏘니");
            Line("         원본이 50~100 % 골인한다 ⇒ 남는 설명은 «14회차-b 의 n=1 시행이 그저 실패한 한 발»이다.");
            Line("         두 회차가 반대로 잰 것은 사실이지만 그것만으로 «비결정»을 선언할 수 없다 (#83).");
            Line(string.Empty);
            Line("     ★★★ [18회차] 그리고 «축을 바꾸면 자기모순이 자기모순이 아니게 되는» 자리가 둘 나왔다.");
            Line("        W1L4 36.5870 : 11·16회차 「미클리어」 ↔ 18회차 5/6 «골인» ⇒ 격자표 쪽이 틀렸다 (라벨 정정).");
            Line("        W1L3 59.5275 : 11회차 「클리어」 ↔ 18회차 1/8 «미골인 우세» ⇒ 격자표 쪽이 틀렸다 (라벨 정정).");
            Line("        두 자리 다 «홀드 ms 축으로는 격자값을 못 짚어서» 생긴 것이다 — 값이 아니라 «축»이 원인이었다.");
            Line("        ⚠ 이 정정은 우리 점수를 «올리는» 방향이다. 그래서 근거를 여기 그대로 적어 둔다 (#48) —");
            Line("          근거는 점수가 아니라 «원본 재측정»(force 축 러너 · 채택 창 ±0.23 · n = 6 과 8)이다.");
        }

        /// <summary>
        /// ★★★★ <b>22회차 신설 — 「도달 불가」를 «따로» 찍는다</b> (`07 §20-b` · 재발방지 #112 · #74).
        ///
        /// <para>
        /// ⚠⚠ <b>이 절은 «면죄부»가 아니다.</b> 도달 불가로 빼는 것은 <b>「원본이 그 값을 못 낸다」</b>는
        /// 뜻이지 <b>「우리가 맞다」</b>는 뜻이 아니다. 그래서 <b>분류 «근거»를 숫자로 같이 찍는다</b> —
        /// 스윕 시행 수 · 격자 간격 · 최근접 Δ · 드리프트 폭. <b>근거 없이 빼면 그게 #48 이다.</b>
        /// </para>
        /// </summary>
        private void ReportUnreachableExclusion(List<LevelSpec> specs)
        {
            Line(string.Empty);
            Line("  ── ★★★★ 채점에서 «뺀» 도달 불가 격자점 (22회차 신설 · `07 §20-b` · 재발방지 #112) ──");
            Line("     ⚠⚠ 「미측정」과 «다르다». 네 갈래를 한 줄로 합치지 않는다 (#74).");
            Line("        비결정   = 원본이 같은 입력에 «다른 답»을 낸다(관측)  ·  처방: 「더 쏴 보라」");
            Line("        미측정   = 그 격자값의 «원본 답이 아직 없다»(부재)    ·  처방: 「가서 재라」");
            Line("        도달불가 = 원본이 그 값을 «원리적으로 못 낸다»        ·  처방: 「격자 위로 옮기거나 버려라」");
            Line("        채점불가 = 원본은 결정적인데 «입력 분해능 1칸 > 허용치» ·  처방: 「이 벡터를 골든에서 버려라」");
            Line(string.Empty);
            Line("     왜 못 내나 — 원본 forceShoot_P1 은 틱마다 «+= 55·dt» 로 누적된다.");
            Line("       ⇒ 도달 가능한 force 가 «이산 격자» 위에 놓이고 그 격자는 «홀드 시작의 프레임 위상»에 물린다.");
            Line("       ⇒ 격자 간격보다 위상 드리프트 폭이 작으면 «격자 사이의 값»은 영영 안 나온다.");
            Line(string.Empty);

            List<GoldenVectors.UnreachablePoint> registry = GoldenVectors.Unreachable();
            int hit = 0;

            for (int i = 0; i < registry.Count; i++)
            {
                GoldenVectors.UnreachablePoint u = registry[i];

                Line($"     [등재] {u.Level} force {u.GridForce:0.0000}");
                Line($"            근거 숫자 — 스윕 {u.SweepTrials}회 · 격자 간격 {u.GridSpacing:0.0000} · " +
                     $"최근접 Δ {u.NearestDelta:0.0000} · 드리프트 «전»폭 {u.DriftSpan:0.0000} (= ±{u.DriftSpan * 0.5:0.0000})");
                Line($"            ★ 판정식: 최근접 Δ({u.NearestDelta:0.0000}) > 드리프트 폭({u.DriftSpan:0.0000}) " +
                     $"⇒ {(u.NearestDelta > u.DriftSpan ? "«도달 불가»" : "⚠ 판정 미성립 — 등재를 다시 봐라")}");
                Line($"            {u.Evidence}");
            }

            for (int i = 0; i < specs.Count; i++)
            {
                for (int k = 0; k < specs[i].Grid.Count; k++)
                {
                    if (specs[i].Grid[k].Unreachable == false)
                        continue;

                    hit++;
                    Line($"     [제외] {specs[i].Level} force {specs[i].Grid[k].Force:0.0000} — 11회차 격자에 «걸린다»");
                }
            }

            Line(string.Empty);
            Line($"  ⇒ 도달 불가 «등재» {registry.Count}자리 / 그 중 11회차 격자에 «걸리는» 것 {hit}자리");
            Line("     ★★ 제외 범위를 «넓히지 않았다» (#48 · #74) — 22회차가 실제로 스윕을 돌린 «한 자리»만이다.");
            Line("     ⚠⚠ W1L1 66.3860 은 «도달 불가가 아니다» — 채점에 그대로 남아 있다.");
            Line("        19회차가 그 값을 «소수 4자리까지 그대로» 짚어 ★골인 +4318.2 ms 를 쟀고(`07 §18-b1` F1#1),");
            Line("        22회차 A런 3발(66.3035 · 66.4025 · 66.3695)도 «전부 원본 골인»이다.");
            Line("        ⇒ 드물지만 «도달한다» ⇒ 그 자리의 정답지는 확정돼 있다 ⇒ 뺄 근거가 «없다».");
            Line("        ★ 우리가 그 자리에서 반발 31회로 갈리는 것은 «우리 결함»이다 (#90 — 차이가 한 방향이다).");
            Line(string.Empty);
            Line("     ★ 「격자 위의 값으로 옮길 것인가」 — 22회차 판단: ⛔ «보류» (#112 의 처방을 이번엔 «안» 쓴다)");
            Line("        ① 옮길 «안정된 격자점이 없다» — 14회 스윕의 최근접값이 65.9075·65.9130·65.9185·65.9240·");
            Line("           65.9295·65.9350 으로 «여섯 값»에 흩어진다. 격자 자체가 위상에 물려 시행마다 움직인다.");
            Line("        ② 실제로 쏜 이웃 3발이 «서로 갈린다» — 65.9020 ★골인 / 65.9185 미골인 / 65.9240 미골인.");
            Line("           폭 0.022(한 양자의 1/20)가 결과를 뒤집는다 ⇒ «바늘»의 지문이다 (`07 §12-c` · #74).");
            Line("           바늘 위로 정답지를 옮기는 것은 «품질이 아니라 우연»을 재게 만든다.");
            Line("        ③ 옮긴 값은 n ≥ 3 으로 갈리는지 먼저 봐야 한다 (#83). 지금은 자리마다 n = 1 이다.");
            Line("        ⇒ 판단은 PD 가 한다. 옮기려면 «그 값에서 n ≥ 3» 을 먼저 재야 한다 — 재측정 대기에 올렸다.");
            Line(string.Empty);
        }

        // ────────────────────────────────────────────────────────── 1. 자기검산

        private void SelfCheckGolden(LevelSpec spec)
        {
            List<Window> windows = ExtractWindows(spec.Grid, true);

            var sb = new StringBuilder();

            for (int i = 0; i < windows.Count; i++)
            {
                if (sb.Length > 0)
                    sb.Append(" · ");

                sb.Append($"[{windows[i].Lower:0.00} ~ {windows[i].Upper:0.00}]");
            }

            bool ok = windows.Count == spec.Expected.Count;

            if (ok)
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    if (Math.Abs(windows[i].Lower - spec.Expected[i].Lower) > 0.01 ||
                        Math.Abs(windows[i].Upper - spec.Expected[i].Upper) > 0.01)
                    {
                        ok = false;
                        break;
                    }
                }
            }

            Check($"{spec.Level} 정답지 재구성 — 격자(바늘 제외) → §12-b 표", ok,
                  $"격자가 만든 창 {windows.Count}개 {sb} / 표 {spec.Expected.Count}개");
        }

        // ────────────────────────────────────────────────────────── 2. 스윕

        private void SweepLevel(LevelSpec spec)
        {
            int clears = 0;
            int needleCount = 0;
            int nondetCount = 0;
            int unmeasuredCount = 0;
            int unreachableCount = 0;
            var unique = new HashSet<string>();

            for (int i = 0; i < spec.Grid.Count; i++)
            {
                SweepPoint p = spec.Grid[i];
                p.OurScored = ShootForce(spec.Level, p.Force);

                if (p.Needle)
                    needleCount++;
                else if (p.Nondeterministic)
                    nondetCount++;
                else if (p.Unmeasured)
                    unmeasuredCount++;
                else if (p.Unreachable)
                    unreachableCount++;
                else if (p.OurScored)
                    clears++;

                unique.Add(p.Force.ToString("0.0000", CultureInfo.InvariantCulture));
            }

            int graded = spec.Grid.Count - needleCount - nondetCount - unmeasuredCount - unreachableCount;
            int originClears = 0;
            int agree = 0;

            for (int i = 0; i < spec.Grid.Count; i++)
            {
                SweepPoint p = spec.Grid[i];

                if (p.Graded == false)
                    continue;

                if (p.OriginScored)
                    originClears++;

                if (p.OriginScored == p.OurScored)
                    agree++;
            }

            _nondetExcluded += nondetCount;
            _unmeasuredExcluded += unmeasuredCount;
            _unreachableExcluded += unreachableCount;

            // ⚠ 네 갈래를 «각각» 센다 — 합치면 「제외 N건」이라는 조용한 상한이 된다 (#74 · `07 §20-c`).
            string excl = $"바늘 {needleCount} · 비결정 {nondetCount} · 도달불가 {unreachableCount} · " +
                          $"미측정 {unmeasuredCount} 제외 후 채점 {graded}발";

            Line($"  [{spec.Level}] 시행 {spec.Grid.Count}발 " +
                 $"(고유 force {unique.Count}개 · {excl}) — {spec.Note}");
            Line($"          클리어  원본 {originClears}/{graded}  ·  우리 {clears}/{graded}  " +
                 $"·  점 단위 일치 {agree}/{graded}");

            // 어긋난 점을 전부 찍는다 — 「창이 왜 달라졌나」는 결국 이 점들이다.
            var diff = new StringBuilder();

            for (int i = 0; i < spec.Grid.Count; i++)
            {
                SweepPoint p = spec.Grid[i];

                if (p.Graded == false || p.OriginScored == p.OurScored)
                    continue;

                if (diff.Length > 0)
                    diff.Append(" · ");

                diff.Append($"{p.Force:0.0000}(원본 {(p.OriginScored ? "O" : "X")}→우리 " +
                            $"{(p.OurScored ? "O" : "X")})");
            }

            if (diff.Length > 0)
                Line($"          어긋난 점: {diff}");

            // ★★ 「비결정 N건 제외」를 «반드시» 찍는다 — 조용한 상한 금지 (재발방지 #74).
            //    우리 쪽 결과도 같이 찍어 다음 회차가 「무엇을 뺐는지」를 눈으로 본다.
            if (nondetCount > 0)
            {
                Line($"          ⚠⚠ [비결정 제외 {nondetCount}건] — 원본이 «스스로도 재현하지 못하는» 입력이다 (`07 §14`).");

                for (int i = 0; i < spec.Grid.Count; i++)
                {
                    SweepPoint p = spec.Grid[i];

                    if (p.Nondeterministic == false)
                        continue;

                    GoldenVectors.NondeterministicPoint nd =
                        GoldenVectors.FindNondeterministic(spec.Level, p.Force);

                    Line($"             force {p.Force:0.0000} → 원본 표기 " +
                         $"{(p.OriginScored ? "클리어" : "미클리어")} · 우리 " +
                         $"{(p.OurScored ? "클리어" : "미클리어")} · " +
                         $"{(nd == null ? "골인율 미측정" : nd.Rate)}");
                    Line($"             · {p.NondeterministicNote}");
                }
            }

            // ★ 「미측정 N건 제외」도 «따로» 찍는다 (`07 §14-h`).
            if (unmeasuredCount > 0)
            {
                Line($"          ⚠ [미측정 제외 {unmeasuredCount}건] — 그 격자값의 «원본 답이 아직 없다» (`07 §14-g2`).");

                for (int i = 0; i < spec.Grid.Count; i++)
                {
                    SweepPoint p = spec.Grid[i];

                    if (p.Unmeasured == false)
                        continue;

                    Line($"             force {p.Force:0.0000} → 우리 {(p.OurScored ? "클리어" : "미클리어")} " +
                         $"· {p.UnmeasuredNote}");
                }
            }

            // ★ [16회차] 「발사대 관통」으로 설명되는 자리와 «아닌» 자리를 갈라 둔다 —
            //   합쳐 두면 다음 회차가 쌍 배제로 다 풀렸다고 오해한다 (#67 「개선을 내 공로로 몰지 마라」).
            if (spec.Level == "W1L4")
            {
                Line("          ★★★ [18회차 해소] W1L4 force 36.5870 — «원본 답이 나왔다»: 5/6 (83 %) 골인.");
                Line("             16회차가 「미측정」으로 남긴 이유는 홀드 ms 축이 35.73 / 37.04 / 37.50 / 37.96 에만");
                Line("             떨어져 가운데를 못 짚은 것이었다. 18회차 force 축 러너가 36.5760~36.5925 여섯 발을 짚었고");
                Line("             ★ 한 발은 실 force 가 36.5870 «그 자체»(소수 4자리 일치)이며 ★골인(+4149.9 ms) 이다.");
                Line("             ⇒ 격자표의 「원본 미클리어」가 반증됐다 — 라벨을 «클리어»로 정정했고, 이 칸은 «우리가 맞다».");
                Line("             ⚠ 비결정 여지는 남는다 — 36.5925 한 발만 미골인(한 양자의 1/40 차이). 다만 «같은 값 두 시행이");
                Line("               갈리는 것»은 못 봤으므로 비결정으로 등재하지 «않는다» (#83 — 등재 기준을 넓히면 #48 이다).");
                Line("             부수 [실측] 37.5000 은 2/2 미골인 · 37.9620 은 2/2 골인 · 37.0435 는 1/1 골인.");
                Line("             ⚠ 여전히 남는 사실 — 이 자리는 «발사대 관통 계열이 아니다»(16회차 6샷 전부 블롭 겹침 0).");
            }
        }

        /// <summary>한 발의 «모양» — 갈린 점의 원인을 짚기 위한 기록. <b>채점에 쓰지 않는다.</b></summary>
        private struct ShotTrace
        {
            public double LaunchSpeed;
            public double LaunchAngleDeg;
            public int Bounces;
            public double FirstBounceMs;
            public double FirstBounceX;
            public double FirstBounceY;
            public bool HasFirstBounce;
            public int TopOverlapTicks;
            public int BottomEnterCount;
            public double Seconds;
            public string End;
        }

        private bool ShootForce(string levelCode, double force)
        {
            ShotTrace ignored;
            return ShootForce(levelCode, force, out ignored);
        }

        /// <summary>
        /// force 하나를 쏜다. <b>레벨을 «매 발» 다시 세운다</b> —
        /// 원본이 시행마다 ↺ 재시작을 하므로 그것과 같게 맞춘다.
        /// ⚠ 블롭은 동적이라 앞 샷의 여파가 남는다. 재사용하면 <b>같은 force 가 순서에 따라 갈린다.</b>
        /// </summary>
        private bool ShootForce(string levelCode, double force, out ShotTrace trace)
        {
            trace = default(ShotTrace);
            trace.End = "발사 안 됨";
            double holdMs = (force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0;

            // ★★★ 오브젝트만 헐면 답이 «앞선 샷 수»에 물린다 — 월드째로 새로 만든다 (RecreateScene 주석).
            RecreateScene();

            BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(levelCode);

            if (runtime == null)
            {
                Fail($"레벨 데이터를 못 읽었다: {levelCode}");
                return false;
            }

            _world.Build(runtime, LoadPrefab);

            if (_world.Ball == null)
            {
                Fail($"공 프리팹을 못 세웠다: {levelCode}");
                return false;
            }

            var sim = new BlumgiShotSimulation(_world.Level, _world.Ball);
            sim.BeginHold();

            double remain = holdMs / 1000.0;

            while (remain > 1e-12)
            {
                double dt = remain > _step ? _step : remain;
                sim.Tick(dt);
                remain -= dt;
            }

            sim.EndHold();
            _shots++;

            if (sim.State != EBlumgiShotState.Flying)
                return false;   // 발사 자체가 안 걸렸다 = 미클리어

            trace.LaunchSpeed = Math.Sqrt(sim.LaunchVelocity.X * sim.LaunchVelocity.X +
                                          sim.LaunchVelocity.Y * sim.LaunchVelocity.Y);
            trace.LaunchAngleDeg = Math.Atan2(-sim.LaunchVelocity.Y, sim.LaunchVelocity.X) * 180.0 / Math.PI;
            trace.End = "시간 상한";

            int limit = (int)(ShotTimeLimitSeconds / _step);
            double half = _config.BallDetectionBoxSize * 0.5;
            BlumgiAabb top = sim.Level.DetectorTop;
            BlumgiAabb bottom = sim.Level.DetectorBottom;
            bool wasBottom = false;
            int lastBounces = 0;

            for (int i = 0; i < limit; i++)
            {
                _physics.Simulate((float)_step);
                sim.Tick(_step);

                BlumgiVec2 p = sim.BallPosition;

                if (trace.HasFirstBounce == false && sim.CollisionCount > lastBounces)
                {
                    trace.HasFirstBounce = true;
                    trace.FirstBounceMs = sim.FlightSeconds * 1000.0;
                    trace.FirstBounceX = p.X;
                    trace.FirstBounceY = p.Y;
                }

                lastBounces = sim.CollisionCount;

                if (top.OverlapsBox(p, half))
                    trace.TopOverlapTicks++;

                bool nowBottom = bottom.OverlapsBox(p, half);

                if (nowBottom && wasBottom == false)
                    trace.BottomEnterCount++;

                wasBottom = nowBottom;

                if (sim.State == EBlumgiShotState.Scored)
                {
                    trace.End = "골인";
                    trace.Bounces = sim.CollisionCount;
                    trace.Seconds = sim.FlightSeconds;
                    return true;
                }

                if (sim.IsBallActive == false)
                {
                    trace.End = "화면 밖으로 낙하";
                    trace.Bounces = sim.CollisionCount;
                    trace.Seconds = sim.FlightSeconds;
                    return false;
                }
            }

            trace.Bounces = sim.CollisionCount;
            trace.Seconds = sim.FlightSeconds;
            return sim.State == EBlumgiShotState.Scored;
        }

        // ────────────────────────────────────────────────────────── 창 추출

        /// <summary>
        /// 격자에서 «연속한 클리어 구간»을 뽑는다.
        /// <b>바늘(`07 §12-c`)과 «비결정»(`07 §14`) 은 없는 셈 친다.</b>
        /// </summary>
        private static List<Window> ExtractWindows(List<SweepPoint> grid, bool useOrigin)
        {
            var windows = new List<Window>(4);
            Window open = null;
            double lastFail = 0.0;
            bool hasLastFail = false;

            for (int i = 0; i < grid.Count; i++)
            {
                SweepPoint p = grid[i];

                // ★ 바늘(`07 §12-c`)과 «비결정»(`07 §14`) 은 둘 다 «없는 셈» 친다.
                //   비결정 점은 원본 쪽 라벨 자체가 회차마다 갈리므로 창 경계의 근거가 될 수 없다.
                if (p.Graded == false)
                    continue;

                bool scored = useOrigin ? p.OriginScored : p.OurScored;

                if (scored)
                {
                    if (open == null)
                    {
                        open = new Window
                        {
                            Lower = p.Force,
                            Upper = p.Force,
                            Count = 0,
                            PrevFail = lastFail,
                            HasPrevFail = hasLastFail
                        };
                    }

                    open.Upper = p.Force;
                    open.Count++;
                    continue;
                }

                lastFail = p.Force;
                hasLastFail = true;

                if (open == null)
                    continue;

                open.NextFail = p.Force;
                open.HasNextFail = true;
                windows.Add(open);
                open = null;
            }

            if (open != null)
                windows.Add(open);

            return windows;
        }

        // ────────────────────────────────────────────────────────── 3. 판정

        private void JudgeLevel(LevelSpec spec)
        {
            List<Window> ours = ExtractWindows(spec.Grid, false);

            Line(string.Empty);
            Line($"  ── {spec.Level} ──────────────────────────────");

            // ① 창 개수
            Check($"{spec.Level} ① 창 «개수»", ours.Count == spec.Expected.Count,
                  $"원본 {spec.Expected.Count}개 / 우리 {ours.Count}개 " +
                  $"({DescribeWindows(ours)})");

            // 개수가 다르면 짝을 지을 수 없다 — 그래도 «겹치는 만큼»은 판정한다.
            int pairs = Math.Min(ours.Count, spec.Expected.Count);

            for (int i = 0; i < pairs; i++)
                JudgeWindow(spec, spec.Expected[i], ours[i]);

            for (int i = pairs; i < spec.Expected.Count; i++)
                Fail($"{spec.Level} {spec.Expected[i].Label} — 우리 쪽에 대응하는 창이 «없다» " +
                     $"(원본 {spec.Expected[i].Lower:0.00}~{spec.Expected[i].Upper:0.00})");

            for (int i = pairs; i < ours.Count; i++)
                Fail($"{spec.Level} — 원본에 «없는» 창이 우리에게 있다: " +
                     $"{ours[i].Lower:0.00}~{ours[i].Upper:0.00} (격자점 {ours[i].Count}개)");

            // ④ 구멍 위치
            JudgeHole(spec);
        }

        private void JudgeWindow(LevelSpec spec, ExpectedWindow e, Window w)
        {
            if (e.ExistenceOnly)
            {
                // W1L4 창B — 「폭」이 아니라 「51.25 ± 1.6 구간에 클리어가 존재하는가」만 본다.
                bool exists = false;

                for (int i = 0; i < spec.Grid.Count; i++)
                {
                    SweepPoint p = spec.Grid[i];

                    if (p.Graded == false || p.OurScored == false)
                        continue;

                    if (Math.Abs(p.Force - IslandCenter) <= IslandTolerance)
                        exists = true;
                }

                Check($"{spec.Level} ②③ {e.Label} — «존재»만 본다 " +
                      $"({IslandCenter:0.00}±{IslandTolerance:0.0} 안에 클리어가 있는가)",
                      exists,
                      $"우리 창 {w.Lower:0.0000}~{w.Upper:0.0000} · " +
                      "⚠ 확정폭 0.04 를 맞추라는 것은 바늘을 맞추라는 것과 같다 → 폭은 채점 안 한다");
                return;
            }

            // ② 창 경계 — 하한
            bool lowOk = w.Lower >= e.LowerBoxMin - 1e-6 && w.Lower <= e.LowerBoxMax + 1e-6;
            string lowBox = e.LowerIsDomainEdge
                ? $"도메인 끝 {e.LowerBoxMax:0.0000} (그 아래는 «발사 자체»가 없다)"
                : $"[{e.LowerBoxMin:0.0000} ~ {e.LowerBoxMax:0.0000}]";

            Check($"{spec.Level} ② {e.Label} «하한» — 원본 최대폭 상자 안인가", lowOk,
                  $"우리 {w.Lower:0.0000} / 원본 {e.Lower:0.0000} · 상자 {lowBox} · " +
                  $"Δ {Math.Abs(w.Lower - e.Lower):0.0000} (참고 기준 {BoundaryReferenceTolerance:0.0})");

            // ② 창 경계 — 상한
            bool highOk = w.Upper >= e.UpperBoxMin - 1e-6 && w.Upper <= e.UpperBoxMax + 1e-6;
            string highBox = e.UpperIsDomainEdge
                ? $"도메인 끝 {e.UpperBoxMin:0.0000} (더 센 입력이 «존재하지 않는다»)"
                : $"[{e.UpperBoxMin:0.0000} ~ {e.UpperBoxMax:0.0000}]";

            Check($"{spec.Level} ② {e.Label} «상한» — 원본 최대폭 상자 안인가", highOk,
                  $"우리 {w.Upper:0.0000} / 원본 {e.Upper:0.0000} · 상자 {highBox} · " +
                  $"Δ {Math.Abs(w.Upper - e.Upper):0.0000} (참고 기준 {BoundaryReferenceTolerance:0.0})");

            // ③ 창 폭 — 원본 «확정폭 ~ 최대폭» 상자 안인가 + 비율
            double ratio = e.ConfirmedWidth > 1e-9 ? w.Width / e.ConfirmedWidth : 0.0;
            bool widthOk = w.Width >= e.MinWidth - 1e-6 && w.Width <= e.MaxWidth + 1e-6;

            Check($"{spec.Level} ③ {e.Label} «폭» — 원본 [확정폭 {e.MinWidth:0.00} ~ 최대폭 {e.MaxWidth:0.00}] 안인가",
                  widthOk,
                  $"우리 폭 {w.Width:0.0000} / 원본 확정폭 {e.ConfirmedWidth:0.0000} · " +
                  $"★ 비율 {ratio:0.000}배 · 격자점 {w.Count}개");
        }

        private void JudgeHole(LevelSpec spec)
        {
            if (spec.HoleRange == null)
            {
                // 구멍이 없어야 하는 레벨 — ① 이 「1개」를 통과하면 자동으로 구멍이 없다.
                Line($"  [{spec.Level}] ④ 구멍 — 원본에 «없다». 창 개수 1개 판정이 곧 「구멍 없음」 판정이다.");
                return;
            }

            double lo = spec.HoleRange[0];
            double hi = spec.HoleRange[1];

            int total = 0;
            int missed = 0;
            var leak = new StringBuilder();

            for (int i = 0; i < spec.Grid.Count; i++)
            {
                SweepPoint p = spec.Grid[i];

                if (p.Graded == false || p.Force <= lo + 1e-6 || p.Force >= hi - 1e-6)
                    continue;

                total++;

                if (p.OurScored == false)
                {
                    missed++;
                    continue;
                }

                if (leak.Length > 0)
                    leak.Append(" · ");

                leak.Append($"{p.Force:0.0000}");
            }

            Check($"{spec.Level} ④ «구멍 위치» — {lo:0.00} ~ {hi:0.00} 사이가 전부 미클리어인가",
                  missed == total,
                  $"우리 미클리어 {missed}/{total}" +
                  (leak.Length > 0 ? $" · ⚠ 새는 자리: {leak}" : string.Empty) +
                  " · ⚠ 바늘 45.7445 는 이 구간에서 «뺐다»");
        }

        private static string DescribeWindows(List<Window> windows)
        {
            if (windows.Count == 0)
                return "창 없음";

            var sb = new StringBuilder();

            for (int i = 0; i < windows.Count; i++)
            {
                if (sb.Length > 0)
                    sb.Append(" · ");

                sb.Append($"[{windows[i].Lower:0.00}~{windows[i].Upper:0.00}]×{windows[i].Count}");
            }

            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────── 4. 바늘 결과

        private void ReportNeedleResults(List<LevelSpec> specs)
        {
            for (int i = 0; i < specs.Count; i++)
            {
                for (int k = 0; k < specs[i].Grid.Count; k++)
                {
                    SweepPoint p = specs[i].Grid[k];

                    if (p.Needle == false)
                        continue;

                    string mark = p.OriginScored == p.OurScored ? "일치" : "갈림";

                    Line($"  {specs[i].Level} force {p.Force:0.0000} — " +
                         $"원본 {(p.OriginScored ? "클리어" : "미클리어")} / " +
                         $"우리 {(p.OurScored ? "클리어" : "미클리어")}  [{mark} · 채점 제외]");
                }
            }

            Line("  ⇒ 여기서 갈려도 «결함이 아니다». 옆값과 0.0055~0.017 차이라 1 px 스침이 뒤집는 자리다 " +
                 "(`07 §12-c` · §10 「모서리 스침 = 카오스 증폭기」).");
        }

        // ────────────────────────────────────────────────────────── 5. 경계 미세화 (참고)

        private void RefineBoundaries(LevelSpec spec)
        {
            List<Window> ours = ExtractWindows(spec.Grid, false);

            for (int i = 0; i < ours.Count; i++)
            {
                Window w = ours[i];

                string low = w.HasPrevFail
                    ? $"{Bisect(spec.Level, w.PrevFail, w.Lower):0.000}"
                    : "도메인 끝";

                string high = w.HasNextFail
                    ? $"{Bisect(spec.Level, w.NextFail, w.Upper):0.000}"
                    : "도메인 끝";

                Line($"  {spec.Level} 창{i + 1} [{w.Lower:0.00}~{w.Upper:0.00}] → " +
                     $"미세화 하한 ≈ {low} · 상한 ≈ {high}  ({RefineIterations}회 이분)");
            }
        }

        /// <summary>
        /// 실패 force 와 성공 force 사이를 이분해 «우리» 경계를 좁힌다. <b>채점하지 않는다.</b>
        /// </summary>
        private double Bisect(string level, double failForce, double passForce)
        {
            double bad = failForce;
            double good = passForce;

            for (int i = 0; i < RefineIterations; i++)
            {
                double mid = (bad + good) * 0.5;

                if (ShootForce(level, mid))
                    good = mid;
                else
                    bad = mid;
            }

            return good;
        }

        // ────────────────────────────────────────────────────────── 7. 어긋난 점 진단

        /// <summary>
        /// ★ <b>「원인은 물리다」를 «추측»으로 남기지 않는다</b> (골든 채점기 §6-b 와 같은 도구).
        /// 갈린 점만 다시 쏴서 <b>발사 · 첫 반발 · 감지기 · 종료 사유</b>를 찍는다.
        /// ⚠ <b>채점하지 않는다.</b> 여기 숫자로 값을 «고르면» 그 순간 채점의 의미가 사라진다.
        /// </summary>
        private void DiagnoseMismatches(LevelSpec spec)
        {
            var rows = new List<SweepPoint>(8);

            for (int i = 0; i < spec.Grid.Count; i++)
            {
                SweepPoint p = spec.Grid[i];

                if (p.Graded && p.OriginScored != p.OurScored)
                    rows.Add(p);
            }

            if (rows.Count == 0)
            {
                Line($"  [{spec.Level}] 갈린 점 «없다» — 격자 전 점이 원본과 같다.");
                return;
            }

            Line($"  [{spec.Level}] 갈린 점 {rows.Count}개");

            for (int i = 0; i < rows.Count; i++)
            {
                SweepPoint p = rows[i];
                ShotTrace t;
                ShootForce(spec.Level, p.Force, out t);

                string bounce = t.HasFirstBounce
                    ? $"첫반발 +{t.FirstBounceMs:0}ms ({t.FirstBounceX:0}, {t.FirstBounceY:0})"
                    : "반발 «없음»";

                Line($"     force {p.Force,9:0.0000}  원본 {(p.OriginScored ? "O" : "X")} → 우리 " +
                     $"{(p.OurScored ? "O" : "X")}  |  |v0| {t.LaunchSpeed:0} · {t.LaunchAngleDeg:0.0}° · " +
                     $"반발 {t.Bounces}회 · {bounce} · 위겹침 {t.TopOverlapTicks}틱 · " +
                     $"아래닿기 {t.BottomEnterCount}회 · {t.Seconds:0.00}s · {t.End}");
            }

            Line("     ⇒ 읽는 법 (`07 §9-e`): 아래 감지기 접촉 0 = «판정»이 아니라 «궤적»이 다르다.");
        }

        // ────────────────────────────────────────────────────────── 7-b. 첫 반발 정답지

        /// <summary>
        /// ★★★ <b>이 절이 패스 ①-h 의 «직접» 판정이다.</b>
        ///
        /// <para>
        /// W1L3 은 발사대에서 벽까지 가로 <b>64.4 px</b> 뿐이라 <b>출발점 오차가 그대로 첫 충돌점 오차</b>다
        /// [12회차 §2-c]. 그래서 <b>「발사점 모델이 실제로 들어갔는가」는 «첫 반발»을 보면 안다</b> —
        /// 클리어 여부는 그 «뒤»의 반발 사슬까지 섞여 있어 이 질문에 답하지 못한다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>채점하지 않는다.</b> 창 판정 31칸을 바꾸면 옛 성적과 비교가 끊긴다 —
        /// 이 절은 <b>「무엇이 좋아졌고 무엇이 안 좋아졌는지」를 가르는 눈금</b>이다.
        /// </para>
        /// </summary>
        private void ReportFirstBounceW1L3()
        {
            // 12회차 실측 — force · 첫 접촉 시각(ms) · 그때 공 중심 (x, y). `07 §8-5-b` 원자료.
            double[,] golden =
            {
                { 15.0563, 417.1, 1113.7, 243.7 },
                { 18.2445, 341.5, 1113.0, 202.4 },
                { 26.5000, 233.4, 1114.0, 157.7 },
                { 37.5000, 167.3, 1113.4, 138.4 },
                { 59.5000, 108.3, 1111.9, 126.8 },
            };

            Line("  원본 12회차 5샷의 «첫 접촉»(Box2D 매니폴드 직독)과 우리 첫 반발을 나란히 놓는다.");
            Line("  ⚠ force 15.0563 은 내부 홀드 91.9ms 로 발사 최소(124ms) 아래다 — 우리는 «발사 자체»가 안 된다.");
            Line("     원본 시행 자체의 이상이다 (12회차 §5). 조용히 빼지 않고 그대로 찍는다.");
            Line("  force   |  원본 t/x/y            |  우리 t/x/y            |  Δt   Δx   Δy");

            for (int i = 0; i < golden.GetLength(0); i++)
            {
                double force = golden[i, 0];
                ShotTrace t;
                ShootForce("W1L3", force, out t);

                if (t.HasFirstBounce == false)
                {
                    Line($"  {force,8:0.0000} | {golden[i, 1],6:0.0} {golden[i, 2],7:0.0} {golden[i, 3],7:0.0} " +
                         $"| 첫 반발 «없음» ({t.End})");
                    continue;
                }

                Line($"  {force,8:0.0000} | {golden[i, 1],6:0.0} {golden[i, 2],7:0.0} {golden[i, 3],7:0.0} " +
                     $"| {t.FirstBounceMs,6:0.0} {t.FirstBounceX,7:0.0} {t.FirstBounceY,7:0.0} " +
                     $"| {Math.Abs(t.FirstBounceMs - golden[i, 1]),4:0.0} " +
                     $"{Math.Abs(t.FirstBounceX - golden[i, 2]),4:0.0} " +
                     $"{Math.Abs(t.FirstBounceY - golden[i, 3]),4:0.0}");
            }

            Line("  ⇒ 읽는 법: Δy 가 작으면 **발사점이 맞은 것**이다. 그래도 클리어가 안 되면");
            Line("     어긋남은 «출발점»이 아니라 **첫 반발 «뒤»의 반발 사슬**에 있다 — 원장의 기존 계열이다.");
        }

        // ────────────────────────────────────────────────────────── 8. 격차 등재부

        /// <summary>
        /// ★ <b>「어긋났다」와 「새로 어긋났다」는 다르다.</b> 이미 원장에 «진짜 격차»로 등재된 것과
        /// 이번에 처음 나온 것을 <b>기계가 갈라 준다</b> — 사람 기억에 맡기면 다음 회차에 섞인다.
        ///
        /// <para>
        /// ⚠⚠ <b>[패스 ①-h 정정] 이 절은 한때 «①-g 회차의 문장»이 통째로 박혀 있었다.</b>
        /// 물리가 바뀌자 「발사대가 (1050.3, 233) 이니…」 같은 **이미 사라진 값**을 계속 찍었다 —
        /// <b>바로 이 주석이 경고하던 「사람 기억에 맡기면 섞인다」를 이 절 자신이 저지르고 있었다.</b>
        /// 그래서 <b>이번 스윕의 실제 데이터로 «기계가 만들게»</b> 바꿨다.
        /// 손으로 적는 것은 <b>원장에 «등재된» 격차 목록</b> 하나뿐이다.
        /// </para>
        /// </summary>
        private void ReportGapRegistry(List<LevelSpec> specs)
        {
            // 원본 샷당 반발 관측 범위 (`07 §10-e`) — 이 위를 넘은 샷은 「다중 반발 누적」 계열이다.
            const int OriginMaxBounces = 13;

            Line("  ── 이미 등재된 격차 (원장 · `07 §9-g` · 10회차 PD 처분) — 손으로 적는 유일한 줄 ──");
            Line("     W1L4 force 24.635 · 25.153 — 원본 클리어 3/3 인데 우리는 미클리어. 「둘째 반발부터 갈린다」");
            Line("       ⚠ 이 두 값은 11회차 격자에 «없다» (격자는 24.19 · 30.17 · 34.76).");
            Line(string.Empty);
            Line($"  ── 이번 스윕의 갈린 점을 «반발 횟수»로 가른다 (원본 관측 범위 0~{OriginMaxBounces}회 · `07 §10-e`) ──");

            int overRange = 0;
            int inRange = 0;

            for (int s = 0; s < specs.Count; s++)
            {
                LevelSpec spec = specs[s];

                for (int i = 0; i < spec.Grid.Count; i++)
                {
                    SweepPoint p = spec.Grid[i];

                    if (p.Graded == false || p.OriginScored == p.OurScored)
                        continue;

                    ShotTrace t;
                    ShootForce(spec.Level, p.Force, out t);

                    bool over = t.Bounces > OriginMaxBounces;

                    if (over)
                        overRange++;
                    else
                        inRange++;

                    Line($"     {(over ? "(가) 반발 범위 «밖»" : "(나) 반발 범위 «안»")}  " +
                         $"{spec.Level} force {p.Force,9:0.0000}  원본 {(p.OriginScored ? "O" : "X")} → " +
                         $"우리 {(p.OurScored ? "O" : "X")}  ·  반발 {t.Bounces}회 · {t.Seconds:0.00}s · {t.End}");
                }
            }

            Line(string.Empty);
            Line("  ── 갈래별 집계 ──");
            Line($"     (가) 다중 반발 누적 — 반발이 원본 범위(0~{OriginMaxBounces})를 «넘은» 샷: {overRange}건");
            Line("          ⇒ 원장에 이미 «진짜 격차»로 등재된 자리와 같은 계열이다 (새 계열이 아니다).");
            Line($"     (나) 반발 수는 «정상»인데 갈리는 샷: {inRange}건");
            Line("          ⇒ 누적이 아니다. §7-b 의 «첫 반발» 대조로 «출발점»인지 «반발 사슬»인지 다시 가른다.");
            Line(string.Empty);
            Line("  ⚠⚠ 이 표를 보고 물리·데이터 값을 «고르지» 않는다 (재발방지 #48).");
            Line("      맞추면 창은 통과하겠지만 그 순간 이 검사는 «우리가 맞춘 값을 확인»하는 것이 된다.");
        }

        // ────────────────────────────────────────────────────────── 6. 스펙트럼

        private void ReportSpectrum(List<LevelSpec> specs)
        {
            Line("  레벨 | 원본 확정폭 | 우리 폭 | 비율");

            double originMin = double.MaxValue;
            double originMax = 0.0;
            double ourMin = double.MaxValue;
            double ourMax = 0.0;

            for (int i = 0; i < specs.Count; i++)
            {
                LevelSpec spec = specs[i];
                List<Window> ours = ExtractWindows(spec.Grid, false);

                for (int k = 0; k < spec.Expected.Count; k++)
                {
                    ExpectedWindow e = spec.Expected[k];
                    double ourWidth = k < ours.Count ? ours[k].Width : 0.0;
                    double ratio = e.ConfirmedWidth > 1e-9 ? ourWidth / e.ConfirmedWidth : 0.0;

                    Line($"  {e.Label,-14} | {e.ConfirmedWidth,10:0.00} | {ourWidth,8:0.00} | {ratio,6:0.000}배" +
                         (e.ExistenceOnly ? "   ⚠ 폭 채점 제외(존재만)" : string.Empty));

                    if (e.ExistenceOnly)
                        continue;

                    if (e.ConfirmedWidth < originMin) originMin = e.ConfirmedWidth;
                    if (e.ConfirmedWidth > originMax) originMax = e.ConfirmedWidth;
                    if (ourWidth < ourMin) ourMin = ourWidth;
                    if (ourWidth > ourMax) ourMax = ourWidth;
                }
            }

            double originSpread = originMin > 1e-9 ? originMax / originMin : 0.0;
            double ourSpread = ourMin > 1e-9 ? ourMax / ourMin : 0.0;

            Line(string.Empty);
            Line($"  주 창 최대/최소 배수 — 원본 {originSpread:0.0}배 / 우리 {ourSpread:0.0}배");
            Line("  ⇒ 이 «모양»이 같아야 밸런싱이 같은 것이다 (`07 §12-d`). [채점 아님 · 읽는 숫자]");
        }

        // ────────────────────────────────────────────────────────── 출력

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
                Line($"  OK   {name} — {actual}");
                return;
            }

            Fail($"{name} — {actual}");
        }

        private void Fail(string line)
        {
            _fail++;
            _failLines.Add(line);
            Line("  FAIL " + line);
        }

        private void Line(string text)
        {
            _report.AppendLine(text);
        }

        private void WriteReport()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, _report.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"창 대조 전문을 못 썼다: {e.Message}");
            }
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            // ★ 다리가 시킨 실행이면 «에디터를 끄지 않는다».
            if (SessionState.GetBool(BlumgiWindowPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }
}
