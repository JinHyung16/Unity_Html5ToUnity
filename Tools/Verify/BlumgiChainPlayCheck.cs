using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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
    /// ★★★ <b>«반발 사슬» 추적기</b> — 진단 전용. <b>채점기가 아니다.</b>
    ///
    /// <para>
    /// <b>왜 필요한가.</b> ①-h 에서 <b>첫 반발</b>은 원본과 Δt ≤ 0.6ms · Δy ≤ 0.7px 로 닫혔는데
    /// <b>창은 15/31 그대로</b>다. 즉 어긋남은 «출발점»이 아니라 <b>«첫 반발 뒤의 사슬»</b>에 있다.
    /// 그런데 지금까지의 도구는 <b>반발 «총수»</b>와 <b>«첫» 반발</b>만 봤다 —
    /// <b>«몇 번째» 접촉에서 갈리기 시작하는지</b>를 아무도 안 찍었다.
    /// </para>
    ///
    /// <para>
    /// ★ 이 도구가 재는 것 셋 —
    /// <b>① 우리 물리 설정 전수 되읽기</b>(「기본값이니까」로 넘긴 자리를 없앤다) ·
    /// <b>② 접촉을 순서대로 전수 기록해 9회차 정답지 35건과 «순서대로» 대조</b> ·
    /// <b>③ 지문</b>(같은 블록 재접촉 · 접촉 지속 스텝 수) — 원본은 재접촉 0건 · 구간 1~2프레임이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 문을 뚫지 않았다.</b> 기록기(<see cref="BlumgiChainProbe"/>)는
    /// 채점기가 «생성된 인스턴스»에 <c>AddComponent</c> 로 붙였다 떼는 것이고 <c>Tools\Verify</c> 안에서만 산다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>여기서 나온 숫자로 값을 «고르지» 않는다</b> (재발방지 #48). 이 도구는 <b>자리를 가리킬 뿐</b>이다.
    /// </para>
    ///
    /// <para>
    /// 실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.BlumgiChainPlayCheck.RunAll</c>
    /// </para>
    /// </summary>
    public static class BlumgiChainPlayCheck
    {
        public const string ArmedKey = "JinHyung.BlumgiChainPlayCheck.Armed";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

#if UNITY_EDITOR
        public static void RunAll()
        {
            SessionState.SetBool(ArmedKey, true);
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

            var go = new GameObject("BlumgiChainProbeRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BlumgiChainRunner>();
#endif
        }
    }

    /// <summary>사슬 추적 실행기. 창 대조기와 <b>같은 방식</b>의 전용 로컬 물리 씬을 쓴다.</summary>
    public sealed class BlumgiChainRunner : MonoBehaviour
    {
        private const string DataFolderUnderAssets = "Blumgi-Bounce/Data";
        private const string PrefabFolder = "Assets/Blumgi-Bounce/Prefabs";
        private const string ReportPath = "Temp/BlumgiChainReport.txt";

        private const double ShotTimeLimitSeconds = 20.0;

        private readonly StringBuilder _report = new StringBuilder(1 << 18);

        private BlumgiConfigData _config;
        private Scene _scene;
        private PhysicsScene2D _physics;
        private BlumgiPhysicsWorld _world;
        private double _step;

        // ────────────────────────────────────────────────────────── 자료형

        /// <summary>우리 쪽 접촉 한 건 — <b>원본 좌표계(y 아래가 +)</b> 로 환산해 담는다.</summary>
        private sealed class OurContact
        {
            public int Index;
            public int BeginStep;
            public int EndStep;
            public bool Closed;

            public string OtherName;
            public int OtherId;
            public int PointCount;

            /// <summary>같은 스텝에 «열린» 접촉 수 (동시 접촉).</summary>
            public int Simultaneous;

            public double TimeMs;
            public double Px;
            public double Py;

            /// <summary>★ 접촉 «직전» 프레임의 공 body 위치 (원본 px · y 아래 +).</summary>
            public double PreX;
            public double PreY;

            /// <summary>★ 상대 body 중심 (원본 px · y 아래 +).</summary>
            public double OtherWorldX;
            public double OtherWorldY;

            /// <summary>
            /// ★ 부딪힌 «그 콜라이더»의 중심 (원본 px). 골대 림은 <b>한 오브젝트에 원이 둘</b>이라
            /// body 중심으로는 «어느 원인지»가 안 갈린다 — 법선을 기하로 만들 때는 이쪽을 쓴다.
            /// </summary>
            public double OtherColliderX;
            public double OtherColliderY;

            /// <summary>
            /// ★ 접촉 전/후 «원시» 속도 (원본 px/s · y 아래 +).
            /// <b>사영 축을 정답지 법선으로 다시 잡으려면 벡터 자체가 있어야 한다</b> —
            /// 동시 접촉에서 «누가 첫 법선인가»가 양쪽 다 임의 순서라, 우리 순서로 축을 잡으면 축이 갈린다.
            /// </summary>
            public double VbX;
            public double VbY;
            public double VaX;
            public double VaY;

            public double Nx;
            public double Ny;

            /// <summary>
            /// ★★ <b>법선이 «유효»한가</b>. 원본에서 <c>localNormal</c> 이 <c>[0, 0]</c> 으로 나오는 접촉이 있다.
            /// ★★★ <b>[18회차 정정 · #96] 조건은 «상대가 골대 림(원)»이다</b> — 「저속·깊은겹침」도
            /// 「접촉 10회 이후」도 아니다. 무효 47/47 이 림이고 유효 109건에 림이 0건이며
            /// 속도 <b>125~1554 px/s</b> · 접촉 번호 <b>2~28</b> 에 걸쳐 나온다 (<c>07 §16-c</c>).
            /// <b>무효인 건에 법선각·v_n·v_t 를 재면 전부 0 이 나오고 「완전히 맞았다」로 읽힌다.</b>
            /// </summary>
            public bool NormalValid;

            public double VnBefore;
            public double VnAfter;
            public double VtBefore;
            public double VtAfter;
            public double OmegaBefore;
            public double OmegaAfter;

            public int Steps
            {
                get { return (EndStep - BeginStep) + 1; }
            }

            public double En
            {
                get { return VnBefore == 0.0 ? double.NaN : Math.Abs(VnAfter / VnBefore); }
            }

            public double VcBefore
            {
                get { return VtBefore - (OmegaBefore * GoldenVectors.ContactBallRadiusPx); }
            }

            public double VcAfter
            {
                get { return VtAfter - (OmegaAfter * GoldenVectors.ContactBallRadiusPx); }
            }

            public bool IsRim
            {
                get { return OtherName != null && OtherName.Contains(BlumgiPhysicsWorld.HoopPrefabName); }
            }
        }

        /// <summary>정답지 접촉 한 건 — 9회차 표에 «시각»과 «상대 종류»를 붙인 것.</summary>
        private sealed class GoldenContact
        {
            public GoldenVectors.ContactEvent Event;
            public double TimeMs;
            public bool Rim;
        }

        /// <summary>정답지가 있는 샷 하나.</summary>
        private sealed class GoldenShot
        {
            public string Level;
            public double HoldMs;
            public string Shot;
            public int OriginBlockBounces;
            public int OriginRimBounces;
            public string Note;
            public List<GoldenContact> Contacts = new List<GoldenContact>(16);
        }

        /// <summary>창 대조에서 갈린 점 하나 (force 축).</summary>
        private sealed class DivergedShot
        {
            public string Level;
            public double Force;
            public bool OriginScored;
        }

        // ────────────────────────────────────────────────────────── 구동

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
                Line($"  ❌ 추적 중 예외: {e}");
            }

            WriteReport();

            Log.Success($"═══ 반발 사슬 추적 완료 · 전문 {ReportPath} ═══");

            yield return null;

            Finish(ok);
        }

        private bool Body()
        {
            Line("★★★ 반발 사슬 추적 — 진단 패스. 채점하지 않는다.");
            Line("   목적 셋 — ① 우리 물리 설정 전수 ② «몇 번째» 접촉에서 갈리나 ③ 지문(재접촉·접촉 스텝)");
            Line(string.Empty);

            if (LoadData() == false)
                return false;

            CreateScene();

            DumpPhysicsSettings();
            DumpBodySettings();

            Section("3. ★★★ 접촉 사슬 «순서» 대조 — 9회차 정답지 35건과 접촉 순서대로 [진단]");
            Line("  ⚠ 홀드 축은 9회차가 «주입»한 벽시계 홀드다 — 내부 force 와 −18~+3 ms 어긋난다 [원장].");
            Line("     그래서 Δt 는 «참고»이고, 갈림 판정의 주축은 «법선 · v_n · ω» 다.");
            Line("  ⚠ 표본 규약 — 원본의 「전/후」는 접촉 «직전/직후» rAF 프레임(8.33 ms)이다.");
            Line("     우리도 «접촉이 열린 스텝의 시작»과 «닫힌 스텝의 끝»에서 잰다 — 두 표본 모두 중력 1스텝을 품는다.");
            Line(string.Empty);

            List<GoldenShot> shots = BuildGoldenShots();

            for (int i = 0; i < shots.Count; i++)
                CompareChain(shots[i]);

            Section("3-b. ★★★ 첫 접촉 «직전» 각속도 — 사슬이 갈리는 첫 자리  [진단]");
            Line("  ★ 우리 공은 발사 프레임에 각속도가 붙는다 — 원본 저작값 −200 °/s [13회차 직독].");
            Line("     ⚠ 부호는 «여기서 가른다» — 원본은 y 아래가 + 라 유니티 축에서는 반전이다.");
            Line("        Δ 가 0 에 붙으면 부호가 맞은 것이고, Δ ≈ ±6.9 면 반대로 돌고 있는 것이다.");
            Line(string.Empty);
            Line("     샷            원본 ω«전»   우리 ω«전»   Δ        원본 t(홀드 제외)  우리 t");
            FirstContactOmega(shots);

            Section("3-c. ★★★ W1L3 «접촉 2~5번째» 정답지 대조 — 몇 번째 접촉에서 «처음» 갈리나  [15회차 28건 · `07 §15`]");
            Line("  ★ 이 구간은 원본이 «결정적»이다 — 같은 force 두 시행의 접촉 2~5 가");
            Line("     시각 0.6~1.5 ms · 위치 0.3 px 안에서 겹친다. ⇒ «비결정 면죄부»가 붙지 않는다. 채점해도 된다.");
            Line("  ⚠⚠ t 기준점이 «발사 프레임»이다 — 9·13회차(§10)의 «홀드 시작»과 다르다. 섞으면 가짜 결론이 나온다.");
            Line("  ⚠⚠ 동시 접촉이 28건 중 17건이다 (블록 충돌상자 84×58.98 > 격자 50). «한 접촉만» 골라 비교하면 오판한다");
            Line("     — 그래서 «같은 시각의 접촉을 하나의 사건으로 묶어» 대조한다 (빈 프레임으로 끊긴 연속 구간 = 1건).");
            Line("  ★ localNormal=[0,0](무효 법선)은 이 28건에 0/28 이다 — 상대가 전부 «블록(폴리곤)»이라서다.");
            Line("     ⚠⚠ [18회차 정정 · 재발방지 #96] 「무효 법선 = 저속·깊은겹침」·「접촉 10회 이후 전용」은 «둘 다 틀렸다».");
            Line("        진짜 조건은 «상대가 골대 림(원 도형)»이다 — 무효 47/47 이 림이고 유효 109건에 림 0건,");
            Line("        속도 125~1554 px/s · 접촉 번호 2~28 이다. 매니폴드 타입 직독으로 닫혔다");
            Line("        (type 0 = e_circles ⟺ [0,0] ⟺ 상대 fixture 원 r 20.11 · 27/27 예외 0건).");
            Line("        ⇒ 상대가 원이면 법선을 normalize(공 중심 − 림 중심) 으로 «만들어» 쓴다. 그 값은 전부 유효하다.");
            Line(string.Empty);
            CompareContacts25();

            Section("3-d. ★★★ 접촉 «6~12» — «값»이 아니라 «구조»로 잰다  [18회차 · `07 §16`]");
            Line("  ★★ 왜 구조인가 — 같은 force 21.4565 두 시행의 접촉 6 이 시각 400 ms · 위치 5 px 갈린다.");
            Line("     원본이 «자기 자신»과도 값이 안 맞는 구간이다 ⇒ 값 허용오차를 매기면 원본에 없는");
            Line("     정밀도를 우리에게 요구하는 것이 된다. 그래서 여기서는 셋만 본다 —");
            Line("       ① 접촉 k 의 «상대 계열» (골대 림인가 · 블록이면 어느 «행 y» 인가)");
            Line("       ② 접촉 «총수»가 원본 범위 안인가        ③ «결과(골인)»가 같은가");
            Line("  ⚠ 정답은 «한 값»이 아니라 «원본 시행들의 합집합»이다 — 좁히면 원본의 실제 답이 오답이 된다 (#48).");
            Line("  ⚠ 15회차의 「e_n 0.692~0.700 · 28/28」도 «접촉 2~5 전용»이다 — 6 이후엔 0.38~7.75 로 흩어지고");
            Line("     저속 접촉에서 v_n«후»가 음수(파고든 채 프레임을 넘김)인 «정착» 구간이 섞인다.");
            Line(string.Empty);
            CompareContacts612();

            Section("3-f. ★★★ «격자값 그 자체» 정답지 — 값 채점 «경계»까지만 센다  [19회차 · `07 §18`]");
            Line("  ★ 여태 정답지는 원본이 실제로 낸 «이웃 force»(53.9945 · 66.3970 …)였다.");
            Line("     여기 둘은 «채점기가 먹이는 격자값과 같은 값»을 원본이 실제로 쏜 시행이다 —");
            Line("     W1L1 66.3860 (★골인 +4318.2ms · 접촉 8) · W1L3 54.0000 (★골인 +4950.2ms · 접촉 10).");
            Line("  ⚠⚠ 값 채점 «경계»가 샷마다 다르다 — 66.3860 은 접촉 3 까지 · 54.0000 은 접촉 4 까지다.");
            Line("     15회차의 「접촉 2~5 는 결정적」도 «그 샷들 한정»이었다. 경계는 «샷마다» 재야 한다.");
            Line("  ⚠ 경계 밖 행은 찍기만 하고 안 센다 — 원본 자신이 거기서 갈리므로 세면 가짜 불일치가 쌓인다.");
            Line("  ★ 림 접촉(mtype 0)은 원본이 법선을 «안 채운다» — 사영 축을 «중심 차»로 만들어 쓴다 (18회차 처방).");
            Line(string.Empty);
            CompareGridShots();

            Section("3-e. ★★ 골대 림 접촉의 법선을 «기하로 만들어» 교차 검산한다  [18회차 처방 · `07 §16-c`]");
            Line("  원본은 림 접촉에서 localNormal 을 «안 채운다»(e_circles 매니폴드). 그래서 채점기의 처방은");
            Line("    「상대가 원이면 법선을 normalize(공 중심 − 림 중심) 으로 «만들어» 쓴다」 였다.");
            Line("  우리 쪽 매니폴드는 림에서도 법선을 «주므로», 그 처방이 맞는지 여기서 «태워 본다» —");
            Line("  두 법선(엔진이 준 것 ↔ 기하로 만든 것)이 같으면 처방이 옳고, 다르면 처방이 틀린 것이다 (#77).");
            Line("  ⚠ 림 중심은 «부딪힌 그 콜라이더»의 중심이다 — 골대 루트 좌표가 «아니다».");
            Line("     골대는 한 오브젝트에 원이 둘이라 루트 좌표로는 어느 원인지 안 갈린다.");
            Line("     그 자리가 데이터의 ±(75, 0) 과 맞는지도 같이 잰다 (환산이 한 곳만 지나는가).");
            Line(string.Empty);
            RimNormalCrossCheck(shots);

            Section("4. ★ 지문 — 같은 블록 재접촉 · 접촉 지속 스텝 수  [원본: 재접촉 0건 · 구간 1~2프레임]");
            Line("  ①-f 때 우리 최대 접촉 구간은 3스텝이었다. 그 뒤로 어떻게 됐는지를 «샷마다» 센다.");
            Line(string.Empty);
            FingerprintAll(shots);

            Section("5. ★★ 창 대조에서 «갈린» 21샷의 사슬 지문  [정답지 없음 — 지문만]");
            Line("  ⚠ 이 샷들에는 «원본 접촉 정답지»가 없다 (9회차는 W1L1 5샷 + W1L4 3샷만 쟀다).");
            Line("     그래서 여기서는 «순서 대조»를 할 수 없고 지문(재접촉·접촉 구간·반발 수)만 찍는다.");
            Line("     ⇒ 이 자리가 «다음 실측이 채워야 할 미측정»이다.");
            Line(string.Empty);
            FingerprintDiverged();

            Section("6. ★ 스텝 주기 민감도 — 같은 샷을 여러 스텝으로 굴린다  [실험 · 확정 아님]");
            Line("  ⚠ 원본의 «물리» 스텝은 아직 미측정이다 (9회차 §6 「물리 스텝 dt·반복횟수」 미측정).");
            Line("     여기서 «고르지» 않는다 — 사슬이 스텝에 얼마나 민감한지만 잰다.");
            Line(string.Empty);
            StepSensitivity(shots);

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
            _scene = SceneManager.CreateScene("BlumgiChain" + (_sceneSerial++).ToString(CultureInfo.InvariantCulture),
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
        /// <b>[실측 · 패스 ①-q]</b> 같은 <c>W1L3 force 21.4565</c> 를 <b>같은 호출로 5회 연속</b> 쏘면
        /// <b>골인 / 미골인 / 골인 / 미골인 / 골인</b> 이 나왔다 — 0·2·4 회차는 서로 <b>비트까지 같고</b>
        /// 1·3 회차도 서로 같다. 즉 <b>답이 두 개로 갈린다.</b> 첫 갈림은 <b>접촉 6 이 걸린 스텝 260</b>
        /// (Δ 0.024 px)이고, 거기서부터 사슬이 통째로 달라진다. <c>W1L1 66.3860</c> 도 같은
        /// 2-주기(반발 37 ↔ 31)로 갈렸다.
        /// </para>
        ///
        /// <para>
        /// 원인은 <b>Box2D 월드의 내부 재사용</b>이다 — <c>Teardown</c> 이 바디를 지우면 브로드페이즈
        /// 프록시·블록 할당자가 <b>자유목록으로 돌아가고</b>, 다음 <c>Build</c> 가 그 자리를 <b>역순으로</b>
        /// 집어 간다. 바디 «순서»가 달라지면 솔버의 누적 임펄스 합 순서가 달라지고(부동소수 비결합),
        /// 그 1e−7 이 접촉 6 에서 증폭된다. <b>새 씬 = 새 <c>b2World</c></b> 라 자유목록이 처음부터다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>씬을 새로 만들면 60발을 쏴도 «전부 같다»</b> [실측 · 앞선 샷 수를 바꿔도 동일].
        /// 값싸다 — 60발에 218 ms.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>점수를 보고 고른 것이 아니다</b> (재발방지 #48). 「같은 입력에 같은 답이 나오는가」
        /// 하나로 고른 것이고, 원본도 시행마다 <b>↺ 로 레이아웃을 다시 깐다</b> — 그쪽이 원본 절차다.
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

        // ────────────────────────────────────────────────────────── 1·2. 설정 전수

        /// <summary>
        /// ★★ <b>「기본값이니까 안 봐도 된다」를 없앤다.</b>
        ///
        /// <para>
        /// 이름을 손으로 나열하면 <b>«내가 아는 것»만 찍힌다</b> — 엔진 판이 바뀌어 새로 생긴 항목이나
        /// 이름이 바뀐 항목은 조용히 빠진다. 그래서 <b>리플렉션으로 «있는 대로»</b> 찍는다.
        /// </para>
        /// </summary>
        private void DumpPhysicsSettings()
        {
            Section("1. ★★ 우리 물리 설정 «전수» — 되읽은 값 그대로  [리플렉션 · 손으로 안 고른다]");
            Line($"  유니티 {Application.unityVersion}");
            Line(string.Empty);
            Line("  ── Time (스텝 주기) ──");
            Line($"    Time.fixedDeltaTime           = {Fmt(Time.fixedDeltaTime)}   ({1.0 / Time.fixedDeltaTime:0.###} Hz)");
            Line($"    Time.maximumDeltaTime         = {Fmt(Time.maximumDeltaTime)}");
            Line($"    Time.timeScale                = {Fmt(Time.timeScale)}");
            Line($"    Time.captureDeltaTime         = {Fmt(Time.captureDeltaTime)}");
            Line($"    데이터 PhysicsStepSeconds     = {_config.PhysicsStepSeconds.ToString("R", CultureInfo.InvariantCulture)}" +
                 $"   ({1.0 / _config.PhysicsStepSeconds:0.###} Hz)  ← BlumgiPhysicsSetup.Apply 가 fixedDeltaTime 에 넣는다");
            Line($"    데이터 MaxDeltaTime           = {_config.MaxDeltaTime.ToString("R", CultureInfo.InvariantCulture)}");
            Line($"    ★ 검사기가 Simulate 에 넣는 폭 = {_step.ToString("R", CultureInfo.InvariantCulture)}");
            Line(string.Empty);
            Line("  ── Physics2D 정적 프로퍼티 전수 ──");

            DumpStatics(typeof(Physics2D));

            Line(string.Empty);
            Line("  ── PhysicsScene2D (검사기 전용 로컬 씬) ──");
            Line($"    IsValid = {_physics.IsValid()}   ·  Scene = {_scene.name}");
            Line($"    ⚠ 이 씬은 LocalPhysicsMode.Physics2D 라 «자동 시뮬레이션이 안 돈다» — 검사기가 Simulate 로 민다.");
        }

        private void DumpStatics(Type type)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
            Array.Sort(properties, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo p = properties[i];

                if (p.CanRead == false)
                    continue;

                string value;

                try
                {
                    value = Describe(p.GetValue(null));
                }
                catch (Exception e)
                {
                    Exception inner = e.InnerException ?? e;
                    value = $"<읽기 실패: {inner.GetType().Name}>";
                }

                bool obsolete = p.GetCustomAttribute<ObsoleteAttribute>() != null;
                Line($"    {p.Name,-32} = {value}{(obsolete ? "   [Obsolete]" : string.Empty)}");
            }
        }

        /// <summary>공·블록·블롭·림의 바디/콜라이더/머티리얼 실효값을 <b>되읽는다</b>.</summary>
        private void DumpBodySettings()
        {
            Section("2. ★ 물체별 실효 설정 — 프리팹이 아니라 «세워진 인스턴스»에서 되읽는다");
            Line("  ⚠ 프리팹 인스펙터에는 auto-mass 결과가 안 적힌다 — 엔진에서 읽어야 진짜다.");
            Line(string.Empty);

            if (EnsureLevel("W1L1") == null || _world.Ball == null || _world.Ball.Body == null)
            {
                Line("  레벨을 못 세웠다 — 물체 설정을 못 읽는다");
                return;
            }

            DumpRigidbody("공 (BlumgiBall)", _world.Ball.Body);
            DumpCollider("공 콜라이더", _world.Ball.Collider);

            Transform blocks = _world.transform.Find("Blocks");

            if (blocks != null && blocks.childCount > 0)
            {
                var blockCollider = blocks.GetChild(0).GetComponentInChildren<Collider2D>();
                DumpCollider("블록 콜라이더 [0]", blockCollider);

                var blockBody = blocks.GetChild(0).GetComponentInChildren<Rigidbody2D>();

                if (blockBody != null)
                    DumpRigidbody("블록 바디 [0]", blockBody);
                else
                    Line("  블록 [0] — Rigidbody2D 가 «없다» (정적 콜라이더). 원본도 정적 바디다 [9회차 §1-d]");
            }

            if (_world.Blob != null)
            {
                DumpRigidbody("블롭 (BlumgiBlob)", _world.Blob);
                DumpCollider("블롭 콜라이더", _world.Blob.GetComponentInChildren<Collider2D>());
            }

            GameObject hoop = GameObject.Find(BlumgiPhysicsWorld.HoopPrefabName + "(Clone)");

            if (hoop != null)
            {
                Collider2D[] rims = hoop.GetComponentsInChildren<Collider2D>();

                for (int i = 0; i < rims.Length; i++)
                    DumpCollider($"림 콜라이더 [{i}]", rims[i]);
            }
        }

        private void DumpRigidbody(string what, Rigidbody2D body)
        {
            Line($"  ── {what} · Rigidbody2D ──");

            if (body == null)
            {
                Line("     <없다>");
                return;
            }

            PropertyInfo[] properties = typeof(Rigidbody2D).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            for (int i = 0; i < properties.Length; i++)
                LineInstance(properties[i], body);

            Line($"     [파생] 반지름 유닛 = {Fmt(_config.BallRadius / BlumgiUnits.WorldPixelsPerUnit)} · " +
                 $"½mR² = {Fmt(0.5 * body.mass * Math.Pow(_config.BallRadius / BlumgiUnits.WorldPixelsPerUnit, 2.0))}");
        }

        private void DumpCollider(string what, Collider2D collider)
        {
            Line($"  ── {what} · {(collider == null ? "<없다>" : collider.GetType().Name)} ──");

            if (collider == null)
                return;

            PropertyInfo[] properties = collider.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (a, b) => string.CompareOrdinal(a.Name, b.Name));

            for (int i = 0; i < properties.Length; i++)
                LineInstance(properties[i], collider);

            PhysicsMaterial2D material = collider.sharedMaterial;

            Line(material == null
                     ? "     sharedMaterial            = <없다>  ⚠ 마찰·반발이 «프로젝트 기본값»으로 돈다"
                     : $"     sharedMaterial            = {material.name} (friction {Fmt(material.friction)} · bounciness {Fmt(material.bounciness)})");
        }

        private void LineInstance(PropertyInfo p, object target)
        {
            if (p.CanRead == false || p.GetIndexParameters().Length > 0)
                return;

            // 트랜스폼·씬 잡동사니는 물리 판정과 무관해 소음이 된다.
            if (p.Name == "gameObject" || p.Name == "transform" || p.Name == "tag" || p.Name == "name" ||
                p.Name == "hideFlags" || p.Name == "runInEditMode" || p.Name == "useGUILayout")
                return;

            string value;

            try
            {
                value = Describe(p.GetValue(target));
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException ?? e;
                value = $"<읽기 실패: {inner.GetType().Name}>";
            }

            bool obsolete = p.GetCustomAttribute<ObsoleteAttribute>() != null;
            Line($"     {p.Name,-25} = {value}{(obsolete ? "   [Obsolete]" : string.Empty)}");
        }

        private static string Describe(object value)
        {
            if (value == null)
                return "<null>";

            if (value is float f)
                return Fmt(f);

            if (value is double d)
                return Fmt(d);

            if (value is Vector2 v2)
                return $"({Fmt(v2.x)}, {Fmt(v2.y)})";

            if (value is Vector3 v3)
                return $"({Fmt(v3.x)}, {Fmt(v3.y)}, {Fmt(v3.z)})";

            if (value is UnityEngine.Object o)
                return o == null ? "<null>" : o.name;

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Fmt(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        // ────────────────────────────────────────────────────────── 3. 사슬 순서 대조

        /// <summary>
        /// 9회차 정답지에 <b>시각</b>과 <b>상대 종류</b>를 붙인다.
        /// 시각은 <c>gap9/결과.md §1-b · §5-a</c> 의 <c>t(ms)</c> 컬럼, 상대는 같은 표의 <c>ᶜ</c> 표시다.
        /// ⚠ <c>GoldenVectors.Contacts()</c> 에는 이 두 컬럼이 없다 — <b>정본 표를 고치지 않고</b> 여기서 잇는다.
        /// </summary>
        private static void GoldenMeta(Dictionary<string, double> time, Dictionary<string, bool> rim)
        {
            void M(string id, double ms, bool isRim)
            {
                time[id] = ms;
                rim[id] = isRim;
            }

            M("1", 899, false);
            M("2", 1252, false);
            M("3", 2183, false);
            M("4", 2807, false);
            M("5", 3441, false);
            M("6", 3891, false);
            M("7", 4174, false);
            M("8", 4424, true);
            M("9", 4516, true);
            M("10", 4766, true);
            M("11", 5074, true);
            M("12", 2408, false);
            M("13", 2636, false);
            M("14", 3567, false);
            M("15", 3834, false);
            M("16", 4658, false);
            M("17", 5208, false);
            M("18", 5258, true);
            M("19", 5503, true);
            M("20", 5674, false);
            M("21", 6076, true);
            M("22", 6250, true);
            M("23", 6366, true);
            M("24", 6500, true);

            M("D1", 926, false);
            M("D2", 2092, true);
            M("D3", 2128, true);
            M("D4", 2217, true);
            M("D5", 2259, false);
            M("D6", 2526, true);
            M("D7", 2700, true);
            M("D8", 2908, true);
            M("D9", 3059, true);
            M("D10", 1833, false);
            M("D11", 1899, false);
        }

        private static List<GoldenShot> BuildGoldenShots()
        {
            var time = new Dictionary<string, double>(48);
            var rim = new Dictionary<string, bool>(48);
            GoldenMeta(time, rim);

            var shots = new List<GoldenShot>
            {
                new GoldenShot { Level = "W1L1", HoldMs = 300,  Shot = "W1L1 h300",  OriginBlockBounces = 1, OriginRimBounces = 0, Note = "미클리어" },
                new GoldenShot { Level = "W1L1", HoldMs = 450,  Shot = "W1L1 h450",  OriginBlockBounces = 1, OriginRimBounces = 0, Note = "클리어" },
                new GoldenShot { Level = "W1L1", HoldMs = 700,  Shot = "W1L1 h700",  OriginBlockBounces = 5, OriginRimBounces = 4, Note = "클리어" },
                new GoldenShot { Level = "W1L1", HoldMs = 900,  Shot = "W1L1 h900",  OriginBlockBounces = 7, OriginRimBounces = 6, Note = "미클리어 (림에서 오래 튕긴다)" },
                new GoldenShot { Level = "W1L1", HoldMs = 1200, Shot = "W1L1 h1200", OriginBlockBounces = 0, OriginRimBounces = 0, Note = "미클리어 (아무것도 안 맞는다)" },
                new GoldenShot { Level = "W1L4", HoldMs = 254,  Shot = "W1L4 h254",  OriginBlockBounces = 2, OriginRimBounces = 7, Note = "클리어" },
                new GoldenShot { Level = "W1L4", HoldMs = 530,  Shot = "W1L4 h530",  OriginBlockBounces = 2, OriginRimBounces = 0, Note = "⚠ 9회차 1시행 클리어 (7회차와 어긋남)" },
                new GoldenShot { Level = "W1L4", HoldMs = 900,  Shot = "W1L4 h900",  OriginBlockBounces = 0, OriginRimBounces = 0, Note = "미클리어" },
            };

            List<GoldenVectors.ContactEvent> all = GoldenVectors.Contacts();

            for (int i = 0; i < all.Count; i++)
            {
                GoldenVectors.ContactEvent e = all[i];

                for (int s = 0; s < shots.Count; s++)
                {
                    if (shots[s].Shot != e.Shot)
                        continue;

                    shots[s].Contacts.Add(new GoldenContact
                    {
                        Event = e,
                        TimeMs = time.TryGetValue(e.Id, out double ms) ? ms : double.NaN,
                        Rim = rim.TryGetValue(e.Id, out bool r) && r,
                    });
                    break;
                }
            }

            // 정답지는 t 오름차순이어야 «순서» 대조가 성립한다.
            for (int s = 0; s < shots.Count; s++)
                shots[s].Contacts.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

            // ★★ 9회차의 t 는 «홀드 시작»부터다 — 릴리즈부터가 아니다.
            //    근거(실측 · 이 도구가 낸 숫자): 원본 t − 홀드 = 899−300=599 · 1252−450=802 ·
            //    2183−700=1483 · 2408−900=1508 · 926−254=672 이고, 우리 «비행 시각»이
            //    583 · 800 · 1458 · 1500 · 672 다 — 전부 30ms 안에 붙는다.
            //    빼지 않고 대조하면 «첫 접촉부터 300~900ms 어긋난다»는 가짜 결론이 나온다.
            for (int s = 0; s < shots.Count; s++)
            {
                for (int c = 0; c < shots[s].Contacts.Count; c++)
                    shots[s].Contacts[c].TimeMs -= shots[s].HoldMs;
            }

            return shots;
        }

        private void CompareChain(GoldenShot shot)
        {
            List<OurContact> ours = RunShotHold(shot.Level, shot.HoldMs, out EBlumgiShotState state, out double flightSeconds);

            Line($"  ── {shot.Shot}  [원본 {shot.Note} · 반발 {shot.OriginBlockBounces + shot.OriginRimBounces}회" +
                 $"(블록 {shot.OriginBlockBounces} · 림 {shot.OriginRimBounces})] ──");

            if (ours == null)
            {
                Line("     발사되지 않았다 — 사슬을 못 잰다");
                Line(string.Empty);
                return;
            }

            int ourBlocks = 0;
            int ourRims = 0;

            for (int i = 0; i < ours.Count; i++)
            {
                if (ours[i].IsRim)
                    ourRims++;
                else if (ours[i].OtherName != null && ours[i].OtherName.Contains(BlumgiPhysicsWorld.BlockPrefabName))
                    ourBlocks++;
            }

            Line($"     우리 접촉 {ours.Count}건 (블록 {ourBlocks} · 림 {ourRims} · 기타 {ours.Count - ourBlocks - ourRims}) · " +
                 $"{flightSeconds:0.000}s · {state}");
            Line(string.Empty);
            Line("      #  |  원본 t     n              v_n 전→후        v_t 전→후        ω 전→후");
            Line("         |  우리 t     n              v_n 전→후        v_t 전→후        ω 전→후    상대(스텝)");
            Line("     ────┼──────────────────────────────────────────────────────────────────────────────");

            int firstNormalGap = -1;
            int invalidNormals = 0;
            int firstVnGap = -1;
            int firstOmegaGap = -1;
            int firstTimeGap = -1;
            int firstOmegaBeforeGap = -1;

            int rows = Math.Max(shot.Contacts.Count, ours.Count);

            for (int i = 0; i < rows; i++)
            {
                GoldenContact g = i < shot.Contacts.Count ? shot.Contacts[i] : null;
                OurContact o = i < ours.Count ? ours[i] : null;

                if (g != null)
                {
                    Line($"     {i + 1,3} |  {g.TimeMs,7:0}   [{g.Event.Nx,6:0.000},{g.Event.Ny,6:0.000}]  " +
                         $"{g.Event.VnBefore,8:0.0}→{g.Event.VnAfter,7:0.0}  " +
                         $"{g.Event.VtBefore,8:0.0}→{g.Event.VtAfter,7:0.0}  " +
                         $"{g.Event.OmegaBefore,7:0.000}→{g.Event.OmegaAfter,7:0.000}  {(g.Rim ? "림" : "블록")} #{g.Event.Id}");
                }
                else
                {
                    Line($"     {i + 1,3} |  {"—",7}   원본에 없다 (원본은 여기서 접촉이 끝난다)");
                }

                if (o != null && o.NormalValid == false)
                {
                    // ★★ [14회차-b] 법선이 없는 접촉은 «법선각 0°» 로 찍히면 안 된다 — 그건 「완벽히 맞았다」로 읽힌다.
                    Line($"         |  {o.TimeMs,7:0}   [법선 «무효»]  " +
                         $"{o.VnBefore,8:0.0}→{o.VnAfter,7:0.0}  " +
                         $"{o.VtBefore,8:0.0}→{o.VtAfter,7:0.0}  " +
                         $"{o.OmegaBefore,7:0.000}→{o.OmegaAfter,7:0.000}  " +
                         $"{Short(o.OtherName)}({o.Steps}스텝) ⚠ 무효 법선(원본 e_circles = 상대가 원) — 법선 대조를 건너뛴다");
                }
                else if (o != null)
                {
                    Line($"         |  {o.TimeMs,7:0}   [{o.Nx,6:0.000},{o.Ny,6:0.000}]  " +
                         $"{o.VnBefore,8:0.0}→{o.VnAfter,7:0.0}  " +
                         $"{o.VtBefore,8:0.0}→{o.VtAfter,7:0.0}  " +
                         $"{o.OmegaBefore,7:0.000}→{o.OmegaAfter,7:0.000}  " +
                         $"{Short(o.OtherName)}({o.Steps}스텝{(o.Simultaneous > 1 ? $" · 동시{o.Simultaneous}" : string.Empty)})");
                }
                else
                {
                    Line($"         |  {"—",7}   우리에겐 없다 (우리는 여기서 접촉이 끝난다)");
                }

                if (g == null || o == null)
                    continue;

                // ★★ [14회차-b] 어느 한쪽이라도 법선이 무효면 «각도»는 재지 않는다 — 0° 로 찍히면 오판한다.
                bool normalComparable = o.NormalValid && g.Event.NormalValid;
                double dot = (g.Event.Nx * o.Nx) + (g.Event.Ny * o.Ny);
                double angle = normalComparable
                    ? Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))) * 180.0 / Math.PI
                    : double.NaN;
                double dVn = Math.Abs(o.VnBefore - g.Event.VnBefore);
                double relVn = Math.Abs(g.Event.VnBefore) < 1e-9 ? 0.0 : dVn / Math.Abs(g.Event.VnBefore);
                double dOmegaBefore = Math.Abs(o.OmegaBefore - g.Event.OmegaBefore);
                double dOmegaAfter = Math.Abs(o.OmegaAfter - g.Event.OmegaAfter);
                double dt = Math.Abs(o.TimeMs - g.TimeMs);

                string angleText = normalComparable ? $"{angle,5:0.0}°" : " 무효";

                Line($"         |  Δ  t {dt,6:0.0}ms · 법선 {angleText} · v_n {relVn * 100.0,5:0.0}% ({dVn:0.0}) · " +
                     $"ω전 {dOmegaBefore,6:0.000} · ω후 {dOmegaAfter,6:0.000} rad/s");

                if (normalComparable == false)
                    invalidNormals++;

                if (firstNormalGap < 0 && normalComparable && angle > 5.0)
                    firstNormalGap = i + 1;

                if (firstVnGap < 0 && relVn > 0.10)
                    firstVnGap = i + 1;

                if (firstOmegaBeforeGap < 0 && dOmegaBefore > 0.5)
                    firstOmegaBeforeGap = i + 1;

                if (firstOmegaGap < 0 && dOmegaAfter > 0.5)
                    firstOmegaGap = i + 1;

                if (firstTimeGap < 0 && dt > 40.0)
                    firstTimeGap = i + 1;
            }

            Line(string.Empty);
            Line($"     ⇒ 첫 갈림 —  시각(>40ms) {Where(firstTimeGap)} · 법선(>5°) {Where(firstNormalGap)} · " +
                 $"v_n(>10%) {Where(firstVnGap)} · ω«전»(>0.5) {Where(firstOmegaBeforeGap)} · ω«후»(>0.5) {Where(firstOmegaGap)}");
            Line($"     ⇒ 접촉 «개수»  원본 {shot.Contacts.Count} / 우리 {ours.Count}" +
                 (shot.Contacts.Count == 0 ? "   (원본 정답지에 접촉이 없는 샷)" : string.Empty));

            // ★★ 0 건이어도 «찍는다» — 장치가 있다는 것과 이번에 발동하지 않았다는 것을 둘 다 남긴다
            //    (재발방지 #77 「안전 장치를 고치고 실제로 발동하는지 안 태워 본다」).
            Line(invalidNormals > 0
                     ? $"     ⚠ [법선 무효] {invalidNormals}건 — 법선 대조에서 «뺐다». " +
                       "★ [18회차] 무효 법선은 «상대가 골대 림(원)»인 접촉이다 — e_circles 매니폴드는 " +
                       "중심 차로 법선을 만들므로 localNormal 을 애초에 안 채운다 [`07 §16-c` · 27/27 예외 0건]. " +
                       "조용히 0° 로 세면 「완벽히 맞았다」로 읽힌다."
                     : "     [법선 무효] 0건 — 이 샷의 접촉은 전부 «면 법선»을 갖는다 " +
                       "(장치는 살아 있다 · 저속 정착 사슬을 정답지에 넣는 회차에 발동한다).");
            Line(string.Empty);
        }

        private static string Where(int index)
        {
            return index < 0 ? "없음" : $"#{index}";
        }

        // ────────────────────────────────────────────────────────── 3-c. 접촉 2~5 정답지 대조

        /// <summary>
        /// 우리 쪽 «접촉 사건» 하나 — <b>같은 시각에 열린 접촉을 묶은 것</b>이다.
        /// 원본의 정의(빈 프레임으로 끊긴 연속 구간 = 1건)와 같게 맞춘다.
        /// </summary>
        private sealed class OurEvent
        {
            public int BeginStep;
            public int EndStep;
            public double TimeMs;
            public double PreX;
            public double PreY;
            public List<OurContact> Members = new List<OurContact>(4);

            public int Frames
            {
                get { return (EndStep - BeginStep) + 1; }
            }

            /// <summary>구간 «직전» 속도 — 가장 먼저 열린 접촉의 것.</summary>
            public double VbX;
            public double VbY;
            public double OmegaBefore;

            /// <summary>구간 «직후» 속도 — 가장 늦게 닫힌 접촉의 것.</summary>
            public double VaX;
            public double VaY;
            public double OmegaAfter;
        }

        /// <summary>갈림 문턱 — <b>표에 적어 두고 리포트에도 찍는다</b>. 조용히 바꾸지 않는다.</summary>
        private const double C25TimeToleranceMs = 40.0;
        private const double C25PositionTolerancePx = 20.0;
        private const double C25NormalToleranceDeg = 5.0;
        private const double C25VnRelTolerance = 0.10;
        private const double C25VtRelTolerance = 0.10;
        private const double C25VtAbsTolerancePxPerSec = 20.0;
        private const double C25OmegaToleranceRad = 0.5;

        /// <summary>
        /// ★★★ <b>W1L3 접촉 2~5 정답지 대조</b> (`07 §15`).
        ///
        /// <para>
        /// <b>사영 축을 «정답지의 첫 법선»으로 통일한다.</b> 동시 접촉에서 「누가 첫 법선인가」는
        /// 원본도 우리도 <b>임의 순서</b>다 — 우리 순서로 축을 잡으면 같은 접촉인데도 <c>v_n</c>/<c>v_t</c> 가
        /// 통째로 갈려 «가짜 갈림»이 나온다. 축을 정답지 쪽으로 고정하면 <b>같은 축의 같은 양</b>을 비교하게 된다.
        /// </para>
        /// </summary>
        private void CompareContacts25()
        {
            List<GoldenVectors.Contact25Event> all = GoldenVectors.Contacts25();

            // 샷 단위로 묶는다 — 순서를 유지해야 표가 정답지 순서로 나온다.
            var order = new List<string>();
            var byShot = new Dictionary<string, List<GoldenVectors.Contact25Event>>();

            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].ShotId;

                if (byShot.TryGetValue(id, out List<GoldenVectors.Contact25Event> bucket) == false)
                {
                    bucket = new List<GoldenVectors.Contact25Event>(4);
                    byShot[id] = bucket;
                    order.Add(id);
                }

                bucket.Add(all[i]);
            }

            Line($"  문턱 — 시각 {C25TimeToleranceMs:0}ms · 위치 {C25PositionTolerancePx:0}px · " +
                 $"법선 {C25NormalToleranceDeg:0.0}° · v_n {C25VnRelTolerance * 100.0:0}% · " +
                 $"v_t {C25VtRelTolerance * 100.0:0}%(또는 {C25VtAbsTolerancePxPerSec:0}px/s) · ω {C25OmegaToleranceRad:0.0} rad/s");
            Line(string.Empty);

            var firstDiverge = new List<string>();
            int graded = 0;
            int matched = 0;

            for (int s = 0; s < order.Count; s++)
            {
                List<GoldenVectors.Contact25Event> shot = byShot[order[s]];
                GoldenVectors.Contact25Event head = shot[0];

                double holdMs = (head.Force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0;

                List<OurContact> ours = RunShotHold(head.Level, holdMs,
                                                    out EBlumgiShotState state, out double seconds);

                Line($"  ── 샷 {head.ShotId}  {head.Level} force {head.Force:0.0000} " +
                     $"(홀드 원본 {head.OriginHoldMs}ms → 우리 {holdMs:0.0}ms) · " +
                     $"원본 ★골인 +{head.OriginGoalMs:0}ms · 원본 접촉 {head.OriginContacts}회 ──");

                if (ours == null)
                {
                    Line("     발사되지 않았다 — 사슬을 못 잰다");
                    firstDiverge.Add($"{head.ShotId}: 발사 실패");
                    Line(string.Empty);
                    continue;
                }

                List<OurEvent> events = GroupEvents(ours);

                Line($"     우리 접촉 «사건» {events.Count}건 (원시 접촉 {ours.Count}건) · " +
                     $"{seconds:0.000}s · {state}   ⇒ 우리 {(state == EBlumgiShotState.Scored ? "골인" : "미골인")}");
                Line(string.Empty);
                Line("      #  |  원본  t        직전(x, y)          v_n 전→후        v_t 전→후        ω 전→후      e_n   동시");
                Line("         |  우리  t        직전(x, y)          v_n 전→후        v_t 전→후        ω 전→후      e_n   동시");
                Line("     ────┼──────────────────────────────────────────────────────────────────────────────────────────");

                // ★ 접촉 «1번»을 기준 행으로 먼저 찍는다 — 2~5 번호가 «맞물렸는지»가 여기서 갈린다.
                //   정답지에는 1번이 없지만(15회차가 2~5 만 쟀다), 1번이 어긋나면 그 아래가 통째로 밀린다.
                if (events.Count > 0)
                {
                    OurEvent e1 = events[0];
                    Line($"       1 |  (정답지에 없다 — 15회차는 접촉 2~5 만 쟀다. 1번은 `07 §13`·§7-b 가 «맞다»고 확인한 자리다)");
                    Line($"         |  {e1.TimeMs,8:0.0}  ({e1.PreX,9:0.000},{e1.PreY,8:0.000})  {BodiesOf(e1)}");
                }

                int shotFirst = -1;
                string shotFirstWhy = null;

                for (int c = 0; c < shot.Count; c++)
                {
                    GoldenVectors.Contact25Event g = shot[c];
                    graded++;

                    Line($"     {g.Index,3} |  {g.TimeMs,8:0.0}  ({g.PreX,9:0.000},{g.PreY,8:0.000})  " +
                         $"{g.VnBefore,8:0.0}→{g.VnAfter,7:0.0}  {g.VtBefore,8:0.0}→{g.VtAfter,8:0.0}  " +
                         $"{g.OmegaBefore,7:0.000}→{g.OmegaAfter,7:0.000}  {g.En:0.000}  {g.Simultaneous}");

                    if (g.Index - 1 >= events.Count)
                    {
                        Line($"         |  우리에겐 «{g.Index}번째 접촉 사건이 없다» (우리 사건 {events.Count}건에서 끝난다)");

                        if (shotFirst < 0)
                        {
                            shotFirst = g.Index;
                            shotFirstWhy = "우리 쪽 접촉 사건이 그 전에 끝난다";
                        }

                        continue;
                    }

                    OurEvent o = events[g.Index - 1];

                    // ★ 사영 축 = 정답지의 «첫 법선». 양쪽 순서가 임의라 축을 정답지 쪽으로 고정한다.
                    double gnx = g.Nx[0];
                    double gny = g.Ny[0];
                    double gtx = -gny;
                    double gty = gnx;

                    double ourVnBefore = (o.VbX * gnx) + (o.VbY * gny);
                    double ourVnAfter = (o.VaX * gnx) + (o.VaY * gny);
                    double ourVtBefore = (o.VbX * gtx) + (o.VbY * gty);
                    double ourVtAfter = (o.VaX * gtx) + (o.VaY * gty);
                    double ourEn = Math.Abs(ourVnBefore) < 1e-9 ? 0.0 : Math.Abs(ourVnAfter) / Math.Abs(ourVnBefore);

                    Line($"         |  {o.TimeMs,8:0.0}  ({o.PreX,9:0.000},{o.PreY,8:0.000})  " +
                         $"{ourVnBefore,8:0.0}→{ourVnAfter,7:0.0}  {ourVtBefore,8:0.0}→{ourVtAfter,8:0.0}  " +
                         $"{o.OmegaBefore,7:0.000}→{o.OmegaAfter,7:0.000}  {ourEn:0.000}  {o.Members.Count}");

                    // ★★ 「무엇이 다른가」의 핵심 — 원본은 «어느 블록의 어느 면»을 쳤고 우리는 «무엇»을 쳤나.
                    Line($"         |  원본 상대 {BodiesOfGolden(g)}");
                    Line($"         |  우리 상대 {BodiesOf(o)}");

                    double dt = Math.Abs(o.TimeMs - g.TimeMs);
                    double dPos = Math.Sqrt(((o.PreX - g.PreX) * (o.PreX - g.PreX)) +
                                            ((o.PreY - g.PreY) * (o.PreY - g.PreY)));
                    double worstNormal = WorstNormalGap(g, o, out int invalid);
                    double dVn = Math.Abs(ourVnBefore - g.VnBefore);
                    double relVn = Math.Abs(g.VnBefore) < 1e-9 ? 0.0 : dVn / Math.Abs(g.VnBefore);
                    double dVt = Math.Abs(ourVtBefore - g.VtBefore);
                    double vtAllow = Math.Max(Math.Abs(g.VtBefore) * C25VtRelTolerance, C25VtAbsTolerancePxPerSec);
                    double dOmegaBefore = Math.Abs(o.OmegaBefore - g.OmegaBefore);
                    double dOmegaAfter = Math.Abs(o.OmegaAfter - g.OmegaAfter);

                    string normalText = double.IsNaN(worstNormal) ? " 무효" : $"{worstNormal,5:0.0}°";

                    Line($"         |  Δ  t {dt,6:0.0}ms · 위치 {dPos,6:0.0}px · 법선 {normalText} · " +
                         $"v_n전 {relVn * 100.0,5:0.0}%({dVn:0.0}) · v_t전 {dVt,6:0.0} · " +
                         $"ω전 {dOmegaBefore,6:0.000} · ω후 {dOmegaAfter,6:0.000}" +
                         (invalid > 0 ? $" · ⚠ 법선 무효 {invalid}건" : string.Empty) +
                         (o.Members.Count != g.Simultaneous ? $" · ⚠ 동시 접촉 수 다름 ({g.Simultaneous}→{o.Members.Count})" : string.Empty));

                    var why = new List<string>(6);

                    if (dt > C25TimeToleranceMs)
                        why.Add($"시각 {dt:0.0}ms");

                    if (dPos > C25PositionTolerancePx)
                        why.Add($"위치 {dPos:0.0}px");

                    if (double.IsNaN(worstNormal) == false && worstNormal > C25NormalToleranceDeg)
                        why.Add($"법선 {worstNormal:0.0}°");

                    if (relVn > C25VnRelTolerance)
                        why.Add($"v_n전 {relVn * 100.0:0.0}%");

                    if (dVt > vtAllow)
                        why.Add($"v_t전 {dVt:0.0}px/s");

                    if (dOmegaBefore > C25OmegaToleranceRad)
                        why.Add($"ω전 {dOmegaBefore:0.000}");

                    if (dOmegaAfter > C25OmegaToleranceRad)
                        why.Add($"ω후 {dOmegaAfter:0.000}");

                    if (why.Count == 0)
                    {
                        matched++;
                    }
                    else if (shotFirst < 0)
                    {
                        shotFirst = g.Index;
                        shotFirstWhy = string.Join(" · ", why.ToArray());
                    }
                }

                Line(string.Empty);

                if (shotFirst < 0)
                {
                    Line("     ⇒ ★ 접촉 2~5 «전부 일치» — 이 샷의 갈림은 접촉 6 이후에 있다.");
                    firstDiverge.Add($"{head.ShotId}(f{head.Force:0.000}): 2~5 전부 일치 → 갈림은 «접촉 6 이후»");
                }
                else
                {
                    Line($"     ⇒ ★★ 첫 갈림 = «접촉 #{shotFirst}»  —  {shotFirstWhy}");
                    firstDiverge.Add($"{head.ShotId}(f{head.Force:0.000}): 첫 갈림 #{shotFirst} — {shotFirstWhy}");
                }

                Line(string.Empty);
            }

            Line("  ══ 샷별 «첫 갈림 접촉 번호» ══");

            for (int i = 0; i < firstDiverge.Count; i++)
                Line($"     {firstDiverge[i]}");

            Line(string.Empty);
            Line($"  ⇒ 접촉 2~5 대조 {matched}/{graded} 일치  [진단 · 이 절은 점수에 넣지 않는다]");
            Line("     ⚠ 여기서 나온 숫자로 물리 값을 «고르지» 않는다 (재발방지 #48). 자리를 «가리킬» 뿐이다.");
            Line(string.Empty);

            LauncherBlobOverlap(all);
        }

        // ────────────────────────────────────────────────────────── 3-f. 19회차 «격자값 그 자체» 정답지

        /// <summary>
        /// ★★★ <b>«격자값 그 자체»를 쏜 원본과 맞댄다</b> [19회차 · <c>07 §18-d/e</c>].
        ///
        /// <para>
        /// ★ <b>이 절이 15·18회차와 다른 점</b> — 여태 정답지는 원본이 실제로 낸 <b>이웃 force</b>
        /// (<c>53.9945</c> · <c>66.3970</c> …)였다. 여기 둘은 <b>채점기가 먹이는 격자값과 «같은» 값을
        /// 원본이 실제로 쏜 시행</b>이다. ⇒ 「이 자리에서 원본은 무엇을 하는가」의 <b>가장 직접적인 답</b>이다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>값 채점은 «경계»까지만 한다</b> — <c>W1L1 66.3860</c> 은 <b>접촉 3</b>,
        /// <c>W1L3 54.0000</c> 은 <b>접촉 4</b> 다. 경계 밖의 행은 <b>찍기만 하고 «세지 않는다»</b> —
        /// 원본 자신이 그 뒤로 갈리므로 세면 <b>가짜 불일치</b>가 쌓인다.
        /// </para>
        /// </summary>
        private void CompareGridShots()
        {
            List<GoldenVectors.GridShotContact> all = GoldenVectors.GridShotContacts();
            List<GoldenVectors.ValueGradeBoundary> bounds = GoldenVectors.ValueGradeBoundaries();

            var order = new List<string>();
            var byShot = new Dictionary<string, List<GoldenVectors.GridShotContact>>();

            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].ShotId;

                if (byShot.TryGetValue(id, out List<GoldenVectors.GridShotContact> bucket) == false)
                {
                    bucket = new List<GoldenVectors.GridShotContact>(12);
                    byShot[id] = bucket;
                    order.Add(id);
                }

                bucket.Add(all[i]);
            }

            int graded = 0;
            int matched = 0;

            for (int s = 0; s < order.Count; s++)
            {
                List<GoldenVectors.GridShotContact> shot = byShot[order[s]];
                GoldenVectors.GridShotContact head = shot[0];

                int boundary = 0;
                int structureBoundary = 0;
                string evidence = "경계 미측정";

                for (int b = 0; b < bounds.Count; b++)
                {
                    if (bounds[b].Level != head.Level || Math.Abs(bounds[b].Force - head.Force) > 1e-9)
                        continue;

                    boundary = bounds[b].MaxIndex;
                    structureBoundary = bounds[b].StructureGradeMaxIndex;
                    evidence = $"n={bounds[b].Trials} · {bounds[b].Evidence}";
                }

                double holdMs = (head.Force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0;

                List<OurContact> ours = RunShotHold(head.Level, holdMs,
                                                    out EBlumgiShotState state, out double seconds);

                // ★ [20회차] 격자값과 «실 force» 가 다를 수 있다 — 다르면 「그 자체를 쟀다」로 읽히면 안 된다.
                bool isGridItself = Math.Abs(head.ActualForce - head.Force) < 1e-9;

                string what = isGridItself
                    ? "«그 자체»"
                    : $"«그 자체가 아니다 — 원본 실 force {head.ActualForce:0.0000}»";

                Line($"  ── {head.Level} 격자값 force {head.Force:0.0000} {what} (샷 {head.ShotId} · 홀드 {holdMs:0.0}ms) · " +
                     $"원본 {(head.OriginScored ? "★골인" : "미골인")} +{head.OriginGoalMs:0.0}ms · 접촉 {head.OriginContacts}회 ──");
                Line($"     ⓪ 값 채점 경계 = «접촉 {boundary} 까지»" +
                     (structureBoundary > 0 ? $" · 구조 채점 경계 = «접촉 {structureBoundary} 까지»" : string.Empty) +
                     $"   [{evidence}]");

                if (isGridItself == false)
                    Line("     ⚠ 이 샷은 «격자값 그 자체»의 정답지가 «아니다» — 그 자체는 미측정이고 재측정 대기다.");

                if (ours == null)
                {
                    Line("     발사되지 않았다 — 못 잰다");
                    Line(string.Empty);
                    continue;
                }

                List<OurEvent> events = GroupEvents(ours);

                Line($"     우리 접촉 «사건» {events.Count}건 (원시 {ours.Count}건) · {seconds:0.000}s · {state}   " +
                     $"⇒ 우리 {(state == EBlumgiShotState.Scored ? "골인" : "미골인")} / 원본 {(head.OriginScored ? "골인" : "미골인")}");
                Line(string.Empty);
                Line("      #  |  원본  t        직전(x, y)          v_n 전→후        ω 전→후      e_n  상대 / 매니폴드");
                Line("         |  우리  t        직전(x, y)          v_n 전→후        ω 전→후      e_n  상대");
                Line("     ────┼──────────────────────────────────────────────────────────────────────────────────────");

                for (int c = 0; c < shot.Count; c++)
                {
                    GoldenVectors.GridShotContact g = shot[c];
                    bool inBoundary = boundary > 0 && g.Index <= boundary;

                    string other = double.IsNaN(g.SecondOtherX)
                        ? $"({g.OtherX:0},{g.OtherY:0})"
                        : $"({g.OtherX:0},{g.OtherY:0})+({g.SecondOtherX:0},{g.SecondOtherY:0})";

                    string kind = g.IsRim ? "★림 mtype 0 — 법선은 «중심 차로 만든다»" : "블록 mtype 1";

                    Line($"     {g.Index,3} |  {g.TimeMs,8:0.0}  ({g.PreX,9:0.000},{g.PreY,8:0.000})  " +
                         $"{g.VnBefore,8:0.0}→{g.VnAfter,7:0.0}  {g.OmegaBefore,7:0.000}→{g.OmegaAfter,7:0.000}  " +
                         $"{g.En:0.000}  {other} {kind}");

                    if (g.Index - 1 >= events.Count)
                    {
                        Line($"         |  우리에겐 «{g.Index}번째 접촉 사건이 없다» (우리 사건 {events.Count}건)" +
                             (inBoundary ? "   ★ 경계 «안»이라 이건 «셈»한다" : "   [경계 밖 — 안 센다]"));

                        if (inBoundary)
                            graded++;

                        continue;
                    }

                    OurEvent o = events[g.Index - 1];

                    // ★ 림 접촉은 원본이 법선을 «안 채운다». 그 행은 사영 축을 만들 수 없으므로
                    //   «중심 차»를 축으로 쓴다 — 18회차 처방 그대로다.
                    double gnx = g.Nx;
                    double gny = g.Ny;

                    if (double.IsNaN(gnx))
                    {
                        double dx = g.PreX - g.OtherX;
                        double dy = g.PreY - g.OtherY;
                        double len = Math.Sqrt((dx * dx) + (dy * dy));
                        gnx = len < 1e-9 ? 0.0 : dx / len;
                        gny = len < 1e-9 ? 0.0 : dy / len;
                    }

                    double ourVnBefore = (o.VbX * gnx) + (o.VbY * gny);
                    double ourVnAfter = (o.VaX * gnx) + (o.VaY * gny);
                    double ourEn = Math.Abs(ourVnBefore) < 1e-9 ? 0.0 : Math.Abs(ourVnAfter) / Math.Abs(ourVnBefore);

                    Line($"         |  {o.TimeMs,8:0.0}  ({o.PreX,9:0.000},{o.PreY,8:0.000})  " +
                         $"{ourVnBefore,8:0.0}→{ourVnAfter,7:0.0}  {o.OmegaBefore,7:0.000}→{o.OmegaAfter,7:0.000}  " +
                         $"{ourEn:0.000}  {BodiesOf(o)}");

                    double dt = Math.Abs(o.TimeMs - g.TimeMs);
                    double dPos = Math.Sqrt(((o.PreX - g.PreX) * (o.PreX - g.PreX)) +
                                            ((o.PreY - g.PreY) * (o.PreY - g.PreY)));
                    double relVn = Math.Abs(g.VnBefore) < 1e-9
                        ? 0.0
                        : Math.Abs(ourVnBefore - g.VnBefore) / Math.Abs(g.VnBefore);
                    double dOmegaAfter = Math.Abs(o.OmegaAfter - g.OmegaAfter);

                    if (inBoundary == false)
                    {
                        Line($"         |  Δ  t {dt,6:0.0}ms · 위치 {dPos,6:0.0}px · v_n전 {relVn * 100.0,5:0.0}% · " +
                             $"ω후 {dOmegaAfter,6:0.000}   [경계 밖 — «참고»다. 원본 자신이 여기서 갈린다]");
                        continue;
                    }

                    graded++;

                    var why = new List<string>(4);

                    if (dt > C25TimeToleranceMs)
                        why.Add($"시각 {dt:0.0}ms");

                    if (dPos > C25PositionTolerancePx)
                        why.Add($"위치 {dPos:0.0}px");

                    if (relVn > C25VnRelTolerance)
                        why.Add($"v_n전 {relVn * 100.0:0.0}%");

                    if (dOmegaAfter > C25OmegaToleranceRad)
                        why.Add($"ω후 {dOmegaAfter:0.000}");

                    if (why.Count == 0)
                        matched++;

                    Line($"         |  Δ  t {dt,6:0.0}ms · 위치 {dPos,6:0.0}px · v_n전 {relVn * 100.0,5:0.0}% · " +
                         $"ω후 {dOmegaAfter,6:0.000}   ⇒ {(why.Count == 0 ? "일치" : "★ " + string.Join(" · ", why.ToArray()))}");
                }

                Line(string.Empty);
            }

            Line($"  ⇒ «경계 안» 값 대조 {matched}/{graded} 일치  [진단 · 이 절은 점수에 넣지 않는다]");
            Line("     ⚠ 경계 밖 행은 «찍기만» 했다 — 세면 원본 자신이 만드는 «가짜 불일치»가 쌓인다.");
            Line("     ⚠⚠ 경계를 «넓혀» 숫자를 올리지 마라 (#48). 경계를 옮기는 근거는 «새 실측»뿐이다.");
            Line(string.Empty);
        }

        /// <summary>
        /// ★★★ <b>원본 공이 «우리 발사대 블롭»이 놓인 자리를 그냥 지나가는가</b> — 기계로 센다.
        ///
        /// <para>
        /// 위 대조에서 우리 접촉 #2 의 상대가 <b>7샷 중 6샷에서 <c>Blob</c></b> 이었는데
        /// <b>정답지 28건의 상대에는 블롭이 «한 건도» 없다</b>. 둘 중 하나다 —
        /// ① 원본 공이 그 자리를 안 지나간다, ② <b>원본에서는 발사대가 공과 «안 부딪힌다»</b>.
        /// </para>
        ///
        /// <para>
        /// 그래서 <b>정답지의 «직전» 공 위치</b>(원본 실측)를 우리 발사대 블롭 AABB 에 넣어 본다.
        /// 겹치면 ①이 반증되고 <b>②만 남는다</b> — 이것은 우리 점수가 아니라 <b>원본 숫자</b>가 내는 결론이다.
        /// </para>
        ///
        /// <para>
        /// ★★★ <b>[16회차 실측 · 반영됨] 답은 ② 였다.</b> 원본 블롭은 충돌체가 있고 · 센서가 아니고 ·
        /// 항상 켜져 있고 · 보이는 rect 그 자리에 실재하는데, <b>공 중심이 그 콜라이더 한가운데인
        /// 프레임에 b2Contact 가 «닿은 것도 안 닿은 broadphase 쌍도» 0 건</b>이다.
        /// 그리고 <b>같은 프레임에 블롭은 깔고 앉은 블록 3개와 접촉 중</b>이다.
        /// ⇒ 배제되는 것은 <b>«공 ↔ 블롭» 쌍 하나뿐</b>이라 우리도 그 쌍만 뺐다
        /// (<c>BlumgiBallBody.ExcludeFromCollision</c>). 이제 이 절은 <b>«그 배제가 실제로 서 있는지»</b>도 잰다.
        /// </para>
        /// </summary>
        private void LauncherBlobOverlap(List<GoldenVectors.Contact25Event> all)
        {
            Line("  ── ★★★ 원본 공이 «우리 발사대 블롭» 자리를 지나가는가  [원본 숫자만으로 판정] ──");

            BlumgiLevelRuntime runtime = EnsureLevel("W1L3");

            if (runtime == null || runtime.Level == null)
            {
                Line("     레벨을 못 세웠다 — 판정 불가");
                return;
            }

            // ★★ 발사대 자리는 «유도값»이다 [20회차 · 재발방지 #102] — 레벨 표에서 읽지 않는다.
            double bx = runtime.LauncherBodyPosition.X;
            double originY = runtime.LauncherBodyPosition.Y;
            double halfW = _config.BlobCollisionWidth * 0.5;
            double halfH = _config.BlobCollisionHeight * 0.5;

            // ★★ [16회차] body 원점이 «bbox 아래변 중앙»이다 — 상자는 원점 «위»에 선다.
            //    중심으로 대조하면 블롭을 못 알아본다 (재발방지 #53).
            double by = originY - halfH;
            double r = _config.BallRadius;

            Line($"     우리 W1L3 발사대 블롭 : body 원점({bx:0.###}, {originY:0.###}) = bbox «아래변 중앙» · " +
                 $"{_config.BlobCollisionWidth:0.####} × {_config.BlobCollisionHeight:0.##} " +
                 $"⇒ AABB x[{bx - halfW:0.###}, {bx + halfW:0.###}] y[{by - halfH:0.###}, {by + halfH:0.###}]");
            Line("        [실측·원본 16회차] W1L3 인스턴스 bbox [956.432, 294.759, 1043.568, 344.759]");
            Line($"     정답지 28건의 «직전» 공 위치(반지름 {r:0.0})와 겹치는가 —");

            int overlap = 0;

            for (int i = 0; i < all.Count; i++)
            {
                GoldenVectors.Contact25Event g = all[i];

                double cx = Math.Max(bx - halfW, Math.Min(g.PreX, bx + halfW));
                double cy = Math.Max(by - halfH, Math.Min(g.PreY, by + halfH));
                double dx = g.PreX - cx;
                double dy = g.PreY - cy;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));

                if (distance >= r)
                    continue;

                overlap++;
                Line($"       ★ {g.ShotId} #{g.Index}  원본 공 ({g.PreX:0.000}, {g.PreY:0.000}) → " +
                     $"블롭 AABB 안으로 {r - distance:0.0} px 파고든다  " +
                     $"[그 시각 원본이 실제로 친 것은 {BodiesOfGolden(g)}]");
            }

            Line(string.Empty);
            Line($"     ⇒ 겹치는 정답지 접촉 «{overlap}/{all.Count}건»");

            if (overlap > 0)
            {
                Line("     ⇒ ★★★ 원본 공은 «우리 발사대 블롭이 있는 자리»를 그대로 통과한다.");
                Line("        그런데 정답지 28건의 상대 body 에 블롭이 «0건»이다 (전부 블록이다).");
                Line("        ⇒ 「원본 공이 그 자리를 안 지나간다」는 반증됐다.");
                Line("           남는 설명은 «원본에서는 발사대가 공과 부딪히지 않는다» 하나다.");
                Line("        ★★★ [16회차] 그것이 원본에서 «직접» 확증됐다 — 공 중심이 블롭 콜라이더");
                Line("             한가운데인 프레임에 b2Contact 가 «닿은 것도 안 닿은 broadphase 쌍도» 0 건이고,");
                Line("             같은 프레임에 블롭은 «깔고 앉은 블록 3개»와 접촉 중이다.");
            }
            else
            {
                Line("     ⇒ 겹치는 접촉이 없다 — 이 가설은 이 표본에서 발동하지 않았다.");
            }

            Line(string.Empty);
            Line("     ★ 우리 쪽 «공 ↔ 블롭» 쌍 배제가 실제로 서 있는지는 골든 채점기가 잰다");
            Line("       (BlumgiGoldenPlayCheck — 배제 · 블롭↔블록 생존 · 공↔블록 생존 · 시뮬 재진입 4항목).");
        }

        /// <summary>
        /// ★★ 원시 접촉을 <b>«접촉 사건»으로 묶는다</b> — 원본 정의(빈 프레임으로 끊긴 연속 구간 = 1건)와 같게.
        /// <b>이것을 안 하면 동시 접촉 17/28 에서 번호가 통째로 밀린다.</b>
        /// </summary>
        private static List<OurEvent> GroupEvents(List<OurContact> contacts)
        {
            var events = new List<OurEvent>(contacts.Count);

            var sorted = new List<OurContact>(contacts);
            sorted.Sort((a, b) => a.BeginStep != b.BeginStep
                                      ? a.BeginStep.CompareTo(b.BeginStep)
                                      : a.EndStep.CompareTo(b.EndStep));

            OurEvent current = null;

            for (int i = 0; i < sorted.Count; i++)
            {
                OurContact c = sorted[i];

                // 빈 프레임이 «하나라도» 끼면 새 사건이다. 이어지거나 겹치면 같은 사건.
                if (current != null && c.BeginStep <= current.EndStep + 1)
                {
                    current.Members.Add(c);

                    if (c.EndStep > current.EndStep)
                        current.EndStep = c.EndStep;

                    continue;
                }

                current = new OurEvent
                {
                    BeginStep = c.BeginStep,
                    EndStep = c.EndStep,
                    TimeMs = c.TimeMs,
                    PreX = c.PreX,
                    PreY = c.PreY,
                    VbX = c.VbX,
                    VbY = c.VbY,
                    OmegaBefore = c.OmegaBefore,
                };

                current.Members.Add(c);
                events.Add(current);
            }

            // «직후» 표본은 가장 늦게 닫힌 접촉의 것이다.
            for (int i = 0; i < events.Count; i++)
            {
                OurEvent e = events[i];
                OurContact last = e.Members[0];

                for (int k = 1; k < e.Members.Count; k++)
                {
                    if (e.Members[k].EndStep >= last.EndStep)
                        last = e.Members[k];
                }

                e.VaX = last.VaX;
                e.VaY = last.VaY;
                e.OmegaAfter = last.OmegaAfter;
            }

            return events;
        }

        // ────────────────────────────────────────────────────────── 3-d. 접촉 6~12 «구조» 채점

        /// <summary>
        /// ★★★ <b>접촉 6~12 를 «구조»로 대조한다</b> [18회차 · <c>07 §16</c>].
        ///
        /// <para>
        /// ⚠ <b>여기서 나온 숫자로 물리 값을 «고르지» 않는다</b> (재발방지 #48).
        /// 이 절은 <b>「사슬이 어디서부터 다른 것을 치기 시작하나」</b>를 가리키는 진단이다.
        /// </para>
        /// </summary>
        private void CompareContacts612()
        {
            List<GoldenVectors.Contact612Structure> all = GoldenVectors.Contacts612();

            int seriesMatched = 0;
            int seriesGraded = 0;
            int chainOk = 0;
            int goalOk = 0;
            int tailMatched = 0;
            int tailGraded = 0;

            for (int s = 0; s < all.Count; s++)
            {
                GoldenVectors.Contact612Structure g = all[s];
                double holdMs = (g.Force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0;

                List<OurContact> ours = RunShotHold(g.Level, holdMs,
                                                    out EBlumgiShotState state, out double seconds);

                double rate = g.Trials <= 0 ? 0.0 : g.Scored * 100.0 / g.Trials;

                Line($"  ── {g.Level} force {g.Force:0.0000} (홀드 {holdMs:0.0}ms) · " +
                     $"원본 골인 {g.Scored}/{g.Trials} ({rate:0} %) · " +
                     $"접촉 {g.MinContacts}~{g.MaxContacts}회 · 골 +{g.MinGoalMs:0}~+{g.MaxGoalMs:0}ms ──");
                Line($"     원본 출처: {g.Source}");

                if (ours == null)
                {
                    Line("     발사되지 않았다 — 구조를 못 잰다");
                    Line(string.Empty);
                    continue;
                }

                List<OurEvent> events = GroupEvents(ours);
                bool scored = state == EBlumgiShotState.Scored;

                bool chainInRange = events.Count >= g.MinContacts && events.Count <= g.MaxContacts;

                if (chainInRange)
                    chainOk++;

                if (scored)
                    goalOk++;

                Line($"     ② 접촉 «총수»  우리 {events.Count}건 / 원본 {g.MinContacts}~{g.MaxContacts}  " +
                     $"⇒ {(chainInRange ? "범위 안" : "★ 범위 «밖»")}");
                Line($"     ③ 결과         우리 {(scored ? "골인" : "미골인")} ({seconds:0.00}s · {state}) / " +
                     $"원본 {g.Scored}/{g.Trials} 골인  ⇒ {(scored ? "같다" : "★ 다르다")}");

                if (g.ValueGradeMaxIndex > 0)
                {
                    Line($"     ⓪ 값 채점 경계 = «접촉 {g.ValueGradeMaxIndex} 까지» " +
                         $"[19회차 n={g.Trials} 실측 · 정본 07 §18-a] — 그 너머는 이 절(구조)이 맡는다");
                }

                // ★★★ [19회차] 번호별 계열 채점이 «꺼진» 샷 — 원본 자신이 그 번호에서 갈린다.
                //   끄고 끝내면 면죄부가 되므로, 번호를 버리고 «집합»으로 재는 것을 대신 붙인다.
                if (g.Series == null)
                {
                    Line($"     ① 계열 «번호별» 채점 — ⛔ 끔.  사유: {g.IndexGradingDisabledWhy}");
                    tailGraded += GradeTailUnion(g, events, ref tailMatched);
                    Line(string.Empty);
                    continue;
                }

                Line("     ① 계열 대조 —  #   원본 계열(합집합)        우리 계열");

                for (int k = 0; k < g.Series.Length; k++)
                {
                    int index = 6 + k;
                    string[] expect = g.Series[k];

                    if (expect == null || expect.Length == 0)
                    {
                        Line($"                    {index,2}   (채점 안 함 — 시행마다 존재가 갈린다)");
                        continue;
                    }

                    seriesGraded++;

                    if (index - 1 >= events.Count)
                    {
                        Line($"                    {index,2}   {Join(expect),-22}  우리에겐 «{index}번째 접촉이 없다»");
                        continue;
                    }

                    OurEvent o = events[index - 1];
                    List<string> mine = SeriesOf(o);
                    bool hit = false;

                    for (int a = 0; a < mine.Count && hit == false; a++)
                    {
                        for (int b = 0; b < expect.Length; b++)
                        {
                            if (mine[a] == expect[b])
                            {
                                hit = true;
                                break;
                            }
                        }
                    }

                    if (hit)
                        seriesMatched++;

                    Line($"                    {index,2}   {Join(expect),-22}  {Join(mine.ToArray()),-22}  " +
                         $"{(hit ? "일치" : "★ 다름")}");
                }

                Line(string.Empty);
            }

            Line($"  ⇒ ① 계열 {seriesMatched}/{seriesGraded} 일치 · ①' 꼬리 «집합» {tailMatched}/{tailGraded} 안 · " +
                 $"② 접촉 총수 범위 안 {chainOk}/{all.Count} · " +
                 $"③ 결과 일치 {goalOk}/{all.Count}   [진단 · 점수에 넣지 않는다]");
            Line("     ⚠ 이 절은 «구조» 채점이다 — 접촉 6 이후의 시각·위치·e_n 은 원본 자신이 갈리므로 재지 않는다.");
            Line("     ⚠⚠ [19회차] 「접촉 N 의 상대 body」로 채점하는 것도 «샷에 따라» 안 된다 —");
            Line("        W1L1 66.3860 은 원본 4시행의 접촉 6 상대가 행727 · 림 · 행827 · 행877 로 «전부 다르고»,");
            Line("        W1L3 54.0000 은 접촉 6 이 «행625(미골인) ↔ 행675(골인)» 로 갈린다.");
            Line("        그 자리에서 상대 body 로 채점하면 «원본 자신이» 떨어진다 ⇒ 번호를 버리고 집합(①')으로 잰다.");
            Line(string.Empty);
        }

        /// <summary>
        /// ★★ <b>꼬리 구간(접촉 6 이후)을 «번호 없이 집합으로» 잰다</b> [19회차].
        ///
        /// <para>
        /// 번호별 채점을 끈 샷에서 <b>그래도 남는 물음</b>은 이것이다 —
        /// <b>「우리가 치는 것들이 원본이 한 번이라도 친 것들인가」.</b>
        /// 순서는 원본도 갈리지만 <b>집합은 안 갈린다</b>. 집합 «밖»으로 나가면 그건 진짜 어긋남이다.
        /// </para>
        /// </summary>
        private int GradeTailUnion(GoldenVectors.Contact612Structure g, List<OurEvent> events, ref int matched)
        {
            if (g.TailUnion == null || g.TailUnion.Length == 0)
            {
                Line("     ①' 꼬리 «집합» 채점 — 합집합이 «없다». 이 샷은 꼬리를 아예 안 잰 것이다 [미측정]");
                return 0;
            }

            Line($"     ①' 꼬리 «집합» 대조 (접촉 6~끝) — 원본 합집합 {{{Join(g.TailUnion)}}}");

            int graded = 0;

            for (int i = 5; i < events.Count; i++)
            {
                List<string> mine = SeriesOf(events[i]);
                graded++;

                bool inside = false;

                for (int a = 0; a < mine.Count && inside == false; a++)
                {
                    for (int b = 0; b < g.TailUnion.Length; b++)
                    {
                        if (mine[a] == g.TailUnion[b])
                        {
                            inside = true;
                            break;
                        }
                    }
                }

                if (inside)
                    matched++;
                else
                    Line($"                    {i + 1,2}   {Join(mine.ToArray()),-22}  ★ 합집합 «밖» — 원본이 안 간 자리다");
            }

            if (graded == 0)
                Line("                    (우리 사슬이 접촉 5 에서 끝났다 — 꼬리가 «없다»)");

            return graded;
        }

        /// <summary>
        /// ★★ <b>골대 림 접촉의 법선을 «기하로» 다시 만들어 엔진 법선과 맞춰 본다</b> [18회차 처방].
        ///
        /// <para>
        /// 18회차가 원본에서 확인한 것은 <b>「<c>e_circles</c> 매니폴드는 <c>localNormal</c> 을 안 채운다.
        /// 법선은 두 원의 중심 차로 그때그때 만든다」</b>였다. 그래서 채점기 처방이
        /// <b><c>normalize(공 중심 − 림 중심)</c></b> 이 됐다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>처방을 «적어 두고 안 태워 보면» 그 처방이 죽어 있어도 모른다</b> (재발방지 #77).
        /// 우리 엔진은 림에서도 법선을 주므로 <b>둘을 맞대 볼 수 있다</b> — 이것이 그 시험이다.
        /// 각이 0 에 붙으면 처방이 옳고, 벌어지면 <b>처방 쪽이 틀린 것</b>이다.
        /// </para>
        /// </summary>
        private void RimNormalCrossCheck(List<GoldenShot> shots)
        {
            double offsetX = _config.GoalPostOffsetX;
            double offsetY = _config.GoalPostOffsetY;

            var runs = new List<string>(16);
            var holds = new List<double>(16);
            var levels = new List<string>(16);

            for (int i = 0; i < shots.Count; i++)
            {
                levels.Add(shots[i].Level);
                holds.Add(shots[i].HoldMs);
                runs.Add(shots[i].Shot);
            }

            List<GoldenVectors.Contact612Structure> structures = GoldenVectors.Contacts612();

            for (int i = 0; i < structures.Count; i++)
            {
                GoldenVectors.Contact612Structure g = structures[i];
                levels.Add(g.Level);
                holds.Add((g.Force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0);
                runs.Add($"{g.Level} f{g.Force:0.0000}");
            }

            int rimContacts = 0;
            double worst = 0.0;
            string worstWhere = null;

            // ⚠ «어느 표본으로» 중심 차를 만드느냐가 결과를 가른다 — 둘 다 재서 같이 찍는다.
            //   ① 접촉점 기준 : 원 ↔ 원은 접촉점이 «두 중심을 잇는 선» 위에 있다 ⇒ 정확해야 한다
            //   ② 직전 프레임 공 중심 기준 : 한 스텝(8.33 ms) 전 위치라 «표본 시점»만큼 어긋난다
            double worstPre = 0.0;

            // 프리팹의 림 자리가 «데이터의 ±(75, 0)» 과 맞는가 — 원본 눈금 → 유니티 환산이 한 곳만 지나는지.
            double worstOffsetError = 0.0;

            // ★ «최댓값 하나»로 처방을 판정하지 않는다 — 분포를 본다 (한 건의 정착 접촉이 전체를 뒤집는다).
            int within1 = 0;
            int within5 = 0;
            var rows = new List<string>(16);

            for (int i = 0; i < runs.Count; i++)
            {
                List<OurContact> ours = RunShotHold(levels[i], holds[i], out _, out _);

                if (ours == null)
                    continue;

                for (int k = 0; k < ours.Count; k++)
                {
                    OurContact c = ours[k];

                    if (c.IsRim == false)
                        continue;

                    // ★★ 림 중심은 «부딪힌 그 콜라이더»의 중심이다 — 골대 루트 위치가 아니다.
                    //   골대는 한 오브젝트에 원이 둘이라 루트 좌표로는 어느 원인지 안 갈린다.
                    //   ⚠ 첫 판은 「루트 ± 75 중 x 가 가까운 쪽」으로 골랐고 그래서 «최대 7.9° 어긋났다» —
                    //     처방이 틀린 것이 아니라 «어느 원인지»를 잘못 골랐던 것이다 (#96 의 축소판).
                    double rimX = c.OtherColliderX;
                    double rimY = c.OtherColliderY;

                    // 데이터 쪽 오프셋(±75, 0)과 «맞는지»를 같이 확인한다 — 어긋나면 프리팹이 데이터와 다르다.
                    double expectLeft = c.OtherWorldX - offsetX;
                    double expectRight = c.OtherWorldX + offsetX;
                    double expectY = c.OtherWorldY + offsetY;
                    double offsetError = Math.Min(Math.Abs(rimX - expectLeft), Math.Abs(rimX - expectRight));
                    offsetError = Math.Max(offsetError, Math.Abs(rimY - expectY));

                    if (offsetError > worstOffsetError)
                        worstOffsetError = offsetError;

                    // ① 접촉점 − 림 중심  (원 ↔ 원은 접촉점이 두 중심을 잇는 선 위에 있다)
                    double gx = c.Px - rimX;
                    double gy = c.Py - rimY;
                    double len = Math.Sqrt((gx * gx) + (gy * gy));

                    // ② 직전 프레임 공 중심 − 림 중심 (표본 시점이 한 스텝 앞선다)
                    double px = c.PreX - rimX;
                    double py = c.PreY - rimY;
                    double plen = Math.Sqrt((px * px) + (py * py));

                    if (len < 1e-9 || plen < 1e-9)
                        continue;

                    gx /= len;
                    gy /= len;
                    px /= plen;
                    py /= plen;

                    rimContacts++;

                    if (c.NormalValid == false)
                        continue;

                    double dot = (gx * c.Nx) + (gy * c.Ny);
                    double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))) * 180.0 / Math.PI;

                    double dotPre = (px * c.Nx) + (py * c.Ny);
                    double anglePre = Math.Acos(Math.Max(-1.0, Math.Min(1.0, dotPre))) * 180.0 / Math.PI;

                    if (anglePre > worstPre)
                        worstPre = anglePre;

                    if (angle <= 1.0)
                        within1++;

                    if (angle <= 5.0)
                        within5++;

                    rows.Add($"       {runs[i],-18} #{c.Index,-3} {c.Steps,3}스텝  " +
                             $"접촉점기준 {angle,6:0.00}°  직전중심기준 {anglePre,6:0.00}°");

                    if (angle > worst)
                    {
                        worst = angle;
                        worstWhere = $"{runs[i]} 접촉 #{c.Index} — 엔진 n[{c.Nx:0.000},{c.Ny:0.000}] ↔ " +
                                     $"기하 n[{gx:0.000},{gy:0.000}] · 림 중심 ({rimX:0.##},{rimY:0.##})";
                    }
                }
            }

            if (rimContacts == 0)
            {
                Line("  ⚠ 림 접촉이 «0 건»이다 — 이 샷 목록에서는 처방을 태울 수 없었다.");
                Line("     ⇒ 「검사했다」가 아니라 «미측정»이다 (#77 — 안 태운 검사는 통과가 아니다).");
                Line(string.Empty);
                return;
            }

            Line($"  림 접촉 {rimContacts}건을 태웠다.");
            Line($"     ★ 림 콜라이더 자리 ↔ 데이터 오프셋 ±({offsetX:0.##}, {offsetY:0.##}) — " +
                 $"«최대» 어긋남 {worstOffsetError:0.0000} px  " +
                 $"⇒ {(worstOffsetError <= 0.01 ? "일치 (환산이 한 곳만 지난다)" : "★ 어긋난다 — 프리팹이 데이터와 다르다")}");
            Line($"     ① «접촉점 − 림 중심»으로 만든 법선   — 엔진 법선과 «최대» 어긋남 {worst:0.000}°");
            Line($"     ② «직전 프레임 공 중심 − 림 중심»으로 — 엔진 법선과 «최대» 어긋남 {worstPre:0.000}°");

            if (worstWhere != null)
                Line($"     ① 최악 자리: {worstWhere}");

            Line($"     ★ 분포 — 1° 이내 {within1}/{rimContacts} · 5° 이내 {within5}/{rimContacts} · 최대 {worst:0.00}°");

            for (int r = 0; r < rows.Count; r++)
                Line(rows[r]);

            Line(string.Empty);
            Line(within5 == rimContacts
                     ? "  ⇒ ★ 처방이 «성립한다» — 상대가 원이면 중심 차로 법선을 만들어 쓸 수 있다."
                     : "  ⇒ ⚠ 처방이 «구간에 따라» 성립한다 — 단발 접촉에서는 붙고 «정착(깊은 겹침)» 구간에서 벌어진다.");
            Line("     ⚠ 벌어지는 자리는 접촉 구간이 길고 겹침이 깊은 «정착» 접촉이다 — 위치 솔버가 접촉점을 밀어");
            Line("        중심선에서 떼어 놓는다. ⇒ 처방은 «단발 반발 접촉»에 한해 쓰고, 정착 구간은 구조 채점으로 간다.");
            Line("     ⚠⚠ 「최댓값 하나」로 처방을 죽이지 않는다 — 그것이 #96 이 경고한 «한 성질로 이름 붙이기»다.");

            Line("  ★★ 그리고 ①과 ②의 차이가 이 절의 두 번째 소득이다 — «어느 표본»을 먹이느냐가 결과를 가른다.");
            Line("     원본 채록은 접촉 «직전» 프레임의 공 중심을 들고 있다. 그걸 그대로 중심 차에 넣으면");
            Line("     한 스텝(8.33 ms)만큼 공이 덜 와 있어 각이 어긋난다 — 처방이 틀린 것이 «아니라» 표본이 틀린 것이다.");
            Line("     ⇒ 원본 쪽 림 접촉의 법선을 복원할 때는 «접촉 프레임»의 공 중심을 써야 한다.");
            Line(string.Empty);
        }

        /// <summary>
        /// 접촉 사건 하나의 «계열» 집합 — <b>골대 림</b>이면 <c>림</c>, 블록이면 <b>그 행 y</b>.
        /// <b>x 는 버린다</b> — 블록 상자(84 px)가 격자(50)보다 커서 이웃 칸끼리 서로 대체되기 때문이다.
        /// </summary>
        private static List<string> SeriesOf(OurEvent e)
        {
            var list = new List<string>(4);

            for (int i = 0; i < e.Members.Count; i++)
            {
                OurContact m = e.Members[i];
                string key = m.IsRim
                    ? GoldenVectors.RimSeries
                    : GoldenVectors.BlockRow(m.OtherWorldY);

                if (list.Contains(key) == false)
                    list.Add(key);
            }

            return list;
        }

        private static string Join(string[] values)
        {
            return string.Join("+", values);
        }

        /// <summary>우리 쪽 접촉 사건의 «상대 body + 법선»을 한 줄로. <b>「무엇을 쳤나」가 여기서 보인다.</b></summary>
        private static string BodiesOf(OurEvent e)
        {
            var sb = new StringBuilder(96);

            for (int i = 0; i < e.Members.Count; i++)
            {
                OurContact m = e.Members[i];

                if (i > 0)
                    sb.Append("  +  ");

                sb.Append($"{Short(m.OtherName)}({m.OtherWorldX:0},{m.OtherWorldY:0}) ");
                sb.Append(m.NormalValid ? $"n[{m.Nx:0.000},{m.Ny:0.000}]" : "n«무효»");
            }

            return sb.ToString();
        }

        /// <summary>정답지 쪽 «상대 body + 법선»을 같은 모양으로.</summary>
        private static string BodiesOfGolden(GoldenVectors.Contact25Event g)
        {
            var sb = new StringBuilder(96);

            for (int i = 0; i < g.Nx.Length; i++)
            {
                if (i > 0)
                    sb.Append("  +  ");

                double bx = i < g.OtherX.Length ? g.OtherX[i] : double.NaN;
                double by = i < g.OtherY.Length ? g.OtherY[i] : double.NaN;

                sb.Append($"블록({bx:0},{by:0}) n[{g.Nx[i]:0.000},{g.Ny[i]:0.000}]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 정답지 법선 «각각»에 대해 우리 법선 중 가장 가까운 것을 찾고, 그 중 <b>가장 나쁜</b> 각을 돌려준다.
        /// <b>순서로 짝짓지 않는다</b> — 동시 접촉의 나열 순서는 양쪽 다 임의다.
        /// </summary>
        private static double WorstNormalGap(GoldenVectors.Contact25Event g, OurEvent o, out int invalid)
        {
            invalid = 0;
            double worst = 0.0;
            bool any = false;

            for (int i = 0; i < g.Nx.Length; i++)
            {
                double best = double.NaN;

                for (int k = 0; k < o.Members.Count; k++)
                {
                    OurContact m = o.Members[k];

                    if (m.NormalValid == false)
                        continue;

                    double dot = (g.Nx[i] * m.Nx) + (g.Ny[i] * m.Ny);
                    double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))) * 180.0 / Math.PI;

                    if (double.IsNaN(best) || angle < best)
                        best = angle;
                }

                if (double.IsNaN(best))
                    continue;

                any = true;

                if (best > worst)
                    worst = best;
            }

            for (int k = 0; k < o.Members.Count; k++)
            {
                if (o.Members[k].NormalValid == false)
                    invalid++;
            }

            return any ? worst : double.NaN;
        }

        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "?";

            return name.Replace("(Clone)", string.Empty).Replace("Blumgi", string.Empty);
        }

        // ────────────────────────────────────────────────────────── 4·5. 지문

        /// <summary>
        /// ★★★ <b>첫 접촉 직전의 각속도만</b> 따로 세운다.
        /// 위치·법선·v_n 이 다 맞는데 사슬이 갈리면 <b>남는 축은 ω 하나</b>다.
        /// </summary>
        private void FirstContactOmega(List<GoldenShot> shots)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (shots[i].Contacts.Count == 0)
                {
                    Line($"     {shots[i].Shot,-12} 원본에 접촉이 없다 (원본도 아무것도 안 맞는 샷)");
                    continue;
                }

                List<OurContact> ours = RunShotHold(shots[i].Level, shots[i].HoldMs, out _, out _);

                if (ours == null || ours.Count == 0)
                {
                    Line($"     {shots[i].Shot,-12} 우리 쪽 접촉 없음");
                    continue;
                }

                GoldenContact g = shots[i].Contacts[0];
                OurContact o = ours[0];

                Line($"     {shots[i].Shot,-12} {g.Event.OmegaBefore,9:0.000}  {o.OmegaBefore,10:0.000}  " +
                     $"{o.OmegaBefore - g.Event.OmegaBefore,8:0.000}   {g.TimeMs,10:0}      {o.TimeMs,7:0}");
            }
        }

        private void FingerprintAll(List<GoldenShot> shots)
        {
            int worstSteps = 0;
            int totalRecontact = 0;

            for (int i = 0; i < shots.Count; i++)
            {
                List<OurContact> ours = RunShotHold(shots[i].Level, shots[i].HoldMs, out EBlumgiShotState state, out double seconds);

                if (ours == null)
                {
                    Line($"  {shots[i].Shot,-12} 발사 안 됨");
                    continue;
                }

                Fingerprint(ours, out int recontact, out int nearRecontact, out int maxSteps, out int rimRecontact);
                totalRecontact += nearRecontact;

                if (maxSteps > worstSteps)
                    worstSteps = maxSteps;

                Line($"  {shots[i].Shot,-12} 접촉 {ours.Count,3}건 · 같은 «블록» 재접촉 {recontact} (0.25초 내 {nearRecontact}) · " +
                     $"같은 림 재접촉 {rimRecontact} · 접촉 구간 최대 {maxSteps}스텝 · {seconds:0.00}s · {state}" +
                     $"   [원본 {shots[i].OriginBlockBounces + shots[i].OriginRimBounces}회]");
            }

            Line(string.Empty);
            Line($"  → 정답지 8샷 합계 — 0.25초 내 같은 블록 재접촉 {totalRecontact}건 · 접촉 구간 최대 {worstSteps}스텝");
            Line("     (원본: 재접촉 0건 · 접촉 구간 1~2프레임 [9회차 §5-c] · ①-f 때 우리 최대 3스텝)");
        }

        private void FingerprintDiverged()
        {
            List<DivergedShot> list = DivergedShots();

            int worstSteps = 0;
            int totalNear = 0;

            for (int i = 0; i < list.Count; i++)
            {
                DivergedShot d = list[i];
                double holdMs = (d.Force - _config.ForceShootStart) / _config.ForceShootRatePerSecond * 1000.0;

                List<OurContact> ours = RunShotHold(d.Level, holdMs, out EBlumgiShotState state, out double seconds);

                if (ours == null)
                {
                    Line($"  {d.Level} force {d.Force,8:0.0000}  발사 안 됨 (원본 {(d.OriginScored ? "O" : "X")})");
                    continue;
                }

                Fingerprint(ours, out int recontact, out int nearRecontact, out int maxSteps, out int rimRecontact);
                totalNear += nearRecontact;

                if (maxSteps > worstSteps)
                    worstSteps = maxSteps;

                // 「몇 번째부터 같은 블록을 다시 치기 시작하나」 — 사슬이 무너지는 자리다.
                int firstRecontactIndex = FirstRecontactIndex(ours, BlumgiPhysicsWorld.BlockPrefabName);

                Line($"  {d.Level} force {d.Force,8:0.0000} 원본 {(d.OriginScored ? "O" : "X")} → 우리 " +
                     $"{(state == EBlumgiShotState.Scored ? "O" : "X")}  |  접촉 {ours.Count,3}건 · " +
                     $"블록 재접촉 {recontact} (0.25초 내 {nearRecontact}) · 림 재접촉 {rimRecontact} · " +
                     $"최대 {maxSteps}스텝 · 첫 재접촉 #{(firstRecontactIndex < 0 ? 0 : firstRecontactIndex)} · {seconds:0.00}s · {state}");
            }

            Line(string.Empty);
            Line($"  → 갈린 {list.Count}샷 합계 — 0.25초 내 같은 블록 재접촉 {totalNear}건 · 접촉 구간 최대 {worstSteps}스텝");
            Line(string.Empty);
            Line("  ⚠⚠ [19회차 정정] «긴 접촉 구간(정착)»을 «우리에게만 있는 이상 거동»으로 읽지 마라.");
            Line("     원본도 정착한다 — W1L1 미골인 샷(force 66.3970)이 한 블록 위에서 698 프레임(5810 ms)을");
            Line("     머물다 «잠든다»(IsAwake 1→0 · t+7823.1 부터 409프레임). 그 사이 굴러간 거리는");
            Line("     Δpos 21.8 px(x 21.789 · y 0.034)뿐이고 ω 는 0.284 → 0 으로 «죽는다» — 굴러 떨어지지 않는다.");
            Line("     ⇒ 우리 「1721 스텝 정착」·「행625 사슬」은 원본의 «미골인 분기» 그 자체다.");
            Line("       정착은 미골인 분기 «전용»이고(원본 골인 3샷의 최장 연속접촉은 전부 2 프레임),");
            Line("       갈림의 자리는 «정착 기전»이 아니라 그 앞의 «모서리 스침 한 번»이다 (`07 §18-b`).");
        }

        private static List<DivergedShot> DivergedShots()
        {
            // ★ 창 대조(`Temp/BlumgiWindowReport.txt` §7)가 «기계로» 뽑은 갈린 점이다.
            //   ⚠ [①-k] 앞 넷은 «이번 회차»에 새로 갈린 것으로 확인된 자리다 —
            //      W1L1 66.3860 · W1L3 38.4185 · 54.0000 · 65.9845.
            //      (W1L3 18.25·21.46·26.96·32.45 는 14회차-b 가 「비결정」으로 뺐다가 15회차 재측정으로 «되돌린» 자리라
            //       목록에 이미 있었다. 59.5275 는 이제 «미측정»이라 창 채점에서는 빠지지만 지문은 그대로 찍는다.)
            return new List<DivergedShot>
            {
                new DivergedShot { Level = "W1L1", Force = 66.3860, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 38.4185, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 54.0000, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 65.9845, OriginScored = true },
                new DivergedShot { Level = "W1L1", Force = 27.4130, OriginScored = false },
                new DivergedShot { Level = "W1L1", Force = 27.4185, OriginScored = false },
                new DivergedShot { Level = "W1L1", Force = 27.4240, OriginScored = false },
                new DivergedShot { Level = "W1L2", Force = 65.4565, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 17.3425, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 17.7880, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 18.2500, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 19.6250, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 20.5490, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 21.4565, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 26.9620, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 32.4455, OriginScored = true },
                new DivergedShot { Level = "W1L3", Force = 59.5275, OriginScored = true },
                new DivergedShot { Level = "W1L4", Force = 18.2390, OriginScored = false },
                new DivergedShot { Level = "W1L4", Force = 24.1900, OriginScored = true },
                new DivergedShot { Level = "W1L4", Force = 36.5870, OriginScored = false },
                new DivergedShot { Level = "W1L4", Force = 37.5000, OriginScored = false },
                new DivergedShot { Level = "W1L4", Force = 38.4185, OriginScored = false },
                new DivergedShot { Level = "W1L4", Force = 41.1630, OriginScored = false },
                new DivergedShot { Level = "W1L4", Force = 51.2940, OriginScored = true },
            };
        }

        private void Fingerprint(List<OurContact> contacts, out int recontact, out int nearRecontact,
                                 out int maxSteps, out int rimRecontact)
        {
            recontact = 0;
            nearRecontact = 0;
            rimRecontact = 0;
            maxSteps = 0;

            int near = (int)Math.Round(0.25 / _step);   // 0.25 초를 «스텝 수»로 — 스텝이 바뀌어도 같은 «시간»을 본다
            var lastBlock = new Dictionary<int, int>(64);
            var lastRim = new Dictionary<int, int>(8);

            for (int i = 0; i < contacts.Count; i++)
            {
                OurContact c = contacts[i];

                if (c.Steps > maxSteps)
                    maxSteps = c.Steps;

                if (c.IsRim)
                {
                    if (lastRim.TryGetValue(c.OtherId, out int previousRim) && c.BeginStep - previousRim <= near)
                        rimRecontact++;

                    lastRim[c.OtherId] = c.BeginStep;
                    continue;
                }

                if (c.OtherName == null || c.OtherName.Contains(BlumgiPhysicsWorld.BlockPrefabName) == false)
                    continue;

                if (lastBlock.TryGetValue(c.OtherId, out int previous))
                {
                    recontact++;

                    if (c.BeginStep - previous <= near)
                        nearRecontact++;
                }

                lastBlock[c.OtherId] = c.BeginStep;
            }
        }

        private static int FirstRecontactIndex(List<OurContact> contacts, string prefabName)
        {
            var seen = new HashSet<int>();

            for (int i = 0; i < contacts.Count; i++)
            {
                OurContact c = contacts[i];

                if (c.OtherName == null || c.OtherName.Contains(prefabName) == false)
                    continue;

                if (seen.Add(c.OtherId) == false)
                    return i + 1;
            }

            return -1;
        }

        // ────────────────────────────────────────────────────────── 6. 스텝 민감도

        /// <summary>
        /// ★ 같은 샷을 <b>여러 스텝</b>으로 굴려 사슬이 얼마나 흔들리는지 본다.
        /// <b>값을 고르는 절이 아니다</b> — 원본 물리 스텝은 미측정이다.
        /// </summary>
        private void StepSensitivity(List<GoldenShot> shots)
        {
            double original = _step;
            double[] candidates = { 1.0 / 60.0, 1.0 / 120.0, 1.0 / 240.0, 0.02 };

            Line("     샷            원본반발 |  " + string.Join(" | ", Array.ConvertAll(candidates, c => $"{1.0 / c,5:0} Hz")));

            for (int s = 0; s < shots.Count; s++)
            {
                var cells = new List<string>(candidates.Length);

                for (int c = 0; c < candidates.Length; c++)
                {
                    _step = candidates[c];
                    Time.fixedDeltaTime = (float)_step;

                    List<OurContact> ours = RunShotHold(shots[s].Level, shots[s].HoldMs, out EBlumgiShotState state, out double seconds);

                    if (ours == null)
                    {
                        cells.Add("발사X");
                        continue;
                    }

                    Fingerprint(ours, out _, out int nearRecontact, out int maxSteps, out _);
                    cells.Add($"{ours.Count,3}건 재{nearRecontact} {maxSteps}스텝 {(state == EBlumgiShotState.Scored ? "골" : "X")}");
                }

                Line($"     {shots[s].Shot,-12} {shots[s].OriginBlockBounces + shots[s].OriginRimBounces,6}   |  " +
                     string.Join(" | ", cells));
            }

            _step = original;
            Time.fixedDeltaTime = (float)original;

            Line(string.Empty);
            Line($"     ⇒ 스텝을 되돌렸다 — {Fmt(_step)} ({1.0 / _step:0.#} Hz)");
        }

        // ────────────────────────────────────────────────────────── 샷 구동

        /// <summary>
        /// 홀드 하나를 쏘고 <b>접촉 사슬을 전수</b> 돌려준다.
        /// ⚠ 레벨을 <b>매 발 다시 세운다</b> — 블롭이 동적이라 앞 샷의 여파가 남는다.
        /// </summary>
        private List<OurContact> RunShotHold(string levelCode, double holdMs,
                                             out EBlumgiShotState state, out double flightSeconds)
        {
            state = EBlumgiShotState.Ready;
            flightSeconds = 0.0;

            // ★★★ 오브젝트만 헐면 답이 «앞선 샷 수»에 물린다 — 월드째로 새로 만든다 (RecreateScene 주석).
            RecreateScene();
            _builtLevel = null;

            BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(levelCode);

            if (runtime == null)
                return null;

            _world.Build(runtime, LoadPrefab);

            if (_world.Ball == null || _world.Ball.Body == null)
                return null;

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
            state = sim.State;

            if (sim.State != EBlumgiShotState.Flying)
                return null;

            var probe = _world.Ball.gameObject.AddComponent<BlumgiChainProbe>();
            probe.Restart();

            Rigidbody2D body = _world.Ball.Body;
            int limit = (int)(ShotTimeLimitSeconds / _step);

            for (int i = 0; i < limit; i++)
            {
                probe.Step = i;

                // ★★★★ 접촉 «시각» 규약 — 원본은 «그 스텝이 끝난 뒤 프레임»에서 접촉을 읽는다.
                //
                //   [실측 · 21회차 `b2World.Step` 프로토타입 후킹] C3 한 틱의 순서는
                //     ① `Step(dt)`  ② 이벤트 시트 액션  ③ rAF 콜백
                //   이고, `raf(N)` → `raf(N+1)` 사이의 `Step` 개수는 «정확히 1» (17/17 · 예외 0)이다.
                //   ⇒ 스텝 k 번째 적분 «중»에 난 접촉은 그 다음 rAF 에서 관측되고, 그 프레임의 시각은
                //     정확히 `k · dt` 다. 원본 정답지의 t 는 전부 그 규약으로 적혔다.
                //
                //   여기서 `sim.FlightSeconds` 는 «이 스텝을 돌기 전» 값이라 `(k−1)·dt` 다 —
                //   그대로 적으면 «정확히 한 스텝(8.33 ms)» 이른 시각이 된다.
                //
                //   ⚠ 이것이 「우리 접촉 시각이 전 구간에서 8.2~9.4 ms 빠르다」(`07 §18-j-6 ㉡`)의 정체다.
                //     물리 위상이 어긋난 것이 «아니다» — 발사 직후 우리 공은 «적분 0회 · 발사점 · v0» 로
                //     원본 프레임 N 과 같은 자리에 있다(패스 ①-q 실측, 잔차 2e−5 px).
                //
                //   ★ 교차검산 [실측] — 우리 접촉 1 의 «적분 횟수»가 원본의 «프레임 수»와 같다:
                //       W1L3 21.4565 → 적분 35회 = 원본 `fi` 35 (원본 t 291.6~291.8 ms · 우리 291.7 ms)
                //       W1L1 66.3860 → 적분 163회 (원본 t 1357.9~1359.4 ms · 우리 1358.3 ms)
                //     ⇒ 점수를 보고 고른 값이 아니라 «프레임 수가 같다»는 항등식이다 (재발방지 #48).
                probe.TimeMs = (sim.FlightSeconds + _step) * 1000.0;

                probe.BeforeVelocity = body.linearVelocity;
                probe.BeforeAngular = body.angularVelocity;
                probe.BeforePosition = body.position;

                _physics.Simulate((float)_step);
                sim.Tick(_step);

                probe.CloseSettled(body.linearVelocity, body.angularVelocity);

                if (sim.State == EBlumgiShotState.Scored || sim.IsBallActive == false)
                    break;
            }

            probe.CloseAll(body.linearVelocity, body.angularVelocity);

            state = sim.State;
            flightSeconds = sim.FlightSeconds;

            List<OurContact> result = ToOurContacts(probe);
            DestroyImmediate(probe);
            return result;
        }

        /// <summary>
        /// 기록기가 <b>유니티 좌표</b>로 모은 것을 <b>원본 좌표계(y 아래 +)</b> 로 옮긴다.
        ///
        /// <para>
        /// ⚠ <b>여기가 규약의 전부다</b> — 위치·속도는 <c>BlumgiUnits.ToWorld</c> 가 y 를 되뒤집고,
        /// <b>각속도는 손잡이가 뒤집히므로 부호가 반대</b>가 되며(유니티 deg/s CCW → 원본 rad/s),
        /// 법선은 <c>(n.x, −n.y)</c> 다. 접선축은 정답지 규약대로 <c>t = (−n.y, n.x)</c> 를 쓴다.
        /// </para>
        /// </summary>
        private static List<OurContact> ToOurContacts(BlumgiChainProbe probe)
        {
            var list = new List<OurContact>(probe.Entries.Count);

            for (int i = 0; i < probe.Entries.Count; i++)
            {
                BlumgiChainProbe.Entry e = probe.Entries[i];

                BlumgiUnits.ToWorld(e.Point, out double px, out double py);
                BlumgiUnits.ToWorld(e.BeforePosition, out double prex, out double prey);
                BlumgiUnits.ToWorld(e.OtherPosition, out double ox, out double oy);
                BlumgiUnits.ToWorld(e.OtherColliderCenter, out double ocx, out double ocy);

                double nx = e.Normal.x;
                double ny = -e.Normal.y;
                double length = Math.Sqrt((nx * nx) + (ny * ny));

                // ★★ «법선 무효» — 원본에서 상대가 «원(골대 림)»인 접촉은 매니폴드가 면 법선을 안 준다
                //   [18회차 정정 · #96 — 「저속·깊은겹침」이 아니다]. 길이가 0 이면 정규화가 «조용히» 0 을 남기고,
                //   그 뒤 v_n·v_t·법선각이 전부 0 이 되어 「완전히 맞았다」로 보인다.
                //   ⇒ 유효 여부를 «들고 다닌다». 대조 쪽이 이 플래그를 먼저 본다.
                bool normalValid = length > 1e-9;

                if (normalValid)
                {
                    nx /= length;
                    ny /= length;
                }

                // 접선축 — 정답지와 «같은» 규약이어야 v_c = v_t − ωR 이 닫힌다.
                double tx = -ny;
                double ty = nx;

                BlumgiUnits.ToWorld(e.BeforeVelocity, out double vbx, out double vby);
                BlumgiUnits.ToWorld(e.AfterVelocity, out double vax, out double vay);

                list.Add(new OurContact
                {
                    Index = i + 1,
                    BeginStep = e.BeginStep,
                    EndStep = e.EndStep,
                    Closed = e.Closed,
                    OtherName = e.OtherName,
                    OtherId = e.OtherId,
                    PointCount = e.PointCount,
                    Simultaneous = e.Simultaneous,
                    TimeMs = e.TimeMs,
                    Px = px,
                    Py = py,
                    PreX = prex,
                    PreY = prey,
                    OtherWorldX = ox,
                    OtherWorldY = oy,
                    OtherColliderX = ocx,
                    OtherColliderY = ocy,
                    VbX = vbx,
                    VbY = vby,
                    VaX = vax,
                    VaY = vay,
                    Nx = nx,
                    Ny = ny,
                    NormalValid = normalValid,
                    VnBefore = (vbx * nx) + (vby * ny),
                    VnAfter = (vax * nx) + (vay * ny),
                    VtBefore = (vbx * tx) + (vby * ty),
                    VtAfter = (vax * tx) + (vay * ty),

                    // 유니티 각속도는 deg/s 이고 y 를 뒤집으면 «회전 방향»도 뒤집힌다.
                    OmegaBefore = -e.BeforeAngular * Math.PI / 180.0,
                    OmegaAfter = -e.AfterAngular * Math.PI / 180.0,
                });
            }

            return list;
        }

        private string _builtLevel;

        private BlumgiLevelRuntime EnsureLevel(string levelCode)
        {
            if (_builtLevel == levelCode && _world.Level != null)
                return _world.Level;

            // ★ 레벨이 바뀌면 «월드째» 새로 만든다 — 헐고 다시 세우면 바디 순서가 갈린다 (RecreateScene 주석).
            RecreateScene();

            BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(levelCode);

            if (runtime == null)
                return null;

            _world.Build(runtime, LoadPrefab);
            _builtLevel = levelCode;
            return _world.Level;
        }

        // ────────────────────────────────────────────────────────── 출력

        private void Section(string title)
        {
            Line(string.Empty);
            Line($"== {title} ==");
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
                Log.Warning($"사슬 추적 전문을 못 썼다: {e.Message}");
            }
        }

        private void Finish(bool ok)
        {
#if UNITY_EDITOR
            if (SessionState.GetBool(BlumgiChainPlayCheck.BridgeDrivingKey, false))
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }
    }

    /// <summary>
    /// ★ <b>사슬 기록기</b> — 접촉을 «순서대로 · 상세하게» 남긴다.
    ///
    /// <para>
    /// 기존 <c>BlumgiContactProbe</c> 는 «횟수와 상대»만 센다. 사슬 대조에는
    /// <b>법선 · 접촉점 · 전후 속도 · 전후 각속도 · 지속 스텝</b>이 다 필요해 별도로 둔다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>게임 코드에 손대지 않는다</b> — 채점기가 <c>AddComponent</c> 로 붙였다 뗀다.
    /// </para>
    /// </summary>
    public sealed class BlumgiChainProbe : MonoBehaviour
    {
        public sealed class Entry
        {
            public int BeginStep;
            public int EndStep;
            public bool Closed;
            public int OtherId;
            public string OtherName;
            public int PointCount;
            public int Simultaneous;
            public double TimeMs;
            public Vector2 Point;
            public Vector2 Normal;
            public Vector2 BeforeVelocity;
            public float BeforeAngular;
            public Vector2 AfterVelocity;
            public float AfterAngular;

            /// <summary>★ 접촉 «직전» 프레임의 «공 body» 위치 — 15회차 정답지의 「직전 (x, y)」 컬럼이다.</summary>
            public Vector2 BeforePosition;

            /// <summary>★ 상대 body 의 중심 — 정답지의 「상대 body」 컬럼과 맞춰 본다.</summary>
            public Vector2 OtherPosition;

            /// <summary>★ 부딪힌 «그 콜라이더»의 경계 중심 — 림처럼 한 오브젝트에 원이 둘일 때 이것으로 가른다.</summary>
            public Vector2 OtherColliderCenter;
        }

        public readonly List<Entry> Entries = new List<Entry>(128);

        private readonly Dictionary<int, Entry> _open = new Dictionary<int, Entry>(16);
        private readonly List<Entry> _justClosed = new List<Entry>(8);

        /// <summary>바깥(구동기)이 스텝마다 세워 준다.</summary>
        public int Step;

        public double TimeMs;

        public Vector2 BeforeVelocity;

        public float BeforeAngular;

        /// <summary>바깥(구동기)이 스텝마다 세워 주는 «직전» 공 위치.</summary>
        public Vector2 BeforePosition;

        public void Restart()
        {
            Entries.Clear();
            _open.Clear();
            _justClosed.Clear();
            Step = 0;
            TimeMs = 0.0;
        }

        /// <summary>이 스텝에 «닫힌» 접촉들에 «직후» 표본을 채운다. 구동기가 Simulate 직후에 부른다.</summary>
        public void CloseSettled(Vector2 velocity, float angular)
        {
            for (int i = 0; i < _justClosed.Count; i++)
            {
                _justClosed[i].AfterVelocity = velocity;
                _justClosed[i].AfterAngular = angular;
            }

            _justClosed.Clear();
        }

        /// <summary>샷이 끝났는데도 «닿은 채»인 접촉을 닫는다 (골인·화면 밖).</summary>
        public void CloseAll(Vector2 velocity, float angular)
        {
            foreach (KeyValuePair<int, Entry> pair in _open)
            {
                pair.Value.EndStep = Step;
                pair.Value.Closed = false;
                pair.Value.AfterVelocity = velocity;
                pair.Value.AfterAngular = angular;
            }

            _open.Clear();
            CloseSettled(velocity, angular);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            int id = collision.collider == null ? 0 : collision.collider.GetInstanceID();

            var entry = new Entry
            {
                BeginStep = Step,
                EndStep = Step,
                Closed = false,
                OtherId = id,
                OtherName = collision.collider == null ? null : collision.collider.gameObject.name,
                PointCount = collision.contactCount,
                TimeMs = TimeMs,
                BeforeVelocity = BeforeVelocity,
                BeforeAngular = BeforeAngular,
                AfterVelocity = BeforeVelocity,
                AfterAngular = BeforeAngular,
                BeforePosition = BeforePosition,
                OtherPosition = collision.collider == null
                    ? Vector2.zero
                    : (Vector2)collision.collider.transform.position,

                // ★ 골대 림은 «한 게임오브젝트에 원 콜라이더 두 개»다 — transform.position 은 둘 다 같은
                //   골대 중심이라 «어느 원과 부딪혔는지»를 못 가린다. 콜라이더 자기 경계의 중심을 같이 든다.
                OtherColliderCenter = collision.collider == null
                    ? Vector2.zero
                    : (Vector2)collision.collider.bounds.center,
            };

            if (collision.contactCount > 0)
            {
                ContactPoint2D point = collision.GetContact(0);
                entry.Normal = point.normal;
                entry.Point = point.point;
            }

            // 같은 스텝에 열린 접촉이 몇 개인가 — 9회차의 «동시접촉» 컬럼과 같은 뜻이다.
            int simultaneous = 1;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].BeginStep != Step)
                    break;

                simultaneous++;
                Entries[i].Simultaneous = simultaneous;
            }

            entry.Simultaneous = simultaneous;

            Entries.Add(entry);
            _open[id] = entry;
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            int id = collision.collider == null ? 0 : collision.collider.GetInstanceID();

            if (_open.TryGetValue(id, out Entry entry) == false)
                return;

            entry.EndStep = Step;
            entry.Closed = true;
            _open.Remove(id);
            _justClosed.Add(entry);
        }
    }
}
