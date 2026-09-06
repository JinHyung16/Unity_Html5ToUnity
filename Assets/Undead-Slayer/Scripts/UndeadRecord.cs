using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 최고 기록 — <b>최고 레벨</b>과 <b>최고 시간</b> [소스 <c>bestLevel</c> · <c>bestTime</c>].
    ///
    /// <para>
    /// ★ 원본은 브라우저 저장소(<c>state</c>)에 둔다. 우리는 그 자리에 <see cref="PlayerPrefs"/> 를 쓴다 —
    /// 「판을 넘겨 남는 값」이라는 성격이 같다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>갱신 시점이 둘 다 «판 중»이다</b> [소스] — 판이 끝날 때가 아니다.
    /// 최고 레벨은 <b>레벨이 오를 때</b>, 최고 시간은 <b>1초마다</b> 저장한다.
    /// 그래서 판을 하다 창을 닫아도 거기까지가 남는다.
    /// </para>
    /// </summary>
    public static class UndeadRecord
    {
        private const string BestLevelKey = "Undead.BestLevel";
        private const string BestTimeKey = "Undead.BestTimeSeconds";

        /// <summary>[소스] 초기값 1.</summary>
        public static int BestLevel
        {
            get { return Mathf.Max(1, PlayerPrefs.GetInt(BestLevelKey, 1)); }
        }

        /// <summary>[소스] 초기값 0.</summary>
        public static double BestTimeSeconds
        {
            get { return Mathf.Max(0f, PlayerPrefs.GetFloat(BestTimeKey, 0f)); }
        }

        /// <summary>레벨이 올랐을 때 [소스 <c>incrementLevel</c> — <c>bestLevel = max(level, bestLevel)</c>].</summary>
        public static void ReportLevel(int level)
        {
            if (level <= BestLevel)
                return;

            PlayerPrefs.SetInt(BestLevelKey, level);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 판이 도는 동안 [소스 <c>clock.update</c> — <b>1초마다</b> 넘겼으면 저장].
        /// <para>⚠ 매 프레임 저장하지 않는다 — 원본도 1초 간격이고, 저장은 싸지 않다.</para>
        /// </summary>
        public static void ReportTime(double seconds)
        {
            if (seconds <= BestTimeSeconds)
                return;

            PlayerPrefs.SetFloat(BestTimeKey, (float)seconds);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 화면에 보일 시간 [소스 <c>max(clock.getRunTime(), state.bestTime)</c>].
        /// <para>저장이 1초 간격이라 «지금 시간»이 최고를 넘으면 곧바로 이 값이 커진다.</para>
        /// </summary>
        public static double DisplayTimeSeconds(double runSeconds)
        {
            return System.Math.Max(runSeconds, BestTimeSeconds);
        }
    }
}
