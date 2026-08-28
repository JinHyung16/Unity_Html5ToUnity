namespace JinHyung.Data
{
    /// <summary>
    /// 테이블 하나를 담는 컨테이너의 계약.
    ///
    /// <para>
    /// <b>「JSON 이 있다」는 이관이 아니다.</b> 컨테이너가 있고 조회가 성공해야 이관이다 —
    /// 컨테이너를 안 만들거나 등록을 빠뜨리면 <b>JSON 이 있어도 아무도 안 읽는다.</b>
    /// 그런데 파일은 다 있으므로 「됐다」로 보인다.
    /// </para>
    /// </summary>
    public interface IDataContainer
    {
        /// <summary><b>JSON 파일명(확장자 제외)</b>과 같아야 한다. 이 이름으로 텍스트를 찾는다.</summary>
        string Name { get; }

        bool Loaded { get; }

        void LoadJson(string text);

        void Clear();

        /// <summary>
        /// 로드 직후 <c>DataManager</c> 가 부른다.
        /// <b>행 수 어설션이 여기 들어간다</b> — 원본 행 수와 다르면 그 자리가 결함이다.
        /// </summary>
        bool Validate(out string errorMessage);

        /// <summary>전 테이블 로드가 끝난 뒤 불린다. <b>테이블 간 참조 검사</b>는 여기서.</summary>
        void AfterAllTableLoaded();
    }
}
