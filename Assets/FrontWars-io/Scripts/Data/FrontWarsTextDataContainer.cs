using System.Text;

namespace JinHyung.Data
{
    /// <summary>화면 문구 표 — 키로 한국어를 준다.</summary>
    public class FrontWarsTextDataContainer : DictionaryContainer<string, FrontWarsTextData>
    {
        /// <summary>화면이 반드시 쓰는 키 — 빠지면 검증에서 막는다.</summary>
        private static readonly string[] RequiredKeys =
        {
            "hud.economy.Classic", "hud.economy.Fast",
            "leaderboard.rank", "leaderboard.player", "leaderboard.owned", "leaderboard.gold", "leaderboard.troops",
            "control.troops", "control.gold", "control.attack_ratio", "control.send_troops", "control.send_gold",
            "events.none", "events.hide", "events.show",
            "filter.attack", "filter.nuke", "filter.trade", "filter.alliance", "filter.chat",
            "menu.singleplayer", "menu.tutorial", "menu.instructions", "menu.name_placeholder",
            "sp.title", "sp.start", "sp.back", "hud.spawn_hint", "end.victory", "end.defeat", "end.exit"
        };

        public override string Name
        {
            get { return "FrontWarsTextTable"; }
        }

        /// <summary>한국어 문구. 없으면 키를 그대로 보여 준다 — 화면에서 바로 눈에 띄게.</summary>
        public string Ko(string key)
        {
            FrontWarsTextData row = Get(key);
            return row != null ? row.Ko : key;
        }

        public string Format(string key, params object[] args)
        {
            return string.Format(Ko(key), args);
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            foreach (string key in RequiredKeys)
            {
                if (ContainsKey(key) == false)
                    sb.AppendLine($"문구 키가 없다: {key}");
            }

            for (int i = 0; i < AllValues.Count; i++)
            {
                if (string.IsNullOrEmpty(AllValues[i].Ko))
                    sb.AppendLine($"빈 문구: {AllValues[i].Key}");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }
    }
}
