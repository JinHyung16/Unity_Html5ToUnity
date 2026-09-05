using System;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 축 정렬 사각형. <b>지금 이걸 쓰는 것은 골인 감지기 둘뿐이다.</b>
    ///
    /// <para>
    /// ⚠ <b>블록·골대·블롭의 충돌은 여기 없다</b> (패스 ①-c) — 유니티 2D 물리(Box2D)가 푼다.
    /// 감지기가 남은 이유는 <b>원본에서도 감지기가 물리체가 아니기</b> 때문이다
    /// [4회차 실측 — W1L1 물리체 38개에 감지기가 없다].
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>크기를 코드에 박지 않는다</b> — 데이터에서 온다.
    /// </para>
    /// </summary>
    public struct BlumgiAabb
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;

        public static BlumgiAabb FromCenterSize(double centerX, double centerY, double width, double height)
        {
            double hw = width * 0.5;
            double hh = height * 0.5;

            return new BlumgiAabb
            {
                MinX = centerX - hw,
                MinY = centerY - hh,
                MaxX = centerX + hw,
                MaxY = centerY + hh,
            };
        }

        /// <summary>
        /// <b>사각</b>과 겹치나 — 중심 <paramref name="center"/> · 반변 <paramref name="halfSize"/>.
        ///
        /// <para>
        /// ★★ <b>원이 아니라 사각이다</b> [6회차 실측]. Construct 3 의 <c>IsOverlapping</c>/<c>OnCollision</c> 은
        /// Box2D 도형이 아니라 <b>스프라이트 충돌 폴리곤</b>을 쓴다 — 공은 물리로는 원(r 43.5)이지만
        /// <b>감지에는 87 × 87 사각</b>이다. 엔진 <c>TestOverlap</c> 4066 샘플 채점에서
        /// 사각 4062 / 원 4055 였고 <b>두 모델이 갈리는 7프레임은 전부 사각이 맞았다</b>.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>회전은 안 본다 — 미측정이다.</b> 공은 구르므로 원본 폴리곤도 같이 도는데
        /// 6회차가 회전각을 채록하지 않아 <b>정지 AABB 로 근사</b>했다 (4066 중 4건 어긋남).
        /// </para>
        /// </summary>
        public bool OverlapsBox(BlumgiVec2 center, double halfSize)
        {
            return center.X + halfSize > MinX
                   && center.X - halfSize < MaxX
                   && center.Y + halfSize > MinY
                   && center.Y - halfSize < MaxY;
        }
    }
}
