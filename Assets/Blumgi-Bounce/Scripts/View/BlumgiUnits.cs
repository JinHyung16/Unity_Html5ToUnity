using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★★ <b>원본 world px ↔ 유니티 유닛 환산은 여기 한 곳만 통과한다.</b>
    ///
    /// <para>
    /// ★ <b>확정 E 개정 반영 완료</b> (패스 ①-c · 4회차 실측 근거).
    /// 원본 물리가 <b>Box2D</b> 이고 그 월드는 «미터»다 — Construct <c>worldScale = 0.02</c>,
    /// 즉 <b>1 m = 50 원본 px</b> (검산: Box2D 중력 30 m/s² ÷ 0.02 = 1500 px/s² 로 3회차 실측과 일치).
    /// Box2D 는 <b>절대 크기에 의존하는 내부 상수</b>(접촉 슬롭 · 반발이 죽는 속도 임계)를 갖기 때문에
    /// 확정 E 의 「world 1:1 · PPU 1」을 <b>「1 유닛 = 1 m = 원본 50 px · PPU 50」</b> 으로 개정했다.
    /// </para>
    ///
    /// <para>
    /// ★ 그래서 <b>PPU 를 코드 여기저기에 상수로 박지 않는다.</b>
    /// 임포터도 · 프리팹 빌더도 · 배치도 전부 <see cref="WorldPixelsPerUnit"/> 하나를 읽는다 —
    /// 확정이 뒤집혀도 <b>이 한 줄</b>만 고치면 된다 (UIUX.md 「환산 상수는 한 곳에서만」).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>스프라이트를 굽는 해상도는 이 값과 무관하다.</b> 굽는 크기는
    /// <c>04_UIUX규칙.md</c> 2-g 「스프라이트 픽셀 크기 전수」의 <b>소스 텍스처 px</b> 가 정한다 —
    /// PPU 는 그 텍스처가 <b>몇 유닛으로 보이는가</b>만 정한다.
    /// </para>
    /// </summary>
    public static class BlumgiUnits
    {
        /// <summary>
        /// ★ <b>단 하나의 환산 상수</b> — 원본 world px 몇 개가 유니티 1 유닛인가.
        ///
        /// <para>
        /// 현재 값 <c>50</c> = <b>확정 E 개정</b> — 1 유닛 = 1 미터 = 원본 50 px (Construct <c>worldScale 0.02</c> 의 역수).
        /// </para>
        ///
        /// <para>
        /// ★★ <b>왜 1:1 이면 안 되나.</b> Box2D 는 «미터»를 전제로 한 절대 상수를 갖는다 —
        /// 접촉 슬롭(<c>b2_linearSlop</c> 0.005 m) 과 <b>반발이 죽는 속도 임계</b>(<c>b2_velocityThreshold</c> 1 m/s).
        /// 1 유닛 = 1 px 로 두면 그 임계가 상대적으로 <b>50배 작아져 공이 원본보다 오래 튄다</b> —
        /// 원본 파리티의 실패 모드와 정확히 겹친다. 그래서 <b>스케일을 원본과 맞추는 것 자체가 파라미터의 일부</b>다.
        /// </para>
        ///
        /// <para>
        /// 부수 검산 — 이 스케일에서 유니티 2D 기본 <c>Physics2D.defaultContactOffset</c> 0.01 유닛이
        /// <b>0.5 px</b> 이고, 4회차가 Box2D fixture 에서 읽은 <b>폴리곤 스킨 0.5 px</b> 와 같다 [실측].
        /// </para>
        ///
        /// <para>
        /// 원장이 요구하는 「데이터는 px 그대로 두고 <b>로드 시 한 곳에서</b> ×0.02」의 «그 한 곳»이 여기다 —
        /// <see cref="ToUnits"/> · <see cref="ToPosition"/> 말고 다른 데서 나누지 않는다.
        /// </para>
        /// </summary>
        public const float WorldPixelsPerUnit = 50f;

        /// <summary>
        /// UI 캔버스의 <c>referencePixelsPerUnit</c>.
        /// ⚠ <b>UI 스프라이트 PPU 와 «같은 값»이어야 한다</b> — 어긋나면 <c>Image</c> 가 9-slice 경계를
        /// 그 비로 나눠 써 <b>모서리가 통째로 늘어난다</b> (첫 게임 실측 사고).
        /// UI 는 월드와 분리돼 있으므로(확정 E) 위 상수의 영향을 받지 않는다.
        /// </summary>
        public const float UiPixelsPerUnit = 100f;

        /// <summary>설계 뷰포트 세로 [실측 <c>_originalViewportHeight</c>]. <b>세로가 고정되는 축</b>이다 (scale outer).</summary>
        public const float DesignViewportHeight = 1280f;

        /// <summary>
        /// 직교 카메라 크기 (유닛). 원본은 <b>세로 1280 world 고정</b>이므로 반이 640 world 다 [UIUX 1-a].
        /// ⚠ 정착 배율은 5레벨 전부 1.0 이고, <b>레벨 시작 줌 펀치는 «연출»이지 레벨 데이터가 아니다</b> [UIUX 2-f].
        /// </summary>
        public static float CameraOrthographicSize
        {
            get { return DesignViewportHeight * 0.5f / WorldPixelsPerUnit; }
        }

        /// <summary>원본 world 길이 → 유닛.</summary>
        public static float ToUnits(double worldLength)
        {
            return (float)(worldLength / WorldPixelsPerUnit);
        }

        /// <summary>
        /// 원본 world 좌표 → 유니티 위치.
        /// ⚠ 원본(Construct 3)은 <b>아래가 +y</b> 이고 유니티는 위가 +y 다 [UIUX 1-g] — <b>여기서만</b> 뒤집는다.
        /// 원점은 원본 레이아웃 좌상단(0,0) 그대로다.
        /// </summary>
        public static Vector3 ToPosition(double worldX, double worldY)
        {
            return new Vector3((float)(worldX / WorldPixelsPerUnit),
                               (float)(-worldY / WorldPixelsPerUnit),
                               0f);
        }

        /// <summary>
        /// 원본 world 벡터(속도 등) → 유니티 벡터. 위치와 <b>같은 y 뒤집기</b>를 탄다.
        /// 속도를 위치와 다른 규칙으로 넘기면 궤적이 «위아래만» 뒤집힌 채로 그럴듯하게 돌아간다.
        /// </summary>
        public static Vector2 ToVector(double worldX, double worldY)
        {
            return new Vector2((float)(worldX / WorldPixelsPerUnit),
                               (float)(-worldY / WorldPixelsPerUnit));
        }

        /// <summary>
        /// ★★ 원본 각속도(<b>°/s</b>) → 유니티 <c>Rigidbody2D.angularVelocity</c>(<b>°/s</b>).
        ///
        /// <para>
        /// ⚠ <b>부호가 뒤집힌다.</b> <see cref="ToPosition"/> · <see cref="ToVector"/> 가 y 를 뒤집는 것은
        /// <b>반사</b>라 <b>회전의 방향(향)이 반전</b>된다 — 길이·속도처럼 「나누기만」 하면 <b>공이 반대로 돈다</b>.
        /// 원본 저작값 <b>−200 °/s</b> 는 유니티에서 <b>+200 °/s</b> 다.
        /// </para>
        ///
        /// <para>
        /// ★ <b>이 부호는 추측이 아니라 접촉 정답지가 가른 것</b>이다 — 채점기가 정답지의 ω 를
        /// 엔진에 넣고 되읽을 때 쓰는 규약(<c>엔진 ω = −원본 ω</c>)과 <b>같은 규약</b>이고,
        /// 그 규약 위에서 접촉 정답지의 ω′ 예측이 맞아 왔다. 여기만 다른 규약을 쓰면
        /// <b>채점기와 게임이 서로 다른 축을 쓰게 된다.</b>
        /// </para>
        /// </summary>
        public static float ToAngularVelocityDegrees(double worldDegreesPerSecond)
        {
            return (float)(-worldDegreesPerSecond);
        }

        /// <summary>유니티 길이 → 원본 world 길이. 엔진에서 «되읽을» 때 쓴다.</summary>
        public static double ToWorldLength(float units)
        {
            return units * (double)WorldPixelsPerUnit;
        }

        /// <summary>
        /// 유니티 벡터 → 원본 world 벡터 (위치·속도 공용).
        /// <see cref="ToVector"/> 의 역이다 — <b>y 를 여기서 되뒤집는다</b>.
        /// </summary>
        public static void ToWorld(Vector2 value, out double worldX, out double worldY)
        {
            worldX = value.x * (double)WorldPixelsPerUnit;
            worldY = -value.y * (double)WorldPixelsPerUnit;
        }

        /// <summary>
        /// 스프라이트 «표시 크기»가 소스 텍스처와 다를 때 쓰는 배율.
        /// [실측] 야자수 소스 179×96 → 표시 434×233(2.43배) · <c>ArrowRestart</c> 소스 100×170 → 표시 86×50.
        /// <b>이 배율은 PPU 와 무관한 순수 비율</b>이라 재검토의 영향을 받지 않는다.
        /// </summary>
        public static Vector3 DisplayScale(float sourceWidth, float sourceHeight,
                                           float displayWidth, float displayHeight)
        {
            return new Vector3(displayWidth / sourceWidth, displayHeight / sourceHeight, 1f);
        }
    }
}
