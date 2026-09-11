using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>입자 한 알 — <b>«물리» 와 «그림» 을 한 구조체에 둔다</b>.</summary>
    /// <remarks>
    /// 원본은 상태(<c>particleStates</c>)와 스프라이트(<c>particleSprites</c>)를 <b>두 배열</b>로 나눠 든다.
    /// 우리는 그리는 쪽이 풀에서 렌더러를 빌려 쓰므로 «스프라이트에 써 두는 값»도 상태로 든다 —
    /// 그래야 <b>렌더러가 몇 번 슬롯을 받든 그림이 같다</b>.
    /// </remarks>
    public struct UndeadParticle
    {
        public bool Active;

        // ── 컨테이너 «안» 좌표·속도다. 화면 값은 컨테이너 배율을 곱한 것이다.
        public double X;
        public double Y;
        public double Vx;
        public double Vy;

        public double LifeMs;
        public double MaxLifeMs;
        public double Size;

        // ── 그림
        public double ScaleX;
        public double ScaleY;
        public double Alpha;
        public Color Tint;
    }

    /// <summary>
    /// 입자 이미터의 <b>설정</b> [소스 <c>Ll</c> 의 생성자 인자].
    /// <para>⚠ <c>ContainerScale</c>·<c>ContainerY</c> 는 «그리는 쪽»이 곱한다 — 물리는 컨테이너 안에서 돈다.</para>
    /// </summary>
    public readonly struct UndeadParticleConfig
    {
        public readonly int MaxParticles;
        public readonly double ContainerScaleX;
        public readonly double ContainerScaleY;
        public readonly double ContainerY;
        public readonly double SpawnAccumulatorCap;

        public UndeadParticleConfig(int maxParticles, double containerScaleX, double containerScaleY,
                                    double containerY, double spawnAccumulatorCap)
        {
            MaxParticles = maxParticles;
            ContainerScaleX = containerScaleX;
            ContainerScaleY = containerScaleY;
            ContainerY = containerY;
            SpawnAccumulatorCap = spawnAccumulatorCap;
        }
    }

    /// <summary>
    /// <b>입자 이미터</b> [소스 <c>Ll</c>] — 모닥불 하트도 · 나무 반짝임도 · 포털 반짝임도 <b>이 한 틀</b>이다.
    ///
    /// <para>
    /// ★ <b>이미터는 «개체 하나»에 하나다</b> [소스 — 나무마다·모닥불마다·포털마다 <c>new</c>].
    /// 알 수는 <c>MaxParticles</c> 로 <b>이미터가 스스로</b> 막는다 — 공용 풀에 「주인마다 3개」 같은
    /// 셈을 따로 두면 원본에 없는 규칙이 하나 더 생긴다.
    /// </para>
    ///
    /// <para>
    /// ★ 난수는 <b>자리로 시드가 걸린다</b> [소스 <c>El(`${x}_${y}_sparks`)</c>] —
    /// 같은 나무는 다시 와도 같은 모양으로 튄다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>dt 는 «밀리초»</b>다 [소스 — 원본 <c>update(t)</c> 의 <c>t</c> 가 ms]. 수명 상수가 전부 ms 기준이라
    /// 초로 넘기면 알이 1000배 오래 산다.
    /// </para>
    /// </summary>
    public abstract class UndeadParticleEmitter
    {
        private readonly UndeadParticle[] _particles;
        private UndeadSeededRandom _rng;
        private double _spawnAccumulator;

        protected UndeadParticleEmitter(UndeadParticleConfig config, string seed)
        {
            Config = config;
            _particles = new UndeadParticle[config.MaxParticles];
            _rng = new UndeadSeededRandom(seed);
        }

        public UndeadParticleConfig Config { get; }

        public int Capacity
        {
            get { return _particles.Length; }
        }

        public UndeadParticle this[int index]
        {
            get { return _particles[index]; }
        }

        /// <summary>
        /// 여태 <b>나온</b> 알 수. 「누적 상한」은 <b>살아 있는 수로는 못 잰다</b> —
        /// 상한이 걸릴 만큼 긴 프레임(0.5초 넘게)이면 <b>그 프레임 안에서 다 죽는다</b>(수명 최대 .62초).
        /// </summary>
        public long SpawnedTotal { get; private set; }

        /// <summary>지금 살아 있는 알 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i].Active)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// 한 프레임 [소스 <c>update(t, e)</c>].
        /// <para>⚠ <paramref name="enabled"/> 가 거짓이면 <b>새로 안 나올 뿐</b>이다 — 있던 알은 계속 돈다 [소스].</para>
        /// </summary>
        public void Step(double dtMs, double progress, bool enabled)
        {
            if (dtMs <= 0.0)
                return;

            double dtSec = dtMs / 1000.0;
            double rate = enabled ? SpawnRate(progress) : 0.0;

            _spawnAccumulator += rate * dtSec;

            if (_spawnAccumulator > Config.SpawnAccumulatorCap)
                _spawnAccumulator = Config.SpawnAccumulatorCap;

            int spawn = (int)System.Math.Floor(_spawnAccumulator);

            if (spawn > 0)
            {
                _spawnAccumulator -= spawn;
                Emit(spawn, progress);
            }

            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].Active == false)
                    continue;

                _particles[i].LifeMs -= dtMs;

                if (_particles[i].LifeMs <= 0.0)
                {
                    _particles[i].Active = false;
                    _particles[i].Alpha = 0.0;
                    continue;
                }

                double lifeProgress = 1.0 - _particles[i].LifeMs / _particles[i].MaxLifeMs;
                UpdateActive(ref _particles[i], dtSec, lifeProgress);
            }
        }

        /// <summary>한꺼번에 뿜는다 [소스 <c>emitBurst</c>] — <b>이미터가 꺼져 있어도 나온다</b>(나무가 터질 때).</summary>
        public void EmitBurst(int count, double progress)
        {
            Emit(count, progress);
        }

        /// <summary>0 ≤ v &lt; 1.</summary>
        protected double Random()
        {
            return _rng.Next();
        }

        protected abstract double SpawnRate(double progress);

        protected abstract UndeadParticle CreateSpawn(double progress);

        protected abstract void UpdateActive(ref UndeadParticle particle, double dtSec, double lifeProgress);

        private void Emit(int count, double progress)
        {
            for (int i = 0; i < count; i++)
            {
                int slot = FreeSlot();

                if (slot < 0)
                    return;

                UndeadParticle spawned = CreateSpawn(progress);
                spawned.Active = true;
                spawned.MaxLifeMs = spawned.LifeMs;
                _particles[slot] = spawned;
                SpawnedTotal++;
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].Active == false)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// <b>반짝임</b> [소스 <c>hd</c> · 설정 <c>ad</c>] — 사악한 나무와 <b>로비 포털</b>이 같이 쓴다.
    ///
    /// <para>
    /// ★ 알은 <b>흰 1×1 텍스처</b>다 [소스 <c>K.WHITE</c>] — 그림이 아니라 «색 있는 막대»다.
    /// 컨테이너가 세로로 4배 늘어나 있어(<c>containerScaleY: 4</c>) 가늘고 긴 불티가 된다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>색은 나이로 갈린다</b> [소스 <c>updateActiveParticle</c>] — 0.4 전 · 0.78 전 · 그 뒤.
    /// 팔레트는 셋이다 (불 · 보라 · 겨울).
    /// </para>
    /// </summary>
    public sealed class UndeadSparkleEmitter : UndeadParticleEmitter
    {
        /// <summary>[소스 <c>ad</c>].</summary>
        public static readonly UndeadParticleConfig SparkleConfig =
            new UndeadParticleConfig(32, 1.5, 4.0, -10.0, 8.0);

        private readonly Color[] _colors;

        public UndeadSparkleEmitter(string seed, Color[] palette)
            : base(SparkleConfig, seed)
        {
            _colors = palette;
        }

        /// <summary>초당 몇 알 [소스 <c>16 + 6·progress</c>] — 나무가 차오를수록 빨라진다.</summary>
        protected override double SpawnRate(double progress)
        {
            return 16.0 + 6.0 * progress;
        }

        protected override UndeadParticle CreateSpawn(double progress)
        {
            // [소스 createParticleSpawn] e = 2r−1 · x = e·(8+14r) · y = 10r−5
            //   size = .8+.4r+.4p · vx = e·(12+30r) · vy = −(30+44r+28p) · life = 320+300r+140p
            double e = 2.0 * Random() - 1.0;
            double size = 0.8 + 0.4 * Random() + 0.4 * progress;

            return new UndeadParticle
            {
                X = e * (8.0 + 14.0 * Random()),
                Y = 10.0 * Random() - 5.0,
                Vx = e * (12.0 + 30.0 * Random()),
                Vy = -(30.0 + 44.0 * Random() + 28.0 * progress),
                LifeMs = 320.0 + 300.0 * Random() + 140.0 * progress,
                Size = size,
                ScaleX = size,
                ScaleY = 1.1 * size,
                Alpha = 0.95,
                Tint = _colors[0],
            };
        }

        protected override void UpdateActive(ref UndeadParticle particle, double dtSec, double lifeProgress)
        {
            // [소스] vy += .1·−dt · x += vx·dt/7 · y += vy·dt/2 · alpha = 1
            particle.Vy += 0.1 * -dtSec;
            particle.X += particle.Vx * dtSec / 7.0;
            particle.Y += particle.Vy * dtSec / 2.0;
            particle.Alpha = 1.0;

            double grown = particle.Size * (1.0 + 0.35 * lifeProgress);
            particle.ScaleX = 0.65 * grown;
            particle.ScaleY = 1.25 * grown;
            particle.Tint = lifeProgress < 0.4 ? _colors[1] : lifeProgress < 0.78 ? _colors[2] : _colors[3];
        }
    }

    /// <summary>
    /// <b>하트</b> [소스 <c>Gl</c> · 설정 <c>zl</c>] — 재고가 남은 모닥불 위로 떠오른다.
    /// <para>모닥불 하나에 <b>세 알</b>뿐이다 — 그 셈은 <c>MaxParticles</c> 가 한다.</para>
    /// </summary>
    public sealed class UndeadHeartEmitter : UndeadParticleEmitter
    {
        /// <summary>[소스 <c>zl</c>].</summary>
        public static readonly UndeadParticleConfig HeartConfig =
            new UndeadParticleConfig(3, 1.3, 1.8, -14.0, 3.0);

        public UndeadHeartEmitter(string seed)
            : base(HeartConfig, seed)
        {
        }

        /// <summary>[소스 <c>getParticleSpawnRate() = 1.2</c>] — 차오름과 무관하다.</summary>
        protected override double SpawnRate(double progress)
        {
            return 1.2;
        }

        protected override UndeadParticle CreateSpawn(double progress)
        {
            // [소스] e = 2r−1 · x = e·(3+6r) · y = 8r−10 · vx = e·(1.5+3.5r) · vy = −(5+8r)
            //   life = 1100+500r · size = .4+.1r
            double e = 2.0 * Random() - 1.0;
            double size = 0.4 + 0.1 * Random();

            return new UndeadParticle
            {
                X = e * (3.0 + 6.0 * Random()),
                Y = 8.0 * Random() - 10.0,
                Vx = e * (1.5 + 3.5 * Random()),
                Vy = -(5.0 + 8.0 * Random()),
                LifeMs = 1100.0 + 500.0 * Random(),
                Size = size,
                ScaleX = size,
                ScaleY = size,
                Alpha = 0.8,
                Tint = Color.white,
            };
        }

        protected override void UpdateActive(ref UndeadParticle particle, double dtSec, double lifeProgress)
        {
            // ⚠ 하트는 «초»로 움직인다 [소스 — vx *= 1−.22·dtSec]. 반짝임과 식이 다르다.
            particle.Vx *= 1.0 - 0.22 * dtSec;
            particle.Vy -= 0.45 * dtSec;
            particle.X += particle.Vx * dtSec;
            particle.Y += particle.Vy * dtSec;

            double grown = particle.Size * (1.0 + 0.08 * lifeProgress);
            particle.ScaleX = grown;
            particle.ScaleY = grown;
            particle.Alpha = 0.8 * (1.0 - lifeProgress);
            particle.Tint = Color.white;
        }
    }

    /// <summary>
    /// 입자를 <b>화면에 놓는 규칙</b> — 전투 뷰와 로비 뷰가 <b>같은 한 곳</b>을 지난다.
    ///
    /// <para>
    /// ⚠ 컨테이너 배율·높이를 여기서 곱한다. 뷰마다 따로 곱하면 <b>한쪽만 궤적이 좁아지는데</b>
    /// 둘을 나란히 놓고 보기 전에는 안 보인다.
    /// </para>
    /// </summary>
    public static class UndeadParticleView
    {
        private static Sprite _white;

        /// <summary>
        /// 입자가 쓰는 <b>흰 1×1 스프라이트</b> [소스 <c>K.WHITE</c>] —
        /// 그림이 아니라 «렌더러 원시 조각»이라 <b>아트 표를 타지 않는다</b>.
        /// <para>⚠ PPU 는 <see cref="UndeadUnits.WorldPixelsPerUnit"/> 다 — 그래야 <b>배율 1 = 원본 1 world px</b> 다.</para>
        /// </summary>
        public static Sprite White
        {
            get
            {
                if (_white != null)
                    return _white;

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "UndeadWhitePixel" };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                _white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f),
                                       UndeadUnits.WorldPixelsPerUnit);
                _white.name = "UndeadWhitePixel";
                return _white;
            }
        }

        /// <summary>알 하나를 놓는다. 돌려주는 값은 <b>원본 world y</b> 다 — 정렬 순서는 뷰마다 규약이 달라 뷰가 정한다.</summary>
        public static double Draw(SpriteRenderer renderer, UndeadParticle particle, UndeadParticleConfig config,
                                  UndeadVec2 ownerPosition, double emitterY)
        {
            double worldX = ownerPosition.X + particle.X * config.ContainerScaleX;
            double worldY = ownerPosition.Y + emitterY + config.ContainerY + particle.Y * config.ContainerScaleY;

            renderer.transform.localPosition = UndeadUnits.ToPosition(worldX, worldY);
            renderer.transform.localScale = new Vector3((float)(particle.ScaleX * config.ContainerScaleX),
                                                        (float)(particle.ScaleY * config.ContainerScaleY), 1f);
            renderer.color = new Color(particle.Tint.r, particle.Tint.g, particle.Tint.b, (float)particle.Alpha);
            return worldY;
        }
    }

    /// <summary>
    /// 반짝임 <b>팔레트 셋</b> [소스 <c>od</c>] — 색은 <b>0 = 처음 · 1·2·3 = 나이순</b>이다.
    /// <para>⚠ 값이 원본의 것이라 튜너블이 아니다.</para>
    /// </summary>
    public static class UndeadSparklePalette
    {
        /// <summary>사악한 나무 [소스 <c>fire</c>].</summary>
        public static readonly Color[] Fire = { Hex(0xFF932D), Hex(0xFF7D1A), Hex(0xFFB347), Hex(0xFFE0A3) };

        /// <summary>묘지 포털 [소스 <c>purple</c>].</summary>
        public static readonly Color[] Purple = { Hex(0xB45CFF), Hex(0x923CFF), Hex(0xD69AFF), Hex(0xF2DDFF) };

        /// <summary>겨울 포털 [소스 <c>winter</c>].</summary>
        public static readonly Color[] Winter = { Hex(0x8FD8FF), Hex(0x5EBCFF), Hex(0xB8E8FF), Hex(0xF0FBFF) };

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
