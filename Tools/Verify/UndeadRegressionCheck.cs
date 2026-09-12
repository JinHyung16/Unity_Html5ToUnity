using System;
using System.Collections;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.UndeadSlayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
            yield return StartScreen(root);
            yield return Battle(root);
            yield return Lobby(root);
            yield return ClaimRing(root);
            yield return Winter(root);
            yield return WipeSave(root);
        }

        // ══════════════════════════════ 시작 화면 — 딤과 버튼

        /// <summary>
        /// <b>사람이 「버튼 주변에 어두운 글로우가 있다」고 알려 준 자리</b> (회차 36).
        ///
        /// <para>
        /// ★ 원인은 버튼이 아니라 <b>뒤에 깔린 딤</b>이었다 — 리니어 합성이라 감마값 그대로 두면
        /// <b>화면이 훨씬 밝고</b>, 그 위에서 버튼 스프라이트의 원본 그림자가 도드라진다 (재발방지 <c>#183</c>).
        /// </para>
        ///
        /// <para>⚠ 버튼은 <b>맥동한다</b> — 한 프레임만 보면 «그 순간의 배율»을 규격으로 적게 된다.</para>
        /// </summary>
        private IEnumerator StartScreen(UndeadGameRoot root)
        {
            yield return Go(root, EUndeadScreenType.Ready);

            Log.AppendLine("── 시작 화면");

            Image start = null;
            Image dimmer = null;

            // ⚠ 스프라이트로 고르면 안 된다 — 「기록 지우기」 버튼도 같은 btn_shadowed 를 쓴다
            foreach (Image image in UnityEngine.Object.FindObjectsByType<Image>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (image.name == "Start" && image.sprite != null)
                    start = image;

                if (image.name == "Dimmer" && image.transform.parent != null
                    && image.transform.parent.name.Contains("Ready"))
                {
                    dimmer = image;
                }
            }

            if (start == null)
            {
                Fail.Add("시작 버튼을 못 찾았다");
                yield break;
            }

            // 엔진이 «실제로 그리는» 테두리 [Image.GenerateSlicedSprite — 인셋 ÷ (스프라이트PPU/기준PPU × 배수)]
            float drawn = start.sprite.border.x
                          / (start.sprite.pixelsPerUnit / start.canvas.referencePixelsPerUnit
                             * start.pixelsPerUnitMultiplier);
            float ratio = drawn / start.rectTransform.rect.height;

            Line($"버튼 칸 {start.rectTransform.rect.width:0.0}×{start.rectTransform.rect.height:0.0}"
                 + $" · 그려지는 테두리 {drawn:0.0} ({ratio:P1} · 원본 16/104 = 15.4%)");
            Check(Math.Abs(ratio - 16f / 104f) < 0.01f, $"9슬라이스 테두리 비율이 {ratio:P1} 다 — 원본은 15.4%");

            // ★ 딤은 «역산값»이어야 한다 — 감마 .5 를 그대로 두면 화면이 밝다
            float alpha = dimmer == null ? -1f : dimmer.color.a;
            Line($"딤 알파 {alpha:0.00} (원본 감마 .50 → 리니어 역산 ≈ .76)");
            Check(alpha > 0.70f, $"딤 알파가 {alpha:0.00} 다 — 감마값을 그대로 뒀다(화면이 밝아진다)");

            // ★★ 맥동은 «여러 프레임» — 최소·최대가 둘 다 나와야 확정된다
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < 12; i++)
            {
                float scale = start.rectTransform.localScale.x;
                min = Mathf.Min(min, scale);
                max = Mathf.Max(max, scale);
                yield return new WaitForSeconds(0.1f);
            }

            Line($"12 프레임 배율 {min:0.000} ~ {max:0.000} (소스 1.000 ~ 1.130)");
            Check(max - min > 0.02f, $"배율이 {min:0.000}~{max:0.000} 로 거의 안 변했다 — 맥동이 안 돈다");
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

            yield return HeroBlink(root, world, sim);
        }

        /// <summary>
        /// <b>사람이 「움직이면 깜빡인다」고 알려 준 자리</b> (회차 36).
        ///
        /// <para>
        /// ★ 원인은 <b>구운 시트의 이동행 마지막 컷이 비어 있던 것</b>이었다 (재발방지 <c>#182</c>).
        /// ⚠ 컷은 12fps 로 돈다 — <b>한 바퀴(0.42초)를 안 보면 못 잡는다</b>. 두 바퀴를 본다.
        /// </para>
        /// </summary>
        private IEnumerator HeroBlink(UndeadGameRoot root, UndeadWorldView world, UndeadSimulation sim)
        {
            SpriteRenderer hero = Field<SpriteRenderer>(world, "_hero");

            if (hero == null)
            {
                Fail.Add("히어로 렌더러를 못 찾았다");
                yield break;
            }

            root.Game.MoveInputSource = () => new UndeadVec2(1.0, 0.0);

            var seen = new List<string>();
            int invisible = 0;
            int faded = 0;
            int hit = 0;

            for (int i = 0; i < 120; i++)
            {
                yield return null;

                if (hero.sprite == null || hero.enabled == false)
                    invisible++;
                else if (seen.Contains(hero.sprite.name) == false)
                    seen.Add(hero.sprite.name);

                if (hero.color.a < 0.999f)
                    faded++;

                if (sim.HeroHitTintRemaining > 0.0)
                    hit++;
            }

            root.Game.MoveInputSource = null;
            seen.Sort();

            Line($"히어로 120프레임(이동) — 나온 컷 {seen.Count}종 {string.Join(" · ", seen)}");
            Line($"그림이 없던 프레임 {invisible} · 흐렸던 프레임 {faded} · 피격 {hit}");

            Check(invisible == 0, $"이동 중 {invisible} 프레임에 히어로 그림이 없다 — 빈 컷이 섞여 있다");
            Check(faded == 0 || hit > 0, $"맞지도 않았는데 {faded} 프레임이 흐리다 — 이동만으로 깜빡인다");
            Check(seen.Count >= 5, $"이동행 컷이 {seen.Count}종뿐이다 — 원본은 5종을 돌린다");
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

        // ══════════════════════════════ 수령 링 — «컷이 갈리나»

        /// <summary>
        /// 링은 <b>채워지는 바가 아니라 컷을 갈아 끼운다</b> [소스 <c>getTextureNameForProgress</c> · 9컷].
        /// <para>⚠ 한 프레임만 보면 «컷 하나»밖에 못 본다 — <b>차는 동안 여러 프레임</b>을 본다.</para>
        /// </summary>
        private IEnumerator ClaimRing(UndeadGameRoot root)
        {
            Log.AppendLine("── 로비 수령 링");

            var hud = FindWindow();

            if (hud == null)
            {
                Fail.Add("로비 HUD 창을 못 찾았다");
                yield break;
            }

            var cuts = Field<Sprite[]>(hud, "_claimRingCuts");
            var ring = Field<Image>(hud, "_claimRing");
            int filled = 0;

            for (int i = 0; cuts != null && i < cuts.Length; i++)
            {
                if (cuts[i] != null)
                    filled++;
            }

            Line($"컷 {(cuts == null ? 0 : cuts.Length)}개 · 비지 않은 것 {filled} (소스 9컷)");
            Check(cuts != null && cuts.Length == 9 && filled == 9, "수령 링 컷 9개가 다 안 들어갔다");
            Check(ring != null && ring.sprite != null, "수령 링 이미지에 스프라이트가 없다 — 화면에 아무것도 안 나온다");

            // ★ 과제 하나를 «완료»만 시켜 놓고(수령은 안 한다) 그 NPC 앞으로 걸어간다
            UndeadSimulation run = root.Game.Simulation;
            run.RecordTaskProgress(UndeadSimulation.MetricTreesActivated, 999);
            yield return null;

            UndeadLobbySimulation lobby = root.Lobby.Simulation;
            int npc = -1;

            for (int i = 0; i < lobby.Npcs.Count; i++)
            {
                int task = lobby.Npcs[i].TaskIndex;

                if (task >= 0 && run.TaskRecords[task].Status == EUndeadTaskStatus.Completed)
                    npc = i;
            }

            if (npc < 0)
            {
                Line("완료된 과제를 든 NPC 가 없어 링이 차는 것은 못 쟀다");
                yield break;
            }

            UndeadVec2 at = lobby.Npcs[npc].Position;
            yield return WalkLobbyTo(root, () => at, () => lobby.Npcs[npc].ClaimProgress > 0.0, 20f);

            // ★★ 차는 동안 여러 프레임 — 컷이 «갈리는지»를 본다.
            //   ⚠ 프레임 수를 «감»으로 잡지 않는다 — 링은 2초에 찬다(ClaimSeconds).
            //     끝까지 차는 것을 «조건»으로 두고, 프레임 수는 그 두 배를 상한으로만 쓴다.
            //     [사고] 처음에 40프레임(≈0.5초)만 보고 「컷이 2종뿐」이라고 적었다 — 검사가 짧았던 것이다.
            var seen = new List<string>();
            int cap = Mathf.CeilToInt((float)UndeadLobbySimulation.ClaimSeconds * 2f / Time.fixedDeltaTime);

            for (int i = 0; i < cap; i++)
            {
                if (ring != null && ring.enabled && ring.sprite != null && seen.Contains(ring.sprite.name) == false)
                    seen.Add(ring.sprite.name);

                if (lobby.Npcs[npc].ClaimProgress >= 1.0 || lobby.Npcs[npc].ClaimTriggered)
                    break;

                yield return null;
            }

            Line($"차는 동안 나온 컷 {seen.Count} 종 — {string.Join(" · ", seen)}"
                 + $" (진행 {lobby.Npcs[npc].ClaimProgress:0.00})");
            Check(seen.Count >= 5, $"링이 차는데 컷이 {seen.Count} 종뿐이다 — 진행률로 안 갈린다");
        }

        private static object FindWindow()
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour.GetType().Name == "UndeadLobbyHudWindow")
                    return behaviour;
            }

            return null;
        }

        // ══════════════════════════════ 기록 지우기 — «정말 지워지나»

        /// <summary>
        /// <b>원본에 없는 버튼</b> — 로컬에 남긴 것을 지운다.
        /// <para>⚠ 「붙였다」가 아니라 <b>「눌러서 지워졌다」</b>로 본다 (재발방지 <c>#159</c>).</para>
        /// </summary>
        private IEnumerator WipeSave(UndeadGameRoot root)
        {
            Log.AppendLine("── 기록 지우기 (원본에 없는 우리 버튼)");

            // 지울 것이 있어야 «지워졌다»를 볼 수 있다
            UndeadRecord.ReportLevel(7);
            UndeadRecord.ReportTime(123.0);
            UndeadRecord.UnlockLobby();
            Line($"지우기 전 — 최고 레벨 {UndeadRecord.BestLevel} · 최고 시간 {UndeadRecord.BestTimeSeconds:0}초"
                 + $" · 로비 해금 {UndeadRecord.LobbyUnlocked}");

            yield return Go(root, EUndeadScreenType.Ready);

            Button wipe = null;

            foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (button.name == "WipeSave")
                    wipe = button;
            }

            if (wipe == null)
            {
                Fail.Add("기록 지우기 버튼이 화면에 없다");
                yield break;
            }

            var image = wipe.GetComponent<Image>();
            Line($"버튼 있음 · 눌릴 수 있나 {wipe.interactable} · 레이캐스트 {image != null && image.raycastTarget}"
                 + $" · 등록된 게임 {SaveWipe.OwnerCount}개");

            Check(wipe.interactable && image != null && image.raycastTarget,
                  "기록 지우기 버튼이 «눌릴 수 없는» 상태다");
            Check(SaveWipe.OwnerCount > 0, "지우는 일이 등록되지 않았다 — 눌러도 아무것도 안 지운다");

            // ★ 실제 클릭 경로로 누른다
            ExecuteEvents.Execute(wipe.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;

            int left = 0;

            for (int i = 0; i < SaveWipe.SampleKeys.Count; i++)
            {
                if (PlayerPrefs.HasKey(SaveWipe.SampleKeys[i]))
                    left++;
            }

            Line($"누른 뒤 — 최고 레벨 {UndeadRecord.BestLevel} · 최고 시간 {UndeadRecord.BestTimeSeconds:0}초"
                 + $" · 로비 해금 {UndeadRecord.LobbyUnlocked} · 남은 키 {left}");

            Check(left == 0, $"버튼을 눌렀는데 키가 {left}개 남았다");
            Check(UndeadRecord.BestLevel == 1 && UndeadRecord.LobbyUnlocked == false,
                  "지웠는데 «처음 켠 사람» 상태가 아니다");
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
