using System;
using System.Collections.Generic;
using System.Text;
using JinHyung.Core;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 아트 표 — 원본 씬 그래프에서 <b>실측한 22종</b>.
    ///
    /// <para>
    /// ⚠ <b>아틀라스 79프레임 전부가 아니다.</b> 이관 회차에 화면에 실제로 나온 것만 든다 —
    /// 못 본 것을 추정으로 채우면 그게 오염이다. 그릴 일이 생기면 그때 재서 넣는다.
    /// </para>
    /// </summary>
    public class UndeadArtDataContainer : DictionaryContainer<string, UndeadArtData>
    {
        /// <summary>
        /// 실측한 아트 종류 수.
        ///
        /// <para>회차 8 에 <b><c>warrior_lay</c></b> 를 더해 23 → <b>24</b> — 과제 목표 개체가 관측됐다
        /// (씬에서 <c>(169.3, 2559.3)</c> 로 잡혔고 아틀라스 실측 192×48).</para>
        ///
        /// <para>
        /// 43 → <b>45</b> — <b><c>warrior_idle</c>(4컷)·<c>warrior_run</c>(8컷)</b> 을 더했다.
        /// 두 장은 <b>원본에 있는데 우리에게 아예 없었다</b> — 구조된 전사가 «누운 그림»으로 따라다녔다
        /// [소스 <c>loadFrames</c> 직독].
        /// </para>
        ///
        /// <para>
        /// 45 → <b>47</b> — 가족은 원본이 <b>«세 명»</b>이고 <b>각기 다른 시트</b>다
        /// [소스 <c>createMembers</c> — <c>family_npc_1/2/3</c>]. 한 장으로 두면 세 명이 똑같이 보인다.
        /// </para>
        ///
        /// <para>
        /// 47 → <b>48</b> — <b><c>kunai</c></b> 를 더했다. <b>구조된 전사가 던지는 무기</b>인데
        /// 그림도 규칙도 통째로 없었다 [소스 <c>class Pd</c> · <c>throwKunais</c> — 1초마다 대각 4방].
        /// 「동료가 공격을 못 한다」로 <b>사람이 화면을 보고</b> 잡아낸 결함이다.
        /// </para>
        ///
        /// <para>
        /// 48 → <b>49</b> — <b><c>bubble_medium</c></b> (140×102) 을 더했다. 전사가 <b>다음 과제를 예고</b>할 때
        /// 쓰는 «큰» 말풍선이다 [소스 <c>getTextureAlias</c> — <c>variant "medium"</c>].
        /// ⚠ 굽는 코드에는 <c>bubble_medium</c> 갈래가 «있었는데» 표에 행이 없어 <b>한 번도 안 불렸다</b>.
        /// </para>
        ///
        /// <para>
        /// 49 → <b>50</b> — <b><c>quest_progress</c></b> (36×36 · 9컷) 을 더했다. 쓰러진 전사 곁에 서 있는 동안
        /// 차오르는 <b>구조 진행 링</b>이다 [소스 <c>class Ad</c> · <c>Cd</c> 9프레임].
        /// ⚠ 시뮬은 <c>RescueSeconds</c> 를 <b>이미 세고 있었는데 아무도 읽지 않았다</b> — 4초를 눈금 없이 기다렸다.
        /// </para>
        ///
        /// <para>
        /// 50 → <b>51</b> — <b><c>reward_button_bg_card</c></b> 를 더했다. 그림은 <c>reward_button_bg</c> 와 <b>같은데</b>
        /// 원본이 <b>인셋을 자리마다 다르게</b> 쓴다 [소스 — 카드 10 · 액션 버튼 15].
        /// 엔진의 9슬라이스 인셋은 스프라이트 에셋에 붙어서 <b>자리마다 다르게 하려면 행이 둘</b>이어야 한다.
        /// </para>
        ///
        /// <para>
        /// 51 → <b>52</b> — <c>hp_segment</c> 를 <b><c>hp_segment_bg</c> + <c>hp_segment_fill</c> 두 겹</b>으로 갈랐다.
        /// 원본 체력바는 «어두운 바탕(항상)» 위에 «빨간 채움(줄었다 늘었다)» 을 얹는다 [소스 <c>rebuildSegments</c>] —
        /// 한 장으로 두면 <b>목숨이 줄 때 칸 자체가 사라진다</b>.
        /// ⚠ 둘 다 <see cref="UndeadArtData.DrawnByCode"/> 다 — 원본이 아틀라스가 아니라 <c>Graphics</c> 로 그린다.
        /// </para>
        /// </summary>
        public const int MeasuredRowCount = 52;

        /// <summary>월드 아트 = <c>Game</c> · UI 아트 = <c>Ui</c>. 다른 값이 들어오면 오류다.</summary>
        public const string CategoryGame = "Game";

        public const string CategoryUi = "Ui";

        private List<UndeadArtData> _game = new List<UndeadArtData>(16);
        private List<UndeadArtData> _ui = new List<UndeadArtData>(16);

        public override string Name
        {
            get { return "UndeadArtTable"; }
        }

        /// <summary>월드에 그리는 아트.</summary>
        public IReadOnlyList<UndeadArtData> GameArt
        {
            get { return _game; }
        }

        /// <summary>캔버스에 그리는 아트.</summary>
        public IReadOnlyList<UndeadArtData> UiArt
        {
            get { return _ui; }
        }

        protected override void SubCollectionConstructor(int count)
        {
            _game = new List<UndeadArtData>(count);
            _ui = new List<UndeadArtData>(count);
        }

        protected override void SubCollectionAdd(string key, UndeadArtData value)
        {
            if (value.Category == CategoryUi)
                _ui.Add(value);
            else
                _game.Add(value);
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != MeasuredRowCount)
                sb.AppendLine($"행 수 {Count} — 실측 {MeasuredRowCount}");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadArtData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.Category != CategoryGame && v.Category != CategoryUi)
                    sb.AppendLine($"{v.Code}: Category '{v.Category}' 가 {CategoryGame}/{CategoryUi} 가 아니다");

                if (v.SheetWidth <= 0 || v.SheetHeight <= 0)
                    sb.AppendLine($"{v.Code}: 시트 크기 {v.SheetWidth}x{v.SheetHeight} 가 0 이하다");

                // ★ 격자가 시트와 «곱»으로 맞아야 한다 — 안 맞으면 컷 수를 잘못 센 것이다.
                if (v.FrameWidth * v.Cols != v.SheetWidth || v.FrameHeight * v.Rows != v.SheetHeight)
                {
                    sb.AppendLine($"{v.Code}: 컷 {v.FrameWidth}x{v.FrameHeight} × 격자 {v.Cols}x{v.Rows} 이 " +
                                  $"시트 {v.SheetWidth}x{v.SheetHeight} 과 안 맞는다");
                }

                // ★ 안 잰 값에 «0 이 아닌 숫자»가 들어오면 추정이 새어 들어온 것이다.
                if (v.FpsMeasured == false && v.Fps != 0.0)
                    sb.AppendLine($"{v.Code}: fps 를 안 쟀는데 {v.Fps} 가 적혀 있다");

                // ★★ 「쟀는데 0」은 <b>«시간으로 안 도는» 시트</b>라는 뜻이다 — 오류가 아니다.
                //   컷을 «상태»가 고르는 것이 있다 (구조 진행 링은 진행률이 컷을 고른다 · 소스 setProgress).
                //   ⚠ 이걸 오류로 두면 그런 시트를 표에 못 올려 «화면에서 통째로 빠진다».
                if (v.FpsMeasured && v.Fps < 0.0)
                    sb.AppendLine($"{v.Code}: fps 를 쟀다면서 {v.Fps} 다");

                // ★ 컷이 여럿인데 fps 가 없으면 «움직일 수 없다» — 그리기 전에 재야 한다.
                if (v.UsedCols <= 0 || v.UsedCols > v.Cols)
                    sb.AppendLine($"{v.Code}: UsedCols {v.UsedCols} 가 1~{v.Cols} 밖이다");

                // 시작 컷 + 쓰는 컷이 시트를 넘으면 «빈 칸»이 구워진다
                if (v.CutOffset < 0 || v.CutOffset + v.UsedCols > v.Cols)
                    sb.AppendLine($"{v.Code}: CutOffset {v.CutOffset} + UsedCols {v.UsedCols} 가 Cols {v.Cols} 를 넘는다");

                // ★★ 「9슬라이스로 늘린다」와 「정수배로 확대한다」는 <b>배타</b>다 [소스].
                //   ⚠ [사고] lvl_bg 가 «둘 다» 켜져 있었다 — 표만 보면 모순인데 아무도 안 봤고,
                //     화면에서는 16×16 그림이 9슬라이스로 늘어나 <b>테두리가 실오라기처럼 얇아졌다</b>.
                //     원본은 같은 그림을 4.5배로 «통째로» 키운다(테두리도 4.5배 굵어진다).
                if (v.NineSlice != (v.NineSliceBorder > 0))
                    sb.AppendLine($"{v.Code}: NineSlice {v.NineSlice} 와 인셋 {v.NineSliceBorder} 가 어긋난다 — 인셋 0 이면 9슬라이스가 아니다");

                if (v.NineSlice && (v.DisplayScaleX != 1.0 || v.DisplayScaleY != 1.0))
                {
                    sb.AppendLine($"{v.Code}: 9슬라이스인데 표시 배율이 {v.DisplayScaleX}x{v.DisplayScaleY} 다 " +
                                  "— 늘리는 것과 확대하는 것은 «둘 중 하나»다");
                }

                // 인셋 둘이 컷을 덮으면 «늘어날 가운데»가 없어 모서리가 뭉개진다
                if (v.NineSliceBorder * 2 > Math.Min(v.FrameWidth, v.FrameHeight))
                {
                    sb.AppendLine($"{v.Code}: 인셋 {v.NineSliceBorder} × 2 가 컷 " +
                                  $"{v.FrameWidth}x{v.FrameHeight} 를 덮는다 — 늘어날 가운데가 없다");
                }

                // ★ fps 의 «뿌리»는 원본의 재생 속도(틱당 진행 컷 수)다 — <c>fps = 속도 × 60</c>.
                //   대개 정수 분주가 되지만(0.2 → 12fps = 5틱) <b>항상 그런 것은 아니다</b> —
                //   회차 9 에 번개가 <c>0.3 → 18fps</c> 로 나왔고 이는 3.33틱이라 정수가 아니다.
                //   ⚠ 그래서 <c>FrameTicks60</c> 은 «정수 분주일 때만» 채우고, 아니면 0 이다.
                if (v.FpsMeasured && v.FrameTicks60 > 0
                    && System.Math.Abs(v.Fps - (60.0 / v.FrameTicks60)) > 0.01)
                {
                    sb.AppendLine($"{v.Code}: fps {v.Fps} 가 60/{v.FrameTicks60} 과 다르다 "
                                  + "— 정수 분주가 아니면 FrameTicks60 을 0 으로 둔다");
                }

                // ★★ 「fps 를 재라」는 «돌리는» 것에만 건다.
                //   ⚠ 판정 기준은 <c>Cols</c> 가 아니라 <b><c>UsedCols</c></b> 다 —
                //     시트에 컷이 여럿이어도 «원본이 돌리는 것을 못 봤으면» 우리는 한 컷만 그리고,
                //     그때는 fps 라는 값 자체가 쓰이지 않는다. 반대로 <b>UsedCols 가 2 이상이면</b>
                //     fps 없이는 «몇 초에 한 컷»인지 정할 수 없어 반드시 추측이 된다.
                if (v.UsedCols * v.Rows > 1 && v.FpsMeasured == false)
                    sb.AppendLine($"{v.Code}: 돌리는 컷이 {v.UsedCols * v.Rows} 개인데 fps 가 미측정이다 — 그리기 전에 잰다");

                if (v.DisplayScaleX <= 0.0 || v.DisplayScaleY <= 0.0)
                    sb.AppendLine($"{v.Code}: 표시 배율 {v.DisplayScaleX}x{v.DisplayScaleY} 가 0 이하다 " +
                                  $"— 좌우 반전은 FlipsHorizontally 로 든다, 음수 배율로 들지 않는다");

                if (v.PivotX < 0.0 || v.PivotX > 1.0 || v.PivotY < 0.0 || v.PivotY > 1.0)
                    sb.AppendLine($"{v.Code}: 피벗 {v.PivotX},{v.PivotY} 가 0~1 밖이다");

                if (v.TintHex == null || v.TintHex.Length != 6)
                    sb.AppendLine($"{v.Code}: TintHex '{v.TintHex}' 가 6자리 hex 가 아니다");

                if (v.Alpha < 0.0 || v.Alpha > 1.0)
                    sb.AppendLine($"{v.Code}: 알파 {v.Alpha} 가 0~1 밖이다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
