using System;
using System.Globalization;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 이 게임의 «판» 하나를 굴린다 — 레벨 적재 · 입력 접수 · 시뮬 구동 · 재장전 · 클리어 통지.
    ///
    /// <para>
    /// <b>순수 C# 이다.</b> <c>MonoBehaviour</c> 가 아니라
    /// <c>BaseGameManager.Bootstrap</c> 이 등록 순서대로 초기화하고 <see cref="OnUpdate"/> 를 분배한다.
    /// </para>
    ///
    /// <para>
    /// 패스 ① 은 여기까지다 — <b>주입 API 를 «선언»만 한다.</b>
    /// 실제로 뷰를 만들고 이 이벤트에 물리는 것은 패스 ②③ 이다.
    /// </para>
    /// </summary>
    public sealed class BlumgiGameManager : BaseManager, IGameUpdate
    {
        /// <summary>월드 하나의 레벨 수 [실측 — 진행률 20% = 1/5].</summary>
        public const int LevelsPerWorld = 5;

        /// <summary>진입 레벨. 원본도 신규 컨텍스트면 항상 여기서 시작한다 [실측].</summary>
        public const string FirstLevelCode = "W1L1";

        /// <summary>
        /// 레벨의 <b>물리 실체</b>를 세우는 곳. 씬 구성이 패스 ③ 몫이라 <b>주입받는다</b>.
        /// ⚠ 없으면 레벨을 올리지 않는다 — 물리 없이 도는 «가짜 판»을 만들지 않는다.
        /// </summary>
        public BlumgiPhysicsWorld PhysicsWorld { get; set; }

        /// <summary>프리팹을 이름으로 건네주는 주입점 (게임 = Addressables · 검사 = 에디터 경로).</summary>
        public BlumgiPrefabLoader PrefabLoader { get; set; }

        /// <summary>현재 레벨. <c>null</c> 이면 아직 아무 레벨도 안 올렸다.</summary>
        public BlumgiLevelRuntime CurrentLevel { get; private set; }

        /// <summary>현재 한 발. 레벨이 안 올라갔으면 <c>null</c>.</summary>
        public BlumgiShotSimulation Shot { get; private set; }

        /// <summary>골인했다. 인자는 방금 깬 레벨 코드다 (원본 <c>layout._name</c> 과 같은 값).</summary>
        public event Action<string> LevelCleared;

        /// <summary>공이 화면 밖으로 떨어져 재장전이 시작됐다.</summary>
        public event Action ShotMissed;

        /// <summary>레벨이 올라갔다. 뷰가 배치를 다시 그려야 하는 시점이다.</summary>
        public event Action<BlumgiLevelRuntime> LevelLoaded;

        /// <summary>
        /// 레벨을 올린다. 데이터에 없으면 <b>아무것도 하지 않고 오류를 남긴다</b> —
        /// 폴백 레벨을 만들면 「빈 판이 돌아가는」 상태가 된다.
        /// </summary>
        public void LoadLevel(string levelCode)
        {
            BlumgiLevelRuntime runtime = BlumgiLevelRuntime.Build(levelCode);

            if (runtime == null)
            {
                Log.Error($"레벨 데이터가 없다: {levelCode}");
                return;
            }

            if (PhysicsWorld == null || PrefabLoader == null)
            {
                Log.Error($"물리 월드가 주입되지 않았다 — 레벨 {levelCode} 을 올리지 않는다 " +
                          "(패스 ③ 배선: PhysicsWorld · PrefabLoader)");
                return;
            }

            // ★ 물리 전역값(중력 30 · 반발 임계 1 · 접촉 오프셋 0.01)은 «레벨을 세우기 전»에 들어가야 한다.
            BlumgiPhysicsSetup.Apply(runtime.Config);
            PhysicsWorld.Build(runtime, PrefabLoader);

            if (PhysicsWorld.Ball == null)
            {
                Log.Error($"공 물리 바디가 없다 — 레벨 {levelCode} 을 올리지 않는다");
                return;
            }

            CurrentLevel = runtime;
            Shot = new BlumgiShotSimulation(runtime, PhysicsWorld.Ball);

            Log.Success($"레벨 적재 {levelCode} — 블록 {PhysicsWorld.BlockCount}칸 · 발사각 {runtime.Level.LaunchAngleDeg}°");

            LevelLoaded?.Invoke(runtime);
        }

        /// <summary>
        /// 지금 레벨을 처음부터 다시. 원본 ↺ 버튼이 하는 일이 이것이다 —
        /// <b>현재 레벨만 리셋</b>하고 진행도는 건드리지 않는다 [실측].
        /// </summary>
        public void RestartLevel()
        {
            if (CurrentLevel == null)
                return;

            LoadLevel(CurrentLevel.Level.Code);
        }

        /// <summary>
        /// 화면을 누르기 시작했다.
        /// ⚠ <b>좌표를 받지 않는다.</b> 원본은 마우스 위치가 발사에 영향이 없는 «파워 1축» 게임이고
        /// (4점 대조로 확정 [실측 §6]), 조준 UI 를 넣으면 그게 결함이다.
        ///
        /// <para>
        /// ★★ <b>재장전이 일어나는 «유일한» 자리가 여기다</b> [8회차 실측 · <c>eControls</c> EVENT#40→#44].
        /// 원본은 「누르고 있다」 아래에서만 <c>spr_BallVisu.SetVisible(1)</c> 을 하고,
        /// 그 액션이 있는 곳은 전 22개 시트에 <b>두 곳</b>(1P·2P)뿐이다 [실측 · 열거].
        /// <b>미스 후 6.5 초를 무입력으로 둬도 공은 돌아오지 않는다</b>는 반례까지 붙었다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「바닥선 통과 + 0.5 초」 자동 복귀 타이머를 되살리지 마라</b> — 패스 ②-c 가 넣었다가
        /// 8회차 실측으로 걷어낸 것이다. 방향(사라졌다 나타난다)은 맞았고 <b>«언제»만 틀렸다.</b>
        /// 그 타이머가 읽던 <c>ReloadDelaySeconds</c> 는 <b>데이터와 코드에서 통째로 지웠다</b> (패스 ①-f) —
        /// 안 읽는 값이 남아 있으면 다음 사람이 «있는 규칙»으로 읽는다.
        /// 남은 것은 <c>ReloadTriggerY</c> 하나고, 그건 <b>미스 판정선</b>이다
        /// (사라지는 «시점»이 아니라 <b>상태</b>를 가르는 선이다).
        /// </para>
        /// </summary>
        public void OnPointerDown()
        {
            if (Shot == null)
                return;

            // ★ 「다음 홀드를 누른 프레임」 = 재장전 기준점 [8회차 실측]. 지연 0 · 1단이다.
            if (Shot.State == EBlumgiShotState.Missed)
                Shot.ResetShot();

            Shot.BeginHold();
        }

        /// <summary>손을 뗐다 → 발사.</summary>
        public void OnPointerUp()
        {
            Shot?.EndHold();
        }

        /// <summary>
        /// 다음 레벨 코드. 월드1 의 마지막이면 <c>null</c> 이다.
        /// ⚠ 원본은 W1L5 다음에 <b>W2L1</b> 로 간다 [실측] — 월드2 는 <b>관측 범위 밖</b>이라 데이터가 없다.
        /// </summary>
        public string GetNextLevelCode()
        {
            if (CurrentLevel == null)
                return null;

            BlumgiLevelData level = CurrentLevel.Level;
            var levels = GameRoot.Instance.BlumgiLevelDataContainer;

            if (levels == null)
                return null;

            string next = $"W{level.WorldNo}L{level.LevelNo + 1}";
            return levels.GetByCode(next) == null ? null : next;
        }

        /// <summary>HUD 가 그대로 찍을 표기값.</summary>
        public BlumgiHudSnapshot GetHudSnapshot()
        {
            var snapshot = new BlumgiHudSnapshot
            {
                WorldText = string.Empty,
                LevelStep = 0,
                LevelStepCount = LevelsPerWorld,
                ProgressPercentText = string.Empty,
            };

            if (CurrentLevel == null)
                return snapshot;

            BlumgiLevelData level = CurrentLevel.Level;

            // 원본 문구 형식: "WORLD " + 숫자 · 정수 + "%" [실측 문구 사전]
            snapshot.WorldText = "WORLD " + level.WorldNo.ToString(CultureInfo.InvariantCulture);
            snapshot.LevelStep = level.LevelNo;

            int percent = level.LevelNo * 100 / LevelsPerWorld;
            snapshot.ProgressPercentText = percent.ToString(CultureInfo.InvariantCulture) + "%";

            return snapshot;
        }

        public void OnUpdate(float deltaTime)
        {
            // 유니티가 주는 dt 만 float 다. 여기 한 번만 double 로 올리고
            // 그 아래로는 float 가 내려가지 않는다 (`Client.md` 「수치 계층」).
            Tick(deltaTime);
        }

        /// <summary>
        /// 실제 진행. <b>dt 를 인자로 받는 이 함수가 곧 주입 지점</b>이다 —
        /// 검사 하네스도 여기를 지나므로 게임 코드에 따로 문을 뚫지 않는다.
        /// </summary>
        public void Tick(double deltaTime)
        {
            if (Shot == null)
                return;

            EBlumgiShotState before = Shot.State;

            Shot.Tick(deltaTime);

            if (before != EBlumgiShotState.Scored && Shot.State == EBlumgiShotState.Scored)
            {
                LevelCleared?.Invoke(CurrentLevel.Level.Code);
                return;
            }

            // ★ 미스는 «알리기만» 한다. 되돌리는 것은 다음 누름이다 (<see cref="OnPointerDown"/>) —
            //   원본에 자동 복귀가 «없다»는 것이 8회차에 시트 열거로 닫혔다.
            if (before != EBlumgiShotState.Missed && Shot.State == EBlumgiShotState.Missed)
                ShotMissed?.Invoke();
        }
    }
}
