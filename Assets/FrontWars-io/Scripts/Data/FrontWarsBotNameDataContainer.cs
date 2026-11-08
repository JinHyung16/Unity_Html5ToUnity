using System.Collections.Generic;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>봇 이름 표 — 앞말 · 뒷말 목록을 표 순서대로 준다.</summary>
    public class FrontWarsBotNameDataContainer : DictionaryContainer<int, FrontWarsBotNameData>
    {
        /// <summary>원본 목록 길이 — 봇 배치 난수 소비가 이 수에 걸려 있다 [소스].</summary>
        public const int PrefixCount = 180;
        public const int SuffixCount = 71;

        private readonly List<string> _prefixes = new List<string>(PrefixCount);
        private readonly List<string> _suffixes = new List<string>(SuffixCount);

        public override string Name
        {
            get { return "FrontWarsBotNameTable"; }
        }

        public IReadOnlyList<string> Prefixes
        {
            get { return _prefixes; }
        }

        public IReadOnlyList<string> Suffixes
        {
            get { return _suffixes; }
        }

        public override void AfterAllTableLoaded()
        {
            base.AfterAllTableLoaded();
            _prefixes.Clear();
            _suffixes.Clear();

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsBotNameData row = AllValues[i];

                if (row.Part == "Prefix")
                    _prefixes.Add(row.Word);
                else if (row.Part == "Suffix")
                    _suffixes.Add(row.Word);
            }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            int prefixes = 0, suffixes = 0;
            var seen = new HashSet<string>();

            for (int i = 0; i < AllValues.Count; i++)
            {
                FrontWarsBotNameData row = AllValues[i];

                if (row.Part == "Prefix")
                    prefixes++;
                else if (row.Part == "Suffix")
                    suffixes++;
                else
                    sb.AppendLine($"Id {row.Id}: Part «{row.Part}» — Prefix · Suffix 만 된다");

                if (string.IsNullOrWhiteSpace(row.Word))
                    sb.AppendLine($"Id {row.Id}: 빈 낱말");
                else if (seen.Add(row.Part + ":" + row.Word) == false)
                    sb.AppendLine($"Id {row.Id}: 같은 낱말이 또 있다 — {row.Word}");
            }

            if (prefixes != PrefixCount)
                sb.AppendLine($"앞말 {prefixes}개 — {PrefixCount}개여야 봇 배치가 원본과 같다");

            if (suffixes != SuffixCount)
                sb.AppendLine($"뒷말 {suffixes}개 — {SuffixCount}개여야 봇 배치가 원본과 같다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
