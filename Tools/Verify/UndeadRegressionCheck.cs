using System;
using System.Collections;
using System.Collections.Generic;
using JinHyung.UndeadSlayer;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// <b>회귀 검사</b> — 회차마다 한 번 돌린다. 하네스(<see cref="UndeadProbeBase"/>) 위에 얹혀 있다.
    ///
    /// <para>
    /// ★ <b>무엇을 지키나</b> — 지난 회차들이 «사람이 화면을 보고 알려 줘서» 고쳐진 것들이다.
    /// 로비로 들어가지나 · 포털이 열리고 걸어 들어가지나 · 바이옴이 화면까지 가나 ·
    /// 입자가 «그려지나»(이미터만 도는 것이 아니라).
    /// </para>
    ///
    /// <para>실행: <c>PLAYMODE=1 Tools/unity-batch.sh JinHyung.EditorTools.UndeadRegressionCheck.RunPlay</c></para>
    /// </summary>
    public static class UndeadRegressionCheck
    {
#if UNITY_EDITOR
        public static void RunPlay()
        {
            UndeadProbe.Launch(typeof(UndeadRegressionProbe));
        }
#endif
    }

    public sealed class UndeadRegressionProbe : UndeadProbeBase
    {
        protected override string ReportName
        {
            get { return "UndeadRegression.txt"; }
        }

        protected override IEnumerator Run(UndeadGameRoot root)
        {
            yield return Battle(root);
            yield return Lobby(root);
            yield return Winter(root);
        }

        // ══════════════════════════════ 전투 — 입자가 «그려지나»

        private IEnumerator Battle(UndeadGameRoot root)
        {
            yield return EnterBattle(root);

            UndeadSimulation sim = root.Game.Simulation;
            var world = Find<UndeadWorldView>();
            var terrain = Find<UndeadTerrainView>();

            if (world == null || terrain == null)
            {
                Fail.Add("전투 뷰가 씬에 없다 — 뒤 검사가 통째로 헛돈다");
                yield break;
            }

            Log.AppendLine("── 전투");
            Line($"바이옴 {root.Game.Biome} · 지형 뷰 {terrain.Biome} · 칠한 칸 {terrain.PaintedCellCount}");
            Check(terrain.PaintedCellCount > 0, "지형이 한 칸도 안 칠해졌다");

            // 나무를 만날 때까지 걷는다 — 지형 노이즈가 놓으므로 처음 자리에 없을 수 있다
            int tree = -1;
            yield return PushRun(sim, () => new UndeadVec2(sim.HeroPosition.X + 500.0, sim.HeroPosition.Y),
                                 () => (tree = FirstTree(sim)) >= 0, 60.0);

            if (tree < 0)
            {
                Fail.Add("걸어도 나무를 못 만났다 — 반짝임을 못 쟀다");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            var sparkles = Field<UndeadSparkleEmitter[]>(world, "_treeSparkles");
            var drawn = Field<List<int>>(world, "_sparkleDraw");
            var hearts = Field<UndeadHeartEmitter[]>(world, "_heartEmitters");

            int alive = sparkles != null && tree < sparkles.Length && sparkles[tree] != null
                ? sparkles[tree].ActiveCount
                : 0;

            Line($"나무 {tree} 반짝임 {alive} 알 · 이번 프레임에 그린 알 {(drawn == null ? -1 : drawn.Count)}");
            Check(alive > 0, "나무가 반짝이지 않는다");
            Check(drawn != null && drawn.Count > 0, "반짝임이 «그려지지» 않는다 — 이미터만 돌고 화면에는 없다");

            int fire = FirstFire(sim, hearts);
            Line(fire < 0
                ? "모닥불이 화면에 없어 하트는 못 쟀다"
                : $"모닥불 {fire} 하트 {hearts[fire].ActiveCount} 알");
        }

        private static int FirstTree(UndeadSimulation sim)
        {
            for (int i = 0; i < sim.Trees.Count; i++)
            {
                if (sim.Trees[i].Active && sim.Trees[i].Bursted == false)
                    return i;
            }

            return -1;
        }

        private static int FirstFire(UndeadSimulation sim, UndeadHeartEmitter[] hearts)
        {
            for (int i = 0; hearts != null && i < sim.Fireplaces.Count && i < hearts.Length; i++)
            {
                if (sim.Fireplaces[i].Active && sim.Fireplaces[i].Lives > 0 && hearts[i] != null)
                    return i;
            }

            return -1;
        }

        // ══════════════════════════════ 로비 — 포털 · 반짝임

        private IEnumerator Lobby(UndeadGameRoot root)
        {
            yield return Go(root, EUndeadScreenType.Lobby);

            if (root.Lobby.IsInside == false)
            {
                Fail.Add("로비로 안 들어갔다");
                yield break;
            }

            var view = Find<UndeadLobbyView>();
            var portalSparkles = Field<List<UndeadSparkleEmitter>>(view, "_portalSparkles");
            UndeadLobbySimulation lobby = root.Lobby.Simulation;

            yield return new WaitForSeconds(0.6f);

            Log.AppendLine("── 로비");

            if (portalSparkles == null || portalSparkles.Count < 2)
            {
                Fail.Add("포털 반짝임 이미터가 둘 다 안 만들어졌다");
                yield break;
            }

            Line($"묘지(열림 {lobby.Portals[0].Open}) 반짝임 {portalSparkles[0].ActiveCount} 알"
                 + $" · 겨울(열림 {lobby.Portals[1].Open}) 반짝임 {portalSparkles[1].ActiveCount} 알");

            Check(portalSparkles[0].ActiveCount > 0, "열린 포털이 반짝이지 않는다");
            Check(lobby.Portals[1].Open || portalSparkles[1].ActiveCount == 0,
                  "잠긴 포털이 반짝인다 — 소스는 열린 것만 뿜는다");
        }

        // ══════════════════════════════ 겨울 포털로 «걸어» 들어간다

        private IEnumerator Winter(UndeadGameRoot root)
        {
            UndeadLobbySimulation lobby = root.Lobby.Simulation;
            ClaimTasks(root.Game.Simulation, 2);
            yield return null;

            if (lobby.Portals[1].Open == false)
            {
                Fail.Add("과제 둘을 받았는데 겨울 포털이 안 열렸다");
                yield break;
            }

            // 문간 한가운데 [소스 Du = (−28, −40, 56, 44)]
            yield return WalkLobbyTo(root,
                                     () => new UndeadVec2(lobby.Portals[1].Position.X,
                                                          lobby.Portals[1].Position.Y - 18.0),
                                     () => root.GameFlow.Current != EUndeadScreenType.Lobby);

            Log.AppendLine("── 바이옴 2 (겨울 황무지)");
            Line($"포털로 들어갔나 {root.GameFlow.Current != EUndeadScreenType.Lobby} · 지금 {root.GameFlow.Current}");

            if (root.GameFlow.Current == EUndeadScreenType.Lobby)
            {
                Fail.Add("겨울 포털에 못 들어갔다");
                yield break;
            }

            yield return EnterBattle(root);

            UndeadSimulation sim = root.Game.Simulation;
            var terrain = Find<UndeadTerrainView>();
            var world = Find<UndeadWorldView>();

            Line($"게임 {root.Game.Biome} · 시뮬 {sim.Biome} · 지형 뷰 {terrain.Biome} · 월드 뷰 {world.Biome}");
            Check(root.Game.Biome == 2 && sim.Biome == 2 && terrain.Biome == 2 && world.Biome == 2,
                  $"바이옴이 화면까지 안 갔다 (게임 {root.Game.Biome} · 시뮬 {sim.Biome}"
                  + $" · 지형 {terrain.Biome} · 월드 {world.Biome})");

            Line($"히어로 시작 ({sim.HeroPosition.X:0}, {sim.HeroPosition.Y:0}) (소스 (640, 610))");
            Check(Math.Abs(sim.HeroPosition.X - 640.0) < 200.0 && Math.Abs(sim.HeroPosition.Y - 610.0) < 200.0,
                  "겨울 시작 자리가 (640, 610) 언저리가 아니다");

            double snow = GrassRatio(terrain);
            Line($"창에서 «눈» 비율 {snow:P1} (소스 문턱 .75 라 대부분이 눈이다)");
            Check(snow > 0.5, $"겨울 눈밭이 {snow:P1} 뿐이다 — 문턱(.75)이 안 먹었다");

            var set = Field<UndeadSpriteSet>(world, "_treeSet");
            string code = set == null || set.Data == null ? "(없음)" : set.Data.Code;
            Line($"중형 오브젝트 시트 «{code}» (소스 snowman)");
            Check(code == "snowman", $"겨울인데 중형 오브젝트가 «{code}» 다");
        }

        /// <summary>창에 칠해진 칸 중 «풀(눈)» 비율 — 지형 캐시를 그대로 센다.</summary>
        private static double GrassRatio(UndeadTerrainView terrain)
        {
            var kinds = Field<Array>(terrain, "_cacheKind");

            if (kinds == null || kinds.Length == 0)
                return 0.0;

            int grass = 0;

            for (int i = 0; i < kinds.Length; i++)
            {
                if ((int)kinds.GetValue(i) == (int)UndeadTerrainView.ETileKind.Grass)
                    grass++;
            }

            return grass / (double)kinds.Length;
        }
    }
}
