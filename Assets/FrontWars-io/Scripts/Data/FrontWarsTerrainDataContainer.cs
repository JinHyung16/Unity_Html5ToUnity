using System.Text;
using JinHyung.FrontWarsIo;

namespace JinHyung.Data
{
    /// <summary>지형 공격 값 표 — 3행.</summary>
    public class FrontWarsTerrainDataContainer : DictionaryContainer<string, FrontWarsTerrainData>
    {
        private const int OriginRowCount = 3;

        public override string Name
        {
            get { return "FrontWarsTerrainTable"; }
        }

        public FrontWarsTerrainData Get(EFrontWarsTerrain terrain)
        {
            return Get(terrain.ToString());
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            CheckRow(sb, EFrontWarsTerrain.Plains, 0);
            CheckRow(sb, EFrontWarsTerrain.Highland, FrontWarsTerrainMap.HighlandMagnitude);
            CheckRow(sb, EFrontWarsTerrain.Mountain, FrontWarsTerrainMap.MountainMagnitude);

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        private void CheckRow(StringBuilder sb, EFrontWarsTerrain terrain, int minMagnitude)
        {
            FrontWarsTerrainData row = Get(terrain);

            if (row == null)
            {
                sb.AppendLine($"{terrain} 행이 없다");
                return;
            }

            if (row.MinMagnitude != minMagnitude)
                sb.AppendLine($"{terrain}: 높이 문턱 {row.MinMagnitude} 이 칸 분류 코드 {minMagnitude} 와 다르다");

            if (row.DefenseValue <= 0.0 || row.SpeedValue <= 0.0)
                sb.AppendLine($"{terrain}: 방어 · 속도 값이 0 이하다");
        }
    }
}
