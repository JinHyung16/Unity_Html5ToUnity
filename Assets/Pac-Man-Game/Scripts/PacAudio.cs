using UnityEngine;

namespace JinHyung.PacMan
{
    /// <summary>
    /// 이 게임의 소리. 원본이 <b>Web Audio 로 합성하던 것</b>을 구운 클립으로 낸다 (확정 B).
    ///
    /// <para>
    /// ⚠ <b>공용으로 올리지 않는다.</b> 오디오를 쓰는 첫 게임이라
    /// 「두 번째 게임이 같은 것을 요구하면」 그때 승격한다 —
    /// 지금 공용에 올리면 <b>아무도 검증하지 않는 코드</b>가 된다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>원본은 «첫 입력»에 배경음을 켠다</b> (브라우저가 제스처 없이 오디오를 못 켠다).
    /// 유니티에는 그 제약이 없지만 <b>시점을 맞춘다</b> — 시작하자마자 울리면 원본과 다르다.
    /// </para>
    /// </summary>
    public class PacAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource _bgm;
        [SerializeField] private AudioSource _sfx;

        [SerializeField] private AudioClip _siren;
        [SerializeField] private AudioClip _pellet;
        [SerializeField] private AudioClip _death;

        private bool _bgmStarted;

        /// <summary>원본 <c>startBackgroundSound</c> — <b>한 번만</b> 켜지고 끝나지 않는다.</summary>
        public void StartBackground()
        {
            if (_bgmStarted || _bgm == null || _siren == null)
                return;

            _bgmStarted = true;
            _bgm.clip = _siren;
            _bgm.loop = true;
            _bgm.Play();
        }

        /// <summary>원본 <c>playPelletSound</c>.</summary>
        public void PlayPellet()
        {
            PlayOnce(_pellet);
        }

        /// <summary>원본 <c>playDeathSound</c>.</summary>
        public void PlayDeath()
        {
            PlayOnce(_death);
        }

        private void PlayOnce(AudioClip clip)
        {
            if (_sfx == null || clip == null)
                return;

            // ⚠ `Play` 가 아니라 `PlayOneShot` 이다 — 펠릿은 «연달아» 먹히므로
            //   앞 소리를 끊으면 원본과 달리 뚝뚝 끊긴다.
            _sfx.PlayOneShot(clip);
        }
    }
}
