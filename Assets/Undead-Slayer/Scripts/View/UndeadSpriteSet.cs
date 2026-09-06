using System.Collections.Generic;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 시트 하나의 <b>컷 모음</b> — 행 = 상태, 열 = 컷.
    ///
    /// <para>
    /// ★ 히어로는 <b>0행 = 대기 · 1행 = 이동</b> 이다 [실측]. 다른 개체는 1행뿐이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>재생은 여기서 하지 않는다.</b> 프레임 인덱스는 «개체 데이터»에 있고
    /// 중앙 틱이 진행시킨다 (확정표 G) — 이 클래스는 <b>인덱스 → 스프라이트</b> 표일 뿐이다.
    /// </para>
    /// </summary>
    public sealed class UndeadSpriteSet
    {
        private readonly Sprite[][] _rows;

        private UndeadSpriteSet(Sprite[][] rows, UndeadArtData data)
        {
            _rows = rows;
            Data = data;
        }

        public UndeadArtData Data { get; }

        public int RowCount
        {
            get { return _rows.Length; }
        }

        /// <summary>
        /// 컷을 꺼낸다. <paramref name="frame"/> 은 <b>실수 누적값</b>이라 여기서 내림·순환시킨다.
        /// ⚠ 행이 없으면 <c>null</c> 이다 — <b>폴백으로 0행을 주지 않는다</b>(조용히 다른 그림이 나온다).
        /// </summary>
        public Sprite Get(int row, double frame)
        {
            if (row < 0 || row >= _rows.Length)
                return null;

            Sprite[] cuts = _rows[row];

            if (cuts == null || cuts.Length == 0)
                return null;

            int index = (int)frame % cuts.Length;

            if (index < 0)
                index += cuts.Length;

            return cuts[index];
        }

        /// <summary>
        /// 어드레서블에서 시트를 읽어 «컷 표»로 만든다.
        ///
        /// <para>
        /// 잘린 스프라이트의 주소는 <c>&lt;시트&gt;[&lt;시트&gt;_&lt;행&gt;_&lt;열&gt;]</c> 다 —
        /// <c>UndeadArtImportSetup.SliceGrid</c> 가 붙인 이름 규약과 <b>한 쌍</b>이다.
        /// 한쪽만 고치면 로드가 조용히 <c>null</c> 이 된다.
        /// </para>
        ///
        /// <para>⚠ <b>비어 있는 컷은 «없는 것»으로 둔다</b> — 히어로 6번째 열이 그렇다(관측 안 됨).</para>
        /// </summary>
        public static async Task<UndeadSpriteSet> LoadAsync(UndeadArtData data)
        {
            if (data == null)
                return null;

            // ★ «도는» 컷만 읽는다 — 관측 안 된 열까지 넣으면 빈 컷이 깜빡인다.
            //   컷 전부를 «한꺼번에» 띄운다 — 한 장씩 기다리면 보스 시트(56컷) 하나에 56번 왕복이다 [회차 11].
            var addresses = new List<string>(data.Rows * data.UsedCols);

            for (int row = 0; row < data.Rows; row++)
            {
                for (int col = 0; col < data.UsedCols; col++)
                    addresses.Add(Address(data, row, col));
            }

            Sprite[] loaded = await ArtLoader.LoadSpritesAsync(addresses);
            var rows = new Sprite[data.Rows][];

            for (int row = 0; row < data.Rows; row++)
            {
                var cuts = new List<Sprite>(data.UsedCols);

                for (int col = 0; col < data.UsedCols; col++)
                {
                    Sprite sprite = loaded[row * data.UsedCols + col];

                    if (sprite != null)
                        cuts.Add(sprite);
                }

                rows[row] = cuts.ToArray();
            }

            return Build(data, rows);
        }

        /// <summary>
        /// 어드레서블 주소. <b>임포터가 붙이는 이름 규약과 한 쌍</b>이다 —
        /// 한쪽만 고치면 로드가 조용히 <c>null</c> 이 된다.
        /// </summary>
        public static string Address(UndeadArtData data, int row, int col)
        {
            bool single = data.Cols * data.Rows <= 1;
            return single ? data.Code : $"{data.Code}[{data.Code}_{row}_{col}]";
        }

        /// <summary>
        /// 이미 읽어 둔 컷으로 조립한다.
        ///
        /// <para>
        /// ★ <b>왜 있는가</b> — 배치 «편집» 모드에서는 <c>await</c> 가 끝나지 않는다
        /// (유니티가 executeMethod 를 붙잡고 있어 어드레서블 콜백이 안 돈다).
        /// 채점기가 <c>WaitForCompletion</c> 으로 동기 로드한 뒤 여기로 넘긴다 —
        /// <b>주소 규약과 조립 규칙은 그대로 이 클래스를 지난다</b>(두 벌이 아니다).
        /// </para>
        /// </summary>
        public static UndeadSpriteSet Build(UndeadArtData data, Sprite[][] rows)
        {
            if (data == null || rows == null || rows.Length == 0 || rows[0] == null || rows[0].Length == 0)
            {
                Log.Error($"스프라이트를 못 읽었다: {(data == null ? "(표 없음)" : data.Code)} — 주소 규약이 임포터와 어긋났는지 본다");
                return null;
            }

            return new UndeadSpriteSet(rows, data);
        }
    }
}
