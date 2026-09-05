using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 저장. ★★★ <b>[해소 · 17회차] 「저장 매체·키 미측정」이 닫혔다.</b>
    ///
    /// <para>
    /// <b><c>localStorage</c> 가 빈 게 아니라 «다른 매체»였다</b> — Construct 3 의 <c>LocalStorage</c>
    /// 플러그인은 이름과 달리 <b>IndexedDB</b> 를 쓴다(localForage). 프레임 3개(포털 · 중간 · 게임)를
    /// 전수 순회해 4매체를 열거하니 게임 프레임에 <b>DB <c>c3-localstorage-63vsue57cg3</c> v2 ·
    /// store <c>keyvaluepairs</c> · key <c>Locals</c></b> 하나가 있었다 [실측].
    /// </para>
    ///
    /// <para>
    /// 값은 <c>c2dictionary</c> 직렬화라 <b>전부 «문자열»</b>이고 <b>7키</b>다 [실측 전수]:
    /// <c>Sound "8"</c> · <c>Music "5"</c> · <c>AudioMute "0"</c> · <c>P1_Score "0"</c> ·
    /// <c>P2_Score "0"</c> · <c>LastLayout "W1L1"</c> · <c>WorldLastLevel_1 "1"</c>.
    /// 키 이름 <c>"Locals"</c> 는 전역변수 <c>LocalName</c> 이 정한다 [실측].
    /// </para>
    ///
    /// <para>
    /// ★★ <b>그래서 진행도는 «월드별 도달 최고 레벨» 숫자 하나뿐</b>이다 —
    /// 레벨별 클리어 비트맵이 아니다. 「어느 레벨에서 다시 시작하나」는 <c>LastLayout</c> 이 든다.
    /// <b>우리 저장 구조를 여기에 맞춰 «줄였다»</b> — 예전에는 우리 이름의 키 하나에 레벨 코드를
    /// 통째로 넣고 있었고, 그건 원본보다 «넓지도 좁지도 않게 다른» 구조였다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>매체는 달라도 된다</b>(우리는 <see cref="PlayerPrefs"/>). 옮기는 것은
    /// <b>키 이름 · 「문자열로 저장」 규약 · 「진행도는 숫자 하나」</b> 셋이다.
    /// </para>
    /// </summary>
    public static class BlumgiProgress
    {
        /// <summary>
        /// 우리 저장의 접두. 원본은 사전 하나(<c>Locals</c>) 안의 키라 접두가 없지만,
        /// <see cref="PlayerPrefs"/> 는 전역 이름공간이라 게임 이름을 앞에 붙인다.
        /// <b>접두 뒤의 이름은 원본 키 그대로</b>다.
        /// </summary>
        public const string Prefix = "Blumgi.Locals.";

        // ── 원본 7키 [실측 · 전부 문자열]
        public const string KeySound = Prefix + "Sound";
        public const string KeyMusic = Prefix + "Music";
        public const string KeyAudioMute = Prefix + "AudioMute";
        public const string KeyPlayer1Score = Prefix + "P1_Score";
        public const string KeyPlayer2Score = Prefix + "P2_Score";

        /// <summary>마지막 레이아웃 이름 — 레벨 코드 그 자체다 [실측 <c>"W1L1"</c>].</summary>
        public const string KeyLastLayout = Prefix + "LastLayout";

        /// <summary>월드별 «도달 최고 레벨». 뒤에 월드 번호가 붙는다 [실측 <c>WorldLastLevel_1</c>].</summary>
        public const string KeyWorldLastLevelPrefix = Prefix + "WorldLastLevel_";

        /// <summary>
        /// 진행도를 담는 키. <b>검사가 시작 상태를 만들 때 이것을 지운다</b>.
        /// ⚠ 예전 이름(<c>Blumgi.Progress.LevelCode</c>)에서 <b>원본 키 이름으로 바뀌었다</b>.
        /// </summary>
        public const string PrefKey = KeyLastLayout;

        // ── 기본값 [실측 전역변수 · 문자열로 저장된다]
        public const string DefaultSound = "8";
        public const string DefaultMusic = "5";
        public const string DefaultAudioMute = "0";
        public const string DefaultScore = "0";

        /// <summary>원본 사전의 키 수 [실측 7]. <b>검사가 «넓히지 않았나»를 이것으로 본다</b>.</summary>
        public const int OriginKeyCount = 7;

        /// <summary>저장된 진입 레벨. 없으면 원본과 같이 <see cref="BlumgiGameManager.FirstLevelCode"/> 다.</summary>
        public static string LoadLevelCode()
        {
            string code = PlayerPrefs.GetString(KeyLastLayout, BlumgiGameManager.FirstLevelCode);
            return string.IsNullOrEmpty(code) ? BlumgiGameManager.FirstLevelCode : code;
        }

        /// <summary>
        /// 진입 레벨을 저장한다. ★ <b>원본과 같이 두 자리에 쓴다</b> —
        /// <c>LastLayout</c>(레이아웃 이름)과 <c>WorldLastLevel_&lt;월드&gt;</c>(그 월드의 «최고» 레벨).
        /// 최고 레벨은 <b>내려가지 않는다</b> — 「도달한 최고」라서 되돌아가도 줄지 않는다.
        /// </summary>
        public static void SaveLevelCode(string levelCode)
        {
            if (string.IsNullOrEmpty(levelCode))
                return;

            PlayerPrefs.SetString(KeyLastLayout, levelCode);

            if (TryParseCode(levelCode, out int worldNo, out int levelNo))
            {
                string key = KeyWorldLastLevelPrefix + worldNo;
                string stored = PlayerPrefs.GetString(key, "1");

                if (int.TryParse(stored, out int best) == false)
                    best = 1;

                if (levelNo > best)
                    PlayerPrefs.SetString(key, levelNo.ToString());
            }

            PlayerPrefs.Save();
        }

        /// <summary>그 월드에서 «도달한 최고 레벨» [실측 — 진행도의 정본은 이 숫자 하나다].</summary>
        public static int LoadWorldLastLevel(int worldNo)
        {
            string stored = PlayerPrefs.GetString(KeyWorldLastLevelPrefix + worldNo, "1");
            return int.TryParse(stored, out int value) ? value : 1;
        }

        /// <summary><c>"W1L3"</c> → 월드 1 · 레벨 3. 원본 레이아웃 이름 규약 그대로다.</summary>
        public static bool TryParseCode(string levelCode, out int worldNo, out int levelNo)
        {
            worldNo = 0;
            levelNo = 0;

            if (string.IsNullOrEmpty(levelCode) || levelCode[0] != 'W')
                return false;

            int split = levelCode.IndexOf('L');

            if (split <= 1 || split >= levelCode.Length - 1)
                return false;

            return int.TryParse(levelCode.Substring(1, split - 1), out worldNo)
                   && int.TryParse(levelCode.Substring(split + 1), out levelNo);
        }
    }
}
