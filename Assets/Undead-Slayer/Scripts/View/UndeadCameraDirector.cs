using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 카메라가 히어로를 따라간다.
    ///
    /// <para>
    /// ★★ <b>«앞서 보는 것»이 아니라 «뒤처지는 것»이다</b> [소스 직독 · 회차 9] —
    /// 원본은 <c>pivot += (hero − pivot) × n</c>, <c>n = 1 − (1 − 0.1)^(dt/16.667)</c> 인
    /// <b>지수 추적</b>이다. 그래서 움직이는 동안 히어로가 진행 방향으로 «밀려 보인다».
    /// </para>
    ///
    /// <para>
    /// ⚠ 회차 1~8 은 이 현상을 보고 「리드 25.5」라는 <b>없는 규칙</b>을 만들었다.
    /// 화면은 비슷했지만 <b>멈출 때의 되돌아옴</b>이 원본과 달랐다 —
    /// 「보이는 것을 설명하는 규칙」과 「원본이 가진 규칙」은 다른 것이다.
    /// </para>
    ///
    /// <para>
    /// ★ 추적 «계산»은 시뮬(<see cref="UndeadSimulation.CameraPivot"/>)이 한다 —
    /// 스폰 자리·조준 후보·화면 밖 판정이 전부 카메라를 보기 때문에 <b>시뮬 안에 있어야</b> 결정적이다.
    /// 여기서는 그 값을 <b>유니티 좌표로 옮기기만</b> 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 원본에는 <b>피격 시 화면 흔들림</b>(<c>triggerShake</c>)과 모바일 줌 0.75 가 있다.
    /// 이관 범위(데스크톱 1판)에서는 줌 1 이고, 흔들림은 <b>아직 안 옮겼다</b>(원장 등재).
    /// </para>
    /// </summary>
    public sealed class UndeadCameraDirector : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        /// <summary>화면 중앙에서 아래로 내린 양 (원본 world px) [소스 — <c>dHeight/2 + 20</c>].</summary>
        [SerializeField] private float _offsetY = 20f;

        public void SetOffsetY(double offsetY)
        {
            _offsetY = (float)offsetY;
        }

        private float _shakeAmplitude;
        private float _shakeRemaining;
        private float _shakeDuration;

        /// <summary>나무가 터질 때 [소스 <c>triggerShake(10, 300)</c>] — 세기가 시간에 따라 줄어드는 무작위 흔들림.</summary>
        public void Shake(double amplitudeWorldPx, double seconds)
        {
            _shakeAmplitude = (float)amplitudeWorldPx;
            _shakeDuration = (float)seconds;
            _shakeRemaining = (float)seconds;
        }

        /// <summary>시뮬이 계산한 <b>카메라 피벗</b>을 그대로 옮긴다.</summary>
        public void Follow(UndeadVec2 pivot)
        {
            if (_camera == null)
                return;

            // 피벗은 «화면 중앙보다 offsetY 만큼 아래»에 놓인다 [소스] —
            // 그래서 화면 중앙이 보는 월드 점은 피벗에서 그만큼 위(원본 y 감소)다.
            Vector3 position = UndeadUnits.ToPosition(pivot.X, pivot.Y - _offsetY);

            if (_shakeRemaining > 0f && _shakeDuration > 0f)
            {
                _shakeRemaining -= Time.deltaTime;
                float strength = _shakeAmplitude * Mathf.Clamp01(_shakeRemaining / _shakeDuration);
                position.x += UndeadUnits.ToUnits(UnityEngine.Random.Range(-strength, strength));
                position.y += UndeadUnits.ToUnits(UnityEngine.Random.Range(-strength, strength));
            }

            position.z = _camera.transform.position.z;
            _camera.transform.position = position;
        }
    }
}
