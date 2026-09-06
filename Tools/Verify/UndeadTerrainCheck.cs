using System.Collections.Generic;
using System.Text;
using JinHyung.UndeadSlayer;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 지형 골든 대조 — <b>우리 생성기가 원본과 «같은 값»을 내는가</b>.
    ///
    /// <para>
    /// 정답지는 <see cref="UndeadTerrainGolden"/> 이고, 그 값은 <b>원본 번들에서 읽은 코드를 그대로 돌려</b>
    /// 뽑았다. 여기서 통과하면 나무·모닥불·타일이 원본과 <b>같은 자리</b>에 선다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadTerrainCheck.RunAll</c></para>
    /// </summary>
    public static class UndeadTerrainCheck
    {
#if UNITY_EDITOR
        public static void RunAll()
        {
            var report = new StringBuilder(1 << 13);
            int pass = 0;
            int fail = 0;

            void Check(string title, bool ok, string detail)
            {
                if (ok)
                    pass++;
                else
                    fail++;

                report.AppendLine($"  {(ok ? "✔" : "✘")} {title}{(ok || string.IsNullOrEmpty(detail) ? "" : "  — " + detail)}");
            }

            // ── ① Alea 0.9 자체 ────────────────────────────────
            report.AppendLine("── Alea 0.9 (시드 → 난수열)");

            foreach (UndeadTerrainGolden.AleaCase c in UndeadTerrainGolden.Alea)
            {
                var rng = new UndeadSeededRandom(c.Seed);
                bool ok = true;
                var got = new List<string>();

                for (int i = 0; i < c.Values.Length; i++)
                {
                    double v = rng.Next();
                    got.Add(v.ToString("F12"));

                    // ⚠ 난수열은 «비트 단위»로 같아야 한다 — 한 발만 어긋나도 뒤가 전부 갈린다
                    if (System.Math.Abs(v - c.Values[i]) > 1e-12)
                        ok = false;
                }

                Check($"Alea(\"{c.Seed}\") 첫 {c.Values.Length}발", ok,
                      ok ? "" : $"우리 [{string.Join(", ", got)}]");
            }

            // ── ② Simplex 노이즈 ───────────────────────────────
            report.AppendLine("── simplex-noise v4 (노이즈 표본)");

            CheckNoise(Check, "detail", UndeadTerrainSeeds.Detail, UndeadTerrainSeeds.DetailFrequency,
                       UndeadTerrainGolden.NoiseDetail, false);
            CheckNoise(Check, "grass", UndeadTerrainSeeds.Grass, UndeadTerrainSeeds.GrassFrequency,
                       UndeadTerrainGolden.NoiseGrass, true);
            CheckNoise(Check, "medium", UndeadTerrainSeeds.MediumObject, UndeadTerrainSeeds.ObjectFrequency,
                       UndeadTerrainGolden.NoiseMedium, false);
            CheckNoise(Check, "healing", UndeadTerrainSeeds.HealingObject, UndeadTerrainSeeds.ObjectFrequency,
                       UndeadTerrainGolden.NoiseHealing, false);

            // road 는 (1−|n|)³ 을 씌운다 [소스]
            {
                var noise = new UndeadTerrainNoise(UndeadTerrainSeeds.Road);
                bool ok = true;

                for (int i = 0; i < UndeadTerrainGolden.Samples.Length; i++)
                {
                    double[] s = UndeadTerrainGolden.Samples[i];
                    double n = noise.Sample(s[0] * UndeadTerrainSeeds.RoadFrequency, s[1] * UndeadTerrainSeeds.RoadFrequency);
                    double v = System.Math.Pow(1.0 - System.Math.Abs(n), 3.0);

                    if (System.Math.Abs(v - UndeadTerrainGolden.NoiseRoad[i]) > 1e-9)
                        ok = false;
                }

                Check("noise road 표본 8점", ok, "");
            }

            // ── ③ 나무·모닥불 자리 ─────────────────────────────
            report.AppendLine("── 오브젝트 배치 (청크 −3..3 전수)");

            var mediumNoise = new UndeadTerrainNoise(UndeadTerrainSeeds.MediumObject);
            var healingNoise = new UndeadTerrainNoise(UndeadTerrainSeeds.HealingObject);

            const int chunk = 24;
            const int treeW = 4;
            const int treeH = 2;

            var trees = new List<Vector2Int>();
            var fires = new List<Vector2Int>();

            for (int cy = -3; cy <= 3; cy++)
            {
                for (int cx = -3; cx <= 3; cx++)
                {
                    int ox = cx * chunk;
                    int oy = cy * chunk;

                    if (mediumNoise.Sample(ox * UndeadTerrainSeeds.ObjectFrequency, oy * UndeadTerrainSeeds.ObjectFrequency) > 0.4)
                    {
                        var r = new UndeadSeededRandom($"{cx},{cy}");
                        trees.Add(new Vector2Int(ox + (int)System.Math.Floor(r.Next() * (chunk - treeW + 1)),
                                                 oy + (int)System.Math.Floor(r.Next() * (chunk - treeH + 1))));
                    }

                    if (healingNoise.Sample(ox * UndeadTerrainSeeds.ObjectFrequency, oy * UndeadTerrainSeeds.ObjectFrequency) > 0.3)
                    {
                        var r = new UndeadSeededRandom($"healing_{cx},{cy}");
                        fires.Add(new Vector2Int(ox + (int)System.Math.Floor(r.Next() * chunk),
                                                 oy + (int)System.Math.Floor(r.Next() * chunk)));
                    }
                }
            }

            Check($"나무 개수 = 원본 {UndeadTerrainGolden.Trees.Length}", trees.Count == UndeadTerrainGolden.Trees.Length, $"우리 {trees.Count}");
            Check($"모닥불 개수 = 원본 {UndeadTerrainGolden.Fireplaces.Length}", fires.Count == UndeadTerrainGolden.Fireplaces.Length, $"우리 {fires.Count}");

            int treeMatch = 0;

            for (int i = 0; i < UndeadTerrainGolden.Trees.Length && i < trees.Count; i++)
            {
                int[] g = UndeadTerrainGolden.Trees[i];

                if (trees[i].x == g[2] && trees[i].y == g[3])
                    treeMatch++;
                else
                    report.AppendLine($"      나무 갈림 — 원본 ({g[2]},{g[3]}) vs 우리 ({trees[i].x},{trees[i].y}) [청크 {g[0]},{g[1]}]");
            }

            Check($"나무 타일 좌표 {treeMatch}/{UndeadTerrainGolden.Trees.Length}", treeMatch == UndeadTerrainGolden.Trees.Length, "");

            int fireMatch = 0;

            for (int i = 0; i < UndeadTerrainGolden.Fireplaces.Length && i < fires.Count; i++)
            {
                int[] g = UndeadTerrainGolden.Fireplaces[i];

                if (fires[i].x == g[2] && fires[i].y == g[3])
                    fireMatch++;
                else
                    report.AppendLine($"      모닥불 갈림 — 원본 ({g[2]},{g[3]}) vs 우리 ({fires[i].x},{fires[i].y}) [청크 {g[0]},{g[1]}]");
            }

            Check($"모닥불 타일 좌표 {fireMatch}/{UndeadTerrainGolden.Fireplaces.Length}", fireMatch == UndeadTerrainGolden.Fireplaces.Length, "");

            // ── ④ 시작 화면에 «실제로 보인» 나무 [원본 실측] ────
            // 원본 카메라 world (1620, 1010) 에서 화면에 잡힌 나무는 타일 (78, 46) 이고
            // 앵커(+2타일=48px)를 더해 world x 1920 에 섰다 [실측 · world_ready.json].
            bool sawIt = trees.Contains(new Vector2Int(78, 46));
            Check("시작 화면의 그 나무 — 타일 (78,46) [원본 실측]", sawIt, "");

            string summary = $"═══ 지형 골든 대조 {pass}/{pass + fail} 통과 ═══";
            report.AppendLine(summary);
            Debug.Log(report.ToString());
            Debug.Log(summary);

            if (fail > 0 && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static void CheckNoise(System.Action<string, bool, string> check, string name,
                                       string seed, double frequency, double[] golden, bool negate)
        {
            var noise = new UndeadTerrainNoise(seed);
            bool ok = true;
            var got = new List<string>();

            for (int i = 0; i < UndeadTerrainGolden.Samples.Length; i++)
            {
                double[] s = UndeadTerrainGolden.Samples[i];
                double v = noise.Sample(s[0] * frequency, s[1] * frequency);

                if (negate)
                    v = -v;

                got.Add(v.ToString("F8"));

                if (System.Math.Abs(v - golden[i]) > 1e-9)
                    ok = false;
            }

            check($"noise {name} 표본 {UndeadTerrainGolden.Samples.Length}점", ok,
                  ok ? "" : $"우리 [{string.Join(", ", got)}]");
        }
#endif
    }
}
