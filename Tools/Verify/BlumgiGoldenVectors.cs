using System;
using System.Collections.Generic;

namespace BlumgiVerify
{
    /// <summary>
    /// 골든 벡터 — 정본은 `Html Games URL 모음/01-Blumgi-Bounce/HtmlToUnityLogic/07_기대값.md` 다.
    /// 이 파일은 그 md 를 읽어 주는 <b>일회용</b>이고 이관이 끝나면 폴더째 지운다.
    ///
    /// <para>
    /// ★★ <b>회차마다 «축»이 다르다 — 그것이 이 파일의 구조를 정한다.</b>
    /// <list type="bullet">
    /// <item><b>3회차(§8)</b>: <c>Date.now()</c> 로 «실» 홀드를 직접 재고, 발사 프레임을 직접 잡고,
    /// <c>|v0|</c> 를 네 가지로 교차 유도했다 — <b>가장 신뢰도가 높다. 여기가 채점의 중심이다</b></item>
    /// <item><b>1·2회차(§2·§3·§5)</b>: 축이 «공칭» 홀드고, 릴리즈 직전 스크린샷이 홀드를 늘렸으며,
    /// <c>|v0|</c> 가 12프레임 피팅 단일값(±4.6% 튄다)이다 —
    /// <b>PD 판정: <c>|v0|</c> 컬럼은 «유도값·신뢰도 낮음». 궤적·최고점이 정본</b></item>
    /// </list>
    /// </para>
    /// </summary>
    public static class GoldenVectors
    {
        // ══════════════════════════════════════════════ 3회차 — 기전 · 최고점 (정본)

        /// <summary>
        /// 3회차 실측 한 점. <b>홀드가 «실» 홀드</b>다 — 공칭이 아니다.
        /// 원시 표: 스크래치패드 `gap3/요약표_전체.md` 「캡 스윕」.
        /// </summary>
        public sealed class MechanismPoint
        {
            public string Level;

            /// <summary>«실» 홀드(ms) — <c>Date.now()</c> 직접 측정, 바이어스 +5~19ms.</summary>
            public int RealHoldMs;

            /// <summary>«긴창» 유도 |v0| — 발사~최고점 전 구간 2차 최소제곱. 폭 0.1% 로 가장 안정적이다.</summary>
            public double Speed;

            /// <summary>실측 발사각(도, 위쪽 +).</summary>
            public double AngleDeg;

            /// <summary>실측 발사 위치.</summary>
            public double PosX;
            public double PosY;

            /// <summary>실측 최고점 (x, y) @ +t ms. <b>독립 관측이라 발사위치·|v0|·각도·g 가 전부 맞아야 맞는다.</b></summary>
            public double ApexX;
            public double ApexY;
            public double ApexTimeMs;

            /// <summary>
            /// 채점에서 뺀 이유(있으면). <b>조용히 빼지 않는다</b> — 화면에 찍는다.
            /// </summary>
            public string ExcludeReason;

            /// <summary>
            /// ★★★ <b>이 최고점을 «어떤 정의»로 쟀나</b> (`07 §6` · `07 §8-3a`).
            /// <b>정의가 다르면 같은 궤적에서도 «한 프레임» 차가 난다</b> — 그래서 값과 함께 들고 다닌다.
            /// </summary>
            public EApexMeasure Measure = EApexMeasure.RafSample;

            /// <summary>
            /// ⛔ <b>「채점 불가(원리)」 사유</b> — 「비결정」과 «다르다».
            /// 원본은 결정적인데 <b>입력 분해능 1칸이 허용치보다 큰 변화</b>를 만드는 자리다.
            /// 어떤 구현도 통과할 수 없으므로 <b>이 벡터를 골든에서 버린다</b>.
            /// </summary>
            public string UngradableReason;
        }

        /// <summary>
        /// 최고점을 «재는 법». <b>원본과 이관본이 같은 값이어야 비교가 성립한다</b> (`07 §8-3a`).
        /// </summary>
        public enum EApexMeasure
        {
            /// <summary>프레임 샘플 중 가장 높은 점. <b>폭이 «샘플 간격 × 속도»만큼 «반드시» 생긴다.</b></summary>
            RafSample = 0,

            /// <summary>
            /// ★ <c>v_y</c> 부호 전환을 선형보간한 <b>진짜 꼭짓점</b>. <b>샘플링 위상에 독립</b>이다.
            /// </summary>
            InterpolatedVertex = 1,
        }

        public static List<MechanismPoint> Mechanism()
        {
            var list = new List<MechanismPoint>();

            // ── W1L1 (조준 300 → 60.0°) · g긴창 1500~1503 = 충돌 없음
            M(list, "W1L1", 817, 1150, 59.8, 200, 484, 580, 155, 658);
            M(list, "W1L1", 1205, 1593, 59.9, 200, 486, 933, -146, 915);
            M(list, "W1L1", 1507, 1949, 59.9, 200, 488, 1292, -460, 1117);
            M(list, "W1L1", 1809, 2098, 59.9, 200, 487, 1471, -611, 1207);
            M(list, "W1L1", 2212, 2099, 59.9, 200, 487, 1471, -611, 1208);
            M(list, "W1L1", 2619, 2098, 59.9, 200, 488, 1471, -610, 1207);
            M(list, "W1L1", 3510, 2098, 59.9, 200, 487, 1471, -611, 1208);
            M(list, "W1L1", 5011, 2097, 59.9, 200, 487, 1471, -611, 1207);
            M(list, "W1L1", 713, 1026, 59.8, 200, 484, 0, 0, -1);   // RUN F (full 모드 · 최고점 미채록)

            // ── W1L2 (290 → 70.0°)
            M(list, "W1L2", 809, 1139, 69.9, 259, 825, 537, 444, 708);
            M(list, "W1L2", 1213, 1602, 69.9, 260, 827, 809, 73, 1000);
            M(list, "W1L2", 1604, 2055, 69.9, 260, 827, 1164, -414, 1283);
            M(list, "W1L2", 2013, 2097, 69.9, 260, 827, 1200, -466, 1309);
            M(list, "W1L2", 2517, 2097, 69.9, 260, 827, 1201, -466, 1308);
            M(list, "W1L2", 3507, 2097, 69.9, 260, 828, 1201, -466, 1309);
            M(list, "W1L2", 5016, 2096, 69.9, 260, 829, 1207, -464, 1317);

            // ── W1L4 (230 → 130.0°) — 월드1 에서 유일하게 «왼쪽 위»로 쏜다
            M(list, "W1L4", 818, 1151, 130.2, 986, 293, 553, 36, 583);
            M(list, "W1L4", 1208, 1604, 130.1, 986, 295, 151, -205, 809);

            // ⛔ [15회차 · `07 §8-3b`] «채점 불가(원리)» — 「비결정」이 아니다.
            //    force 가 포화 «전»이라 시행마다 95.265 ~ 99.412 로 흔들리고(n=8),
            //    민감도가 −27.6 px / force 1  ⇒  1프레임(0.4585) = 최고점 x 12.6 px > 허용치 ±10 px.
            //    ⇒ 어떤 구현도 통과할 수 없다. 값을 고치는 것이 아니라 «벡터를 버린다».
            Ungradable(M(list, "W1L4", 1604, 2056, 130.1, 986, 296, -394, -528, 1042),
                "force 포화 «전» — 원본 실측 force 가 95.265~99.412 로 흔들리고(15회차 n=8) " +
                "민감도 −27.6 px/force1 ⇒ 1프레임(0.4585) = 최고점 x 12.6 px > 허용치 ±10 px. " +
                "원본은 결정적이다(같은 force 면 0.3 px 안) — «입력 분해능»이 허용치보다 굵어서 못 재는 것이다 [`07 §8-3b`]");

            // ── ★★ W1L4 캡 4샷 — «보간 꼭짓점»으로 정정 [15회차 재측정 n=16 · `07 §8-3a`]
            //    옛 정답지 : 2008 −456 · 2508 −444 · 3503 −456 · 5018 −444  (rAF 샘플 · 위상이 섞였다)
            //    ⇒ −456 과 −444 의 차 11.6 px 은 «정확히 한 프레임»이다 (|v_x| 1351 px/s × 8.33 ms = 11.3 px).
            //    15회차가 캡 구간을 16회 다시 쏘니 rAF 샘플 x = −455.5 ± 0.2 · 보간 꼭짓점 x = −465.22 ± 0.03.
            //    ⚠ 근거는 «점수»가 아니라 «원본 재측정»이다 (재발방지 #48).
            Vertex(M(list, "W1L4", 2008, 2098, 130.1, 986, 297, -465.22, -561, 1074));
            Vertex(M(list, "W1L4", 2508, 2098, 130.1, 986, 296, -465.22, -561, 1074));
            Vertex(M(list, "W1L4", 3503, 2098, 130.1, 986, 297, -465.22, -561, 1074));
            Vertex(M(list, "W1L4", 5018, 2098, 130.1, 986, 296, -465.22, -561, 1074));

            M(list, "W1L4", 408, 678, 130.3, 986, 290, 836, 201, 342);

            // ── W1L5 (330 «추정» → 30.0°) — 1000ms 이상은 최고점이 천장(y 148~149)에 막혀 유도 무효
            M(list, "W1L5", 410, 681, 29.5, 187, 265, 315, 228, 217);
            M(list, "W1L5", 607, 914, 29.7, 187, 267, 418, 199, 291);
            M(list, "W1L5", 803, 1132, 29.7, 187, 268, 548, 163, 366);
            M(list, "W1L5", 404, 670, 29.5, 187, 265, 314, 229, 216);
            M(list, "W1L5", 416, 698, 29.5, 187, 265, 317, 227, 216);
            Exclude(M(list, "W1L5", 1010, 1373, 29.8, 187, 270, 475, 149, 241),
                "최고점 y 가 천장(148~149)에 붙었다 — 최고점 유도가 무효다 [3회차 §3-1]");
            Exclude(M(list, "W1L5", 1214, 1603, 29.8, 187, 271, 443, 149, 183),
                "같은 사유(천장)");
            Exclude(M(list, "W1L5", 1608, 2061, 30.0, 187, 271, 426, 148, 134),
                "같은 사유(천장). 단 이 샷이 «W1L5 를 클리어한 캡 근처 샷»이다 (§6 클리어에서 쓴다)");

            // ── W1L3 — 발사 직후 충돌이 섞여 g 가 1632~2354 로 튄다. 유도 셋 다 무효 [3회차 §2-3]
            Exclude(M(list, "W1L3", 818, 945, 72.1, 1050, 232, 975, 51, 433),
                "g긴창 2354 — 최고점 전에 충돌이 섞였다. 12f·긴창·최고점 모두 무효 [3회차 §2-3·§7]");
            Exclude(M(list, "W1L3", 1214, 1056, 101.3, 1050, 234, 822, -81, 600),
                "g긴창 1835 — 같은 사유");

            return list;
        }

        private static MechanismPoint M(
            List<MechanismPoint> list, string level, int realHold, double speed, double angle,
            double px, double py, double apexX, double apexY, double apexT)
        {
            var p = new MechanismPoint
            {
                Level = level,
                RealHoldMs = realHold,
                Speed = speed,
                AngleDeg = angle,
                PosX = px,
                PosY = py,
                ApexX = apexX,
                ApexY = apexY,
                ApexTimeMs = apexT,
            };

            list.Add(p);
            return p;
        }

        private static void Exclude(MechanismPoint p, string reason)
        {
            p.ExcludeReason = reason;
        }

        /// <summary>이 최고점 정답지는 <b>보간 꼭짓점</b> 정의다 (`07 §8-3a`).</summary>
        private static void Vertex(MechanismPoint p)
        {
            p.Measure = EApexMeasure.InterpolatedVertex;
        }

        /// <summary>⛔ <b>「채점 불가(원리)」</b> — 「비결정」과 다르다 (`07 §8-3b`).</summary>
        private static void Ungradable(MechanismPoint p, string reason)
        {
            p.UngradableReason = reason;
        }

        // ══════════════════════════════════════════════ 12회차 — W1L3 «진짜 발사 프레임» (정본)

        /// <summary>
        /// ★★★ <b>12회차가 직독한 W1L3 발사 프레임</b> — <c>07 §8-5-b</c> 정본.
        ///
        /// <para>
        /// <b>축이 «force» 다</b> — 벽시계 홀드가 아니다. 원본 전역 <c>forceShoot_P1</c> 을 릴리즈 직전에
        /// 직접 읽은 값이라 7·10·11회차 규약과 같은 눈금이다.
        /// </para>
        ///
        /// <para>
        /// <b>이 프레임이 «진짜»인 근거</b>: 5샷 전부 <c>atan2(−vy, vx) = 60.000°</c> 로 조준각과
        /// <b>소수 3자리까지</b> 같다 — 중력이 한 틱이라도 걸렸으면 각이 낮아졌을 것이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 3회차가 이 레벨을 「발사 직후 충돌로 못 잰다」고 남긴 그 자리다. 방법이 달랐다 —
        /// 3회차는 <b>궤적에서 역산</b>했고 12회차는 <b>발사 프레임을 직접 집었다.</b>
        /// </para>
        /// </summary>
        public sealed class LaunchFramePoint
        {
            /// <summary>레벨 코드. 14회차-b 가 5레벨 전수로 넓혔다.</summary>
            public string Level = "W1L3";

            /// <summary>릴리즈 직전 <c>forceShoot_P1</c> [실측].</summary>
            public double Force;

            /// <summary>그 프레임의 공 위치 (world px).</summary>
            public double PosX;
            public double PosY;

            /// <summary>그 프레임의 속도 크기 (world px/s).</summary>
            public double Speed;

            /// <summary>
            /// 그 프레임의 각속도 (rad/s · 원본 좌표계). <c>0</c> = 그 회차가 안 쟀다.
            /// 14회차-b 20샷은 전부 <b>−3.4906585216522217 (= −200 °/s)</b> 였다 [표준편차 0].
            /// </summary>
            public double OmegaRad;

            /// <summary>채점에서 뺀 이유. <b>조용히 빼지 않는다</b> — 화면에 찍는다.</summary>
            public string ExcludeReason;
        }

        /// <summary>발사 각속도 — 13·14회차 누적 36샷 표준편차 0 [실측].</summary>
        public const double LaunchOmegaRad = -3.4906585216522217;

        /// <summary>12회차 W1L3 5샷. 원자료: 스크래치패드 <c>gap12/res_A.json</c> · <c>rec_A_0..4.json</c>.</summary>
        public static List<LaunchFramePoint> LaunchFramesW1L3()
        {
            return new List<LaunchFramePoint>
            {
                new LaunchFramePoint { Force = 15.0563, PosX = 1050.136, PosY = 225.925, Speed = 316.59 },
                new LaunchFramePoint { Force = 18.2445, PosX = 1050.146, PosY = 226.280, Speed = 383.63 },
                new LaunchFramePoint { Force = 26.5000, PosX = 1050.188, PosY = 227.847, Speed = 557.22 },
                new LaunchFramePoint { Force = 37.5000, PosX = 1050.240, PosY = 229.750, Speed = 788.52 },
                new LaunchFramePoint { Force = 59.5000, PosX = 1050.321, PosY = 232.747, Speed = 1251.12 },
            };
        }

        /// <summary>
        /// ★★★ <b>14회차-b 발사 프레임 20샷 — W1L1 · W1L2 · W1L4 · W1L5</b> (`07 §13-a`).
        ///
        /// <para>
        /// <b>이 표가 「발사점 트윈은 전역이다」의 직접 근거다.</b> 절편을 각 레벨의 정지 머리공
        /// 중심으로 «고정»하고 진폭 하나로 맞춰 5레벨 38점이 <b>잔차 y ≤ 0.066 px</b> 안에 들어왔다.
        /// 12회차 W1L3 5점만 있을 때는 「다른 4레벨은 미측정」이었다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b><c>holdSec &gt; 1.5</c> 인 2점은 채점에서 뺀다</b> — 그 구간에서는 <b>원본 자신이</b>
        /// 트윈 종료값(dy 9.87)과 10.88~11.66 사이를 오간다 [14회차-b §1-g · n=26 · 원인 미측정].
        /// 우리 모델은 <c>min(1, …)</c> 클램프라 원리적으로 그 튐을 못 낸다.
        /// <b>조용히 빼지 않는다 — 검사기가 이유를 찍는다.</b>
        /// </para>
        /// </summary>
        public static List<LaunchFramePoint> LaunchFrames14b()
        {
            var list = new List<LaunchFramePoint>();

            // ── W1L1 (조준 300 → 60.000°)
            LF(list, "W1L1", 21.4565, 200.163, 478.900, 451.17);
            LF(list, "W1L1", 27.4185, 200.193, 480.017, 576.54);
            LF(list, "W1L1", 38.4185, 200.244, 481.898, 807.83);
            LF(list, "W1L1", 59.9675, 200.323, 484.797, 1260.95);
            LF(list, "W1L1", 87.4583, 200.372, 486.619, 1839.01);

            // ── W1L2 (290 → 70.000°) — 최고 force 92.4945 는 holdSec 1.4999 로 «간발로» 창 안이다
            LF(list, "W1L2", 21.4620, 259.296, 819.676, 451.29);
            LF(list, "W1L2", 26.4945, 259.321, 820.624, 557.11);
            LF(list, "W1L2", 38.5065, 259.377, 822.682, 809.68);
            LF(list, "W1L2", 71.4240, 259.484, 826.622, 1501.85);
            LF(list, "W1L2", 92.4945, 259.506, 827.447, 1944.90);

            // ── W1L4 (230 → 130.000°)
            LF(list, "W1L4", 20.5380, 985.958, 287.768, 431.86);
            LF(list, "W1L4", 33.8315, 986.023, 290.187, 711.38);
            LF(list, "W1L4", 48.9510, 986.086, 292.501, 1029.30);
            LF(list, "W1L4", 70.9510, 986.150, 294.853, 1491.90);
            Skip(LF(list, "W1L4", 93.8695, 986.216, 297.292, 1973.81),
                 "holdSec 1.5249 — 트윈 지속 1.5 s 를 «넘긴» 점이다. 그 구간은 원본이 " +
                 "dy 9.87 ↔ 10.88~11.66 두 갈래로 «튄다»(14회차-b §1-g · n=26 · 원인 미측정). " +
                 "여기 dy 는 11.445 로 튄 쪽이다 — 클램프 모델로는 원리적으로 못 낸다. " +
                 "⚠ 이관 창 밖이다(창 상한이 5레벨 전부 force 77 이하)");

            // ── W1L5 (330 → 30.000°)
            LF(list, "W1L5", 20.0760, 187.097, 262.909, 422.14);
            LF(list, "W1L5", 32.9130, 187.160, 265.260, 692.07);
            LF(list, "W1L5", 48.9620, 187.227, 267.730, 1029.53);
            LF(list, "W1L5", 71.4185, 187.292, 270.118, 1501.73);
            Skip(LF(list, "W1L5", 93.3690, 187.342, 271.957, 1963.29),
                 "holdSec 1.5158 — 같은 사유(트윈 1.5 s 초과 · dy 10.880 으로 튄 쪽)");

            return list;
        }

        private static LaunchFramePoint LF(List<LaunchFramePoint> list, string level, double force,
                                           double x, double y, double speed)
        {
            var p = new LaunchFramePoint
            {
                Level = level,
                Force = force,
                PosX = x,
                PosY = y,
                Speed = speed,
                OmegaRad = LaunchOmegaRad,
            };

            list.Add(p);
            return p;
        }

        private static void Skip(LaunchFramePoint p, string reason)
        {
            p.ExcludeReason = reason;
        }

        // ══════════════════════════════════════════════ 14회차-b — 첫 접촉 «직전» (정본)

        /// <summary>
        /// ★★★ <b>첫 접촉 «직전» 정답지</b> — `07 §13-b` [14회차-b 실측 · 4레벨 20샷].
        ///
        /// <para>
        /// ⚠⚠ <b><see cref="PreTimeMs"/> 의 기준점은 «발사 프레임»이다</b> — 9·13회차 정답지의
        /// «홀드 시작» 기준과 <b>다르다</b>. 섞으면 「첫 접촉부터 수백 ms 어긋난다」는 가짜 결론이 나온다
        /// (사슬 추적기가 실제로 한 번 그렇게 읽었다 — <c>BlumgiChainPlayCheck</c> 의 홀드 빼기 주석).
        /// </para>
        ///
        /// <para>
        /// ★ <b>이 표가 말해 주는 것 — 첫 접촉 «전»은 순수 포물선이다.</b> 측정된 16점 전부
        /// <c>v_x</c> 가 <b>발사값에서 한 자리도 안 변한다</b> ⇒ 선감쇠·공기저항 <b>0</b>.
        /// 그래서 여기가 어긋나면 원인은 <b>발사점 · |v0| · 각도 · 중력</b> 넷뿐이다 —
        /// 「접촉 이후만 보면 어디서부터 어긋나는지 못 가른다」를 여기서 가른다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>「접촉 없음」 4점은 «부존재»가 정답이다</b> — 원본이 6 s 안에 아무것도 안 친다
        /// (전부 force ≥ 70.9 · 경기장 이탈). <b>우리 쪽 「화면 밖으로 나간다」가 그 자체로 결함이 아니다.</b>
        /// </para>
        /// </summary>
        public sealed class FirstContactPoint
        {
            public string Level;

            /// <summary>축 — 릴리즈 직전 <c>forceShoot_P1</c> [실측].</summary>
            public double Force;

            /// <summary>원본에 첫 접촉이 있나. <c>false</c> = 6 s 안에 접촉 없음(정답이 «부존재»다).</summary>
            public bool HasContact;

            /// <summary>접촉 «직전» rAF 프레임 시각 — <b>발사 프레임 기준 ms</b>.</summary>
            public double PreTimeMs;

            public double PreX;
            public double PreY;
            public double PreVx;
            public double PreVy;

            /// <summary>접촉 직전 각속도 (rad/s). 각감쇠 0.01 로만 준다 — 비행이 길수록 더 준다.</summary>
            public double PreOmega;

            /// <summary>접촉 법선 (면 → 공). <c>NormalValid</c> 가 false 면 값이 없다.</summary>
            public double Nx;
            public double Ny;

            /// <summary>
            /// ★ <b>법선이 «유효»한가.</b> 저속·깊은겹침 접촉에서 원본 매니폴드 <c>localNormal</c> 이
            /// <c>[0,0]</c> 으로 나온다 [14회차-b §4-a · 19건 중 5건] — <b>법선을 그대로 믿는 채점기는
            /// 그 구간에서 오판한다.</b> 이 표의 20점에는 없지만 «칸을 만들어 둔다».
            /// </summary>
            public bool NormalValid;

            /// <summary>친 상대 body 중심 (world px).</summary>
            public double OtherX;
            public double OtherY;

            /// <summary>접촉 «그» 프레임의 공 위치 (world px).</summary>
            public double HitX;
            public double HitY;

            /// <summary>발사 → 접촉 프레임 (ms).</summary>
            public double FlightMs;
        }

        /// <summary>
        /// 첫 접촉 직전 20점 (측정 16 + 부존재 4).
        ///
        /// <para>
        /// ⚠ <b>14회차-b 원문의 「n = 15 / 20」·「접촉 없이 6 s — 5 / 20」 은 «셈이 하나씩 어긋났다».</b>
        /// 표를 세면 <b>측정 16 · 부존재 4</b> 다 (부존재는 #5 · #10 · #14 · #15 넉 줄).
        /// <b>파일명·라벨을 고치지 않고 표를 정본으로 쓴다</b> — 이 md 의 원칙(§캡처 목록 C62)과 같다.
        /// </para>
        /// </summary>
        public static List<FirstContactPoint> FirstContacts()
        {
            var list = new List<FirstContactPoint>();

            // ── W1L1
            FC(list, "W1L1", 21.4565, 614.7, 339.259, 526.980, 225.585, 534.175, -3.469, -1.0, 0.0, 425, 525, 337.932, 530.458, 624.5);
            FC(list, "W1L1", 27.4185, 590.9, 370.704, 450.786, 288.267, 387.956, -3.470, -0.239, -0.971, 425, 525, 372.536, 452.522, 601.6);
            FC(list, "W1L1", 38.4185, 1258.2, 708.572, 797.180, 403.917, 1188.146, -3.447, 0.0, -1.0, 675, 877, 711.479, 801.746, 1266.5);
            FC(list, "W1L1", 59.9675, 1482.7, 1135.567, 524.533, 630.475, 1133.086, -3.439, -1.0, 0.0, 1225, 525, 1138.523, 533.486, 1490.5);
            None(list, "W1L1", 87.4583);

            // ── W1L2
            FC(list, "W1L2", 21.4620, 708.4, 368.636, 900.067, 154.349, 638.530, -3.466, 0.0, -1.0, 350, 975, 369.420, 899.472, 716.7);
            FC(list, "W1L2", 26.4945, 834.0, 418.119, 910.458, 190.541, 726.592, -3.462, 0.598, -0.802, 350, 975, 422.078, 909.940, 841.8);
            FC(list, "W1L2", 38.5065, 649.3, 439.104, 648.848, 276.928, 212.646, -3.468, -1.0, 0.0, 525, 675, 437.456, 650.552, 657.7);
            FC(list, "W1L2", 71.4240, 1692.1, 1128.498, 596.246, 513.662, 1126.426, -3.432, 0.0, -1.0, 1125, 675, 1131.876, 599.275, 1700.8);
            None(list, "W1L2", 92.4945);

            // ── W1L4
            FC(list, "W1L4", 20.5380, 574.7, 826.344, 349.108, -277.592, 531.678, -3.471, 0.0, -1.0, 825, 425, 824.451, 350.714, 583.4);
            FC(list, "W1L4", 33.8315, 1199.8, 437.392, 723.494, -457.268, 1254.750, -3.449, 0.963, -0.270, 350, 775, 434.336, 733.893, 1208.2);
            FC(list, "W1L4", 48.9510, 1283.7, 137.091, 523.680, -661.623, 1136.308, -3.446, 1.0, 0.0, 50, 525, 138.344, 530.890, 1292.8);
            None(list, "W1L4", 70.9510);
            None(list, "W1L4", 93.8695);

            // ── W1L5 — ★ 고파워 2점은 «천장»(y=75 블록)을 아래에서 친다 (법선 [0, +1])
            FC(list, "W1L5", 20.0760, 400.1, 333.367, 301.024, 365.586, 389.079, -3.477, 0.0, -1.0, 325, 375, 335.518, 300.263, 409.1);
            FC(list, "W1L5", 32.9130, 1058.3, 821.332, 745.421, 599.349, 1241.116, -3.454, 0.173, -0.985, 775, 825, 825.628, 749.180, 1066.9);
            FC(list, "W1L5", 48.9620, 1066.6, 1138.212, 578.574, 891.603, 1085.133, -3.454, -1.0, 0.0, 1225, 575, 1135.092, 585.677, 1075.0);
            FC(list, "W1L5", 71.4185, 200.0, 447.269, 151.238, 1300.538, -451.016, -3.484, 0.0, 1.0, 475, 75, 457.942, 148.334, 208.5);
            FC(list, "W1L5", 93.3690, 141.8, 428.438, 148.728, 1700.258, -768.944, -3.486, -0.054, 0.999, 475, 75, 438.361, 152.650, 150.2);

            return list;
        }

        private static void FC(List<FirstContactPoint> list, string level, double force, double preT,
                               double px, double py, double vx, double vy, double omega,
                               double nx, double ny, double ox, double oy,
                               double hx, double hy, double flightMs)
        {
            list.Add(new FirstContactPoint
            {
                Level = level,
                Force = force,
                HasContact = true,
                PreTimeMs = preT,
                PreX = px,
                PreY = py,
                PreVx = vx,
                PreVy = vy,
                PreOmega = omega,
                Nx = nx,
                Ny = ny,
                NormalValid = (nx * nx) + (ny * ny) > 1e-12,
                OtherX = ox,
                OtherY = oy,
                HitX = hx,
                HitY = hy,
                FlightMs = flightMs,
            });
        }

        /// <summary>원본에 첫 접촉이 «없는» 샷 — 6 s 채록 전수에서 접촉 0건 [실측].</summary>
        private static void None(List<FirstContactPoint> list, string level, double force)
        {
            list.Add(new FirstContactPoint { Level = level, Force = force, HasContact = false });
        }

        // ══════════════════════════════════════════════ 1·2회차 — |v0| 컬럼 (참고 전용)

        /// <summary>
        /// `07 §2`·`§2-a`·`§5` 의 <c>|v0|</c> 컬럼.
        /// <b>PD 판정: 유도값 · 신뢰도 낮음 — 채점에 쓰지 않는다.</b>
        /// 그래도 «지우지 않는 이유»는 3회차 기전과의 차를 계속 화면에 찍어
        /// 「어디가 얼마나 어긋났나」를 다음 사람이 볼 수 있게 하기 위해서다.
        /// </summary>
        public sealed class LegacySpeed
        {
            public string Level;
            public int NominalHoldMs;
            public double Speed;
            public double AngleDeg;
            public int Round;
        }

        public static List<LegacySpeed> LegacySpeeds()
        {
            return new List<LegacySpeed>
            {
                // §2 · §2-a — 1회차 W1L1 (신규 컨텍스트 첫 샷)
                L("W1L1", 300, 508, 52.80, 1), L("W1L1", 400, 626, 55.52, 1),
                L("W1L1", 500, 761, 56.58, 1), L("W1L1", 600, 915, 57.29, 1),
                L("W1L1", 700, 1034, 57.92, 1), L("W1L1", 900, 1300, 58.65, 1),
                L("W1L1", 1200, 1598, 59.23, 1), L("W1L1", 1500, 1936, 59.46, 1),
                L("W1L1", 1800, 2059, 59.37, 1), L("W1L1", 2000, 2079, 59.40, 1),
                L("W1L1", 3000, 2074, 59.41, 1),

                // §5 — 2회차 W1L2~W1L5
                L("W1L2", 150, 482, 69.99, 2), L("W1L2", 400, 801, 69.89, 2),
                L("W1L2", 800, 1328, 69.91, 2), L("W1L2", 1500, 2081, 69.87, 2),
                L("W1L2", 2500, 2406, 69.83, 2),
                L("W1L3", 150, 473, 59.90, 2), L("W1L3", 400, 816, 59.60, 2),
                L("W1L3", 600, 1018, 59.74, 2), L("W1L3", 600, 1010, 59.89, 2),
                L("W1L3", 600, 1043, 59.84, 2),
                L("W1L4", 100, 415, 130.37, 2), L("W1L4", 100, 433, 130.57, 2),
                L("W1L4", 180, 485, 130.02, 2), L("W1L4", 400, 814, 130.32, 2),
                L("W1L4", 500, 956, 130.32, 2), L("W1L4", 600, 1036, 130.17, 2),
                L("W1L4", 600, 994, 130.20, 2), L("W1L4", 800, 1248, 130.22, 2),
                L("W1L4", 1000, 1521, 130.16, 2), L("W1L4", 2500, 2115, 130.20, 2),
                L("W1L5", 180, 584, 29.67, 2), L("W1L5", 400, 866, 28.84, 2),
            };
        }

        private static LegacySpeed L(string level, int hold, double speed, double angle, int round)
        {
            return new LegacySpeed { Level = level, NominalHoldMs = hold, Speed = speed, AngleDeg = angle, Round = round };
        }

        // ══════════════════════════════════════════════ 궤적 (정본)

        /// <summary>궤적 점열 — `07 §3` · `§5`. 발사 «검출» t=0 기준 (ms, x, y).</summary>
        public sealed class Trajectory
        {
            public string Level;

            /// <summary>골든이 붙인 «라벨» 홀드. 실제와 다를 수 있다 — 아래 <see cref="LabelNote"/>.</summary>
            public int LabelHoldMs;

            public double[] TimeMs;
            public double[] X;
            public double[] Y;

            /// <summary>라벨 어긋남 판정이 있으면 그 내용. 채점은 계속한다.</summary>
            public string LabelNote;

            public string ExcludeReason;
        }

        public static List<Trajectory> Trajectories()
        {
            var list = new List<Trajectory>
            {
                Traj("W1L1", 700, new double[]
                {
                    0,223,446, 200,330,304, 401,438,221, 608,551,198, 808,658,238, 1009,766,337,
                    1209,874,497, 1417,986,727, 1625,838,724, 1825,652,733, 2025,527,732, 2225,632,710,
                    2425,737,747, 2625,842,845, 2825,829,891, 3025,793,875,
                }),
                Traj("W1L1", 900, new double[]
                {
                    0,223,448, 200,355,259, 400,488,130, 600,620,62, 800,753,53, 1008,892,108,
                    1208,1024,222, 1408,1127,389, 1608,1034,568, 1808,941,802, 2008,833,646, 2208,724,551,
                    2408,616,515, 2608,513,537, 2808,593,556, 3008,673,635,
                }),
                Traj("W1L1", 1500, new double[]
                {
                    0,226,446, 200,426,138, 408,634,-120, 608,835,-305, 808,1035,-431, 1008,1235,-497,
                    1208,1435,-502, 1408,1636,-448, 1608,1836,-333, 1816,2044,-151, 2016,2245,86,
                    2216,2445,383, 2417,2645,740, 2625,2854,1176, 2825,3054,1655, 3025,3255,2194,
                }),
                Traj("W1L1", 2000, new double[]
                {
                    0,227,442, 200,437,116, 407,656,-159, 608,866,-362, 816,1085,-509, 1016,1296,-590,
                    1224,1514,-610, 1424,1725,-568, 1624,1935,-466, 1824,2145,-304, 2025,2356,-82,
                    2233,2575,213, 2441,2794,573, 2641,3004,981, 2849,3223,1468, 3050,3434,1999,
                }),
                Traj("W1L1", 3000, new double[]
                {
                    0,227,444, 201,437,118, 408,656,-157, 608,866,-360, 808,1076,-503, 1009,1287,-586,
                    1209,1497,-609, 1409,1707,-572, 1609,1918,-475, 1817,2137,-310, 2025,2356,-80,
                    2225,2566,202, 2425,2776,544, 2625,2986,946, 2825,3197,1408, 3025,3407,1930,
                }),
                Traj("W1L2", 800, new double[]
                {
                    0,260,826, 207,351,609, 415,442,457, 615,530,373, 815,617,349, 1015,705,385,
                    1215,792,480, 1415,880,636, 1615,920,831, 1816,865,1036, 2023,875,1210, 2223,875,1475,
                    2423,875,1801, 2623,875,2187, 2823,875,2633, 3023,875,3139,
                }),
                Traj("W1L4", 180, new double[]
                {
                    0,986,289, 200,918,239, 400,850,250, 601,782,320, 808,721,306, 1009,666,296,
                    1217,609,350, 1418,554,463, 1625,496,644, 1827,443,877, 2034,611,829, 2234,632,830,
                    2442,524,856, 2644,447,872, 2852,448,831, 3059,457,844, 3259,465,918, 3459,467,1146,
                }),
                Traj("W1L5", 180, new double[]
                {
                    0,187,264, 200,289,237, 401,390,270, 607,497,363, 808,586,418, 1008,647,360,
                    1208,709,363, 1417,773,429, 1617,834,554, 1825,898,748, 2025,977,916, 2233,950,984,
                    2441,952,965, 2641,926,1005, 2843,957,1136, 3050,957,1428, 3250,957,1771,
                }),
            };

            // ★ `07 §3` 의 「홀드 1200ms」 블록은 **실제로 1500ms 샷**이다 — 3회차 §5 가 실측으로 확정했다.
            //   ① 3회차 h1200(실홀드 1205) 의 최고점이 **@915ms** 인데 §3 블록은 그 시각에 아직 오르고 있다
            //   ② §3 블록의 궤적 역산 |v0| 1966 이 §2-a 의 1500ms 행(1936)·3회차 h1500 실측(1949)과 1% 안에서 맞는다
            //   → 라벨을 1500 으로 «옮겨» 채점한다. 조용히 빼지 않는다.
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Level == "W1L1" && list[i].LabelHoldMs == 1500)
                {
                    list[i].LabelNote =
                        "`07 §3` 에는 「홀드 1200ms」로 적혀 있으나 3회차 §5 가 «1500ms 샷»으로 확정했다 " +
                        "(3회차 h1200 최고점 @915ms · h1500 |v0| 1949 vs 이 블록 역산 1966). 라벨을 옮겨 채점한다";
                }
            }

            return list;
        }

        private static Trajectory Traj(string level, int hold, double[] flat)
        {
            int n = flat.Length / 3;
            var t = new Trajectory
            {
                Level = level,
                LabelHoldMs = hold,
                TimeMs = new double[n],
                X = new double[n],
                Y = new double[n],
            };

            for (int i = 0; i < n; i++)
            {
                t.TimeMs[i] = flat[(i * 3) + 0];
                t.X[i] = flat[(i * 3) + 1];
                t.Y[i] = flat[(i * 3) + 2];
            }

            return t;
        }

        // ══════════════════════════════════════════════ 클리어 (합격 기준 5)

        /// <summary>
        /// 「입력 → 클리어 여부」 한 점.
        ///
        /// <para>
        /// ★★★ <b>7회차에 «축»이 또 바뀌었다 — 입력은 홀드ms 가 아니라 전역변수 <c>forceShoot_P1</c> 이다.</b>
        /// 3회차가 「공칭 → 실홀드」로 옮긴 것도 <b>여전히 «우리 벽시계»</b>였고, 게임이 실제로 쓴 값은
        /// 런타임에서 <b>직접 읽힌다</b>. 어긋남은 −18 ~ +3ms 다 [7회차 15시행].
        /// </para>
        ///
        /// <para>
        /// 그래서 이 클래스는 축을 <b>둘로 나눠</b> 든다 —
        /// <see cref="Force"/> 가 있으면 <b>그것이 축</b>이고, 없으면 <see cref="HoldMs"/>(우리 시계)다.
        /// <b>모르는 force 를 지어내지 않는다.</b>
        /// </para>
        ///
        /// <para>
        /// 합격 기준 5 = 레벨당 ≥ 3점(실패下-성공-실패上).
        /// ⚠ <b>W1L5 만 「실패下 1 + 성공 2」</b>다 — 실패上이 <b>원리적으로 존재하지 않음</b>이 3회차에 증명됐다.
        /// </para>
        /// </summary>
        public sealed class ClearVector
        {
            public string Level;
            public bool ExpectScored;

            /// <summary>
            /// ★ <b>게임 내부 입력값</b> <c>forceShoot_P1</c> (홀드 중 최댓값). <c>0</c> = 그 벡터는 안 쟀다.
            /// <b>있으면 이것이 축이다</b> — 우리 시계 오차가 통째로 빠진다.
            /// </summary>
            public double Force;

            /// <summary>
            /// <see cref="Force"/> 를 모를 때 쓰는 홀드(ms). <b>«우리 시계»로 잰 값</b>이다.
            /// </summary>
            public int HoldMs;

            /// <summary>
            /// 홀드 축의 <b>불확실 구간</b> (ms, <see cref="HoldMs"/> 에 더한다).
            /// <b>둘 다 0 이 아니면 «양 끝에서 둘 다» 기대값이 나와야 통과</b>다 —
            /// ⚠ 한 값을 고르면 그게 바로 재발방지 #48(골든이 통과하도록 값을 고르는 것)이다.
            /// </summary>
            public int BandMinMs;

            public int BandMaxMs;

            /// <summary>어느 실측에서 온 점인가 — 실패했을 때 원인을 짚으려면 이게 있어야 한다.</summary>
            public string Source;

            /// <summary>축이 «게임 값»이 아니면 그 사실. <c>null</c> = force 축(정본).</summary>
            public string AxisNote;

            /// <summary>이 벡터를 먹일 홀드(ms) 후보. force 축이면 한 점, 구간이면 양 끝이다.</summary>
            public List<double> FeedHoldMs()
            {
                var feeds = new List<double>(2);

                if (Force > 0.0)
                {
                    feeds.Add(HoldMsFromForce(Force));
                    return feeds;
                }

                if (BandMinMs == 0 && BandMaxMs == 0)
                {
                    feeds.Add(HoldMs);
                    return feeds;
                }

                feeds.Add(HoldMs + BandMinMs);
                feeds.Add(HoldMs + BandMaxMs);
                return feeds;
            }

            /// <summary>보고서에 찍을 축 표기.</summary>
            public string AxisLabel()
            {
                if (Force > 0.0)
                    return $"force {Force:0.000} (내부홀드 {HoldMsFromForce(Force):0}ms)";

                if (BandMinMs == 0 && BandMaxMs == 0)
                    return $"홀드 {HoldMs}ms";

                return $"홀드 {HoldMs}+[{BandMinMs},{BandMaxMs}]ms";
            }
        }

        /// <summary>
        /// <c>forceShoot_P1</c> → 홀드ms. 원본 기전 <c>force = 10 + 55 · 홀드초</c> 의 역이다
        /// [3회차 기전 · 7회차 재확인].
        /// </summary>
        public static double HoldMsFromForce(double force)
        {
            return (force - 10.0) / 55.0 * 1000.0;
        }

        /// <summary>
        /// 공칭 홀드 → «게임 내부» 홀드의 <b>실측 바이어스 구간</b> (ms).
        ///
        /// <para>
        /// ⚠⚠ <b>폐기된 「+130ms」의 자리다.</b> 1·2회차가 궤적 <c>vx</c> 역산으로 추정한 그 값은
        /// 7회차가 <b>같은 시행에서 벽시계와 게임 내부 값을 나란히 재</b> 반증했다 —
        /// 공칭 → 벽시계 <b>+3 ~ +24ms</b>, 벽시계 → 내부 <b>−18 ~ +3ms</b>.
        /// </para>
        ///
        /// <para>
        /// ★ 그 폐기된 가정 위에서 만들어진 <c>W1L3 「실홀드 530ms → 미클리어」</c> 는
        /// <b>실제로 쏘니 클리어 3/3</b> 이었다 (`07 §9-d`). <b>추정으로 만든 입력값은 골든에 넣지 않는다.</b>
        /// </para>
        /// </summary>
        public const int NominalToInternalBiasMinMs = 3;

        public const int NominalToInternalBiasMaxMs = 24;

        public static List<ClearVector> Clears()
        {
            var list = new List<ClearVector>();

            // ══════ 7회차가 force 로 «다시 쏜» 벡터 6개 — 이 축이 정본이다 (`07 §9-c`)
            //   각 벡터 3시행의 force 중앙값을 쓴다. 6개 전부 3/3 또는 0/3 으로 «결정적»이었다.

            // ── W1L1 : 실패下 1 · 성공 2 · 실패上 1
            Hold(list, "W1L1", 0, false, "`07 §2` G1 — 0ms 는 발사 자체가 안 된다 [1회차 실측]",
                 "발사 임계 아래라 축이 결과를 바꾸지 않는다");
            Force(list, "W1L1", 50.337, true,
                  "`07 §9-1` — force 50.32~50.78 로 3시행 **클리어 3/3** [7회차 실측] (벽시계 735~742ms)");
            Hold(list, "W1L1", 817, true, "3회차 RUN A — 실홀드 817ms → CLEAR (809·813 포함 3/3)",
                 "3회차 «벽시계» 홀드 — 내부와 −18~+3ms [7회차]");
            Hold(list, "W1L1", 2000, false, "`07 §2` G8/G9 — 캡 구간은 전부 미클리어 [1회차 실측]",
                 "캡(포화 1633ms) «위»라 축이 결과를 바꾸지 않는다");

            // ── W1L2 : ⚠ 7회차가 «안 잰» 레벨이다. force 를 모른다 — 지어내지 않는다.
            Band(list, "W1L2", 150, false, "`07 §5-1` L2-1 h150 — 미클리어(실패下)");
            Hold(list, "W1L2", 805, true, "3회차 RUN D — 실홀드 805ms → CLEAR", "3회차 «벽시계» 홀드");
            Hold(list, "W1L2", 808, true, "3회차 RUN A — 실홀드 808ms → CLEAR (810 포함 3/3)", "3회차 «벽시계» 홀드");
            Hold(list, "W1L2", 2500, false, "`07 §5-1` L2-5 h2500 — 미클리어(실패上)", "캡 위라 축이 무의미");

            // ── W1L3 : ★★ 여기서 정답지가 «정정»됐다 (`07 §9-d`)
            //   옛 벡터 「실홀드 530ms → 미클리어」는 «공칭 400 + 추정 바이어스 130» 으로 만든 라벨이었고,
            //   7회차가 진짜 그 근처(force 39.3~40.3)를 쏘니 **원본이 클리어 3/3** 이었다.
            //   ⇒ 고친 것은 우리 코드가 아니라 정답지다. 근거는 «점수»가 아니라 «실측»이다.
            Force(list, "W1L3", 39.760, true,
                  "★ `07 §9-2` — force 39.34~40.27 로 3시행 **클리어 3/3** [7회차 실측]. " +
                  "옛 골든의 「실홀드 530ms → 미클리어」를 **이 행이 대체한다**");
            Hold(list, "W1L3", 603, true, "3회차 RUN D — 실홀드 603ms → CLEAR", "3회차 «벽시계» 홀드");
            Hold(list, "W1L3", 607, true, "3회차 RUN A — 실홀드 607ms → CLEAR (609 포함 3/3 · 2회차 누적 5/6)",
                 "3회차 «벽시계» 홀드");
            Force(list, "W1L3", 83.793, false,
                  "`07 §9-3` — force 83.34~83.83 로 3시행 **미클리어 0/3** · 아래 감지기 접촉 **0회** [7회차 실측]");

            // ── W1L4 : 성공 구간이 가장 좁은 레벨 — 세 점이 전부 force 축이 됐다
            Hold(list, "W1L4", 0, false, "`07 §5-3` L4-0 h0 — 발사 안 함(실패下) [2회차 실측]",
                 "발사 임계 아래라 축이 결과를 바꾸지 않는다");
            Force(list, "W1L4", 25.153, true,
                  "`07 §9-4` — force 24.63~25.60 로 3시행 **클리어 3/3** [7회차 실측] (벽시계 265~283ms)");
            Force(list, "W1L4", 24.635, true,
                  "`07 §9-5` — force 24.15~25.17 로 3시행 **클리어 3/3** [7회차 실측] (벽시계 260~276ms)");
            Force(list, "W1L4", 39.859, false,
                  "`07 §9-6` — force 39.36~40.17 로 3시행 **미클리어 0/3** · 아래 감지기 접촉 **0회** [7회차 실측]");
            Hold(list, "W1L4", 2500, false, "`07 §5-3` L4-9 h2500 — 미클리어(실패上)", "캡 위라 축이 무의미");

            // ── W1L5 : ★ 실패上이 «원리적으로» 없다 → 「실패下 1 + 성공 2」로 닫는다 (원장 합격 기준 5)
            Hold(list, "W1L5", 0, false, "`07 §5-4` L5-0 h0 — 발사 안 함(실패下) [2회차 실측]",
                 "발사 임계 아래라 축이 결과를 바꾸지 않는다");
            Hold(list, "W1L5", 409, true, "3회차 RUN B — 실홀드 409ms → CLEAR", "3회차 «벽시계» 홀드");
            Hold(list, "W1L5", 8004, true, "3회차 RUN D — 실홀드 8004ms(포화의 4.9배) → CLEAR. " +
                 "«최대 파워로도 실패하지 않는다»의 직접 증거다", "캡 위라 축이 무의미");

            return list;
        }

        /// <summary>★ force 축 — 게임이 «본» 값이다. 채점의 정본.</summary>
        private static void Force(List<ClearVector> list, string level, double force, bool scored, string source)
        {
            list.Add(new ClearVector
            {
                Level = level,
                Force = force,
                ExpectScored = scored,
                Source = source,
                AxisNote = null,
            });
        }

        /// <summary>홀드 축 — <b>우리 시계</b>로 잰 값이거나 공칭이다. 그 사실을 화면에 찍는다.</summary>
        private static void Hold(List<ClearVector> list, string level, int holdMs, bool scored, string source, string axisNote)
        {
            list.Add(new ClearVector
            {
                Level = level,
                HoldMs = holdMs,
                ExpectScored = scored,
                Source = source,
                AxisNote = axisNote,
            });
        }

        /// <summary>
        /// 공칭 축 + <b>실측 바이어스 구간</b>. <b>양 끝에서 둘 다</b> 기대값이 나와야 통과다 —
        /// 한 값을 고르지 않기 위해서다.
        /// </summary>
        private static void Band(List<ClearVector> list, string level, int nominalMs, bool scored, string source)
        {
            list.Add(new ClearVector
            {
                Level = level,
                HoldMs = nominalMs,
                BandMinMs = NominalToInternalBiasMinMs,
                BandMaxMs = NominalToInternalBiasMaxMs,
                ExpectScored = scored,
                Source = source,
                AxisNote = $"공칭 {nominalMs}ms + 실측 바이어스 구간 [+{NominalToInternalBiasMinMs}, " +
                           $"+{NominalToInternalBiasMaxMs}]ms — ⚠ 7회차가 «안 잰» 벡터라 force 를 모른다. " +
                           "구간 양 끝에서 둘 다 기대값이어야 통과",
            });
        }

        // ══════════════════════════════════════════════ 9회차 — 충돌 «접촉점» 정답지 (정본)

        /// <summary>공 반지름(원본 px). 접촉점 상대속도의 팔 길이다 [9회차 §1-d 직접 읽음].</summary>
        public const double ContactBallRadiusPx = 43.5;

        /// <summary>마찰 계수 (혼합 후) [9회차 §1-d].</summary>
        public const double ContactFriction = 0.5;

        /// <summary>
        /// 균일 원판이라 <c>m_eff = m/3</c> — 스틱 한계가 <c>3μΔv_n</c> 인 이유가 이 3 이다.
        /// <b>관성이 ½mR² 가 아니면 이 3 이 다른 수가 된다</b> — 그래서 §0-d 가 관성을 먼저 잰다.
        /// </summary>
        public const double ContactDiskFactor = 3.0;

        /// <summary>
        /// ★★★ 충돌 한 건 — <b>접촉점 기준</b> 정답지 [9회차 §1-b · §5-a · 전수 35건].
        ///
        /// <para>
        /// 좌표는 <b>원본(y 아래가 +)</b> 그대로다. 접선 축은 <c>t = (−n.y, n.x)</c> 이고
        /// 이 규약에서 <c>v_c = v_t − ω·R</c> 이 성립한다 (표의 <c>v_c</c> 컬럼이 그 산술로 닫힌다).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>4회차의 「접선비 −0.56~+3.54」는 축이 틀린 것이었다.</b> 축을 중심속도 <c>v_t</c> 에서
        /// 접촉점 상대속도 <c>v_c</c> 로 옮기면 35건 중 34건이 <b>한 규칙</b>으로 닫힌다.
        /// </para>
        /// </summary>
        public sealed class ContactEvent
        {
            /// <summary>정답지 번호 (§1-b 는 1~24 · §5-a 는 D1~D11).</summary>
            public string Id;

            /// <summary>어느 샷에서 났나 (레벨 · 홀드).</summary>
            public string Shot;

            /// <summary>법선 (면 → 공). 원본 좌표계 · 단위벡터.</summary>
            public double Nx;
            public double Ny;

            /// <summary>법선 방향 속도 (px/s). <b>충돌 전은 음수</b>(면으로 들어간다).</summary>
            public double VnBefore;
            public double VnAfter;

            /// <summary>접선 방향 속도 (px/s) — 축은 <c>t = (−n.y, n.x)</c>.</summary>
            public double VtBefore;
            public double VtAfter;

            /// <summary>각속도 (rad/s). <b>이 값이 이 정답지의 주인공</b>이다.</summary>
            public double OmegaBefore;
            public double OmegaAfter;

            /// <summary>실효 법선 반발 <c>|v_n'/v_n|</c>. 0.70 에서 벗어난 건은 «표본이 어긋난» 건이다.</summary>
            public double En;

            /// <summary><c>"스틱"</c> 또는 <c>"슬립"</c> [9회차 판정].</summary>
            public string Verdict;

            /// <summary>같은 프레임에 잡힌 접촉 수.</summary>
            public int Simultaneous;

            /// <summary>채점 대상인가. <b>false 면 반드시 <see cref="ExcludeReason"/> 가 찍힌다</b> — 조용히 빼지 않는다.</summary>
            public bool Scored;

            /// <summary>채점에서 뺀 이유.</summary>
            public string ExcludeReason;

            /// <summary>접촉점 상대 접선속도 <c>v_c = v_t − ω·R</c> (충돌 «전»).</summary>
            public double VcBefore
            {
                get { return VtBefore - (OmegaBefore * ContactBallRadiusPx); }
            }

            /// <summary>접촉점 상대 접선속도 (충돌 «후»).</summary>
            public double VcAfter
            {
                get { return VtAfter - (OmegaAfter * ContactBallRadiusPx); }
            }

            /// <summary>스틱 한계 <c>3μ(v_n' − v_n)</c>. <c>|v_c| ≤</c> 이면 구름 전이가 일어난다.</summary>
            public double StickLimit
            {
                get { return ContactDiskFactor * ContactFriction * (VnAfter - VnBefore); }
            }

            /// <summary>
            /// ★★ <b>법선이 «유효»한가</b> — <c>localNormal = [0,0]</c> 인 접촉인가.
            ///
            /// <para>
            /// ★★★ <b>[18회차 정정 · 재발방지 #96] 조건은 «저속»도 «후반»도 아니다 — «상대가 원(골대 림)»이다.</b>
            /// 14회차-b 는 「저속·깊은겹침」, 15회차는 「접촉 10회 이후 저속 정착 구간 전용」이라고 적었는데
            /// <b>둘 다 틀렸다.</b> 18회차가 접촉 156건을 <b>상대 body 열로 갈라</b> 보니
            /// 무효 <b>47/47 이 골대 림</b>이고 유효 109건에는 <b>림이 0건</b>이었다
            /// (속도 <b>125~1554 px/s</b> · 접촉 번호 <b>2~28</b> — 저속도 후반도 아니다).
            /// 결정타는 <b>매니폴드 «타입» 직독</b>이다 — <c>manifold.get_type()</c> 이
            /// <c>0 = e_circles</c> ⟺ <c>[0,0]</c> ⟺ 상대 fixture 가 <b>원 r 20.11</b>,
            /// <c>1 = e_faceA</c> ⟺ 유효 법선 ⟺ 상대가 폴리곤(스킨 0.5) 으로 <b>27/27 예외 0건</b>이다.
            /// </para>
            ///
            /// <para>
            /// <b>왜 틀렸었나</b> — 「같이 관측된 성질」(그때 마침 느렸고 그때 마침 후반이었다)로 조건에
            /// 이름을 붙였다. 진짜 축은 <b>「상대가 무엇인가」</b>였고, <b>그 열은 이미 데이터에 있었는데
            /// 갈라 보지 않았다.</b> 틀린 조건을 깐 채점기는 <b>고속 구간(1554 px/s)에서 오판한다.</b>
            /// </para>
            ///
            /// <para>
            /// ⇒ <b>채점 규칙</b> — 「<c>[0,0]</c> 이면 못 믿는다」가 아니라
            /// <b>「상대가 원이면 법선을 <c>normalize(공 중심 − 림 중심)</c> 으로 «만들어» 쓴다」</b>다.
            /// <c>e_circles</c> 매니폴드는 두 원의 중심 차로 그때그때 법선을 만들기 때문에
            /// <c>localNormal</c> 필드를 <b>애초에 채우지 않는다</b> — 버그도 무효 데이터도 아니고,
            /// <b>그 접촉의 값은 전부 유효하다.</b>
            /// </para>
            /// </summary>
            public bool NormalValid
            {
                get { return (Nx * Nx) + (Ny * Ny) > 1e-12; }
            }
        }

        /// <summary>
        /// 35건 전수. <b>이 표가 §8 채점의 정답지</b>다 — 정본은 <c>07_기대값.md §10</c> 이다.
        ///
        /// <para>
        /// 채점 대상 규칙(명시) : <b>① <c>|e_n − 0.700| ≤ 0.01</c></b> 이고
        /// <b>② 그 프레임의 유효 법선이 «하나»</b> 인 건만 센다.
        /// ①은 「표본 창이 어긋났거나 한 프레임에 접촉이 둘」인 건을 거르고 (9회차 §1-e),
        /// ②는 우리 리그가 «면 하나»로 재현하기 때문이다.
        /// </para>
        /// </summary>
        public static List<ContactEvent> Contacts()
        {
            var list = new List<ContactEvent>();

            // ── §1-b · W1L1 (24건)
            C(list, "1", "W1L1 h300", -0.360, -0.933, -460.8, 318.3, 125.8, 25.6, -3.470, 0.700, 0.6907, "스틱", 1);
            C(list, "2", "W1L1 h450", 0.594, -0.805, -231.6, 149.1, 624.8, 441.5, -3.463, 5.947, 0.6439, "슬립", 1,
              "e_n 0.644 — 표본 창이 어긋난 건 (9회차 §1-e 가 판정에서 뺀 건)");
            C(list, "3", "W1L1 h700", 0.0, -1.0, -1308.6, 912.9, 514.3, 292.3, -3.440, 6.738, 0.6976, "스틱", 1);
            C(list, "4", "W1L1 h700", -1.0, 0.0, -292.3, 204.6, 0.6, 76.7, 6.697, 2.061, 0.7000, "스틱", 1);
            C(list, "5", "W1L1 h700", -0.518, -0.855, -620.1, 431.6, -614.7, -389.5, 2.049, -8.844, 0.6961, "스틱", 1);
            C(list, "6", "W1L1 h700", 0.932, -0.363, -694.0, 484.4, 248.1, 57.2, -8.805, 1.041, 0.6979, "스틱", 1);
            C(list, "7", "W1L1 h700", -1.0, 0.0, -472.1, 330.5, -277.7, -190.4, 1.038, -4.106, 0.7000, "스틱", 1);
            C(list, "8", "W1L1 h700", 0.876, -0.482, -549.9, 384.8, 313.9, 164.7, -4.097, 3.587, 0.6999, "스틱", 1);
            C(list, "9", "W1L1 h700", -0.889, -0.459, -402.9, 275.2, 127.7, 107.4, 3.584, 2.977, 0.6830, "스틱", 1,
              "e_n 0.683 — 0.70 에서 0.017 벗어난다 (표본 창)");
            C(list, "10", "W1L1 h700", 0.734, -0.679, -222.4, 153.2, -47.1, 26.0, 2.971, 0.401, 0.6891, "스틱", 1,
              "e_n 0.689 — 0.70 에서 0.011 벗어난다 (표본 창)");
            C(list, "11", "W1L1 h700", -1.000, -0.030, -140.7, 113.1, -348.3, -241.2, 0.399, -5.332, 0.8037, "스틱", 1,
              "e_n 0.804 — 한 프레임에 접촉이 둘 (9회차 §1-e)");
            C(list, "12", "W1L1 h900", -1.0, 0.0, -630.1, 441.0, -1139.2, -828.4, -3.439, -18.823, 0.7000, "스틱", 1);
            C(list, "13", "W1L1 h900", 0.0, -1.0, -1140.9, 800.4, -441.0, -566.0, -18.784, -13.054, 0.7016, "스틱", 1);
            // #14 는 «세로로 겹친 이웃» 두 개를 동시에 치는데 매니폴드 법선이 «완전히 동일»(1,0) 이라
            //     면 하나와 구별되지 않는다 [9회차 §2-b] — 그래서 채점한다.
            C(list, "14", "W1L1 h900", 1.0, 0.0, -566.0, 396.2, 575.8, 216.1, -12.934, 4.691, 0.7000, "스틱", 2);
            C(list, "15", "W1L1 h900", 0.0, -1.0, -590.8, 410.3, 396.2, 331.9, 4.680, 7.634, 0.6945, "스틱", 1);
            C(list, "16", "W1L1 h900", -0.987, 0.159, -200.4, 166.8, -845.2, 459.1, 7.572, 10.883, 0.8326, "스틱", 1,
              "★ 상대가 둘이다 — 림과 블록을 연달아 친다 (9회차 §1-e · 모형이 유일하게 틀린 1건)");
            C(list, "17", "W1L1 h900", 1.0, 0.0, -237.5, 166.3, 360.5, 417.6, 10.826, 9.357, 0.7000, "스틱", 1);
            C(list, "18", "W1L1 h900", 0.317, -0.949, -391.6, 272.7, 306.0, 342.4, 9.354, 7.840, 0.6965, "스틱", 1);
            C(list, "19", "W1L1 h900", -0.484, -0.875, -361.3, 242.1, 269.9, 274.7, 7.823, 6.633, 0.6700, "스틱", 1,
              "e_n 0.670 — 0.70 에서 0.030 벗어난다 (표본 창)");
            C(list, "20", "W1L1 h900", -1.0, 0.0, -123.1, 86.2, 119.8, 154.8, 6.623, 3.884, 0.7000, "스틱", 1);
            C(list, "21", "W1L1 h900", -0.726, -0.687, -226.2, 168.2, -364.4, -192.5, 3.869, -4.349, 0.7434, "스틱", 1,
              "e_n 0.743 — 한 프레임에 접촉이 둘 (9회차 §1-e)");
            C(list, "22", "W1L1 h900", 0.951, -0.309, -323.0, 225.8, 170.3, 67.8, -4.342, 1.320, 0.6991, "스틱", 1);
            C(list, "23", "W1L1 h900", -0.984, -0.179, -257.9, 179.9, -100.9, -69.0, 1.319, -1.286, 0.6976, "스틱", 1);
            C(list, "24", "W1L1 h900", 0.996, 0.085, -170.8, 122.5, 224.7, 150.7, -1.284, 3.188, 0.7170, "스틱", 1,
              "e_n 0.717 — 한 프레임에 접촉이 둘 (9회차 §1-e)");

            // ── §5-a · W1L4 (11건)
            C(list, "D1", "W1L4 h254", 0.0, -1.0, -568.0, 378.4, -332.2, -271.4, -3.468, -6.256, 0.6662, "스틱", 1,
              "e_n 0.666 — 0.70 에서 0.034 벗어난다 (표본 창)");
            C(list, "D2", "W1L4 h254", 0.913, -0.408, -791.1, 658.2, 1104.1, 570.6, -6.185, 14.038, 0.8320, "스틱", 1,
              "e_n 0.832 — 한 프레임에 접촉이 둘");
            C(list, "D3", "W1L4 h254", -0.947, -0.322, -879.9, 618.2, 2.1, 170.5, 14.035, 4.341, 0.7026, "스틱", 1);
            C(list, "D4", "W1L4 h254", 0.656, -0.755, -161.5, 105.6, -562.7, -390.0, 4.337, -2.409, 0.6538, "슬립", 1,
              "e_n 0.654 — 0.70 에서 0.046 벗어난다 (표본 창)");
            C(list, "D5", "W1L4 h254", 0.966, 0.260, -297.8, 209.4, -241.3, -175.5, -2.408, -4.332, 0.7031, "스틱", 1);
            C(list, "D6", "W1L4 h254", -0.776, -0.630, -356.3, 247.0, -45.7, -107.8, -4.321, -2.271, 0.6932, "스틱", 1);
            C(list, "D7", "W1L4 h254", 0.870, -0.494, -306.8, 208.6, 14.3, 5.6, -2.268, -0.335, 0.6800, "스틱", 1,
              "e_n 0.680 — 0.70 에서 0.020 벗어난다 (표본 창)");
            C(list, "D8", "W1L4 h254", -0.926, -0.378, -237.2, 166.2, -94.0, -82.8, -0.335, -1.697, 0.7008, "스틱", 1);
            C(list, "D9", "W1L4 h254", 0.995, -0.101, -205.9, 142.7, 195.1, 137.7, -1.695, 2.604, 0.6927, "스틱", 1);
            // D10 은 «가로로 겹친 이웃»을 동시에 치는데 두 법선이 9.6° 벌어져 있다 [9회차 §5-b].
            //     면 하나짜리 리그로는 «어느 쪽 법선»인지가 결과를 바꾼다 — 그래서 채점하지 않고 리포트만 한다.
            C(list, "D10", "W1L4 h530", 0.166, -0.986, -1342.8, 935.9, -319.4, -363.1, -3.446, -8.362, 0.6970, "스틱", 2,
              "★ 가로 이음매 — 두 법선이 9.6° 다르다 (면 하나짜리 리그로 재현되지 않는다)");
            C(list, "D11", "W1L4 h530", 1.0, 0.0, -202.5, 141.8, -905.2, -711.6, -8.357, -16.237, 0.7000, "슬립", 1);

            return list;
        }

        // ══════════════════════════════════════════════ 14회차-b — «비결정» 구간 (채점 제외 · 정본 `07 §14`)

        /// <summary>
        /// ★★★ <b>원본이 «스스로도 재현하지 못하는» 격자점</b> — 정본은 <c>07_기대값.md §14</c> 다.
        ///
        /// <para>
        /// <b>왜 생겼나.</b> 14회차-b 가 W1L3 에서 <c>force 36.587</c> 을 두 번 쐈다.
        /// 발사 상태가 <b>0.001 px 이내로 같은데 한쪽만 골인</b>했다. 접촉 4까지 같다가
        /// <b>접촉 5(모서리 스침 · 법선 [−0.167, −0.986])에서 33 ms 어긋나고</b> 그 뒤 경로가 통째로 갈린다.
        /// 씨앗은 <b>원본의 가변 rAF dt</b>(7.4~9.1 ms) 다 — Box2D 가 매 프레임 다른 <c>dt</c> 로 적분하니
        /// 같은 초기조건에서 <b>+858 ms 에 0.594 px</b> 이 벌어진다.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>이것을 「우리 결함이 아니다」의 면죄부로 쓰지 마라</b> (재발방지 #48 · #69).
        /// <b>원본의 골인«율»은 아직 미측정</b>이다 — 지금 하는 일은 「원본이 갈리는 자리를 «표시»하는 것」까지다.
        /// 그래서 <b>등재 기준을 «실측 증거가 있는 점»으로만</b> 좁혔다:
        /// 같은 원본을 두 회차가 «반대로» 재고, 그 샷의 <b>원본 접촉 사슬이 5회 이상</b>인 점.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>다른 4레벨은 «미측정»이지 «결정적»이 아니다</b> — 그 레벨들의 원본 접촉 사슬 길이를
        /// 아무도 안 쟀다. <b>미측정을 「제외 안 함」으로 두는 쪽이 안전하다</b>(제외 범위를 넓히면 #48).
        /// </para>
        /// </summary>
        public sealed class NondeterministicPoint
        {
            /// <summary>레벨 코드.</summary>
            public string Level;

            /// <summary>11회차 «격자»의 force 값 — 이 값으로 격자점을 찾는다.</summary>
            public double GridForce;

            /// <summary>14회차-b 가 다시 쏜 force (거의 같은 값이지만 «같지는 않다»).</summary>
            public double RemeasuredForce;

            /// <summary>14회차-b 가 잰 원본 접촉 사슬 길이.</summary>
            public int OriginContacts;

            /// <summary>11회차가 잰 원본 결과.</summary>
            public bool Round11Scored;

            /// <summary>14회차-b 가 잰 원본 결과 (<c>iv[2]</c> 직독).</summary>
            public bool Round14Scored;

            /// <summary>
            /// ★★ <b>원본 골인 «횟수»</b> — 「비결정」과 「원본이 거의 안 넣는다」를 <b>표에서 가른다</b>.
            /// <c>13 % 는 0 % 가 아니다</c> — 갈린다는 사실과 «어느 쪽이 우세한가»는 다른 정보다.
            /// 0 이면 아직 율을 안 쟀다는 뜻이다.
            /// </summary>
            public int ScoredTrials;

            /// <summary>원본 채택 시행 수 (골인율의 분모). 0 이면 미측정.</summary>
            public int TotalTrials;

            /// <summary>골인율 표기 — 리포트에 그대로 찍는다. 분모가 0 이면 「율 미측정」.</summary>
            public string Rate
            {
                get
                {
                    if (TotalTrials <= 0)
                        return "골인율 미측정";

                    return $"원본 골인 {ScoredTrials}/{TotalTrials} " +
                           $"({ScoredTrials * 100.0 / TotalTrials:0} %)";
                }
            }

            public string Evidence;
        }

        /// <summary>
        /// 비결정 등재 목록. <b>전부 W1L3 이고 전부 «두 회차가 반대로 잰» 점</b>이다.
        ///
        /// <para>
        /// ⚠ 두 회차의 방법 차이도 같이 적어 둔다 — 11회차는 릴리즈 후 <c>GetMainRunningLayout()._name</c> 을
        /// 폴링했고, 14회차-b 는 공 인스턴스 변수 <c>iv[2] (hasScored)</c> 를 rAF 마다 직독했다.
        /// <b>14회차 방법이 더 낫다</b>(레벨 전이가 4.1~8.3 s 라 폴링은 골인을 «놓친다»).
        /// 그런데 <b>어긋남의 방향이 반대다</b> — 11회차 O · 14회차 X. 폴링이 놓쳤다면 그 반대여야 한다.
        /// ⇒ <b>방법 차이로는 설명되지 않는다.</b> 남는 설명이 «원본이 갈린다» 하나다.
        /// </para>
        /// </summary>
        public static List<NondeterministicPoint> Nondeterministic()
        {
            // ══════════════════════════════════════════════════════════════════════════════════
            // ★★★ [15회차 정정 · `07 §14-g`] 등재를 «하나»로 줄였다.
            //
            //   왜 등재했었나 : 14회차-b 가 5자리(격자 6자리)를 「11회차 O / 14회차 X」로 «반대로» 재서 올렸다.
            //   왜 푸는가     : 그 판정은 «자리마다 n = 1» 위에 서 있었다. 15회차가 원본에서 124발을 다시 쏘니
            //                   26.9620 이 8/8 · 32.4455 가 3/4 · 21.4565 가 1/1 · 18.2500 이 1/2 골인이다.
            //                   ⇒ 「원본이 갈린다」의 근거가 무너졌다. 갈린 자리는 «우리 결함»이다.
            //
            //   ⚠ 근거는 «점수»가 아니라 «원본 재측정»이다 (재발방지 #48). 제외를 «줄이는» 방향이라
            //      점수는 오히려 나빠진다 — 그래도 이것이 맞다.
            //
            //   ★ 남는 등재 기준 (더 좁혔다 · #83)
            //       ✅ «같은 force 두 시행이 갈리는 것을 직접 본» 자리만
            //       ❌ 두 회차가 각각 n=1 로 «반대로» 잰 자리는 등재하지 않는다
            //
            //   ⚠ 이 자리(W1L3 36.5870)는 11회차 격자에 «없다» — 그래서 실제로 채점에서 빠지는
            //      격자점은 «0 자리»다. 그 사실도 리포트에 그대로 찍는다 (조용한 상한 금지 #74).
            // ══════════════════════════════════════════════════════════════════════════════════
            return new List<NondeterministicPoint>
            {
                new NondeterministicPoint
                {
                    Level = "W1L3",
                    GridForce = 36.5870,
                    RemeasuredForce = 36.5870,
                    OriginContacts = 8,
                    Round11Scored = true,
                    Round14Scored = false,
                    Evidence = "★ 14회차-b 가 같은 force 36.587 을 «두 번» 쐈고 발사 상태가 0.001 px 이내로 같은데 " +
                               "한쪽만 골인했다 (E런 ★골인 +4908 ms / F런 미골인) [`07 §14-c`] — " +
                               "«두 시행이 갈리는 것을 직접 본» 자리다. 접촉 4까지 같다가 " +
                               "접촉 5(모서리 스침 · 법선 [−0.167,−0.986])에서 33 ms 어긋난다. " +
                               "씨앗은 원본의 가변 rAF dt(7.4~9.1 ms) — +858 ms 에 0.594 px 이 벌어진다",
                },

                // ══════════════════════════════════════════════════════════════════════════════
                // ★★★ [18회차 신규 등재 · `07 §14-i`] — «두 번째» 직접 관측 사례.
                //
                //   왜 이제야 등재하나 : 15·16회차는 «홀드 ms» 축으로 쐈고, 그 축은 한 양자(0.46)
                //     아래를 못 겨눈다 — 목표 59.5275 가 매번 59.94(위) 또는 59.05(아래)로 떨어졌다.
                //     18회차가 «홀드 중 forceShoot_P1 을 폴링하다 목표에서 릴리즈»하는 러너로 바꿔
                //     실 force 59.478~59.511 «채택 8발»을 실제로 짚었다.
                //
                //   ★ 직접 관측 : 실 force 59.5000 «세 발»이 2 미골인 ↔ 1 골인으로 갈렸다
                //     (E2#0 미골인 · E2#9 미골인 · E2#7 ★골인 +4463.2 ms).
                //
                //   ⚠ 그리고 «비결정»과 «원본이 거의 안 넣는다»는 다른 정보다 — 여기는 1/8 (13 %) 다.
                //     13 % 는 0 % 가 «아니다». 그래서 율을 같이 들고 다닌다 (Rate).
                //
                //   ⇒ 이 자리는 「미측정」에서 내려와 「비결정」으로 옮겨 왔다.
                //      근거는 «점수»가 아니라 원본 재측정이다 (재발방지 #48).
                // ══════════════════════════════════════════════════════════════════════════════
                new NondeterministicPoint
                {
                    Level = "W1L3",
                    GridForce = 59.5275,
                    RemeasuredForce = 59.5000,
                    OriginContacts = 10,
                    Round11Scored = true,
                    Round14Scored = false,
                    ScoredTrials = 1,
                    TotalTrials = 8,
                    Evidence = "★ [18회차 실측 n = 8 채택] 실 force 59.4780~59.5110 을 여덟 발 쏴서 " +
                               "골인 1 · 미골인 7 (13 %) 이다. ★ 결정타 — 실 force 59.5000 «세 발»이 " +
                               "«2 미골인 ↔ 1 골인»으로 직접 갈렸다 (E2#0 · E2#9 미골인 / E2#7 ★골인 +4463.2 ms) " +
                               "⇒ 14회차-b 가 36.587 하나에서만 봤던 비결정을 «두 번째 자리»에서 재현했다. " +
                               "⚠ 다만 «비결정 = 반반»이 아니다 — 이 자리는 원본이 87 % 못 넣는다. " +
                               "11회차 격자의 「클리어」와 14회차-b 의 「미클리어」는 서로를 반증한 것이 아니라 " +
                               "같은 분포에서 다른 표본을 뽑은 것이다. 구멍 `59.05 < f ≤ 59.96` 안이 " +
                               "«0 %» 가 아니라 «13 %» 임이 이걸로 닫혔다 [`07 §14-i`]",
                },

                // ══════════════════════════════════════════════════════════════════════════════
                // ★★★★ [22회차 신규 등재 · `07 §20-a`] — «세 번째» 직접 관측 사례.
                //   그리고 지금까지 중 «가장 강한» 증거다.
                //
                //   ★ 무엇을 봤나 : 같은 실 force 54.0000 두 발이
                //       발사 위치 (1050.304, 232.109) · v(567.735, −983.345) · ω −3.490659
                //       — 소수 «세 자리»까지 같은데 한 발은 미골인 · 한 발은 ★골인 +3308.8 ms 다.
                //
                //   ⇒ 앞의 두 등재(36.587 · 59.5275)는 「발사 상태가 0.001 px 이내로 같다」였는데
                //      이 자리는 «채록한 소수 자리가 전부 같다». 「원본도 갈린다」의 «직접 증거»다 (#90 요구 충족).
                //
                //   ⚠⚠ 이것이 면죄부가 아닌 이유 — #90 은 「차이가 한 방향으로만 쏠렸으면 계통 오차」라고
                //      못 박는다. 이 자리는 «원본 자신이 양쪽으로 갈렸다» — 한 방향이 아니다.
                //      그리고 골인율을 같이 들고 다닌다(아래 14/16) : 비결정이라고 반반이 아니다.
                //
                //   ⚠ 골인율의 분모 — 15회차 12/13 (`07 §14-g1`) + 22회차 B런 2/3 = «14/16 (88 %)».
                //      22회차 B런 3발 : #0 f 53.9780 ★골인 · #1 f 54.0000 미골인 · #2 f 54.0000 ★골인.
                //      (묶는 규칙은 `07 §14-g1` 그대로 — 실 force 가 격자값 ±0.46 안이면 그 격자에 넣는다)
                //
                //   ★ 그런데 «우리는 이 자리에서 0» 이다. 원본 88 % ↔ 우리 0 % 는 #90 의 계통 오차 지문이기도 하다.
                //      ⇒ 채점에서는 빼되(원본이 스스로 갈리므로) «우리 결함이 아니라는 뜻이 아니다».
                //         이 사실을 리포트가 반드시 같이 찍는다 — 아래 ReportNondeterministicExclusion.
                // ══════════════════════════════════════════════════════════════════════════════
                new NondeterministicPoint
                {
                    Level = "W1L3",
                    GridForce = 54.0000,
                    RemeasuredForce = 54.0000,
                    OriginContacts = 9,
                    Round11Scored = true,
                    Round14Scored = false,
                    ScoredTrials = 14,
                    TotalTrials = 16,
                    Evidence = "★★★★ [22회차 실측 · 결정적 실험] 같은 실 force 54.0000 두 발의 발사 상태가 " +
                               "위치 (1050.304, 232.109) · v(567.735, −983.345) · ω −3.490659 로 " +
                               "«채록한 소수 자리가 전부 같은데» 한 발은 미골인(B#1) · 한 발은 ★골인 +3308.8 ms(B#2) 다. " +
                               "접촉 1 은 둘 다 t+117 fi 14 로 같고 접촉 3 이후에서 갈린다. " +
                               "⇒ 앞 두 등재는 「0.001 px 이내로 같다」였는데 이 자리는 «비트 수준으로 같다» — " +
                               "「원본도 갈린다」의 «직접 증거»다 (재발방지 #90 이 요구하는 근거). " +
                               "⚠ 그래도 면죄부가 아니다 — 원본 골인율은 14/16 (88 %) 인데 우리는 0 이다. " +
                               "«한 방향으로 쏠린 차이»는 #90 이 말하는 계통 오차의 지문이기도 하다. " +
                               "채점에서 빼는 것은 «원본이 자기 자신과 안 맞기 때문»이지 " +
                               "«우리가 맞다는 뜻이 아니다» [`07 §20-a`]",
                },
            };
        }

        /// <summary>
        /// ★★★★ <b>「도달 불가」 격자점</b> — 22회차 신설. <b>「미측정」과 «다르다»</b> (`07 §20-b` · 재발방지 #112).
        ///
        /// <para>
        /// <b>미측정</b>은 «아직 안 잰 것»이고 처방은 「가서 재라」다.
        /// <b>도달 불가</b>는 <b>«원리적으로 못 내는 것»</b>이고 — 가서 재도 안 나온다.
        /// </para>
        ///
        /// <para>
        /// 왜 못 내나 : 원본의 <c>forceShoot_P1</c> 은 틱마다 <c>+= 55·dt</c> 로 «누적»된다.
        /// ⇒ 도달 가능한 force 가 <b>이산 격자</b> 위에 놓이고, 그 격자는 «홀드 시작의 프레임 위상»에 물린다.
        /// 격자 간격보다 <b>위상 드리프트 폭이 작으면 격자 사이의 값은 영영 안 나온다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠⚠ <b>등재 조건을 좁게 못 박는다</b> (#48 · #74) — <b>서브프레임 오프셋 스윕을 실제로 돌려</b>
        /// ① 격자 시작·간격을 «전 표본 채록»으로 산출하고 ② <b>드리프트 폭 &lt; 필요한 Δ</b> 를 수치로 보인 자리만이다.
        /// 「몇 번 쐈는데 안 나왔다」는 <b>미측정</b>이지 도달 불가가 아니다.
        /// </para>
        /// </summary>
        public sealed class UnreachablePoint
        {
            public string Level;
            public double GridForce;

            /// <summary>실측한 격자 «간격» (force). 이 값보다 드리프트가 작으면 사이 값을 못 낸다.</summary>
            public double GridSpacing;

            /// <summary>실측한 «최근접» 격자값까지의 최소 Δ — 이 값이 드리프트 폭보다 크면 도달 불가다.</summary>
            public double NearestDelta;

            /// <summary>
            /// 서브프레임 위상 드리프트 <b>«전» 폭</b> (force) — 격자 시작값이 시행마다 흔들리는 폭이다.
            /// <see cref="NearestDelta"/> 와 <b>이 값을 그대로</b> 비교한다 (± 반값이 아니다).
            /// </summary>
            public double DriftSpan;

            /// <summary>스윕 시행 수 (도달 «성공» 0회의 분모).</summary>
            public int SweepTrials;

            public string Evidence;
        }

        /// <summary>
        /// 도달 불가 등재 목록. <b>지금 «한 자리»뿐이다</b>.
        ///
        /// <para>
        /// ⚠ <b>W1L1 66.3860 은 여기 «없다»</b> — 19회차가 그 값을 <b>소수 4자리까지 그대로 짚어</b>
        /// ★골인 +4318.2 ms 를 쟀다 (`07 §18-b1` F1#1). <b>드물지만 도달한다</b> ⇒ 채점 대상으로 «남긴다».
        /// 22회차 A런 3발도 66.3035 · 66.4025 · 66.3695 로 «전부 원본 골인»이라 그 자리의 답은 확정돼 있다.
        /// <b>제외 범위를 넓히지 않는다</b> (#48 · #74).
        /// </para>
        /// </summary>
        public static List<UnreachablePoint> Unreachable()
        {
            return new List<UnreachablePoint>
            {
                new UnreachablePoint
                {
                    Level = "W1L3",
                    GridForce = 65.9845,
                    GridSpacing = 0.4455,
                    NearestDelta = 0.0495,
                    DriftSpan = 0.0440,   // 격자 시작 10.451 ~ 10.495 의 «전» 폭 (= ±0.0220)
                    SweepTrials = 14,
                    Evidence = "★★★★ [22회차 실측 · 서브프레임 오프셋 스윕 n = 14] 홀드 1120 ms 를 " +
                               "프레임 «안»에서 옮겨 가며 14회 쏘고 홀드 중 forceShoot_P1 을 전 표본 채록했다 " +
                               "(회당 표본 135). 도달 가능한 격자 — 시작 10.451~10.495 · 간격 0.4455~0.4675. " +
                               "★ 목표 65.9845 최근접값은 65.9075~65.9350 이고 «Δ 0.0495 ~ 0.0770», " +
                               "★★ «격자에 정확히 있음(Δ ≤ 0.005) 0/14 · Δ ≤ 0.05 조차 1/14» 이다. " +
                               "위상 드리프트 폭(격자 시작이 흔들리는 폭)은 0.044 = ±0.022 인데 " +
                               "필요한 Δ 가 0.0495 라 «드리프트로도 못 덮는다» ⇒ 120 Hz 환경에서 원리적으로 못 낸다. " +
                               "⚠ 19회차가 BIAS 0.30 → 0.05 로도 0/6 이었던 것(§19-d)과 정합한다 — " +
                               "필요한 것은 바이어스가 아니었다. " +
                               "⚠ 「미측정」이 아니다 — 가서 재도 안 나온다 [`07 §20-b` · 재발방지 #112]",
                },
            };
        }

        /// <summary>이 (레벨, force) 격자점이 «도달 불가 등재»인가.</summary>
        public static UnreachablePoint FindUnreachable(string level, double gridForce)
        {
            List<UnreachablePoint> all = Unreachable();

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Level == level && Math.Abs(all[i].GridForce - gridForce) <= 1e-9)
                    return all[i];
            }

            return null;
        }

        /// <summary>
        /// ★★ <b>「미측정」 격자점</b> — 「비결정」과 «다르다» (`07 §14-g2`).
        ///
        /// <para>
        /// 「비결정」은 <b>원본이 같은 입력에 다른 답을 낸다</b>는 «관측»이고,
        /// 「미측정」은 <b>그 격자값의 원본 답이 아직 없다</b>는 «부재»다.
        /// 둘 다 채점에서 빠지지만 <b>처방이 다르다</b> — 비결정은 「더 쏴 보라」, 미측정은 <b>「가서 재라」</b>다.
        /// 합쳐서 「제외 N건」으로 찍으면 <b>조용한 상한</b>이 된다 (재발방지 #74).
        /// </para>
        /// </summary>
        public sealed class UnmeasuredPoint
        {
            public string Level;
            public double GridForce;
            public string Reason;
        }

        public static List<UnmeasuredPoint> Unmeasured()
        {
            // ══════════════════════════════════════════════════════════════════════════════════
            // ★★★ [18회차 정정] 이 목록은 «비었다» — 유일한 등재였던 W1L3 59.5275 가 실제로 측정됐다.
            //
            //   15·16회차가 못 짚은 이유는 «축»이었다. 홀드 ms 축은 한 양자(force 0.46) 아래를
            //   못 겨눈다 — 892 ms 는 59.94(위), 880 ms 는 59.05(아래)로 떨어졌다.
            //   18회차가 «홀드 중 forceShoot_P1 폴링 → 목표 force 에서 릴리즈» 러너로 바꾸니
            //   실 force 59.478~59.511 을 여덟 발 짚었다 (골인 1/8 = 13 %).
            //
            //   ⇒ 그 자리는 「미측정」이 아니라 「비결정(미골인 우세)」이다 ⇒ Nondeterministic() 로 옮겼다.
            //
            //   ⚠ 목록을 «지우지 않고 비워 둔다» — 다음 회차가 또 미측정 자리를 만나면 여기 올린다.
            //      그리고 리포트는 「미측정 0건 제외」를 그대로 찍어야 한다 (조용한 상한 금지 #74).
            // ══════════════════════════════════════════════════════════════════════════════════
            return new List<UnmeasuredPoint>();
        }

        /// <summary>이 (레벨, force) 격자점이 «미측정 등재»인가.</summary>
        public static UnmeasuredPoint FindUnmeasured(string level, double gridForce)
        {
            List<UnmeasuredPoint> all = Unmeasured();

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Level == level && Math.Abs(all[i].GridForce - gridForce) <= 1e-9)
                    return all[i];
            }

            return null;
        }

        /// <summary>
        /// 이 (레벨, force) 격자점이 «비결정 등재»인가. 격자 force 는 소수 4자리로 적혀 있어
        /// <b>정확 비교</b>가 성립한다 — 근사 매칭으로 두면 제외 범위가 조용히 넓어진다 (#48).
        /// </summary>
        public static NondeterministicPoint FindNondeterministic(string level, double gridForce)
        {
            List<NondeterministicPoint> all = Nondeterministic();

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Level == level && Math.Abs(all[i].GridForce - gridForce) <= 1e-9)
                    return all[i];
            }

            return null;
        }

        private static void C(List<ContactEvent> list, string id, string shot,
                              double nx, double ny,
                              double vnBefore, double vnAfter,
                              double vtBefore, double vtAfter,
                              double omegaBefore, double omegaAfter,
                              double en, string verdict, int simultaneous,
                              string excludeReason = null)
        {
            list.Add(new ContactEvent
            {
                Id = id,
                Shot = shot,
                Nx = nx,
                Ny = ny,
                VnBefore = vnBefore,
                VnAfter = vnAfter,
                VtBefore = vtBefore,
                VtAfter = vtAfter,
                OmegaBefore = omegaBefore,
                OmegaAfter = omegaAfter,
                En = en,
                Verdict = verdict,
                Simultaneous = simultaneous,
                Scored = excludeReason == null,
                ExcludeReason = excludeReason,
            });
        }

        // ══════════════════════════════════════════════ 15회차 — W1L3 «접촉 2~5번째» 정답지 (정본 `07 §15`)

        /// <summary>
        /// ★★★ <b>W1L3 접촉 2~5번째 정답지</b> — 정본은 <c>07_기대값.md §15</c> 다.
        ///
        /// <para>
        /// ⚠⚠ <b><c>t</c> 의 기준점이 «발사 프레임»이다</b> — 9·13회차(<c>§10</c>)의 «홀드 시작»과 <b>다르다.</b>
        /// 섞으면 홀드 길이만큼 통째로 어긋난 «가짜 결론»이 나온다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>이 구간은 원본이 «결정적»이다 — 그래서 채점해도 된다.</b> 같은 force 두 시행
        /// (A#2↔A#3 · 26.5 두 시행)의 접촉 2~5 가 <b>시각 0.6~1.5 ms · 위치 0.3 px 안에서 겹친다.</b>
        /// 갈림은 더 뒤에서 생긴다 ⇒ <b>여기에는 «비결정 면죄부»가 붙지 않는다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>동시 접촉이 28건 중 17건</b>이다 (블록 충돌상자 84×58.98 이 격자 50 보다 커서 이웃끼리 겹친다).
        /// <b>채점기가 «한 접촉만» 골라 비교하면 오판한다</b> — 같은 시각의 접촉을 <b>묶어서</b> 대조해야 한다.
        /// 그래서 이 구조체는 법선·상대 body 를 <b>목록</b>으로 들고 있다.
        /// </para>
        ///
        /// <para>
        /// ★ <c>localNormal = [0,0]</c>(무효 법선)은 <b>아래 28건에 0/28</b> 이다 —
        /// <b>이 28건의 상대가 전부 «블록(폴리곤)»이기 때문</b>이다. <b>여기선 법선을 믿어라.</b>
        /// <br/>
        /// ⚠⚠ <b>[18회차 정정 · #96] 「무효 법선 = 접촉 10회 이후 저속 전용」은 «틀렸다».</b>
        /// 진짜 조건은 <b>«상대가 골대 림(원)»</b>이고 <b>사슬 어디에서나 나온다</b> —
        /// 18회차 C#0 은 <b>접촉 2</b>(속도 <b>1554 px/s</b>)가 이미 림이다.
        /// 「접촉 번호로 걸러도 안전하다」는 가정을 깐 채점기는 <b>고속·초반 구간에서 오판한다.</b>
        /// </para>
        ///
        /// <para>
        /// ★ <b>「이 구간은 결정적이다」도 «이 구간에 한해서만» 참이다</b> [18회차].
        /// 같은 force <c>21.4565</c> 두 시행의 <b>접촉 6</b> 이 <b>시각 400 ms · 위치 5 px</b> 갈린다.
        /// 결정성은 <b>접촉 번호와 «함께»</b> 적어야 한다 — <c>Contact612Structure</c> 를 보라.
        /// </para>
        /// </summary>
        public sealed class Contact25Event
        {
            /// <summary>샷 식별자 — 같은 force 두 시행을 가른다.</summary>
            public string ShotId;

            public string Level;

            /// <summary>원본이 실제로 쓴 force (격자값이 아니다).</summary>
            public double Force;

            /// <summary>원본 홀드(ms) — 참고용. 우리는 <c>Force</c> 로 먹인다.</summary>
            public int OriginHoldMs;

            /// <summary>원본이 골인한 시각(발사 기준 ms). 이 7샷은 <b>전부 골인</b>이다.</summary>
            public double OriginGoalMs;

            /// <summary>원본의 전체 접촉 «사건» 수.</summary>
            public int OriginContacts;

            /// <summary>접촉 번호 (2 ~ 5).</summary>
            public int Index;

            /// <summary>★ <b>발사 프레임 기준</b> 접촉 시각(ms).</summary>
            public double TimeMs;

            /// <summary>접촉 «사건»이 이어진 프레임 수.</summary>
            public int Frames;

            /// <summary>접촉 구간 «직전» 프레임의 공 body 위치 (원본 px · y 아래 +).</summary>
            public double PreX;
            public double PreY;

            /// <summary>사영 축 = <b>첫 법선</b>. 부호는 <c>VnBefore &lt; 0</c>(접근) 이 되도록 통일돼 있다.</summary>
            public double VnBefore;
            public double VnAfter;
            public double VtBefore;
            public double VtAfter;
            public double OmegaBefore;
            public double OmegaAfter;

            /// <summary>그 프레임의 법선 «전부» (동시 접촉이면 2개 이상). <c>[0]</c> 이 사영 축이다.</summary>
            public double[] Nx;
            public double[] Ny;

            /// <summary>상대 body 중심 «전부» (법선과 같은 순서).</summary>
            public double[] OtherX;
            public double[] OtherY;

            /// <summary>동시 접촉 개수.</summary>
            public int Simultaneous
            {
                get { return Nx == null ? 0 : Nx.Length; }
            }

            /// <summary>반발 계수 — 28/28 이 0.692 ~ 0.700 이다.</summary>
            public double En
            {
                get { return Math.Abs(VnBefore) < 1e-9 ? 0.0 : Math.Abs(VnAfter) / Math.Abs(VnBefore); }
            }
        }

        /// <summary>
        /// 접촉 2~5 정답지 <b>28건 / 7샷</b> (<c>07 §15-b</c> ~ <c>§15-h</c>).
        /// <b>force 는 5가지</b>이고 그 중 둘은 «같은 force 를 두 번 쏜 재현 시행»이다 —
        /// 우리 쪽은 결정적이라 한 번만 돌면 되고, <b>두 시행의 폭이 곧 «원본 자신의 산포»</b>다.
        /// </summary>
        public static List<Contact25Event> Contacts25()
        {
            var list = new List<Contact25Event>(28);

            // ── 15-b. force 17.3315 (홀드 125 · ★골인 +5733 ms · 접촉 20회) — 샷 A#2
            K(list, "A#2", 17.3315, 125, 5733, 20, 2,  600.1, 1, 1083.998, 300.048, -548.3, -127.6, -4.559,  379.7, -151.0, -3.482,
              N(0, -1),                 B(1050, 375));
            K(list, "A#2", 17.3315, 125, 5733, 20, 3, 1125.1, 1, 1004.815, 300.802, -382.6, -151.0, -3.464,  264.6, -150.8, -3.476,
              N(0, -1, -0.086, -0.996), B(1000, 375, 1050, 375));
            K(list, "A#2", 17.3315, 125, 5733, 20, 4, 1500.2, 1,  948.279, 302.027, -291.8, -110.2, -3.463,  200.7, -120.8, -2.817,
              N(0.143, -0.99, 0, -1),   B(900, 375, 950, 375));
            K(list, "A#2", 17.3315, 125, 5733, 20, 5, 1809.1, 1,  920.278, 301.087, -222.0,  -90.8, -2.809,  151.7, -101.2, -2.333,
              N(0, -1, 0, -1),          B(900, 375, 950, 375));

            // ── 15-c. force 17.3315 (홀드 125 · ★골인 +5842 ms · 접촉 22회) — 샷 A#3 · A#2 의 «재현»
            K(list, "A#3", 17.3315, 125, 5842, 22, 2,  599.5, 1, 1084.024, 299.947, -548.0, -127.6, -4.557,  380.0, -151.0, -3.481,
              N(0, -1),                 B(1050, 375));
            K(list, "A#3", 17.3315, 125, 5842, 22, 3, 1124.6, 1, 1004.850, 300.767, -382.5, -151.0, -3.464,  264.1, -150.7, -3.475,
              N(0, -1, -0.086, -0.996), B(1000, 375, 1050, 375));
            K(list, "A#3", 17.3315, 125, 5842, 22, 4, 1499.9, 1,  948.311, 302.224, -292.5, -109.8, -3.463,  201.0, -120.2, -2.814,
              N(0.144, -0.99, 0, -1),   B(900, 375, 950, 375));
            K(list, "A#3", 17.3315, 125, 5842, 22, 5, 1807.6, 1,  920.557, 300.739, -221.6,  -90.0, -2.806,  151.9, -100.6, -2.320,
              N(0, -1, 0, -1),          B(900, 375, 950, 375));

            // ── 15-d. force 19.163 (홀드 167 · ★골인 +5867 ms · 접촉 16회)
            K(list, "C", 19.163, 167, 5867, 16, 2,  624.8, 1, 1072.556, 301.498, -580.0, -141.0, -3.282,  402.6, -141.5, -3.262,
              N(0, -1),                 B(1050, 375));
            K(list, "C", 19.163, 167, 5867, 16, 3, 1183.6, 1,  993.558, 300.823, -413.4, -131.2, -3.244,  285.7, -144.8, -3.343,
              N(0.025, -1, 0, -1),      B(950, 375, 1000, 375));
            K(list, "C", 19.163, 167, 5867, 16, 4, 1591.5, 1,  937.404, 302.143, -298.3, -137.5, -3.330,  205.2, -139.6, -3.233,
              N(0, -1, 0, -1),          B(950, 375, 900, 375));
            K(list, "C", 19.163, 167, 5867, 16, 5, 1883.1, 2,  896.645, 300.668, -207.6, -139.6, -3.224,  147.4, -122.3, -3.137,
              N(0, -1, 0.084, -0.996),  B(900, 375, 850, 375));

            // ── 15-e. force 19.625 (홀드 167 · ★골인 +6634 ms · 접촉 22회)
            K(list, "D", 19.625, 167, 6634, 22, 2,  624.5, 1, 1070.433, 298.509, -578.5, -144.4, -2.961,  401.6, -139.1, -3.206,
              N(0, -1),                 B(1050, 375));
            K(list, "D", 19.625, 167, 6634, 22, 3, 1174.5, 1,  993.885, 300.450, -402.7, -127.5, -3.189,  278.3, -142.5, -3.295,
              N(0.029, -1, 0, -1),      B(950, 375, 1000, 375));
            K(list, "D", 19.625, 167, 6634, 22, 4, 1566.1, 1,  941.254, 300.311, -280.5, -134.3, -3.282,  193.0, -137.0, -3.159,
              N(0, -1, 0, -1),          B(900, 375, 950, 375));
            K(list, "D", 19.625, 167, 6634, 22, 5, 1841.4, 1,  903.607, 301.657, -194.6, -137.0, -3.150,  132.3, -136.9, -3.153,
              N(0, -1),                 B(900, 375));

            // ── 15-f. force 26.500 (홀드 300 · ★골인 +5375 ms · 접촉 14회)
            K(list, "E#1", 26.500, 300, 5375, 14, 2,  700.1, 1, 1024.549, 300.880, -649.9, -195.0,  0.865,  451.4, -117.2, -2.702,
              N(0, -1, 0, -1),          B(1000, 375, 1050, 375));
            K(list, "E#1", 26.500, 300, 5375, 14, 3, 1325.0, 1,  951.154, 301.943, -461.1, -117.2, -2.686,  319.1, -116.9, -2.700,
              N(0, -1),                 B(950, 375));
            K(list, "E#1", 26.500, 300, 5375, 14, 4, 1775.6, 1,  898.556, 301.745, -330.8, -116.9, -2.689,  227.7, -116.8, -2.692,
              N(0, -1),                 B(900, 375));
            K(list, "E#1", 26.500, 300, 5375, 14, 5, 2100.1, 1,  860.585, 300.882, -234.9, -116.8, -2.684,  160.4, -116.7, -2.690,
              N(0, -1, 0, -1),          B(900, 375, 850, 375));

            // ── 15-g. force 26.500 (홀드 300 · ★골인 +5534 ms · 접촉 15회) — E#1 의 «재현»
            K(list, "E#2", 26.500, 300, 5534, 15, 2,  700.4, 1, 1024.549, 300.834, -649.8, -195.0,  0.860,  451.1, -117.3, -2.704,
              N(0, -1, 0, -1),          B(1000, 375, 1050, 375));
            K(list, "E#2", 26.500, 300, 5534, 15, 3, 1325.1, 1,  951.083, 302.114, -461.5, -117.3, -2.687,  319.3, -116.9, -2.705,
              N(0, -1, -0.157, -0.988), B(950, 375, 1000, 375));
            K(list, "E#2", 26.500, 300, 5534, 15, 4, 1775.2, 1,  898.489, 301.915, -330.8, -116.9, -2.694,  228.2, -116.8, -2.697,
              N(0, -1),                 B(900, 375));
            K(list, "E#2", 26.500, 300, 5534, 15, 5, 2100.1, 1,  860.533, 300.862, -234.3, -116.8, -2.689,  160.1, -116.7, -2.691,
              N(0, -1, 0, -1),          B(900, 375, 850, 375));

            // ── 15-h. force 53.9945 (홀드 792 · ★골인 +4350 ms · 접촉 10회) — «비스듬한 법선»이 섞이는 샷
            K(list, "G", 53.9945, 792, 4350, 10, 2, 1257.4, 2,  624.919, 562.660, -307.4, -1262.1,  11.109,  205.7, -1027.0,  -1.122,
              N(-0.838, -0.545),        B(700, 625));
            K(list, "G", 53.9945, 792, 4350, 10, 3, 1299.1, 1,  596.192, 597.581, -774.1,  -732.6,  -1.122,  537.8,  -503.7, -11.615,
              N(0, -1),                 B(600, 675));
            K(list, "G", 53.9945, 792, 4350, 10, 4, 1950.1, 1,  267.501, 560.315, -629.6,  -165.9, -11.542,  437.8,  -265.7,  -6.313,
              N(0.587, -0.81),          B(200, 625));
            K(list, "G", 53.9945, 792, 4350, 10, 5, 2724.6, 1,  299.809, 600.710, -609.5,   153.9,  -6.265,  422.9,  -131.8,  -1.430,
              N(0.18, -0.984, 0, -1),   B(250, 675, 300, 675));

            return list;
        }

        /// <summary>법선 목록 — 짝수 개 인자를 (x, y) 쌍으로 읽는다.</summary>
        private static double[] N(params double[] xy)
        {
            return xy;
        }

        /// <summary>상대 body 중심 목록 — 짝수 개 인자를 (x, y) 쌍으로 읽는다.</summary>
        private static double[] B(params double[] xy)
        {
            return xy;
        }

        private static void K(List<Contact25Event> list, string shotId, double force, int holdMs,
                              double goalMs, int originContacts, int index,
                              double timeMs, int frames, double preX, double preY,
                              double vnBefore, double vtBefore, double omegaBefore,
                              double vnAfter, double vtAfter, double omegaAfter,
                              double[] normals, double[] others)
        {
            int pairs = normals.Length / 2;
            var nx = new double[pairs];
            var ny = new double[pairs];

            for (int i = 0; i < pairs; i++)
            {
                nx[i] = normals[i * 2];
                ny[i] = normals[(i * 2) + 1];
            }

            int otherPairs = others.Length / 2;
            var ox = new double[otherPairs];
            var oy = new double[otherPairs];

            for (int i = 0; i < otherPairs; i++)
            {
                ox[i] = others[i * 2];
                oy[i] = others[(i * 2) + 1];
            }

            list.Add(new Contact25Event
            {
                ShotId = shotId,
                Level = "W1L3",
                Force = force,
                OriginHoldMs = holdMs,
                OriginGoalMs = goalMs,
                OriginContacts = originContacts,
                Index = index,
                TimeMs = timeMs,
                Frames = frames,
                PreX = preX,
                PreY = preY,
                VnBefore = vnBefore,
                VtBefore = vtBefore,
                OmegaBefore = omegaBefore,
                VnAfter = vnAfter,
                VtAfter = vtAfter,
                OmegaAfter = omegaAfter,
                Nx = nx,
                Ny = ny,
                OtherX = ox,
                OtherY = oy,
            });
        }

        // ══════════════════════════════════════════════ 18회차 — 접촉 «6~12» 구조 정답지 (정본 `07 §16`)

        /// <summary>
        /// ★★★ <b>접촉 6 이후는 «값» 채점이 원리적으로 불가능하다</b> [18회차 실측 · 정본 <c>07 §16</c>].
        ///
        /// <para>
        /// <b>왜 «구조»인가.</b> 같은 force <c>21.4565</c> 두 시행(A#0 · A2#0)의 <b>접촉 6</b> 이
        /// 시각 <c>+2574.2 ↔ +2173.9</c>(<b>400 ms 차</b>) · 위치 <c>(861.7, 302.1) ↔ (856.1, 302.1)</c>
        /// (<b>5 px 차</b>)이고 접촉 수도 <b>17 ↔ 15</b>, 골 시각도 <b>+6066.6 ↔ +5774.6</b> 이다.
        /// ⇒ <b>원본이 «자기 자신»과도 값이 안 맞는다.</b> 이 구간에 값 허용오차를 매기는 것은
        /// 원본에 없는 정밀도를 우리에게 요구하는 것이다 (재발방지 #48 의 거울상).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>같이 무너지는 것</b> — 15회차의 <c>e_n = 0.692~0.700 (28/28)</c> 도 <b>접촉 2~5 전용</b>이다.
        /// 접촉 6 이후에는 <b>0.38 ~ 7.75</b> 로 흩어진다(A#0 #10 = 0.382 · A2#0 #10 = 7.748).
        /// 저속 접촉에서 <c>v_n 후</c> 가 <b>음수</b>(A#0 #11: −24.5 → −17.7 · 파고든 채 프레임을 넘긴다)로
        /// 나오는 <b>정착(resting) 구간</b>이 섞이기 때문이다. <b>단일 반발계수로 안 적힌다.</b>
        /// </para>
        ///
        /// <para>
        /// ⇒ <b>그래서 채점 가능한 것은 «구조»뿐이다</b> — 접촉 k 의 <b>상대가 무엇 계열인가</b>
        /// (골대 림인가 · 블록이면 어느 «행 y»인가) · <b>접촉 총수가 범위 안인가</b> · <b>결과(골인)가 같은가</b>.
        /// 원본 자신이 시행마다 갈리므로 <b>정답은 «한 값»이 아니라 «시행들의 합집합»</b>이다.
        /// </para>
        /// </summary>
        public sealed class Contact612Structure
        {
            public string Level;
            public double Force;

            /// <summary>원본 채택 시행 수 (골인율의 분모).</summary>
            public int Trials;

            /// <summary>원본 골인 시행 수.</summary>
            public int Scored;

            /// <summary>원본 접촉 «총수» 범위 — 이 밖이면 사슬 길이가 통째로 다르다.</summary>
            public int MinContacts;

            public int MaxContacts;

            /// <summary>원본 골 시각 범위 (ms · 발사 프레임 기준). 미골인 우세 자리는 <c>-1</c>.</summary>
            public double MinGoalMs;

            public double MaxGoalMs;

            /// <summary>
            /// 접촉 <b>6~12</b> 의 «계열» — <c>Series[k]</c> 가 <b>접촉 (6 + k)</b> 에서 원본이
            /// <b>실제로 친 것들의 합집합</b>이다. 원소는 <c>"림"</c> 또는 <c>"행 375"</c> 같은 <b>블록 행 y</b>.
            /// <c>null</c> 이면 그 번호는 시행마다 존재 여부가 갈려 <b>채점하지 않는다</b>.
            /// <br/>
            /// ⚠⚠ <b>[19회차] 배열 «전체»가 <c>null</c> 이면 «번호별 채점 자체»를 끈다</b> —
            /// 원본이 그 번호에서 자기 자신과도 상대가 갈리기 때문이다. 이유는 <see cref="IndexGradingDisabledWhy"/>.
            /// </summary>
            public string[][] Series;

            /// <summary>
            /// ★★★ <b>값 채점의 «경계»</b> — 이 접촉 번호<b>까지</b>는 값으로 채점할 수 있고 그 너머는 구조뿐이다.
            /// <c>0</c> 이면 이 샷에 대해 경계를 «안 쟀다»는 뜻이다 (<c>GoldenVectors.ValueGradeBoundaries()</c> 가 정본).
            /// </summary>
            public int ValueGradeMaxIndex;

            /// <summary>
            /// ★★★ <b>번호별 계열 채점을 «끈» 이유</b> [19회차].
            ///
            /// <para>
            /// <c>Series</c> 가 <c>null</c> 인 샷은 여기에 <b>왜 껐는지</b>가 «반드시» 적혀 있어야 한다 —
            /// 이유 없이 꺼진 채점은 <b>면죄부</b>가 되고, 그건 #48 의 거울상이다.
            /// </para>
            /// </summary>
            public string IndexGradingDisabledWhy;

            /// <summary>
            /// ★★ <b>«꼬리 구간»(접촉 6 이후) 상대 계열의 «합집합»</b> — 번호를 버리고 <b>집합으로만</b> 본다.
            ///
            /// <para>
            /// 번호별 채점이 꺼진 샷에서 <b>그래도 잴 수 있는 것</b>이다. 원본 시행마다 «순서»는 갈리지만
            /// «어떤 것들을 치는가»는 이 집합 안이다. 우리 꼬리 접촉이 이 집합 <b>밖</b>으로 나가면
            /// 그건 원본이 한 번도 안 간 자리라 <b>진짜 어긋남</b>이다.
            /// </para>
            ///
            /// <para>
            /// ⚠ <b>합집합을 «좁히지» 마라</b> (#48). 넓히는 것도 «새 실측»으로만 한다.
            /// </para>
            /// </summary>
            public string[] TailUnion;

            /// <summary>어느 시행에서 뽑았나 — 리포트에 그대로 찍는다.</summary>
            public string Source;
        }

        /// <summary>블록 행 계열 이름. <b>x 는 버리고 y(행)만 본다</b> — 이웃 칸은 84 px 상자가 겹쳐 서로 대체된다.</summary>
        public static string BlockRow(double worldY)
        {
            return $"행{worldY:0}";
        }

        /// <summary>골대 림 계열 이름.</summary>
        public const string RimSeries = "림";

        /// <summary>
        /// 18회차 채록 그대로. <b>정본은 <c>07_기대값.md §16</c> 이고 원자료는 <c>gap18/tables.md</c></b> 다.
        /// ⚠ <b>합집합을 «좁히지» 마라</b> — 좁히면 원본이 실제로 내는 답이 오답으로 찍힌다 (#48).
        /// </summary>
        public static List<Contact612Structure> Contacts612()
        {
            return new List<Contact612Structure>
            {
                // ── W1L3 f21.4565 — 채택 4 · 골인 3/4 (75 %) · 접촉 15~22 · 골 +5775~+6734 ms
                //    A#0 : 6[900,375]+[850,375] 7~11[850,375] 12[750,625]+[700,625]
                //    A2#0: 6[900,375]+[850,375] 7~10[850,375]  11[700,625] 12[600,675]
                new Contact612Structure
                {
                    Level = "W1L3", Force = 21.4565,
                    Trials = 7, Scored = 6,
                    MinContacts = 15, MaxContacts = 22,
                    MinGoalMs = 5774.6, MaxGoalMs = 6734.4,
                    ValueGradeMaxIndex = 3,
                    Source = "18회차 A#0 · A2#0 (접촉 6 부터 이미 400 ms 갈린다) + " +
                             "★ 20회차 K2#0~#2 (3/3 골인 · 접촉 20~21 · 골 +5791~+6317 · " +
                             "K2#0 은 격자값 «그 자체» 21.4565 를 정확히 냈다)",
                    TailUnion = new[] { "행375", "행625", "행675", RimSeries },
                    Series = new[]
                    {
                        new[] { "행375" },              // 6  (동시 접촉 — 같은 행 두 칸)
                        new[] { "행375" },              // 7
                        new[] { "행375" },              // 8
                        new[] { "행375" },              // 9
                        new[] { "행375" },              // 10
                        new[] { "행375", "행625" },     // 11 ← 두 시행이 여기서 갈린다
                        new[] { "행625", "행675" },     // 12 ←   〃
                    },
                },

                // ── W1L3 f54.0000 — 누적 채택 8 · 골인 6/8 (75 %) · 접촉 9~14 · 골 +4618~+5426 ms
                //    18회차 B#0 : 6[300,675] 7림 8[300,675] 9림 10림
                //    18회차 B2#0: 6[300,675] 7림 8[600,675] 9림 10림 11림 12림
                //    ★★ 19회차가 «미골인 분기»를 처음 떴다 —
                //       B1#0(53.9945 · 미골인): 6[150,625]+[200,625] 7[100,625]+[150,625] 8~10[100,625]
                //       ⇒ 접촉 6 의 계열이 «행675 ↔ 행625» 로 원본 «자신이» 갈린다.
                //    ★ 이 샷이 «골대 림 접촉이 사슬을 만드는» 대표 자리다 (골인 분기 한정).
                new Contact612Structure
                {
                    Level = "W1L3", Force = 54.0000,
                    Trials = 8, Scored = 6,
                    MinContacts = 9, MaxContacts = 14,
                    MinGoalMs = 4618.2, MaxGoalMs = 5425.7,
                    ValueGradeMaxIndex = 4,
                    Source = "18회차 B#0 · B2#0 (골인 분기 · 림 왕복) + 19회차 B1#0~#2 (미골인 분기 · 행625 사슬)",
                    IndexGradingDisabledWhy =
                        "19회차 n=3 이 접촉 6 에서 «행625(미골인) ↔ 행675(골인)» 로 갈렸다 — " +
                        "접촉 6 의 계열로 채점하면 원본 자신이 떨어진다",
                    Series = null,
                    TailUnion = new[] { "행625", "행675", RimSeries },
                },

                // ── W1L3 f65.9845 — 채택 4 · 골인 4/4 (100 %) · 접촉 25~28 · 골 +6958~+7984 ms
                //    C#0 : 6[150,625]+[200,625] 7~12[200,625]  (★ 접촉 2·3 이 이미 림이다 — 고속 1554 px/s)
                new Contact612Structure
                {
                    Level = "W1L3", Force = 65.9845,
                    Trials = 7, Scored = 7,
                    MinContacts = 13, MaxContacts = 28,
                    MinGoalMs = 5358.4, MaxGoalMs = 7983.6,
                    ValueGradeMaxIndex = 3,
                    Source = "18회차 C#0 · C2#0~#2 + ★ 20회차 K1#0~#2 (3/3 골인 · 누적 7/7). " +
                             "⚠ 이 샷은 접촉 2·3 이 이미 림이다(속도 1554 px/s) — 「림은 저속·후반 전용」이 여기서 깨진다. " +
                             "★★ [20회차] 골인 «안»에도 두 갈래가 있다 — 실 force 65.9185 는 접촉 27 · +7208, " +
                             "65.9075 는 접촉 13 · +5358 로 1.9 s 빠르다. 그래서 접촉 수 하한이 25 → 13 으로 내려갔다",
                    // ⚠ [20회차] 합집합을 «넓혔다» — K1#0 의 꼬리가 행675(250·300,675) 와
                    //   림(375,775 · 525,775) 까지 간다. 넓히는 근거는 «새 실측»뿐이고 여기가 그 경우다.
                    TailUnion = new[] { "행625", "행675", RimSeries },
                    Series = new[]
                    {
                        new[] { "행625" },              // 6  (동시 접촉 — 같은 행 두 칸)
                        new[] { "행625" },              // 7
                        new[] { "행625" },              // 8
                        new[] { "행625" },              // 9
                        new[] { "행625" },              // 10
                        new[] { "행625" },              // 11
                        new[] { "행625" },              // 12
                    },
                },

                // ── W1L1 f66.3860 — 누적 채택 14 · 골인 13/14 (93 %) · 접촉 7~16 · 골 +4318~+7876 ms
                //    18회차 F#0 : 6[575,827] 7[975,877] 8[675,877] 9림 10[675,877] 11림 12림
                //    ★★★ 19회차 4시행이 «접촉 6 의 상대»를 통째로 반증했다 —
                //       F1#0 [1225,727](행727 · 미골인) · F1#1 [900,977](림) · F1#2 [575,827] · F1#3 [975,877]
                //       ⇒ 네 시행이 «전부 다르다». 접촉 6 상대로 채점하면 원본 자신이 3/4 를 떨어뜨린다.
                //    ★★ 우리 쪽 「1721 스텝 정착 · 행625 사슬」도 원본에 «있다» — F1#0 이 698 프레임을
                //       한 블록 위에 머물다 잠든다. 정착은 «미골인 분기» 그 자체다 (`07 §18-b`).
                new Contact612Structure
                {
                    Level = "W1L1", Force = 66.3860,
                    Trials = 14, Scored = 13,
                    MinContacts = 7, MaxContacts = 16,
                    MinGoalMs = 4318.2, MaxGoalMs = 7876.3,
                    ValueGradeMaxIndex = 3,
                    Source = "18회차 F#0~#5 · M#0~#1 · G#1 · G#3 (10/10) + 19회차 F1#0~#3 (3/4). " +
                             "★ 격자값 66.3860 «그 자체»는 골인 1/1 (+4318.2 ms · 접촉 8)",
                    IndexGradingDisabledWhy =
                        "19회차 n=4 의 접촉 6 상대가 행727 · 림 · 행827 · 행877 로 «전부 다르다» — " +
                        "그 중 하나(행727)가 정확히 우리 값이다. 지문 「우리 행727 / 원본 행827」은 반증됐다",
                    Series = null,
                    TailUnion = new[] { "행727", "행827", "행877", RimSeries },
                },
            };
        }

        // ══════════════════════════════════════════════ 19회차 — «격자값 그 자체» 정답지 (정본 `07 §18`)

        /// <summary>
        /// ★★★ <b>값 채점의 «경계»</b> — 이 번호<b>까지만</b> 값으로 채점하고 그 너머는 구조로 간다.
        ///
        /// <para>
        /// ⚠⚠ <b>경계는 샷마다 다르다.</b> 15회차의 「접촉 2~5 는 결정적」과 18회차의 「접촉 6 이후는 불가」는
        /// <b>그 샷들에서만</b> 참이었다. 19회차가 두 샷을 각각 <b>4·3 시행</b> 떠서 «어디서 갈리는지»를 직접 봤고,
        /// <b>경계가 접촉 3 과 접촉 4 로 서로 달랐다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>경계를 «넓히지» 마라 — 점수가 오르는 쪽으로 옮기는 것은 #48 위반이다.</b>
        /// 경계를 옮기는 근거는 <b>새 실측(같은 입력을 n 회 떠서 갈리는 번호를 확인)</b>뿐이다.
        /// </para>
        /// </summary>
        public sealed class ValueGradeBoundary
        {
            public string Level;
            public double Force;

            /// <summary>이 접촉 번호 <b>까지</b> 값 채점. 그 다음부터는 구조 채점만 가능하다.</summary>
            public int MaxIndex;

            /// <summary>원본을 몇 번 떠서 정한 경계인가.</summary>
            public int Trials;

            /// <summary>
            /// ★★ <b>구조 채점의 경계</b> — 이 번호<b>까지</b>는 시행 전부가 «상대 body» 가 같다.
            /// 값 경계(<see cref="MaxIndex"/>)보다 «길다».
            ///
            /// <para>
            /// ⚠ <b>둘을 한 숫자로 합치지 마라.</b> 20회차가 네 자리에서
            /// 값 <c>3 · 4 · 3 · 3</c> ↔ 구조 <c>5 · 6 · 5 · 10</c> 을 냈다 — <b>두 배 넘게 벌어진다.</b>
            /// <b>평면 접촉만 이어지는 샷이 길고, 원(림)·모서리가 끼면 짧다.</b>
            /// </para>
            ///
            /// <para><c>0</c> 이면 «안 쟀다»는 뜻이다.</para>
            /// </summary>
            public int StructureGradeMaxIndex;

            /// <summary>왜 거기가 경계인가 — <b>실측 근거</b>. 「점수가 잘 나와서」는 근거가 아니다.</summary>
            public string Evidence;
        }

        /// <summary>
        /// 샷별 값 채점 경계 (정본 <c>07 §18-a</c>).
        /// </summary>
        public static List<ValueGradeBoundary> ValueGradeBoundaries()
        {
            return new List<ValueGradeBoundary>
            {
                new ValueGradeBoundary
                {
                    Level = "W1L1", Force = 66.3860, MaxIndex = 3, Trials = 4,
                    StructureGradeMaxIndex = 3,
                    Evidence = "접촉 1 은 4/4 가 블록 1225,275 이고 t 폭이 1.5 ms(+1357.9~+1359.4) · " +
                               "접촉 2 는 4/4 가 «행827» · 접촉 3 은 4/4 가 «x=425 열의 세로면 [1,0]». " +
                               "접촉 4 부터 상대가 갈린다",
                },
                new ValueGradeBoundary
                {
                    Level = "W1L3", Force = 54.0000, MaxIndex = 4, Trials = 3,
                    Evidence = "접촉 1~4 는 3/3 이 상대가 같고 접촉 2 의 pre 가 0.6 ms · 0.6 px 안이다. " +
                               "접촉 5 에서 법선이 «윗면 평타 [0,−1] ↔ 모서리 [0.785,−0.620]» 로 갈리고 " +
                               "접촉 6 에서 «행625 ↔ 300,675» 로 분기가 확정된다. " +
                               "⚠ 단 ω«후»는 접촉 2 부터 이미 −0.902 ~ −2.139 (2.4배) 로 흩어진다",
                    StructureGradeMaxIndex = 6,
                },

                // ── ★ [20회차 신규] 두 자리를 각각 3시행 떠서 경계를 직접 봤다 (`07 §18-j`).
                new ValueGradeBoundary
                {
                    Level = "W1L3", Force = 65.9845, MaxIndex = 3, Trials = 3,
                    StructureGradeMaxIndex = 5,
                    Evidence = "접촉 1~3 이 Δt ≤ 1.5 ms · Δ직전위치 ≤ 0.61 px · Δω후 ≤ 0.22 안이다. " +
                               "접촉 4 는 상대·법선·e_n 이 3/3 완전 동일한데 Δt 가 정확히 «1 프레임»(9.0 ms) · " +
                               "Δy 8.5 px 라 «구조만» 센다. 접촉 5 까지 상대가 3/3 같고 접촉 6 부터 원본 자신이 갈린다. " +
                               "★ 갈림의 씨앗은 접촉 3(림 525,775)의 e_n 산포 0.688~0.750 이다 — " +
                               "«원(림) 접촉이 값을 가장 크게 흔든다»(같은 자리 평면 접촉은 0.700 3/3)",
                },
                new ValueGradeBoundary
                {
                    Level = "W1L3", Force = 21.4565, MaxIndex = 3, Trials = 3,
                    StructureGradeMaxIndex = 10,
                    Evidence = "접촉 1~3 이 Δt ≤ 0.8 ms · Δω후 ≤ 0.015 안이다. " +
                               "⚠ Δ직전 y 1.4~3.0 px 는 «어긋남이 아니라 rAF 위상»이다 — 이 구간 낙하속도가 " +
                               "430~610 px/s 라 반 프레임(4 ms)이 곧 2~2.5 px 다(낙하 구간은 y 를 위상으로 보정해 읽는다). " +
                               "★★ 구조는 접촉 10 까지 3/3 이 완전히 같다 — 이 샷은 블록 윗면을 왼쪽으로 " +
                               "«튀며 굴러가는» 사슬이라 모서리 스침이 없어 갈릴 씨앗이 안 생긴다",
                },
            };
        }

        /// <summary>
        /// ★★★ <b>«격자값 그 자체»를 쏜 원본 정답지</b> [19회차 실측 · 정본 <c>07 §18-d/e</c>].
        ///
        /// <para>
        /// ★ <b>왜 «격자값 그 자체»가 중요한가</b> — 18·15회차의 정답지는 원본이 실제로 낸
        /// <c>53.9945</c> · <c>66.3970</c> 같은 <b>이웃 force</b> 였다. 우리 채점기는 <b>격자값</b>을 먹이므로
        /// 「원본은 이 자리에서 무엇을 하는가」의 <b>가장 직접적인 답</b>이 이 두 샷이다.
        /// 둘 다 <b>★골인</b>이라 격자표의 답도 <b>「골인」 그대로</b>다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>림 접촉(<c>ManifoldType = 0</c>)의 법선은 원본이 «안 채운다»</b> — <c>e_circles</c> 매니폴드가
        /// 중심 차로 그때그때 만들기 때문이다 (18회차 §16-c). 그래서 <see cref="Nx"/>·<see cref="Ny"/> 를
        /// <c>NaN</c> 으로 둔다. <b>「법선 무효」가 아니라 «만들어 쓰는 자리»다</b> — 그 접촉의 값은 전부 유효하고
        /// <c>e_n</c> 도 0.69~0.74 로 정상이다.
        /// </para>
        /// </summary>
        public sealed class GridShotContact
        {
            /// <summary>격자값 — 채점기가 실제로 먹이는 force.</summary>
            public string Level;
            public double Force;

            /// <summary>
            /// 원본이 그 시행에서 <b>실제로 쓴</b> force.
            ///
            /// <para>
            /// ⚠ <b>[20회차] <see cref="Force"/> 와 «다를 수 있다».</b> 19회차까지는 격자값을 정확히
            /// 맞힌 시행만 담았는데, 20회차 <c>65.9845</c> 는 <b>폴링 릴리즈의 되먹임이
            /// 65.9075~65.9185 에 수렴</b>해 격자값 자체를 못 냈다(창 ±0.23 안이라 채택).
            /// <b>둘이 다르면 「격자값 그 자체의 정답지」가 «아니다»</b> — 리포트에 그대로 찍어
            /// 「그 자체를 쟀다」로 읽히지 않게 한다.
            /// </para>
            /// </summary>
            public double ActualForce;

            public string ShotId;
            public bool OriginScored;
            public double OriginGoalMs;
            public int OriginContacts;

            /// <summary>접촉 번호 (1 부터).</summary>
            public int Index;

            /// <summary>★ <b>발사 프레임</b> 기준 접촉 시각 (ms).</summary>
            public double TimeMs;

            /// <summary>접촉 «직전» 프레임의 공 body 위치 (원본 px · y 아래 +).</summary>
            public double PreX;
            public double PreY;

            /// <summary>접촉점 (원본 px).</summary>
            public double ContactX;
            public double ContactY;

            /// <summary>첫 법선. <b>림 접촉이면 <c>NaN</c></b> — 원본이 안 채우는 필드다.</summary>
            public double Nx;
            public double Ny;

            /// <summary>상대 body 중심 (첫 상대). 동시 접촉이면 <see cref="SecondOtherX"/> 도 찬다.</summary>
            public double OtherX;
            public double OtherY;

            /// <summary>동시 접촉의 둘째 상대. 없으면 <c>NaN</c>.</summary>
            public double SecondOtherX;
            public double SecondOtherY;

            public double VnBefore;
            public double VnAfter;
            public double VtBefore;
            public double VtAfter;
            public double OmegaBefore;
            public double OmegaAfter;

            /// <summary>원본이 계산해 준 반발 계수 (표에 적힌 값 그대로).</summary>
            public double En;

            /// <summary><c>1 = e_faceA</c>(폴리곤 = 블록) · <c>0 = e_circles</c>(원 = 골대 림).</summary>
            public int ManifoldType;

            /// <summary>골대 림 접촉인가 — <c>ManifoldType == 0</c> 과 동치다 (18회차 27/27 예외 0건).</summary>
            public bool IsRim
            {
                get { return ManifoldType == 0; }
            }
        }

        /// <summary>
        /// 19회차 채록 그대로. <b>정본은 <c>07_기대값.md §18-d · §18-e</c></b> 이고 원자료는 <c>gap19/res_*.json</c> 이다.
        /// </summary>
        public static List<GridShotContact> GridShotContacts()
        {
            var list = new List<GridShotContact>(50);

            // ── §18-d. W1L1 격자값 66.3860 «그 자체» (F1#1 · ★골인 +4318.2 ms · 접촉 8)
            //    ★ 접촉 6·8 이 «골대 림»(mtype 0) 이다 — 림이 공을 골대로 몰아넣는 그 자리다.
            const string L1 = "W1L1";
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              1,  1359.4, 1142.790,  229.085, 1176.078,  246.456, -0.926, -0.378, 1225,  275, NA, NA,  -954.8,  667.9,  -492.2, -395.1,  -3.444,  -8.908, 0.700, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              2,  2133.4,  548.341,  752.247,  542.448,  790.797,  0.000, -1.000,  575,  827, 525, 827, -1250.1,  871.8,  -767.7, -639.1,  -8.841, -14.734, 0.697, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              3,  2191.6,  510.747,  705.583,  471.095,  699.183,  1.000,  0.000,  425,  727, 425, 677,  -639.1,  447.4,  -810.0, -731.1, -14.728, -17.155, 0.700, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              4,  3275.5,  995.494,  772.624, 1027.476,  798.559, -0.846, -0.534, 1075,  827, NA, NA,  -841.8,  587.3,  -495.4, -593.8, -16.973, -13.452, 0.698, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              5,  3558.3,  764.357,  884.250,  717.386,  887.339,  1.000,  0.000,  675,  877, NA, NA,  -813.6,  569.5,   593.8,  230.4, -13.416,   4.741, 0.700, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              6,  3716.2,  846.140,  935.876,  883.709,  964.545,     NA,     NA,  900,  977, NA, NA,  -713.7,  496.7,     4.1,   53.2,   4.734,   1.485, 0.696, 0);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              7,  3967.4,  761.217,  897.123,  718.670,  897.367,  1.000,  0.000,  675,  877, NA, NA,  -362.3,  253.6,     6.8,   45.4,   1.482,   0.791, 0.700, 1);
            G(list, L1, 66.3860, 66.3860, "F1#1", true, 4318.2, 8,
              8,  4266.5,  836.280,  973.353,  879.456,  976.742,     NA,     NA,  900,  977, NA, NA,  -259.5,  192.7,  -469.4, -312.7,   0.788,  -7.034, 0.743, 0);

            // ── §18-e. W1L3 격자값 54.0000 «그 자체» (B1#2 · ★골인 +4950.2 ms · 접촉 10)
            //    ★★ 접촉 7~10 이 «골대 림 왕복»(525,775 ↔ 375,775) 이다 — 골인 분기의 지문.
            //    ⚠ 접촉 2 가 갈림의 «씨앗»이다 (§18-e 아래 민감도 표).
            const string L3 = "W1L3";
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              1,   116.4, 1112.016,  134.758, 1156.175,  129.128, -1.000,  0.000, 1200,  150, 1200, 100, -567.7,  430.5,   820.3,  462.0,  -3.487,  11.225, 0.758, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              2,  1258.2,  625.019,  562.332,  656.859,  596.359, -0.841, -0.541,  700,  625, NA, NA,  -300.9,  209.2, -1263.6, -1023.2, 11.100,  -0.902, 0.695, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              3,  1300.3,  596.422,  597.889,  591.118,  643.806,  0.000, -1.000,  600,  675, NA, NA,  -784.9,  544.1,  -729.5, -498.4,  -0.902, -11.491, 0.693, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              4,  1958.0,  267.136,  558.692,  242.204,  591.898,  0.557, -0.830,  200,  625, NA, NA,  -624.1,  433.9,  -181.5, -274.9, -11.418,  -6.497, 0.695, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              5,  2682.7,  276.172,  568.506,  244.625,  595.584,  0.785, -0.620,  200,  625, NA, NA,  -329.5,  228.6,   439.1,  214.1,  -6.451,   4.724, 0.694, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              6,  2882.4,  338.985,  600.488,  341.451,  644.716,  0.000, -1.000,  300,  675, NA, NA,  -303.5,  211.0,   312.0,  276.0,   4.716,   6.365, 0.695, 1);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              7,  3450.1,  495.470,  714.966,  515.776,  756.264,     NA,     NA,  525,  775, NA, NA,  -674.5,  468.7,   -24.4,   64.3,   6.330,   1.637, 0.695, 0);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              8,  4067.3,  405.778,  717.747,  385.241,  756.464,     NA,     NA,  375,  775, NA, NA,  -466.5,  323.7,    87.1,   88.6,   1.627,   1.937, 0.694, 0);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              9,  4424.9,  488.104,  723.594,  512.313,  757.108,     NA,     NA,  525,  775, NA, NA,  -356.3,  246.4,    34.3,   35.6,   1.931,   1.029, 0.692, 0);
            G(list, L3, 54.0000, 54.0000, "B1#2", true, 4950.2, 10,
              10, 4866.4,  437.894,  763.902,  394.996,  772.428,     NA,     NA,  375,  775, NA, NA,  -165.7,  122.5,   398.7,  297.3,   1.024,   6.613, 0.739, 0);

            // ── ★ [20회차] §3-1. W1L3 격자값 65.9845 — ⚠ 원본이 «그 자체»를 못 냈다 (K1#0 · 실 force 65.9185)
            //    ★골인 +7208.5 ms · 접촉 27 · 창 ±0.23 안이라 채택했다.
            //    ⚠ 그래서 이 샷은 «격자값 그 자체»가 «아니다» — 폴링 릴리즈의 되먹임이 65.9075~65.9185 에
            //      수렴해 격자값을 못 맞혔다. 「격자값 그 자체」의 정답지는 «미측정»이고 재측정 대기다.
            //    접촉 7~22 는 «`200,625` 윗면에서 16회 잦아드는 반복»이라 원자료에도 개별 채록이 없다 —
            //      여기서도 «비운다». 없는 것을 있는 것처럼 채우지 않는다.
            //    ★★ 갈림의 씨앗이 접촉 3(림 `525,775`)이다 — 3시행의 e_n 이 0.688~0.750 으로 흩어지고
            //      그 0.06 이 접촉 5 의 착지 x 를 10~15 px 벌려 «접촉 27/+7208 ↔ 접촉 13/+5358» 로 갈린다.
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              1  ,    100.2,  1113.821,  130.299,  1154.363,  124.389,  -1.000,   0.000,  1200,  150,  1200,  100,  -693.041,  485.129,  1062.982,   635.637,   -3.487,   14.947, 0.700, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              2  ,   1525.1,   423.532,  720.110,   390.002,  760.638,      NA,      NA,   375,  775,    NA,   NA, -1371.682,  969.677,   731.313,   696.339,   14.738,   16.071, 0.707, 0);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              3  ,   1583.3,   480.524,  723.730,   513.852,  758.252,      NA,      NA,   525,  775,    NA,   NA,  -567.302,  409.409,  1042.279,   907.713,   16.064,   21.173, 0.722, 0);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              4  ,   1633.5,   511.692,  688.064,   556.702,  681.137,  -1.000,   0.000,   600,  675,    NA,   NA,  -528.760,  370.132,   793.834,   814.460,   21.166,   19.063, 0.700, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              5  ,   2541.7,   180.194,  550.158,   178.867,  593.582,   0.000,  -1.000,   150,  625,   200,  625,  -522.939,  362.428,  -370.132,    27.918,   18.893,    0.644, 0.693, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              6  ,   3041.7,   192.595,  550.155,   192.086,  594.580,   0.017,  -1.000,   150,  625,   200,  625,  -362.145,  250.169,    34.079,    23.622,    0.641,    0.643, 0.691, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              23 ,   5500.0,   292.280,  600.051,   292.264,  644.799,   0.024,  -1.000,   250,  675,   300,  675,  -333.380,  234.556,   129.455,   131.527,    3.881,    3.161, 0.704, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              24 ,   5825.1,   336.760,  601.264,   337.897,  644.559,   0.000,  -1.000,   300,  675,    NA,   NA,  -231.267,  158.152,   137.117,   136.969,    3.152,    3.158, 0.684, 1);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              25 ,   6359.1,   409.820,  721.556,   387.789,  757.324,      NA,      NA,   375,  775,    NA,   NA,  -419.621,  312.988,   472.681,   352.096,    3.142,    8.186, 0.746, 0);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              26 ,   6508.8,   479.896,  729.498,   510.271,  759.128,      NA,      NA,   525,  775,    NA,   NA,  -430.607,  309.091,   239.864,   249.903,    8.176,    6.123, 0.718, 0);
            G(list, L3, 65.9845, 65.9185, "K1#0", true, 7208.5, 27,
              27 ,   7116.7,   464.455,  755.558,   504.831,  769.567,      NA,      NA,   525,  775,    NA,   NA,  -101.450,  104.157,  -480.703,  -385.646,    6.087,    1.088, 1.027, 0);

            // ── ★★ [20회차] §3-2. W1L3 격자값 21.4565 «그 자체» (K2#0 · 실 force 21.4565 · ★골인 +6316.6 ms · 접촉 21)
            //    ★ 폴링 릴리즈가 격자값을 «정확히» 맞힌 시행이다 — 이 자리의 가장 직접적인 답.
            //    ★★ 구조 경계가 «접촉 10» 으로 여태 중 가장 길다 — 블록 윗면을 왼쪽으로 튀며 굴러가
            //      모서리·림이 안 끼기 때문이다. 그래도 «값» 경계는 접촉 3 이다 (§3-3).
            //    ⚠ 접촉 16 의 e_n 0.111 · 접촉 21 의 2.090 은 우리 결함이 아니다 —
            //      전자는 저속(47 px/s)이라 b2_velocityThreshold 아래에서 반발이 죽은 것이고
            //      (15회차 브래킷 12.2 < v_th ≤ 63.7 px/s 와 정합), 후자는 원 상대의 «중심차» 법선을
            //      한 프레임 늦게 만든 사영의 한계다.
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              1  ,    291.9,  1114.072,  178.174,  1156.245,  178.860,  -1.000,   0.000,  1200,  200,  1200,  150,  -225.585,  157.910,   -34.225,   -94.130,   -3.481,   -1.879, 0.700, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              2  ,    649.9,  1057.492,  301.449,  1056.335,  342.148,   0.000,  -1.000,  1050,  375,    NA,   NA,  -606.530,  421.196,  -157.910,  -132.243,   -1.873,   -3.049, 0.694, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              3  ,   1233.2,   980.309,  300.912,   979.212,  343.639,   0.000,  -1.000,   950,  375,  1000,  375,  -428.854,  296.313,  -132.243,  -131.989,   -3.032,   -3.043, 0.691, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              4  ,   1650.0,   925.321,  301.594,   924.226,  343.258,   0.000,  -1.000,   950,  375,   900,  375,  -303.537,  208.591,  -131.989,  -131.890,   -3.031,   -3.035, 0.687, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              5  ,   1949.8,   885.740,  300.900,   884.646,  344.881,   0.000,  -1.000,   900,  375,   850,  375,  -216.509,  147.821,  -131.890,  -131.673,   -3.026,   -3.036, 0.683, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              6  ,   2166.6,   857.218,  301.840,   856.886,  344.377,  -0.018,  -1.000,   900,  375,   850,  375,  -149.785,  105.040,  -134.390,  -132.419,   -3.029,   -3.099, 0.701, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              7  ,   2324.9,   835.947,  301.423,   834.832,  345.141,   0.000,  -1.000,   850,  375,    NA,   NA,  -109.910,   60.602,  -134.288,  -134.255,   -3.094,   -3.095, 0.551, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              8  ,   2441.5,   820.293,  301.838,   819.166,  344.812,   0.000,  -1.000,   850,  375,    NA,   NA,   -76.798,   50.129,  -134.255,  -134.166,   -3.092,   -3.096, 0.653, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              9  ,   2533.3,   807.963,  301.839,   806.919,  344.916,  -0.001,  -1.000,   850,  375,    NA,   NA,   -62.537,   39.565,  -134.229,  -134.172,   -3.093,   -3.096, 0.633, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              10 ,   2641.5,   793.441,  304.334,   806.594,  345.452,  -0.333,  -0.943,   850,  375,    NA,   NA,   -47.689,   29.991,  -159.174,  -157.667,   -3.093,   -3.543, 0.629, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              11 ,   3208.3,   703.547,  548.270,   702.221,  593.014,   0.000,  -1.000,   700,  625,   750,  625,  -849.220,  590.974,  -158.656,  -156.685,   -3.524,   -3.613, 0.696, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              12 ,   4091.6,   565.137,  601.265,   563.824,  641.718,   0.000,  -1.000,   600,  675,    NA,   NA,  -709.076,  492.723,  -156.685,  -156.238,   -3.582,   -3.602, 0.695, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              13 ,   4974.9,   426.929,  736.865,   393.274,  762.899,      NA,      NA,   375,  775,    NA,   NA,  -576.998,  420.465,   588.363,   335.138,   -3.571,    7.745, 0.729, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              14 ,   5050.5,   465.054,  744.931,   507.090,  765.650,      NA,      NA,   525,  775,    NA,   NA,  -537.153,  375.054,   128.424,   176.475,    7.740,    4.359, 0.698, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              15 ,   5366.6,   391.278,  712.978,   379.621,  755.491,      NA,      NA,   375,  775,    NA,   NA,  -173.693,  106.167,  -216.611,   -72.802,    4.346,   -1.818, 0.611, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              16 ,   5458.2,   385.783,  706.796,   342.357,  704.251,   0.999,   0.053,   300,  675,    NA,   NA,   -47.378,    5.273,   -17.703,    71.270,   -1.817,   -0.745, 0.111, 1);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              17 ,   5575.0,   385.957,  712.950,   378.559,  754.955,      NA,      NA,   375,  775,    NA,   NA,  -131.675,   88.770,    24.891,     9.130,   -0.744,    0.164, 0.674, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              18 ,   5716.6,   389.427,  713.196,   379.777,  754.965,      NA,      NA,   375,  775,    NA,   NA,   -93.237,   62.315,    47.423,    38.092,    0.164,    0.819, 0.668, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              19 ,   5833.3,   395.433,  715.162,   381.720,  755.890,      NA,      NA,   375,  775,    NA,   NA,   -75.429,   50.631,    81.120,    71.657,    0.819,    1.572, 0.671, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              20 ,   5933.6,   403.889,  718.391,   384.532,  756.941,      NA,      NA,   375,  775,    NA,   NA,   -50.024,   34.051,   121.831,   112.561,    1.571,    2.466, 0.681, 0);
            G(list, L3, 21.4565, 21.4565, "K2#0", true, 6316.6, 21,
              21 ,   6033.2,   415.422,  726.238,   388.004,  759.998,      NA,      NA,   375,  775,    NA,   NA,   -35.634,  -74.463,   183.664,   249.881,    2.464,    4.371, 2.090, 0);

            return list;
        }

        /// <summary>「이 칸은 원본이 «안 채운다»」 — 림 법선·둘째 상대에 쓴다.</summary>
        public static readonly double NA = double.NaN;

        private static void G(List<GridShotContact> list, string level, double force, double actualForce,
                              string shotId, bool scored, double goalMs, int originContacts, int index,
                              double timeMs, double preX, double preY, double contactX, double contactY,
                              double nx, double ny, double ox, double oy, double ox2, double oy2,
                              double vnBefore, double vnAfter, double vtBefore, double vtAfter,
                              double omegaBefore, double omegaAfter, double en, int manifoldType)
        {
            list.Add(new GridShotContact
            {
                Level = level,
                Force = force,
                ActualForce = actualForce,
                ShotId = shotId,
                OriginScored = scored,
                OriginGoalMs = goalMs,
                OriginContacts = originContacts,
                Index = index,
                TimeMs = timeMs,
                PreX = preX,
                PreY = preY,
                ContactX = contactX,
                ContactY = contactY,
                Nx = nx,
                Ny = ny,
                OtherX = ox,
                OtherY = oy,
                SecondOtherX = ox2,
                SecondOtherY = oy2,
                VnBefore = vnBefore,
                VnAfter = vnAfter,
                VtBefore = vtBefore,
                VtAfter = vtAfter,
                OmegaBefore = omegaBefore,
                OmegaAfter = omegaAfter,
                En = en,
                ManifoldType = manifoldType,
            });
        }
    }
}
