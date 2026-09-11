using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JinHyung.UndeadSlayer;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Undead Slayer <b>재생 검사 하네스</b> — 회차마다 다시 쓰던 «준비·전환·이동·보고»를 한 곳에 둔다.
    ///
    /// <para>
    /// ★★ <b>이것은 «재는 도구»다 — 이관이 끝나면 <c>Tools/Verify/</c> 와 함께 지운다.</b>
    /// 다만 <b>회차마다 지우지 않는다</b>. 규칙의 원문은 「<b>이관이 끝나면</b> 지운다」이고,
    /// 회차마다 지우면 <b>같은 120줄(기다리기·화면 전환·걸어가기·시뮬 밀기·보고서)을 매 회차 다시 쓴다</b> —
    /// 실제로 한 세션에 세 번 다시 썼다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>검사기 하나는 이제 세 줄이면 선다.</b>
    /// <code>
    /// public static class MyCheck
    /// {
    ///     public static void RunPlay() { UndeadProbe.Launch(typeof(MyProbe)); }
    /// }
    ///
    /// public sealed class MyProbe : UndeadProbeBase
    /// {
    ///     protected override string ReportName { get { return "MyCheck.txt"; } }
    ///     protected override IEnumerator Run(UndeadGameRoot root) { ... }
    /// }
    /// </code>
    /// ⚠ 파일 이름은 <b>진입 클래스 이름과 같아야</b> 한다 — 러너가 그 이름으로 스테이징한다.
    /// </para>
    /// </summary>
    public static class UndeadProbe
    {
        /// <summary>재생에 들어간 뒤 «어느 검사기를 세울지»를 넘기는 자리.</summary>
        public const string ProbeKey = "JinHyung.UndeadProbe.Type";

        /// <summary>다리가 시킨 실행인가 — <c>EditorCommandBridge</c> 와 <b>같은 키</b>를 본다.</summary>
        public const string BridgeDrivingKey = "JinHyung.EditorBridge.Driving";

        public const string ScenePath = "Assets/Undead-Slayer/Scenes/UndeadSlayer.unity";

        /// <summary>산출물은 <b>Assets 밖</b>이다 (검증 산출물 규칙).</summary>
        public const string OutDir = "Tools/Verify/out";

#if UNITY_EDITOR
        /// <summary>씬을 열고 재생에 들어가 <paramref name="probeType"/> 를 세운다.</summary>
        public static void Launch(Type probeType)
        {
            if (probeType == null || typeof(UndeadProbeBase).IsAssignableFrom(probeType) == false)
            {
                Debug.LogError($"검사기가 {nameof(UndeadProbeBase)} 를 상속하지 않는다: {probeType}");
                return;
            }

            SessionState.SetString(ProbeKey, probeType.AssemblyQualifiedName);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>보고서를 남기고 콘솔에 찍는다 — <b>실패 목록이 곧 판정</b>이다.</summary>
        public static void Report(string fileName, StringBuilder log, List<string> fail)
        {
            if (fail.Count == 0)
            {
                log.AppendLine("");
                log.AppendLine("✔ 전부 통과");
            }
            else
            {
                log.AppendLine("");
                log.AppendLine($"✘ {fail.Count} 건 실패");

                for (int i = 0; i < fail.Count; i++)
                    log.AppendLine($"  - {fail[i]}");
            }

            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, fileName), log.ToString(), Encoding.UTF8);
            Debug.Log(log.ToString());
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Arm()
        {
#if UNITY_EDITOR
            string typeName = SessionState.GetString(ProbeKey, string.Empty);

            if (string.IsNullOrEmpty(typeName))
                return;

            // ⚠ 한 번만 선다 — 안 지우면 다음 재생에서 또 붙는다
            SessionState.SetString(ProbeKey, string.Empty);

            Type type = Type.GetType(typeName);

            if (type == null)
            {
                Debug.LogError($"검사기 타입을 못 찾았다: {typeName}");
                return;
            }

            var go = new GameObject(type.Name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent(type);
#endif
        }
    }

    /// <summary>
    /// 재생 검사기의 <b>바탕</b>. 상속해 <see cref="Run"/> 만 쓴다.
    ///
    /// <para>
    /// ★ 여기 들어온 것은 전부 <b>회차마다 다시 쓰던 것</b>이고, 하나같이 «한 번 데인 것»이다 —
    /// 포커스 없는 재생 throttle(<c>#156</c>) · 구독 전에 화면을 바꿈(<c>#178</c>) ·
    /// 정지 중에 잼(<c>#163</c>) · 실시간으로 걸어가다 죽음(회차 34).
    /// </para>
    /// </summary>
    public abstract class UndeadProbeBase : MonoBehaviour
    {
        protected readonly StringBuilder Log = new StringBuilder();
        protected readonly List<string> Fail = new List<string>();

        /// <summary>검사가 가는 길에 되살린 횟수 — <b>보고서에 남긴다</b>(측정 조건이 달라진 것을 숨기지 않는다).</summary>
        protected int Revives { get; private set; }

        /// <summary>산출물 파일 이름.</summary>
        protected abstract string ReportName { get; }

        /// <summary>검사 본문. <paramref name="root"/> 는 <b>배선까지 끝난</b> 게임 루트다.</summary>
        protected abstract IEnumerator Run(UndeadGameRoot root);

        /// <summary>화면을 바꾼 뒤 «구독이 붙을» 틈 [재발방지 <c>#178</c>].</summary>
        protected const float SettleSeconds = 1.0f;

        private IEnumerator Start()
        {
            // ⚠ 포커스가 없어도 프레임이 돌아야 한다 [재발방지 #156] — 안 켜면 «다리가 느리다»로 오해한다
            Application.runInBackground = true;
            Log.AppendLine($"[{GetType().Name}]");

            UndeadGameRoot root = null;

            for (float t = 0f; t < 60f && root == null; t += Time.unscaledDeltaTime)
            {
                root = ReadyRoot();
                yield return null;
            }

            if (root == null)
            {
                Fail.Add("배선이 60초 안에 안 끝났다 (로비 시뮬·뷰가 안 섰다)");
                Finish();
                yield break;
            }

            yield return new WaitForSeconds(SettleSeconds);
            yield return Run(root);
            Finish();
        }

        /// <summary>
        /// «준비됐다»의 정의 — <b>부트스트랩만으로는 이르다</b>.
        /// <para>배선(<c>UndeadGameInitialize</c>)이 표·아트를 읽고 매니저를 세우는 것이 그 뒤다.</para>
        /// </summary>
        private static UndeadGameRoot ReadyRoot()
        {
            UndeadGameRoot root = UndeadGameRoot.Instance;

            if (root == null || root.IsBootstrapped == false)
                return null;

            if (root.Lobby == null || root.Lobby.Simulation == null)
                return null;

            if (root.Game == null || root.Game.Simulation == null)
                return null;

            return UnityEngine.Object.FindFirstObjectByType<UndeadLobbyView>(FindObjectsInactive.Include) == null
                ? null
                : root;
        }

        // ══════════════════════════════ 화면

        /// <summary>
        /// 화면을 바꾸고 한 박자 쉰다. <b>안 먹었으면 한 번 더 민다</b> —
        /// 시작 화면이 늦게 자기 화면을 덮어쓰는 경우가 있다.
        /// </summary>
        protected IEnumerator Go(UndeadGameRoot root, EUndeadScreenType screen)
        {
            root.GameFlow.ChangeScreen(screen);
            yield return new WaitForSeconds(SettleSeconds);

            if (root.GameFlow.Current == screen)
                yield break;

            Log.AppendLine($"  (전이가 안 먹었다 — 지금 {root.GameFlow.Current}. 한 번 더 민다)");
            root.GameFlow.ChangeScreen(screen);
            yield return new WaitForSeconds(SettleSeconds);

            if (root.GameFlow.Current != screen)
                Fail.Add($"{screen} 화면으로 못 갔다 (지금 {root.GameFlow.Current})");
        }

        /// <summary>전투 화면으로 가서 <b>시간을 흐르게</b> 한다.</summary>
        protected IEnumerator EnterBattle(UndeadGameRoot root, float runSeconds = 0.5f)
        {
            yield return Go(root, EUndeadScreenType.Battle);
            root.Game.Paused = false;
            yield return new WaitForSeconds(runSeconds);
        }

        // ══════════════════════════════ 로비

        /// <summary>
        /// 과제를 <paramref name="count"/> 개까지 «완료 → 수령»한다 — 게임 코드의 그 함수를 그대로 쓴다.
        /// <para>겨울 포털은 <b>둘</b>을 받아야 열린다 [소스 <c>getClaimedTaskCount() &gt;= 2</c>].</para>
        /// </summary>
        protected static void ClaimTasks(UndeadSimulation run, int count)
        {
            string[] metrics =
            {
                UndeadSimulation.MetricTreesActivated,
                UndeadSimulation.MetricFireplaceHpRestored,
                UndeadSimulation.MetricSheepReturned,
                UndeadSimulation.MetricEnemiesKilledWithDash,
            };

            for (int m = 0; m < metrics.Length && run.ClaimedTaskCount < count; m++)
            {
                run.RecordTaskProgress(metrics[m], 999);

                for (int i = 0; i < run.TaskRecords.Count && run.ClaimedTaskCount < count; i++)
                    run.ClaimTaskReward(i);
            }
        }

        /// <summary>로비에서 <b>실시간으로</b> 걸어간다 — 로비는 30×20칸이라 몇 초면 닿는다.</summary>
        protected IEnumerator WalkLobbyTo(UndeadGameRoot root, Func<UndeadVec2> target,
                                          Func<bool> until, float seconds = 30f)
        {
            UndeadLobbySimulation lobby = root.Lobby.Simulation;

            root.Lobby.MoveInputSource = () =>
            {
                UndeadVec2 to = target() - lobby.HeroPosition;
                return to.Magnitude <= 2.0 ? UndeadVec2.Zero : to.Normalized;
            };

            bool done = false;

            for (float t = 0f; t < seconds && done == false; t += Time.deltaTime)
            {
                done = until();
                yield return null;
            }

            // ⚠ 입력을 끄고 끝낸다 — 켜 둔 채 두면 다음 검사까지 히어로가 흐른다
            root.Lobby.MoveInputSource = null;
        }

        // ══════════════════════════════ 전투 — 시뮬을 «직접» 민다

        /// <summary>
        /// 시뮬을 목표 쪽으로 <b>1/60씩 잘게</b> 민다.
        ///
        /// <para>
        /// ★ <b>실시간으로 걸어가지 않는다</b> — 퀘스트 지점이 수천 px 라 1분이 넘고,
        /// 그 사이 레벨업 카드가 떠 정지한다. 매니저를 세워 두고 여기서만 민다
        /// (규칙은 그대로 게임 코드가 돈다).
        /// </para>
        ///
        /// <para>⚠ 토막마다 <b>한 프레임 쉰다</b> — 안 쉬면 뷰가 한 번도 안 그려서 «그림»을 못 본다.</para>
        /// <para>⚠ 가는 길에 죽으면 게임의 «부활» 함수를 부르고 <see cref="Revives"/> 에 센다.</para>
        /// </summary>
        protected IEnumerator PushRun(UndeadSimulation sim, Func<UndeadVec2> target,
                                      Func<bool> until, double maxSimSeconds)
        {
            const double step = 1.0 / 60.0;
            const int chunk = 60;
            int total = (int)(maxSimSeconds / step);

            for (int i = 0; i < total; i++)
            {
                if (until())
                    yield break;

                if (sim.HeroDead)
                {
                    sim.Revive();
                    Revives++;
                }

                UndeadVec2 to = target() - sim.HeroPosition;
                sim.Step(step, to.Magnitude <= 4.0 ? UndeadVec2.Zero : to.Normalized);

                if (i % chunk == chunk - 1)
                    yield return null;
            }
        }

        /// <summary>시뮬을 <b>제자리에서</b> 민다 — 시간만 흘리고 싶을 때.</summary>
        protected static void PushIdle(UndeadSimulation sim, double simSeconds)
        {
            const double step = 1.0 / 60.0;

            for (int i = 0; i < (int)(simSeconds / step); i++)
                sim.Step(step, UndeadVec2.Zero);
        }

        // ══════════════════════════════ 읽기·적기

        /// <summary>보고서 한 줄.</summary>
        protected void Line(string text)
        {
            Log.AppendLine("  " + text);
        }

        /// <summary>조건이 거짓이면 실패로 적는다.</summary>
        protected void Check(bool condition, string whenFalse)
        {
            if (condition == false)
                Fail.Add(whenFalse);
        }

        /// <summary>비공개 «참조» 필드를 읽는다 — 검사는 화면 코드에 문을 뚫지 않는다.</summary>
        protected static T Field<T>(object target, string name)
            where T : class
        {
            if (target == null)
                return null;

            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(target) as T;
        }

        /// <summary>비공개 «값» 필드를 읽는다.</summary>
        protected static T FieldValue<T>(object target, string name)
        {
            if (target == null)
                return default;

            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? default : (T)field.GetValue(target);
        }

        /// <summary>씬에서 하나 찾는다 — <b>꺼져 있어도</b> 찾는다(로비는 전투 중에 꺼져 있다).</summary>
        protected static T Find<T>()
            where T : Component
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private void Finish()
        {
            if (Revives > 0)
                Line($"(가는 길에 되살린 횟수 {Revives})");

#if UNITY_EDITOR
            UndeadProbe.Report(ReportName, Log, Fail);

            if (SessionState.GetBool(UndeadProbe.BridgeDrivingKey, false))
                EditorApplication.ExitPlaymode();
            else
                EditorApplication.Exit(Fail.Count == 0 ? 0 : 1);
#endif
        }
    }
}
