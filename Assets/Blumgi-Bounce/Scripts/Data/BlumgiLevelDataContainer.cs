using System.Collections.Generic;
using System.Text;
using JinHyung.Core;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 레벨 표. <b>이관 범위는 월드 1 의 5레벨</b>이다 (원장 관측 범위 — W2L1 부터는 미측정).
    /// </summary>
    public class BlumgiLevelDataContainer : DictionaryContainer<int, BlumgiLevelData>
    {
        /// <summary>원본 월드 1 = 5레벨 [실측 — 진행률 20% = 1/5 로 교차 확인].</summary>
        public const int OriginRowCount = 5;

        /// <summary>
        /// 조준각을 «인스턴스 변수로 못 읽어» 추정으로 채운 레벨 수 — <b>0</b>.
        ///
        /// <para>
        /// 3회차 시점에는 W1L5 하나가 추정(330)이었으나 <b>4회차가 <c>spr_SpriteRotator</c> 인스턴스 변수를
        /// 직접 읽어 330 을 확정</b>했다 (실측 화면각 29.5~29.8° 와 일치). 추정칸이 사라졌다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 이 상수를 <b>다시 올리려면 실측 근거가 있어야 한다</b> — 반대로 추정 플래그가
        /// 근거 없이 «내려가는» 것을 막는 것이 아래 <see cref="Validate"/> 의 원래 목적이고, 그 목적은 그대로다.
        /// </para>
        /// </summary>
        public const int EstimatedAimRowCount = 0;

        /// <summary>
        /// 골인 감지기 좌표를 «유도»로 채운 레벨 수 — <b>0</b> [7회차 실측].
        ///
        /// <para>
        /// 6회차는 W1L1 하나만 떠서 W1L2~L5 넷이 유도였다. <b>7회차가 5레벨을 나란히 떠
        /// 오프셋이 소수점까지 같음을 확인</b>해 전부 내렸다 (`07 §9-a`).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>월드2 이후는 여전히 미측정</b>이다 — 그 레벨이 들어오면 이 상수를 «올려» 등재하고
        /// 실측이 오면 다시 내린다. <b>「같을 것」을 「같다」로 조용히 바꾸는 것</b>을 막는 자리다.
        /// </para>
        /// </summary>
        public const int DerivedDetectorRowCount = 0;

        private Dictionary<string, BlumgiLevelData> _byCode = new Dictionary<string, BlumgiLevelData>(8);

        public override string Name
        {
            get { return "BlumgiLevelTable"; }
        }

        /// <summary>원본 <c>layout._name</c> 과 같은 코드로 찾는다 — 원본 로그와 바로 대조된다.</summary>
        public BlumgiLevelData GetByCode(string code)
        {
            if (code.IsNullOrEmpty())
                return null;

            _byCode.TryGetValue(code, out var value);
            return value;
        }

        protected override void SubCollectionConstructor(int count)
        {
            _byCode = new Dictionary<string, BlumgiLevelData>(count);
        }

        protected override void SubCollectionAdd(int key, BlumgiLevelData value)
        {
            if (value.Code.IsNullOrEmpty())
            {
                Log.Error($"{Name}: Code 가 빈 행이 있다 — Id {key}");
                return;
            }

            if (_byCode.ContainsKey(value.Code))
            {
                Log.Error($"{Name}: Code 가 중복이다 — {value.Code}");
                return;
            }

            _byCode.Add(value.Code, value);
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 월드1 {OriginRowCount}");

            // ★ 「추정으로 채운 칸」이 조용히 «실측»으로 승격되는 것을 막는다.
            //   4회차가 5레벨 전부를 인스턴스 변수로 읽어 추정칸이 0 이 됐다 —
            //   그래도 검사를 지우지 않는다. 다음 게임·다음 레벨에서 추정칸이 다시 생기면
            //   그때 이 상수를 «올려» 등재해야 하고, 등재 없이 늘어나면 여기서 걸린다.
            int estimated = 0;

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].AimRotatorEstimated)
                    estimated++;
            }

            if (estimated != EstimatedAimRowCount)
                sb.AppendLine($"조준각 «추정» 행 수 {estimated} — 4·7회차 실측 후 {EstimatedAimRowCount} 이다. 추정칸이 생기면 이 상수부터 등재한다");

            // ★ 감지기 좌표도 같은 처방이다 — 7회차가 5레벨 전수로 실측해 «유도» 칸이 0 이 됐다.
            //   월드2 가 들어오면 다시 유도가 생긴다. 그때 상수를 올려 등재하지 않으면 여기서 걸린다.
            int derivedDetectors = 0;

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (AllValues[i].DetectorOffsetDerived)
                    derivedDetectors++;
            }

            if (derivedDetectors != DerivedDetectorRowCount)
            {
                sb.AppendLine($"감지기 «유도» 행 수 {derivedDetectors} — 7회차 5레벨 전수 실측 후 " +
                              $"{DerivedDetectorRowCount} 이다. 유도칸이 생기면 이 상수부터 등재한다");
            }

            for (int i = 0; i < AllValues.Count; i++)
            {
                BlumgiLevelData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                // ★ 조준각은 «원본 인스턴스 변수 값»이다. 0~360 밖이면 도(°)를 잘못 적은 것이다.
                if (v.AimRotatorValue < 0.0 || v.AimRotatorValue > 360.0)
                    sb.AppendLine($"{v.Code}: AimRotatorValue {v.AimRotatorValue} 가 0~360 밖이다 — 도(°)를 적은 것 아닌가");

                AppendColorError(sb, v.Code, nameof(v.BgColorHex), v.BgColorHex);
                AppendColorError(sb, v.Code, nameof(v.GridLineColorHex), v.GridLineColorHex);
                AppendColorError(sb, v.Code, nameof(v.BlockFillColorHex), v.BlockFillColorHex);
                AppendColorError(sb, v.Code, nameof(v.BlockSquareColorHex), v.BlockSquareColorHex);
                AppendColorError(sb, v.Code, nameof(v.BlockLeftCapColorHex), v.BlockLeftCapColorHex);
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        private static void AppendColorError(StringBuilder sb, string code, string column, string hex)
        {
            if (hex != null && hex.Length == 6)
                return;

            sb.AppendLine($"{code}: {column} 이 6자리 hex 가 아니다 — '{hex}'");
        }
    }
}
