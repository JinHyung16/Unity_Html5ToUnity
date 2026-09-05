using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 패스 ③ 의 <b>월드 배선</b> — 레벨이 올라올 때마다 «보이는 것»을 데이터에 맞춘다.
    ///
    /// <para>
    /// ★ <b>여기는 판정을 하지 않는다.</b> 물리·골인은 <c>BlumgiShotSimulation</c> 이 든다 —
    /// 이 클래스는 <b>색 · 그리기 순서 · 좌캡 · 연출 파라미터</b>만 넣는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>프리팹을 손으로 고치지 않는다</b> (확정표 10-b) — 여기서 하는 것은 «런타임 주입»이다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlumgiLevelPresenter : MonoBehaviour
    {
        /// <summary>
        /// 블록 한 칸의 «그리기 깊이» 눈금 (유닛/원본 px).
        ///
        /// <para>
        /// ★★ <b>원본은 좌캡을 «항상» 그리고 왼쪽 이웃이 z 순서로 덮는다</b> [5회차 실측 —
        /// 가로 이웃 331쌍 전부 「왼쪽이 위」, 위반 0]. 그 기전을 옮기려면 <b>x 가 작을수록 위</b>여야 한다.
        /// 정렬 순서(<c>sortingOrder</c>)는 공·골대·블롭과 «한 띠»를 쓰므로 여기에 462칸을 끼워 넣을 수 없다 —
        /// 그래서 같은 <c>sortingOrder</c> 안에서 <b>z 로</b> 가른다 (직교 카메라는 z 로 앞뒤를 정한다).
        /// </para>
        ///
        /// <para>
        /// x 1250 에서도 0.025 유닛이라 물리·카메라에 영향이 없다 (2D 물리는 z 를 안 본다).
        /// </para>
        /// </summary>
        private const float BlockDepthPerWorldPixel = 1e-5f;

        /// <summary>
        /// 골 화살표 부유 진폭 (원본 world px) [8회차 실측 · Sine <c>_mag</c> = 10 ·
        /// 화면 bbox y 796.19~816.11 ⇒ 진폭 9.96 — <b>두 경로가 일치</b>].
        /// </summary>
        private const float ArrowAmplitudeWorld = 10f;

        /// <summary>
        /// 골 화살표 부유 주기 (초) [8회차 실측 · Sine <c>_period</c> = 0.25 · 화면 극값 간격 125 ms × 2].
        ///
        /// <para>
        /// ⚠ <b>1회차가 「진동 없음 · 미측정」으로 남긴 이유가 이 값이다</b> — 캡처 격자가 140 ms 라
        /// 250 ms 주기를 <b>앨리어싱</b>해 «정지»로 보였다. 주기성은 캡처가 아니라 비헤이비어 상수에서 읽는다.
        /// </para>
        /// </summary>
        private const float ArrowPeriodSeconds = 0.25f;

        /// <summary>
        /// 골 화살표 부유의 기준 y (원본 world) [8회차 실측 W1L1 · Sine <c>_initialValue</c> = 788].
        /// ⚠ <b>W1L1 만 쟀다</b> — 다른 레벨은 미측정이라 「림 기준 상대 오프셋이 전 레벨 동일」을 가정한다
        /// `추정(근거: 골대 6부품이 전부 상수 오프셋이라는 7회차 실측)`.
        /// </summary>
        private const double ArrowBaseWorldY = 788.0;

        private BlumgiGameManager _game;
        private BlumgiPhysicsWorld _world;
        private BlumgiCameraDirector _camera;
        private Camera _gameCamera;
        private BlumgiLevelBackground _background;
        private BlumgiBallTrailView _trail;
        private BlumgiRestBallView _restBall;
        private BlumgiBlobView _blob;
        private BlumgiHoopView _hoop;

        private readonly List<BlumgiBlockView> _blocks = new List<BlumgiBlockView>(256);

        private int _lastCollisionCount;

        /// <summary>꼬리가 화면에 떠 있나 — 비행이 끝난 «그 한 번»만 지우기 위한 표식이다.</summary>
        private bool _trailAlive;

        /// <summary>
        /// 직전 프레임의 샷 상태. <b>연출은 «상태»가 아니라 «전이»에 걸린다</b> —
        /// 원본이 <c>TriggerOnce</c> · 「누른 프레임」으로 쓰기 때문이다 [8회차 실측].
        /// </summary>
        private EBlumgiShotState _lastShotState = EBlumgiShotState.Ready;

        /// <summary>골대 «림» 의 유니티 좌표.</summary>
        public Vector3 HoopWorldPosition { get; private set; }

        /// <summary>
        /// 원본 world 좌표를 얹는 프레임 (블록·골대가 앉아 있는 그것).
        /// 컨페티 캐논처럼 <b>원본 절대 좌표</b>를 쓰는 연출이 이것을 받아 간다.
        /// </summary>
        public Transform WorldFrame
        {
            get { return _world == null ? null : _world.transform; }
        }

        /// <summary>
        /// 좌캡이 «보일» 칸 수 — <b>교차 검산 숫자다. 화면을 바꾸지 않는다.</b>
        /// 5회차 표(L1 22 · L2 22 · L3 14 · L4 22 · L5 51)와 대조한다.
        /// </summary>
        public int VisibleCapCount { get; private set; }

        public void Bind(BlumgiGameManager game,
                         BlumgiPhysicsWorld world,
                         BlumgiCameraDirector cameraDirector,
                         Camera gameCamera,
                         BlumgiLevelBackground background,
                         BlumgiBallTrailView trail,
                         BlumgiRestBallView restBall)
        {
            _game = game;
            _world = world;
            _camera = cameraDirector;
            _gameCamera = gameCamera;
            _background = background;
            _trail = trail;
            _restBall = restBall;

            if (_game != null)
                _game.LevelLoaded += ApplyLevel;
        }

        private void OnDestroy()
        {
            if (_game != null)
                _game.LevelLoaded -= ApplyLevel;
        }

        /// <summary>레벨이 올라간 직후. <b>물리 실체는 이미 서 있다</b> — 그 위에 «보이는 값»을 얹는다.</summary>
        public void ApplyLevel(BlumgiLevelRuntime runtime)
        {
            if (runtime == null || _world == null)
                return;

            BlumgiLevelData level = runtime.Level;
            BlumgiConfigData config = runtime.Config;

            ApplyBackground(level, config);
            ApplyBlocks(runtime);
            ApplyHoop(level, config);
            ApplyLauncher();
            ApplyRestBall(runtime);

            _lastCollisionCount = 0;
            _lastShotState = EBlumgiShotState.Ready;

            if (_trail != null)
            {
                _trail.Clear();
                _trailAlive = false;
            }

            // ⚠ 카메라를 «레벨 위치로 옮긴 뒤»에 기준을 잡는다 — 순서가 바뀌면 펀치가 원위치로 되돌린다.
            if (_camera != null)
            {
                _camera.CaptureBase();
                _camera.PlayLevelStartPunch();
            }
        }

        private void ApplyBackground(BlumgiLevelData level, BlumgiConfigData config)
        {
            if (_background != null)
            {
                // 격자선 색을 배경에서 «파생시키지 않는다» — W1L5 만 색조가 돈다 [05_연출 §1].
                _background.SetTheme(Hex(level.BgColorHex), Hex(level.GridLineColorHex));

                // 야자수는 테마를 따라가지 않는다 — 5레벨 전부 같은 색이라 설정에서 온다 [실측].
                _background.SetPalmColor(Hex(config.PalmColorHex));
            }

            // 배경 판 밖(카메라가 지우는 자리)도 같은 색이어야 «판이 끝난 자리»가 안 보인다.
            if (_gameCamera != null)
                _gameCamera.backgroundColor = Hex(level.BgColorHex);
        }

        /// <summary>
        /// 블록 — 테마 3색 · 그리기 순서 · 좌캡.
        ///
        /// <para>
        /// ★★ <b>좌캡을 켜고 끄지 않는다.</b> 원본은 좌캡을 «항상» 그리고 왼쪽 이웃이 덮는다 [5회차 실측] —
        /// 그 기전을 그대로 옮겼다: 스프라이트 기하(몸통 오른쪽 = 캡 오른쪽 + 격자 피치 50)와
        /// 그리기 순서(캡 8 &lt; 몸통 10)만으로 원본 픽셀이 나온다.
        /// ⚠ 「덮였으니 캡을 끈다」는 <b>기전이 아니라 파생 규칙</b>이라 예전에 여기 들어와 있었다 —
        /// 자산을 고쳐 걷어냈다 (PD 판정 3 · 재발방지 #53).
        /// </para>
        ///
        /// <para>
        /// 아래에서 세는 <see cref="VisibleCapCount"/> 는 <b>교차 검산용</b>이다 (5회차 지침) —
        /// 파생 규칙과 기전이 어긋나면 숫자가 5회차 표와 갈린다. <b>화면에는 아무 영향이 없다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ 프레임 5종은 «좌캡 구분»이 아니라 <b>레벨 팔레트</b>였다 (L1→0 · L2→3 · L3→1 · L4→2 · L5→4).
        /// 우리는 흰 스프라이트를 <b>런타임 tint</b> 하므로(확정 F) 그 팔레트가
        /// <c>BlockSquareColorHex</c> · <c>BlockLeftCapColorHex</c> <b>레벨 데이터</b>로 이미 들어와 있다.
        /// </para>
        /// </summary>
        private void ApplyBlocks(BlumgiLevelRuntime runtime)
        {
            _blocks.Clear();

            Transform blockRoot = _world.transform.Find("Blocks");

            if (blockRoot == null)
            {
                Log.Error("물리 월드에 Blocks 루트가 없다 — 레벨이 안 세워졌다");
                return;
            }

            IReadOnlyList<BlumgiVec2> centers = runtime.BlockCenters;

            if (blockRoot.childCount != centers.Count)
            {
                Log.Error($"블록 실체 {blockRoot.childCount}개 ≠ 데이터 {centers.Count}행 — 프리팹이 하나 안 나왔다");
            }

            BlumgiLevelData level = runtime.Level;

            // ★★ [정정 · 17회차] 블록은 «틴트»가 아니라 «프레임»이다 — 색은 그림에 구워져 있고
            //    인스턴스 색은 5레벨 전부 (255,255,255) 였다 [실측 462 인스턴스].
            //    레벨 번호 → 프레임 [실측 · 예외 0건] L1→0 · L2→3 · L3→1 · L4→2 · L5→4.
            int skinFrame = BlockSkinFrameOf(level.LevelNo);

            double pitch = runtime.Config.BlockGridPitch;

            // ⚠ 원본 이상 #4 (W1L2 한 칸에 블록 2개)를 지우지 않는다 — 집합이라 중복이 «있는지»만 본다.
            var occupied = new HashSet<long>();

            for (int i = 0; i < centers.Count; i++)
                occupied.Add(CellKey(centers[i].X, centers[i].Y));

            int count = Mathf.Min(blockRoot.childCount, centers.Count);
            VisibleCapCount = 0;

            for (int i = 0; i < count; i++)
            {
                Transform child = blockRoot.GetChild(i);
                var view = child.GetComponent<BlumgiBlockView>();

                if (view == null)
                    continue;

                _blocks.Add(view);
                view.SetSkinFrame(skinFrame);

                BlumgiVec2 center = centers[i];

                // ① 그리기 순서 — x 가 작을수록 카메라에 가깝다(= 나중에 그려져 위로 온다).
                Vector3 position = child.localPosition;
                position.z = (float)center.X * BlockDepthPerWorldPixel;
                child.localPosition = position;

                // ② 좌캡 «검산만» 한다 — 켜고 끄지 않는다. 왼쪽 이웃이 없으면 원본에서도 캡이 보인다.
                if (occupied.Contains(CellKey(center.X - pitch, center.Y)) == false)
                    VisibleCapCount++;
            }

            Log.Success($"{level.Code} 블록 {count}칸 · 스킨 프레임 {skinFrame} — 좌캡(검산) {VisibleCapCount}칸 " +
                        "(5회차 실측: L1 22 · L2 22 · L3 14 · L4 22 · L5 51)");
        }

        /// <summary>
        /// 레벨 번호 → 블록 스킨 프레임 [실측 5레벨 462 인스턴스 · 예외 0건].
        /// ⚠ <b>「번호 − 1」이 아니다</b> — L2 가 3, L3 이 1, L4 가 2 다. 규칙이 아니라 «표»다.
        /// </summary>
        public static int BlockSkinFrameOf(int levelNo)
        {
            int index = levelNo - 1;
            int[] table = BlumgiArtAddress.BlockSkinFrameByLevelNo;

            return index < 0 || index >= table.Length ? 0 : table[index];
        }

        /// <summary>격자 좌표를 정수 키로. <b>0.5 world 안쪽의 흔들림</b>까지만 같은 칸으로 본다.</summary>
        private static long CellKey(double worldX, double worldY)
        {
            long x = (long)System.Math.Round(worldX * 2.0);
            long y = (long)System.Math.Round(worldY * 2.0);
            return (x << 24) ^ y;
        }

        private void ApplyHoop(BlumgiLevelData level, BlumgiConfigData config)
        {
            HoopWorldPosition = _world.transform.TransformPoint(
                BlumgiUnits.ToPosition(level.GoalRimX, level.GoalRimY));

            var hoop = _world.GetComponentInChildren<BlumgiHoopView>(true);
            _hoop = hoop;

            if (hoop == null)
                return;

            // 골인에 사라졌던 화살표를 다시 켠다 [실측 — 새 레벨에는 다시 있다].
            hoop.SetArrowVisible(true);
            hoop.ResetGoalAnimation();

            // 골대 색은 «전 레벨 동일»이라 레벨이 아니라 설정에서 온다 [05_연출 §1 실측].
            hoop.SetNetColor(Hex(config.GoalNetColorHex));
            hoop.SetRimTint(Hex(config.GoalRimLightColorHex));

            // ── 골 화살표 부유 — 8회차가 Sine 비헤이비어 상수를 «직독»해 닫았다 [실측 §3-a].
            //    수직(movement 1) · 정현파(wave 0) · 주기 0.25 s · 진폭 ±10 px.
            hoop.ArrowFloat?.Configure(ArrowAmplitudeWorld, ArrowPeriodSeconds, 0f);

            // 기준 y — Sine `_initialValue` = 788 [실측 W1L1]. 림(GoalRimY) 기준 오프셋으로 바꿔 넣는다.
            //   ⚠ 한 레벨만 쟀다 — 다른 레벨에서 오프셋이 갈리면 여기가 아니라 «데이터»로 올라가야 한다.
            hoop.SetArrowBaseOffset((float)(ArrowBaseWorldY - level.GoalRimY));
        }

        private void ApplyLauncher()
        {
            _blob = _world.GetComponentInChildren<BlumgiBlobView>(true);
            _blob?.ResetPose();
        }

        /// <summary>
        /// 머리 위 공 — <b>물리와 무관한 그림</b>이다 (<c>spr_BallVisu</c>).
        ///
        /// <para>
        /// ★★ 물리 공은 <b>경기장 밖 (0, 10000)</b> 에서 낙하한다 [6회차 실측 · 패스 ①-d].
        /// 여기서 «보이는» 공을 다시 경기장에 올리는 것이 아니라, 원본과 같이
        /// <b>별개 오브젝트</b>를 <c>BallRestX/Y</c> 자리에 세운다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 좌표는 <b>레벨 데이터 절대값</b>이다 — 발사대 기준 오프셋으로 파생시키면 안 된다
        /// (04 §6: 5레벨 오프셋이 (+50,−96)~(−64,−84.5) 로 흩어진다 · 「레벨 데이터로 쓰면 안 된다」).
        /// </para>
        /// </summary>
        private void ApplyRestBall(BlumgiLevelRuntime runtime)
        {
            if (_restBall == null)
                return;

            BlumgiVec2 rest = runtime.BallRestPosition;

            // 블록·골대와 «같은 좌표 프레임»에 앉힌다 — 물리 월드가 옮겨져도 따라간다.
            _restBall.SetRestPosition(_world.transform.TransformPoint(
                                          BlumgiUnits.ToPosition(rest.X, rest.Y)));

            // 레벨이 올라온 직후는 «발사 전»이라 원본에서도 공이 머리 위에 있다 (크기도 대기값 94×93).
            _restBall.ShowOnPress();
        }

        private void Update()
        {
            if (_game == null || _world == null)
                return;

            BlumgiShotSimulation shot = _game.Shot;

            if (shot == null)
                return;

            ApplyShotPresentation(shot);

            BlumgiBallBody ball = _world.Ball;

            if (ball == null)
                return;

            // ── 트레일은 파티클이 아니라 «공 잔상 31장»이다 [05_연출 3-2].
            //    ★ <b>비행이 끝나면 꼬리가 «사라진다»</b> [실측 3-2: 「공이 빠르게 움직이는 동안만.
            //      골인·정지하면 사라진다」]. 안 지우면 재장전 뒤에도 마지막 잔상이 화면에 박혀 있다
            //      (패스 ②-c 재생 캡처에서 실제로 남아 있었다).
            if (_trail != null)
            {
                bool flying = shot.State == EBlumgiShotState.Flying;

                if (flying)
                {
                    // ★ 표본 간격은 «원본 프레임 8.33 ms» 다 — 매 프레임 밀면 꼬리 길이가
                    //   우리 프레임률에 끌려간다 (BlumgiBallTrailView 주석의 3중 검산 참조).
                    _trail.Advance(ball.transform.position, Time.deltaTime);
                    _trailAlive = true;
                }
                else if (_trailAlive)
                {
                    _trail.Clear();
                    _trailAlive = false;
                }
            }

            // ── 착탄 흔들림.
            if (ball.CollisionCount != _lastCollisionCount)
            {
                _lastCollisionCount = ball.CollisionCount;
                _camera?.PlayImpactShake();
            }
        }

        /// <summary>
        /// 발사 연출 — <b>머리 위 공 · 블롭 스쿼시 · 표정</b>.
        ///
        /// <para>
        /// ★★ <b>「상태가 무엇인가」가 아니라 「무엇으로 바뀌었나」로 건다.</b> 원본이 그렇게 돼 있다 —
        /// 재장전은 <c>ShootLoad_P1 = 1</c> 이 «되는» 틱(누른 프레임)이고,
        /// 숨김은 <c>TriggerOnce</c> 가 걸리는 발사 틱이다 [8회차 실측 · <c>eControls</c> #37~#45].
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[정정 · 8회차] 예전 구현은 「<c>Ready</c> 또는 <c>Charging</c> 이면 보인다」였다.</b>
        /// 그러면 매니저의 <b>「바닥선 + 0.5 s」 자동 복귀</b>가 <c>Missed → Ready</c> 를 만드는 순간
        /// 공이 되살아난다 — <b>원본에 그런 복귀가 없다.</b> 미스 후 <b>6.5 초 무입력</b>으로 둬도
        /// 원본은 공을 돌려놓지 않는다 [실측 반례]. 그래서 타이머를 걷어내고 <b>전이</b>로 바꿨다.
        /// </para>
        /// </summary>
        private void ApplyShotPresentation(BlumgiShotSimulation shot)
        {
            EBlumgiShotState now = shot.State;
            EBlumgiShotState before = _lastShotState;
            _lastShotState = now;

            // ── ① 누른 «프레임» — 재장전. 1단이고 지연이 0 이다 [실측].
            if (before != EBlumgiShotState.Charging && now == EBlumgiShotState.Charging)
            {
                _restBall?.ShowOnPress();
                _blob?.ResetPose();
            }

            // ── ⓪ 골 «프레임» — 화살표가 사라지고 [8회차 실측 · ArrowRestart.SetVisible(0)],
            //    골대가 «골인 전용» Animation 3 4프레임으로 갈아탄다 [17회차 실측].
            if (before != EBlumgiShotState.Scored && now == EBlumgiShotState.Scored)
            {
                _hoop?.SetArrowVisible(false);
                _hoop?.PlayGoalAnimation();

                // ★★ 골 프레임에 «셰이크»가 시작하고 «줌»은 한 프레임 뒤에 점프한다 [17회차 실측].
                _camera?.PlayGoalPunch();
            }

            // ── ② 발사 «프레임» — 머리공이 즉시 숨고 블롭이 세로로 늘어났다 돌아온다 [실측].
            if (before == EBlumgiShotState.Charging && now == EBlumgiShotState.Flying)
            {
                _restBall?.HideOnLaunch();
                _blob?.PlayRelease();
            }

            // ── ③ 홀드가 «모자라» 발사가 안 걸린 경우 — 원본에도 발사 액션이 안 돈다. 대기 형태로.
            if (before == EBlumgiShotState.Charging && now == EBlumgiShotState.Ready)
                _blob?.ResetPose();

            if (now != EBlumgiShotState.Charging)
                return;

            // ── ④ 홀드 중 — 크기 트윈 둘 다 «초»로 돈다 (1.5 s 트윈 ≠ 1.633 s 힘 포화).
            var seconds = (float)shot.HoldSeconds;

            _restBall?.SetHoldSeconds(seconds);
            _blob?.SetHoldSeconds(seconds);
        }

        /// <summary>
        /// 원본 sRGB hex 를 그대로 색으로. ⚠ <b>Linear 역산을 하지 않는다</b> (CLAUDE.md 「렌더 규칙」) —
        /// 어긋나는 만큼은 «자산»에서 흡수한다.
        /// </summary>
        private static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Color.white;

            if (ColorUtility.TryParseHtmlString(hex[0] == '#' ? hex : "#" + hex, out Color color))
                return color;

            Log.Error($"색 문자열을 못 읽었다: {hex}");
            return Color.white;
        }
    }
}
