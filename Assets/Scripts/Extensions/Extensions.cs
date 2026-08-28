using System.Collections.Generic;

namespace JinHyung.Extensions
{
    /// <summary>
    /// 판정을 읽히게 만드는 확장 메서드 모음.
    ///
    /// <para>
    /// <c>list == null || list.Count == 0</c> 같은 조건을 호출부마다 다시 쓰면
    /// 한 곳에서 <c>null</c> 검사를 빠뜨려도 아무도 모른다.
    /// 판정을 이름으로 만들어 두면 빠뜨릴 자리가 없어진다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>받는 타입은 <c>IReadOnly~</c> 다.</b> <c>List</c>·배열·컨테이너의 <c>AllValues</c> 가
    /// 전부 이걸 구현하므로 하나로 덮인다. <c>ICollection</c> 과 둘 다 두면
    /// <c>List</c> 처럼 양쪽을 구현하는 타입에서 <b>호출이 모호해진다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>새 확장을 만들기 전에 여기 같은 것이 있는지 먼저 본다.</b>
    /// 같은 판정이 두 개 생기면 호출부마다 다른 쪽을 쓰게 되고 동작이 갈린다.
    /// </para>
    /// </summary>
    public static class Extensions
    {
        /// <summary>컬렉션이 <c>null</c> 이거나 비어 있으면 참.</summary>
        public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>원소가 하나라도 있으면 참. <see cref="IsNullOrEmpty{T}"/> 의 반대다.</summary>
        public static bool HasValue<T>(this IReadOnlyCollection<T> collection)
        {
            return collection != null && collection.Count > 0;
        }

        /// <summary>문자열이 <c>null</c> 이거나 비어 있으면 참.</summary>
        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 인덱스가 범위 안이면 참.
        /// 1차원 배열을 격자로 쓰는 코드에서 <b>경계 판정을 한 곳으로 모은다.</b>
        /// </summary>
        public static bool IsValidIndex<T>(this IReadOnlyList<T> list, int index)
        {
            if (list == null)
                return false;

            return index >= 0 && index < list.Count;
        }

        /// <summary>
        /// 범위 안이면 원소를, 벗어나면 <paramref name="fallback"/> 을 준다.
        ///
        /// <para>
        /// ⚠ <b>조회 실패를 감추는 용도로 쓰지 않는다.</b> 있어야 할 것이 없으면 그건 오류다.
        /// 「없을 수도 있다」가 사양인 자리에서만 쓴다.
        /// </para>
        /// </summary>
        public static T GetOrDefault<T>(this IReadOnlyList<T> list, int index, T fallback = default)
        {
            if (list.IsValidIndex(index) == false)
                return fallback;

            return list[index];
        }
    }
}
