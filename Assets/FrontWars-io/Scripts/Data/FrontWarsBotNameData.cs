namespace JinHyung.Data
{
    /// <summary>
    /// 봇 이름 한 조각 — 이름 = 앞말 + " " + 뒷말.
    /// <para>
    /// 원본 목록(앞말 180 · 뒷말 71)은 옮기지 않았다 (R11 · 의도된 차이 8). 우리가 새로 지은 낱말이고, 개수만 원본과 같게 맞췄다 —
    /// 개수가 봇 배치 난수 소비와 같게 걸려 있기 때문이다 `[소스] Cq3gr8XO.js:randomBotName(){…nextInt(0,Ot.length)…nextInt(0,kt.length)`.
    /// </para>
    /// </summary>
    public class FrontWarsBotNameData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>Prefix · Suffix.</summary>
        public string Part { get; set; }

        public string Word { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
