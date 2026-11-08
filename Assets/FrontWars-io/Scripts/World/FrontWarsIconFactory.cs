using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 지도 위 유닛 · 건물 아이콘 — 흰 실루엣을 코드로 그려 캐시한다. 색은 주인 색으로 칠한다.
    /// <para>원본은 SVG 아이콘(CityIconWhite.svg 등)을 쓴다 — 옮기지 않았다 (R11 · 확정 「퀄리티 동일 · 다시 그림」). 원본을 베낀 것이 아니라 「무엇인지 알아보는 실루엣」 을 새로 그렸다.</para>
    /// <para>그리는 법: 64×64 에 부호 거리로 흰색 채우기 + 가장자리만 어둡게. 지도에서 20픽셀 안팎이라 가장 가는 획도 반폭 0.10(정규화) 밑으로 내리지 않는다.</para>
    /// </summary>
    public static class FrontWarsIconFactory
    {
        /// <summary>
        /// 지도 아이콘 모양 — 「무엇인지 알아보는 실루엣」 이 이름이다 (도형 이름이 아니다).
        /// <para>건물은 이 실루엣이 바탕 도형 안의 기호가 된다 (한 변 = 도형의 16/28). 움직이는 유닛은 이 실루엣 자체를 칸 크기로 그린다 — 크기는 <see cref="FrontWarsUnitView"/> 표에 있다.</para>
        /// </summary>
        public enum EShape
        {
            /// <summary>도시 — 높이가 다른 건물 세 채.</summary>
            City,

            /// <summary>공장 — 낮고 넓은 건물 + 굴뚝 + 연기.</summary>
            Factory,

            /// <summary>항구 — 닻.</summary>
            Anchor,

            /// <summary>방어초소 — 방패.</summary>
            Shield,

            /// <summary>미사일 격납고 — 발사대에 선 수직 미사일.</summary>
            Missile,

            /// <summary>SAM — 바퀴 달린 차대에 비스듬히 얹은 미사일.</summary>
            SamLauncher,

            /// <summary>군함 — 선체 + 함교 + 포신.</summary>
            Warship,

            /// <summary>무역선 — 선체 + 쌓은 상자.</summary>
            TradeShip,

            /// <summary>수송선 — 선체 + 사람.</summary>
            TransportShip,

            /// <summary>기차 — 운전실 · 굴뚝 · 바퀴 달린 기관차.</summary>
            Train,

            /// <summary>원자폭탄 — 보통 크기 방사능 표식.</summary>
            RadiationSmall,

            /// <summary>수소폭탄 — 크고 두꺼운 방사능 표식.</summary>
            RadiationLarge,

            /// <summary>MIRV — 작은 방사능 표식 + 둘레의 탄두 세 개.</summary>
            RadiationMirv,

            /// <summary>MIRV 탄두 — 가장 작은 방사능 표식.</summary>
            RadiationWarhead,

            /// <summary>포탄 — 작은 점.</summary>
            Dot,

            /// <summary>요격 미사일 — 날개 달린 뾰족한 화살.</summary>
            Dart,

            /// <summary>공사 중 — 망치.</summary>
            Hammer,

            /// <summary>별 — 지도에 안 쓴다. 게임 종료 창이 쓴다.</summary>
            Star
        }

        /// <summary>HUD 버튼 아이콘 — 원본 SVG 대신 코드로 그린 흰 도형 (색은 이미지 색으로 칠한다).</summary>
        public enum EUiIcon
        {
            Pause,
            Play,
            Close,
            Gear,
            Replay,
            Menu,
            Back
        }

        private const int TextureSize = 64;
        private const float PixelsPerUnit = TextureSize;
        /// <summary>바깥 테두리 두께 (픽셀) — 실루엣 안쪽은 흰색, 가장자리만 어둡게 해 땅 색과 갈라 놓는다.
        /// <para>아이콘에 잔가지가 생겨서 3 → 1.5 로 줄였다. 가장 가는 획(반폭 0.10)도 흰 속이 3픽셀쯤 남는다.</para></summary>
        private const float OutlineWidth = 1.5f;

        private static readonly Dictionary<EShape, Sprite> Cache = new Dictionary<EShape, Sprite>();
        private static readonly Dictionary<EUiIcon, Sprite> UiCache = new Dictionary<EUiIcon, Sprite>();

        /// <summary>HUD 아이콘 — 테두리 없는 흰 도형. 스프라이트 이름은 「FrontWarsUiIcon_종류」.</summary>
        public static Sprite GetUi(EUiIcon icon)
        {
            if (UiCache.TryGetValue(icon, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Build($"FrontWarsUiIcon_{icon}", (x, y) => UiSignedDistance(icon, x, y), false);
            UiCache[icon] = sprite;
            return sprite;
        }

        /// <summary>
        /// 부호 거리 하나를 64×64 스프라이트로 굽는다 — 안쪽은 흰색, <paramref name="shadeEdge"/> 면 가장자리만 어둡게.
        /// <para>한 변이 월드 1 단위다 (쓰는 쪽이 칸 수로 키운다).</para>
        /// </summary>
        private static Sprite Build(string name, Func<float, float, float> distance, bool shadeEdge)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };

            Color32[] pixels = Draw(distance, shadeEdge);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        /// <summary>도형 픽셀만 그린다 (텍스처로 올리지 않는다) — 이모지를 TMP 스프라이트로 굽는 도구가 이 값을 읽는다.</summary>
        private static Color32[] Draw(Func<float, float, float> distance, bool shadeEdge)
        {
            var pixels = new Color32[TextureSize * TextureSize];
            float c = (TextureSize - 1) / 2f;
            float r = c - 1f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float d = distance((x - c) / r, (y - c) / r) * r;
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    byte shade = shadeEdge && d > -OutlineWidth ? (byte)40 : (byte)255;
                    pixels[y * TextureSize + x] = new Color32(shade, shade, shade, alpha);
                }
            }

            return pixels;
        }

        /// <summary>HUD 아이콘 부호 거리 — 정규화 좌표(−1~1, y 위로 +), 음수가 안쪽.</summary>
        private static float UiSignedDistance(EUiIcon icon, float x, float y)
        {
            switch (icon)
            {
                case EUiIcon.Pause:
                    return Mathf.Min(Box(x - 0.35f, y, 0.18f, 0.7f), Box(x + 0.35f, y, 0.18f, 0.7f));
                case EUiIcon.Play:
                    return Mathf.Max(-x - 0.6f, (Mathf.Abs(y) * 1.7f + x) / 2f - 0.4f);
                case EUiIcon.Close:
                {
                    float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
                    return Mathf.Min(Box(u, v, 0.85f, 0.16f), Box(v, u, 0.85f, 0.16f));
                }
                case EUiIcon.Gear:
                {
                    float radius = Mathf.Sqrt(x * x + y * y);
                    float teeth = 0.66f + 0.16f * Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(y, x) * 8f) * 3f);
                    return Mathf.Max(radius - teeth, 0.26f - radius);
                }
                case EUiIcon.Replay:
                {
                    float radius = Mathf.Sqrt(x * x + y * y);
                    float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                    float ring = Mathf.Abs(radius - 0.6f) - 0.14f;

                    // 오른쪽 위 40° 를 비우고 그 끝에 아래를 향한 화살촉
                    float gap = angle > -10f && angle < 45f ? 1f : -1f;
                    float arc = Mathf.Max(ring, gap);
                    float hx = x - 0.6f * Mathf.Cos(45f * Mathf.Deg2Rad), hy = y - 0.6f * Mathf.Sin(45f * Mathf.Deg2Rad);
                    float head = Mathf.Max(hy - 0.12f, (Mathf.Abs(hx) * 1.3f - hy) / 2f - 0.2f);
                    return Mathf.Min(arc, head);
                }
                case EUiIcon.Menu:
                    return Mathf.Min(Box(x, y - 0.5f, 0.75f, 0.12f), Mathf.Min(Box(x, y, 0.75f, 0.12f), Box(x, y + 0.5f, 0.75f, 0.12f)));
                case EUiIcon.Back:
                    return Mathf.Min(Segment(x, y, 0.25f, 0.6f, -0.35f, 0f), Segment(x, y, -0.35f, 0f, 0.25f, -0.6f)) - 0.13f;
                default:
                    return Mathf.Sqrt(x * x + y * y) - 0.8f;
            }
        }

        /// <summary>점에서 선분까지 거리.</summary>
        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>가운데가 원점인 사각형 부호 거리 (반폭 · 반높이).</summary>
        private static float Box(float x, float y, float halfWidth, float halfHeight)
        {
            return Mathf.Max(Mathf.Abs(x) - halfWidth, Mathf.Abs(y) - halfHeight);
        }

        /// <summary>종류마다 모양 — 무엇인지 알아볼 수 있는 실루엣을 고른다.</summary>
        public static EShape ShapeOf(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.City: return EShape.City;
                case EFrontWarsUnit.Factory: return EShape.Factory;
                case EFrontWarsUnit.Port: return EShape.Anchor;
                case EFrontWarsUnit.DefensePost: return EShape.Shield;
                case EFrontWarsUnit.MissileSilo: return EShape.Missile;
                case EFrontWarsUnit.SAMLauncher: return EShape.SamLauncher;
                case EFrontWarsUnit.Construction: return EShape.Hammer;
                case EFrontWarsUnit.Warship: return EShape.Warship;
                case EFrontWarsUnit.TradeShip: return EShape.TradeShip;
                case EFrontWarsUnit.TransportShip: return EShape.TransportShip;
                case EFrontWarsUnit.Train: return EShape.Train;
                case EFrontWarsUnit.AtomBomb: return EShape.RadiationSmall;
                case EFrontWarsUnit.HydrogenBomb: return EShape.RadiationLarge;
                case EFrontWarsUnit.MIRV: return EShape.RadiationMirv;
                case EFrontWarsUnit.MIRVWarhead: return EShape.RadiationWarhead;
                case EFrontWarsUnit.SAMMissile: return EShape.Dart;
                default: return EShape.Dot;
            }
        }

        public static Sprite Get(EShape shape)
        {
            if (Cache.TryGetValue(shape, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Build($"FrontWarsIcon_{shape}", (x, y) => SignedDistance(shape, x, y), true);
            Cache[shape] = sprite;
            return sprite;
        }

        // ── 건물 바탕 도형 ─────────────────────────────────────────────

        /// <summary>
        /// 건물 바탕 도형 — 원본은 이 도형을 주인 색으로 채우고 가운데에 기호를 찍는다 `[소스] BMmXchgl.js:ia={[D.City]:\`circle\``.
        /// </summary>
        public enum EStructureShape
        {
            /// <summary>도시 · 공장.</summary>
            Circle,

            /// <summary>항구.</summary>
            Pentagon,

            /// <summary>방어 초소.</summary>
            Hexagon,

            /// <summary>SAM.</summary>
            Square,

            /// <summary>미사일 기지.</summary>
            Triangle
        }

        /// <summary>도형 안 기호 그림 한 변 (원본 캔버스 px) `[소스] BMmXchgl.js:16×16 기호 PNG`.</summary>
        public const float StructureSymbolPx = 16f;

        /// <summary>모드 0(아주 멀리)에서 도형을 줄이는 비 `[소스] BMmXchgl.js:Math.ceil(aa[t]/2.5)`.</summary>
        public const float StructureDotDivisor = 2.5f;

        private const float ZoomScaleBase = 8f;
        private const float OaMinZoom = 0.2f;
        private const float OaBase = 1.1f;
        private const float OaRange = 4.8f;

        private static readonly Dictionary<EStructureShape, Sprite> StructureCache = new Dictionary<EStructureShape, Sprite>();

        /// <summary>건물 종류 → 바탕 도형 `[소스] BMmXchgl.js:ia={[D.City]:\`circle\`,[D.Port]:\`pentagon\``.</summary>
        public static EStructureShape StructureShapeOf(EFrontWarsUnit type)
        {
            switch (type)
            {
                case EFrontWarsUnit.Port: return EStructureShape.Pentagon;
                case EFrontWarsUnit.DefensePost: return EStructureShape.Hexagon;
                case EFrontWarsUnit.SAMLauncher: return EStructureShape.Square;
                case EFrontWarsUnit.MissileSilo: return EStructureShape.Triangle;
                default: return EStructureShape.Circle;   // 도시 · 공장 · 공사 중
            }
        }

        /// <summary>도형 기본 한 변 (원본 캔버스 px) `[소스] BMmXchgl.js:aa={circle:28,octagon:28,hexagon:28,pentagon:30,square:28,triangle:28`.</summary>
        public static float StructureBasePx(EStructureShape shape)
        {
            return shape == EStructureShape.Pentagon ? 30f : 28f;
        }

        /// <summary>
        /// 도형 한가운데에서 기호 그림 가운데까지 거리 (원본 캔버스 px · y 는 아래가 +) `[소스] BMmXchgl.js:{triangle:[6,11],square:[5,5],hexagon:[6,6],circle:[6,6],pentagon:[7,7]}`.
        /// <para>원본은 기호 왼쪽 위 모서리를 적는다 — 여기서는 (오프셋 + 8) − 기본크기/2 로 가운데 기준으로 바꾼다.</para>
        /// </summary>
        public static Vector2 StructureSymbolOffsetPx(EStructureShape shape)
        {
            Vector2 topLeft;

            switch (shape)
            {
                case EStructureShape.Triangle: topLeft = new Vector2(6f, 11f); break;
                case EStructureShape.Square: topLeft = new Vector2(5f, 5f); break;
                case EStructureShape.Pentagon: topLeft = new Vector2(7f, 7f); break;
                default: topLeft = new Vector2(6f, 6f); break;
            }

            float half = StructureBasePx(shape) * 0.5f;
            return new Vector2(topLeft.x + StructureSymbolPx * 0.5f - half, topLeft.y + StructureSymbolPx * 0.5f - half);
        }

        /// <summary>
        /// 확대에 따른 아이콘 확대 보정 `[소스] BMmXchgl.js:function oa(e){return e>5?$i:$i+1*((5-Math.max(Qi,Math.min(e,5)))/4.8)}` (`Qi=.2,$i=1.1`).
        /// </summary>
        public static float ZoomPad(float zoom)
        {
            if (zoom > 5f)
                return OaBase;

            return OaBase + (5f - Mathf.Max(OaMinZoom, Mathf.Min(zoom, 5f))) / OaRange;
        }

        /// <summary>건물 아이콘 한 변 (화면 px) = 8 × zoom × oa(zoom) `[소스] BMmXchgl.js:sa(zoom,pad)`.</summary>
        public static float StructureScreenPx(float zoom)
        {
            return ZoomScaleBase * zoom * ZoomPad(zoom);
        }

        /// <summary>바탕 도형 스프라이트 — 안쪽 흰색(주인 색으로 칠한다) · 가장자리만 어둡게(원본 테두리 rgb(26,26,26) 자리).</summary>
        public static Sprite GetStructure(EStructureShape shape)
        {
            if (StructureCache.TryGetValue(shape, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Build($"FrontWarsShape_{shape}", (x, y) => StructureDistance(shape, x, y), true);
            StructureCache[shape] = sprite;
            return sprite;
        }

        /// <summary>바탕 도형 부호 거리 — 한 변이 스프라이트 한 변에 꽉 차게 잡는다.</summary>
        private static float StructureDistance(EStructureShape shape, float x, float y)
        {
            switch (shape)
            {
                case EStructureShape.Square: return Box(x, y, 0.94f, 0.94f);
                case EStructureShape.Triangle: return Cone(y, x, -0.92f, 0.94f, 0.94f);
                case EStructureShape.Pentagon: return Polygon(x, y, 5, 0.97f);
                case EStructureShape.Hexagon: return Polygon(x, y, 6, 0.97f);
                default: return Circle(x, y, 0.94f);
            }
        }

        /// <summary>꼭지가 위를 보는 정다각형 — 변마다 반평면을 잘라 만든다.</summary>
        private static float Polygon(float x, float y, int sides, float radius)
        {
            float apothem = radius * Mathf.Cos(Mathf.PI / sides);
            float d = float.NegativeInfinity;

            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 0.5f + 2f * Mathf.PI * i / sides;
                d = Mathf.Max(d, x * Mathf.Cos(a) + y * Mathf.Sin(a) - apothem);
            }

            return d;
        }

        // ── 이름표 아이콘 ─────────────────────────────────────────────

        /// <summary>
        /// 이름표 위 아이콘 줄 `[소스] BMmXchgl.js:e===this._topPlayer&&t.push(\`crown\`)`.
        /// <para>원본은 SVG 를 쓴다 — 옮기지 않고 뜻이 통하는 실루엣을 새로 그렸다 (R11).</para>
        /// </summary>
        public enum ENameIcon
        {
            /// <summary>왕관 — 땅 1위.</summary>
            Crown,

            /// <summary>단검 — 배신자.</summary>
            Traitor,

            /// <summary>끊긴 신호 — 접속 끊김.</summary>
            Disconnected,

            /// <summary>맞물린 고리 둘 — 동맹.</summary>
            Alliance,

            /// <summary>맞물린 고리 + 물음표 — 동맹 요청.</summary>
            AllianceRequest,

            /// <summary>과녁 — 내 목표.</summary>
            Target,

            /// <summary>금지 표시 — 금수.</summary>
            Embargo,

            /// <summary>방사능 — 그 플레이어의 핵이 날아가는 중.</summary>
            Nuke
        }

        private static readonly Dictionary<ENameIcon, Sprite> NameIconCache = new Dictionary<ENameIcon, Sprite>();

        /// <summary>이름표 아이콘 스프라이트 — 흰 실루엣 (쓰는 쪽이 색 · 투명도를 준다).</summary>
        public static Sprite GetNameIcon(ENameIcon icon)
        {
            if (icon == ENameIcon.Crown)
                return GetMark(EEmojiMark.Crown);

            if (icon == ENameIcon.Target)
                return GetMark(EEmojiMark.Target);

            if (icon == ENameIcon.Nuke)
                return Get(EShape.RadiationSmall);

            if (NameIconCache.TryGetValue(icon, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Build($"FrontWarsNameIcon_{icon}", (x, y) => NameIconDistance(icon, x, y), true);
            NameIconCache[icon] = sprite;
            return sprite;
        }

        private static float NameIconDistance(ENameIcon icon, float x, float y)
        {
            switch (icon)
            {
                case ENameIcon.Traitor:
                {
                    // 단검 — 비스듬한 날 + 가로 코등이 + 손잡이
                    float blade = Line(x, y, 0.62f, -0.82f, -0.18f, 0.28f, 0.13f);
                    float guard = Line(x, y, -0.52f, 0.14f, 0.08f, 0.56f, 0.11f);
                    float grip = Line(x, y, -0.24f, 0.36f, -0.62f, 0.84f, 0.13f);
                    return Mathf.Min(blade, Mathf.Min(guard, grip));
                }
                case ENameIcon.Disconnected:
                {
                    // 부채꼴 두 줄 + 가운데 점, 그 위에 사선을 파낸다
                    float dot = Circle(x, y + 0.62f, 0.17f);
                    float inner = Mathf.Max(Ring(x, y + 0.62f, 0.50f, 0.11f), y + 0.62f);
                    float outer = Mathf.Max(Ring(x, y + 0.62f, 0.86f, 0.11f), y + 0.62f);
                    float slash = Line(x, y, -0.78f, -0.78f, 0.78f, 0.78f, 0.19f);
                    return Mathf.Max(Mathf.Min(dot, Mathf.Min(inner, outer)), -slash);
                }
                case ENameIcon.Alliance:
                {
                    // 맞물린 고리 둘
                    return Mathf.Min(Ring(x + 0.34f, y, 0.52f, 0.13f), Ring(x - 0.34f, y, 0.52f, 0.13f));
                }
                case ENameIcon.AllianceRequest:
                {
                    // 작은 고리 둘 + 오른쪽 위 물음표 (요청 중)
                    float rings = Mathf.Min(Ring(x + 0.44f, y + 0.30f, 0.44f, 0.12f), Ring(x - 0.16f, y + 0.30f, 0.44f, 0.12f));
                    float hook = Mathf.Max(Ring(x - 0.48f, y - 0.54f, 0.26f, 0.11f), AngleGate(x, y, -0.48f, -0.54f, -40f, 215f));
                    float stem = Line(x, y, -0.26f, -0.70f, -0.48f, -0.40f, 0.10f);
                    return Mathf.Min(rings, Mathf.Min(hook, Mathf.Min(stem, Circle(x - 0.48f, y - 0.18f, 0.12f))));
                }
                case ENameIcon.Embargo:
                {
                    // 금지 표시 — 두꺼운 고리 + 사선
                    return Mathf.Min(Ring(x, y, 0.74f, 0.16f), Line(x, y, -0.52f, -0.52f, 0.52f, 0.52f, 0.16f));
                }
                default:
                    return Circle(x, y, 0.62f);
            }
        }

        /// <summary>가운데가 원점인 원.</summary>
        private static float Circle(float x, float y, float radius)
        {
            return Mathf.Sqrt(x * x + y * y) - radius;
        }

        /// <summary>가운데가 원점인 고리 — 반지름 radius, 두께 2×thickness.</summary>
        private static float Ring(float x, float y, float radius, float thickness)
        {
            return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - radius) - thickness;
        }

        /// <summary>두께 있는 선분 — 양 끝은 둥글다.</summary>
        private static float Line(float px, float py, float ax, float ay, float bx, float by, float halfWidth)
        {
            return Segment(px, py, ax, ay, bx, by) - halfWidth;
        }

        /// <summary>
        /// 한쪽으로 뾰족한 뿔 — 축 좌표 <paramref name="a"/> 가 baseA(반폭 halfWidth)에서 tipA(꼭지)로 간다.
        /// <para>tipA 가 baseA 보다 작으면 반대 방향으로 뾰족해진다. 축을 바꿔 넣으면 가로로도 쓴다.</para>
        /// </summary>
        private static float Cone(float a, float b, float baseA, float tipA, float halfWidth)
        {
            float height = tipA - baseA;
            float t = (a - baseA) / height;
            float f = Mathf.Abs(b) / halfWidth + t - 1f;
            float grad = Mathf.Sqrt(1f / (halfWidth * halfWidth) + 1f / (height * height));
            return Mathf.Max(f / grad, -t * Mathf.Abs(height));
        }

        /// <summary>방사능 표식 — 가운데 점 + 위 · 왼아래 · 오른아래 날개 세 장.</summary>
        private static float Trefoil(float x, float y, float inner, float outer, float dot, float halfAngle)
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float fold = Mathf.Repeat(angle - 90f + 60f, 120f) - 60f;   // 가장 가까운 날개 중심에서 벗어난 각도
            float band = Mathf.Max(radius - outer, inner - radius);
            float side = (Mathf.Abs(fold) - halfAngle) * Mathf.Deg2Rad * Mathf.Max(radius, 0.01f);
            return Mathf.Min(Circle(x, y, dot), Mathf.Max(band, side));
        }

        /// <summary>배 선체 — 윗면은 평평하고 아래로 좁아지는 사다리꼴.</summary>
        private static float Hull(float x, float y)
        {
            float deck = y + 0.05f;
            float keel = -0.62f - y;
            float left = -(0.885f * (x + 0.88f) + 0.466f * (y + 0.05f));
            float right = -(-0.865f * (x - 0.88f) + 0.501f * (y + 0.05f));
            return Mathf.Max(Mathf.Max(deck, keel), Mathf.Max(left, right));
        }

        /// <summary>정규화 좌표(−1~1, y 위로 +)의 부호 거리 — 음수가 안쪽.</summary>
        private static float SignedDistance(EShape shape, float x, float y)
        {
            switch (shape)
            {
                case EShape.City:
                {
                    // 낮은 · 높은 · 중간 건물 세 채가 바닥선 하나에 서 있다
                    float left = Box(x + 0.565f, y + 0.30f, 0.235f, 0.50f);
                    float middle = Box(x, y - 0.025f, 0.24f, 0.825f);
                    float right = Box(x - 0.565f, y + 0.15f, 0.235f, 0.65f);
                    float ground = Box(x, y + 0.72f, 0.80f, 0.08f);
                    return Mathf.Min(Mathf.Min(left, middle), Mathf.Min(right, ground));
                }
                case EShape.Factory:
                {
                    // 넓고 낮은 건물 + 왼쪽에 높은 굴뚝 + 그 위 연기 한 덩이
                    float body = Box(x, y + 0.45f, 0.85f, 0.35f);
                    float chimney = Box(x + 0.56f, y + 0.125f, 0.16f, 0.675f);
                    float smoke = Circle(x + 0.44f, y - 0.78f, 0.20f);
                    return Mathf.Min(body, Mathf.Min(chimney, smoke));
                }
                case EShape.Anchor:
                {
                    // 닻 — 세로 기둥 · 위 고리 · 가로대 · 아래 반원
                    float shaft = Box(x, y + 0.05f, 0.13f, 0.75f);
                    float eye = Ring(x, y - 0.68f, 0.20f, 0.12f);
                    float bar = Box(x, y - 0.35f, 0.58f, 0.12f);
                    float arc = Mathf.Max(Ring(x, y + 0.25f, 0.55f, 0.13f), y + 0.25f);
                    return Mathf.Min(Mathf.Min(shaft, eye), Mathf.Min(bar, arc));
                }
                case EShape.Shield:
                {
                    // 방패 — 위는 네모, 아래는 한 점으로 모인다
                    float top = Box(x, y - 0.30f, 0.62f, 0.55f);
                    float tip = Cone(y, x, -0.25f, -0.85f, 0.62f);
                    return Mathf.Min(top, tip);
                }
                case EShape.Missile:
                {
                    // 발사대 위에 선 수직 미사일 (날개 두 장 · 뾰족한 탄두)
                    float pad = Box(x, y + 0.72f, 0.68f, 0.12f);
                    float body = Box(x, y + 0.06f, 0.20f, 0.56f);
                    float fins = Mathf.Min(Box(x + 0.33f, y + 0.40f, 0.15f, 0.20f), Box(x - 0.33f, y + 0.40f, 0.15f, 0.20f));
                    float nose = Cone(y, x, 0.50f, 0.92f, 0.20f);
                    return Mathf.Min(Mathf.Min(pad, body), Mathf.Min(fins, nose));
                }
                case EShape.SamLauncher:
                {
                    // 바퀴 달린 차대 + 50° 로 기울인 미사일 (격납고와 기울기로 갈린다)
                    float chassis = Box(x, y + 0.49f, 0.68f, 0.21f);
                    float wheels = Mathf.Min(Circle(x + 0.40f, y + 0.80f, 0.16f), Circle(x - 0.40f, y + 0.80f, 0.16f));
                    float mx = x, my = y - 0.08f;
                    float u = mx * 0.643f + my * 0.766f;     // 미사일 축 (50°)
                    float v = -mx * 0.766f + my * 0.643f;    // 미사일 가로
                    float body = Box(u, v, 0.45f, 0.15f);
                    float nose = Cone(u, v, 0.45f, 0.88f, 0.15f);
                    return Mathf.Min(Mathf.Min(chassis, wheels), Mathf.Min(body, nose));
                }
                case EShape.Warship:
                {
                    // 선체 + 함교 + 돛대 + 비스듬한 포신
                    float deck = Box(x + 0.10f, y - 0.12f, 0.30f, 0.22f);
                    float mast = Box(x + 0.10f, y - 0.45f, 0.10f, 0.22f);
                    float gun = Line(x, y, 0.16f, 0.02f, 0.72f, 0.44f, 0.11f);
                    return Mathf.Min(Hull(x, y), Mathf.Min(deck, Mathf.Min(mast, gun)));
                }
                case EShape.TradeShip:
                {
                    // 선체 + 상자 세 개 (아래 둘, 위 하나)
                    float crateLeft = Box(x + 0.32f, y - 0.19f, 0.26f, 0.22f);
                    float crateRight = Box(x - 0.32f, y - 0.19f, 0.26f, 0.22f);
                    float crateTop = Box(x, y - 0.64f, 0.26f, 0.21f);
                    return Mathf.Min(Hull(x, y), Mathf.Min(crateLeft, Mathf.Min(crateRight, crateTop)));
                }
                case EShape.TransportShip:
                {
                    // 선체 + 사람 (동그란 머리 · 몸통 · 두 팔)
                    float torso = Box(x, y - 0.09f, 0.23f, 0.19f);
                    float head = Circle(x, y - 0.48f, 0.22f);
                    float arms = Box(x, y - 0.14f, 0.48f, 0.09f);
                    return Mathf.Min(Hull(x, y), Mathf.Min(torso, Mathf.Min(head, arms)));
                }
                case EShape.Train:
                {
                    // 기관차 — 몸통 + 운전실 + 굴뚝 + 바퀴 둘
                    float body = Box(x, y + 0.12f, 0.80f, 0.32f);
                    float cab = Box(x + 0.42f, y - 0.30f, 0.34f, 0.26f);
                    float chimney = Box(x - 0.52f, y - 0.28f, 0.13f, 0.24f);
                    float wheels = Mathf.Min(Circle(x + 0.42f, y + 0.58f, 0.18f), Circle(x - 0.38f, y + 0.58f, 0.18f));
                    return Mathf.Min(Mathf.Min(body, cab), Mathf.Min(chimney, wheels));
                }
                case EShape.RadiationSmall:
                    return Trefoil(x, y, 0.28f, 0.72f, 0.16f, 30f);
                case EShape.RadiationLarge:
                    return Trefoil(x, y, 0.32f, 0.96f, 0.20f, 38f);
                case EShape.RadiationMirv:
                {
                    // 작은 표식 + 날개 사이 빈 곳에 탄두 세 개
                    float d = Trefoil(x, y, 0.20f, 0.50f, 0.12f, 30f);

                    for (int i = 0; i < 3; i++)
                    {
                        float a = (30f + 120f * i) * Mathf.Deg2Rad;
                        d = Mathf.Min(d, Circle(x - 0.80f * Mathf.Cos(a), y - 0.80f * Mathf.Sin(a), 0.17f));
                    }

                    return d;
                }
                case EShape.RadiationWarhead:
                    return Trefoil(x, y, 0.20f, 0.52f, 0.12f, 26f);
                case EShape.Dart:
                {
                    // 요격 미사일 — 길게 뾰족한 탄두 + 꼬리 날개
                    float body = Box(x, y + 0.38f, 0.24f, 0.48f);
                    float nose = Cone(y, x, 0.10f, 0.95f, 0.24f);
                    float fins = Mathf.Min(Box(x + 0.40f, y + 0.66f, 0.18f, 0.20f), Box(x - 0.40f, y + 0.66f, 0.18f, 0.20f));
                    return Mathf.Min(Mathf.Min(body, nose), fins);
                }
                case EShape.Hammer:
                {
                    // 망치 — 넓은 머리 + 오른쪽으로 치우친 자루
                    float head = Box(x, y - 0.52f, 0.60f, 0.22f);
                    float handle = Box(x - 0.24f, y + 0.21f, 0.16f, 0.61f);
                    return Mathf.Min(head, handle);
                }
                case EShape.Star:
                {
                    float angle = Mathf.Atan2(y, x);
                    float radius = Mathf.Sqrt(x * x + y * y);
                    float spike = 0.55f + 0.3f * Mathf.Cos(angle * 5f);
                    return radius - spike;
                }
                default:
                    return Circle(x, y, 0.62f);   // Dot — 포탄
            }
        }

        // ── 이모지 표식 ─────────────────────────────────────────────

        /// <summary>
        /// 이모지 대신 쓰는 흰 표식 — 이모지 글꼴을 안 넣기로 해서 코드로 그린다 (안 그리면 글꼴에 그림이 없어 두부 □ 로 뜬다).
        /// <para>이모지 하나하나가 아니라 뜻이 비슷한 것끼리 묶는다. 어떤 이모지가 어느 표식으로 가는지는 <see cref="BuildMarkTable"/> 한 곳에만 있다 — 바꾸려면 거기만 고친다.</para>
        /// <para>원본 그림을 옮긴 것이 아니라 새로 그렸다 (R11 · 「퀄리티 동일 · 다시 그림」).</para>
        /// </summary>
        public enum EEmojiMark
        {
            /// <summary>웃는 얼굴 — 기쁨 · 좋음.</summary>
            Smile,

            /// <summary>찡그린 얼굴 — 슬픔 · 화남 · 비웃음.</summary>
            Frown,

            /// <summary>위로 든 엄지 — 잘했다 · 힘.</summary>
            ThumbUp,

            /// <summary>아래로 내린 엄지 — 싫다 · 욕.</summary>
            ThumbDown,

            /// <summary>펼친 손 — 인사 · 박수 · 악수 · 부탁.</summary>
            Hand,

            /// <summary>하트 — 사랑.</summary>
            Heart,

            /// <summary>깨진 하트 — 실망.</summary>
            HeartBroken,

            /// <summary>물음표 — 뭐지?</summary>
            Question,

            /// <summary>느낌표 든 세모 — 경고 · 구조 요청.</summary>
            Warning,

            /// <summary>깃발 — 평화 · 항복 · 깃발 꽂기.</summary>
            Flag,

            /// <summary>모래시계 — 기다림.</summary>
            Hourglass,

            /// <summary>터지는 별 — 불 · 폭발.</summary>
            Explosion,

            /// <summary>해골 — 죽음.</summary>
            Skull,

            /// <summary>과녁 — 목표.</summary>
            Target,

            /// <summary>왕관 — 1등 · 지배.</summary>
            Crown,

            /// <summary>메달 — 순위.</summary>
            Medal,

            /// <summary>쌓인 동전 — 돈.</summary>
            Coin,

            /// <summary>집 — 살림.</summary>
            House,

            /// <summary>작은 짐승 — 닭 · 쥐.</summary>
            Animal,

            /// <summary>지구 — 지도 · 땅.</summary>
            Globe,

            /// <summary>투구 — 군대.</summary>
            Helmet,

            /// <summary>성 — 요새.</summary>
            Castle,

            /// <summary>엇갈린 칼 두 자루 — 전투 · 복수.</summary>
            Swords,

            /// <summary>말풍선 — 모르는 이모지가 오면 이것으로 띄운다.</summary>
            Bubble,

            /// <summary>화살표 여덟 방향 — 위 하나를 돌려 쓴다.</summary>
            ArrowUp,
            ArrowUpRight,
            ArrowRight,
            ArrowDownRight,
            ArrowDown,
            ArrowDownLeft,
            ArrowLeft,
            ArrowUpLeft,

            /// <summary>방사능 — 이미 그린 원자폭탄 아이콘을 그대로 쓴다.</summary>
            Radiation,

            /// <summary>닻 — 이미 그린 항구 아이콘을 그대로 쓴다.</summary>
            Anchor,

            /// <summary>배 — 이미 그린 무역선 아이콘을 그대로 쓴다.</summary>
            Ship,

            /// <summary>방패 — 이미 그린 방어초소 아이콘을 그대로 쓴다.</summary>
            Shield,

            /// <summary>도시 — 이미 그린 도시 아이콘을 그대로 쓴다.</summary>
            CityMark,

            /// <summary>체크 — 꽉 찬 네모에서 체크 표시를 파낸다 (완료).</summary>
            Check,

            /// <summary>위를 가리키는 손가락 — 주먹 + 세운 검지.</summary>
            PointUp,

            /// <summary>집는 손 — 손바닥 + 굽은 검지 + 엄지 (끝이 거의 닿는다).</summary>
            Pinch,

            /// <summary>톱니바퀴 — HUD 설정 아이콘과 같은 모양 (설정).</summary>
            Gear,

            /// <summary>문 — 세로 네모 + 손잡이 구멍 (나가기).</summary>
            Door,

            /// <summary>빨리 감기 — 오른쪽 세모 두 개.</summary>
            FastForward,

            /// <summary>일시정지 — HUD 일시정지 아이콘과 같은 세로 막대 두 개.</summary>
            Pause
        }

        /// <summary>
        /// 게임 데이터(<c>FrontWarsEmojis.All</c> · 튜토리얼 목표 아이콘) 밖에서 글 속에만 나오는 이모지 — 튜토리얼 원본 문구에 섞여 있다.
        /// <para>이모지 굽는 도구가 이 목록도 같이 굽는다. 글에 새 이모지가 나오면 여기와 <see cref="BuildMarkTable"/> 에 같이 넣는다.</para>
        /// </summary>
        public static IReadOnlyList<string> ExtraTextEmojis { get; } = new[]
        {
            "✅", "💪", "👆", "✋", "🤏", "⚙️", "🚪", "⏩", "⏸️", "🏠"
        };

        private static readonly Dictionary<EEmojiMark, Sprite> MarkCache = new Dictionary<EEmojiMark, Sprite>();
        private static readonly Dictionary<string, EEmojiMark> MarkTable = BuildMarkTable();

        /// <summary>
        /// 이모지 → 표식 대응표 (여기 한 곳이 전부다).
        /// <para>목록은 <c>FrontWarsEmojis.All</c> 55개 + 튜토리얼 목표 아이콘 + <see cref="ExtraTextEmojis"/> 다. 여기 없는 이모지는 말풍선으로 뜬다.</para>
        /// </summary>
        private static Dictionary<string, EEmojiMark> BuildMarkTable()
        {
            var table = new Dictionary<string, EEmojiMark>();

            Add(table, EEmojiMark.Smile, "😀", "😊", "😇", "😎");
            Add(table, EEmojiMark.Frown, "😞", "🥺", "😭", "😱", "😡", "😈", "🤡", "🥱", "🤦‍♂️");
            Add(table, EEmojiMark.ThumbUp, "👍", "💪");
            Add(table, EEmojiMark.ThumbDown, "👎", "🖕");
            Add(table, EEmojiMark.Hand, "👋", "👏", "🤌", "🫡", "🤝", "🙏", "✋");
            Add(table, EEmojiMark.Heart, "❤️", "🥰");
            Add(table, EEmojiMark.HeartBroken, "💔");
            Add(table, EEmojiMark.Question, "❓");
            Add(table, EEmojiMark.Warning, "⚠️", "🆘");
            Add(table, EEmojiMark.Flag, "🏳️", "🕊️", "🚩");
            Add(table, EEmojiMark.Hourglass, "⏳");
            Add(table, EEmojiMark.Explosion, "🔥", "💥");
            Add(table, EEmojiMark.Skull, "💀");
            Add(table, EEmojiMark.Target, "🎯");
            Add(table, EEmojiMark.Crown, "👑", "🏆");
            Add(table, EEmojiMark.Medal, "🥇", "🥈", "🥉");
            Add(table, EEmojiMark.Coin, "💰");
            Add(table, EEmojiMark.House, "🏡", "🏠");
            Add(table, EEmojiMark.Animal, "🐔", "🐀");
            Add(table, EEmojiMark.Globe, "🗺️", "🌏");
            Add(table, EEmojiMark.Helmet, "🪖");
            Add(table, EEmojiMark.Castle, "🏰");
            Add(table, EEmojiMark.Swords, "⚔️");

            Add(table, EEmojiMark.ArrowUp, "⬆️");
            Add(table, EEmojiMark.ArrowUpRight, "↗️");
            Add(table, EEmojiMark.ArrowRight, "➡️");
            Add(table, EEmojiMark.ArrowDownRight, "↘️");
            Add(table, EEmojiMark.ArrowDown, "⬇️");
            Add(table, EEmojiMark.ArrowDownLeft, "↙️");
            Add(table, EEmojiMark.ArrowLeft, "⬅️");
            Add(table, EEmojiMark.ArrowUpLeft, "↖️");

            Add(table, EEmojiMark.Radiation, "☢️");
            Add(table, EEmojiMark.Anchor, "⚓");
            Add(table, EEmojiMark.Ship, "⛵");
            Add(table, EEmojiMark.Shield, "🛡️");
            Add(table, EEmojiMark.CityMark, "🏙️");

            // 튜토리얼 글 속 이모지 (ExtraTextEmojis). 💪 · ✋ · 🏠 은 위 줄의 엄지 · 손 · 집에 들어 있다
            Add(table, EEmojiMark.Check, "✅");
            Add(table, EEmojiMark.PointUp, "👆");
            Add(table, EEmojiMark.Pinch, "🤏");
            Add(table, EEmojiMark.Gear, "⚙️");
            Add(table, EEmojiMark.Door, "🚪");
            Add(table, EEmojiMark.FastForward, "⏩");
            Add(table, EEmojiMark.Pause, "⏸️");

            return table;
        }

        private static void Add(Dictionary<string, EEmojiMark> table, EEmojiMark mark, params string[] emojis)
        {
            foreach (string emoji in emojis)
                table[Key(emoji)] = mark;
        }

        /// <summary>
        /// 이모지 표식 — 아는 이모지면 그 표식, 모르면 말풍선. 절대 null 을 돌려주지 않는다.
        /// </summary>
        public static Sprite GetEmoji(string emoji)
        {
            return GetMark(MarkOf(emoji));
        }

        /// <summary>이모지가 어느 표식으로 가는지 — 모르면 말풍선.</summary>
        public static EEmojiMark MarkOf(string emoji)
        {
            if (string.IsNullOrEmpty(emoji))
                return EEmojiMark.Bubble;

            return MarkTable.TryGetValue(Key(emoji), out EEmojiMark mark) ? mark : EEmojiMark.Bubble;
        }

        /// <summary>표식 스프라이트 — 유닛 아이콘을 그대로 쓰는 표식은 그쪽 캐시를 쓴다.</summary>
        public static Sprite GetMark(EEmojiMark mark)
        {
            if (TryReuseShape(mark, out EShape shape))
                return Get(shape);

            if (MarkCache.TryGetValue(mark, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Build($"FrontWarsEmoji_{mark}", (x, y) => EmojiDistance(mark, x, y), true);
            MarkCache[mark] = sprite;
            return sprite;
        }

        /// <summary>이모지 표식의 픽셀 (64×64) — TMP 스프라이트로 굽는 도구가 쓴다. 텍스처는 올린 뒤 못 읽어서 여기서 다시 그린다.</summary>
        public static Color32[] EmojiPixels(string emoji)
        {
            EEmojiMark mark = MarkOf(emoji);

            if (TryReuseShape(mark, out EShape shape))
                return Draw((x, y) => SignedDistance(shape, x, y), true);

            return Draw((x, y) => EmojiDistance(mark, x, y), true);
        }

        /// <summary>그림 한 변 (픽셀).</summary>
        public static int PixelSize => TextureSize;

        /// <summary>이미 그린 유닛 아이콘을 그대로 쓰는 표식인가.</summary>
        private static bool TryReuseShape(EEmojiMark mark, out EShape shape)
        {
            switch (mark)
            {
                case EEmojiMark.Radiation: shape = EShape.RadiationSmall; return true;
                case EEmojiMark.Anchor: shape = EShape.Anchor; return true;
                case EEmojiMark.Ship: shape = EShape.TradeShip; return true;
                case EEmojiMark.Shield: shape = EShape.Shield; return true;
                case EEmojiMark.CityMark: shape = EShape.City; return true;
                default: shape = EShape.Dot; return false;
            }
        }

        /// <summary>
        /// 대응표 열쇠 — 모양 고르는 글자(U+FE0F · U+FE0E)를 떼고 맞춘다.
        /// <para>같은 이모지를 붙은 것 · 안 붙은 것 두 가지로 쓰는 곳이 있어서다 (🛡️ 과 🛡).</para>
        /// </summary>
        private static string Key(string emoji)
        {
            if (emoji.IndexOf('️') < 0 && emoji.IndexOf('︎') < 0)
                return emoji;

            var builder = new StringBuilder(emoji.Length);

            foreach (char ch in emoji)
            {
                if (ch != '️' && ch != '︎')
                    builder.Append(ch);
            }

            return builder.ToString();
        }

        /// <summary>가운데가 원점인 타원 — 반지름 (a, b).</summary>
        private static float Ellipse(float x, float y, float a, float b)
        {
            return (Mathf.Sqrt(x / a * (x / a) + y / b * (y / b)) - 1f) * Mathf.Min(a, b);
        }

        /// <summary>가운데가 원점인 타원 고리 — 두께 2×thickness.</summary>
        private static float EllipseRing(float x, float y, float a, float b, float thickness)
        {
            return Mathf.Abs(Mathf.Sqrt(x / a * (x / a) + y / b * (y / b)) - 1f) * Mathf.Min(a, b) - thickness;
        }

        /// <summary>중심 (cx, cy) 에서 본 각도가 [lo, hi] (도) 안이면 −1, 밖이면 +1 — 고리를 잘라 쓸 때.</summary>
        private static float AngleGate(float x, float y, float cx, float cy, float lo, float hi)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg, 360f);
            lo = Mathf.Repeat(lo, 360f);
            hi = Mathf.Repeat(hi, 360f);
            bool inside = lo <= hi ? angle >= lo && angle <= hi : angle >= lo || angle <= hi;
            return inside ? -1f : 1f;
        }

        /// <summary>위를 보는 화살표를 시계 방향으로 <paramref name="degrees"/> 돌린 것 — 표본점을 반시계로 돌려 같은 그림을 쓴다.</summary>
        private static float Arrow(float x, float y, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float u = x * ca - y * sa;
            float v = x * sa + y * ca;
            float shaft = Box(u, v + 0.22f, 0.17f, 0.52f);
            float head = Cone(v, u, 0.12f, 0.92f, 0.50f);
            return Mathf.Min(shaft, head);
        }

        /// <summary>얼굴 테두리 — 웃는 얼굴 · 찡그린 얼굴이 같이 쓴다.</summary>
        private static float Face(float x, float y)
        {
            return Ring(x, y, 0.85f, 0.12f);
        }

        /// <summary>표식 부호 거리 — 정규화 좌표(−1~1, y 위로 +), 음수가 안쪽. 지도에서 작게 보이니 굵은 실루엣만 쓴다 (미측정).</summary>
        private static float EmojiDistance(EEmojiMark mark, float x, float y)
        {
            switch (mark)
            {
                case EEmojiMark.Smile:
                {
                    // 동그란 얼굴 + 눈 두 개 + 위로 휜 입
                    float eyes = Mathf.Min(Circle(x + 0.32f, y - 0.28f, 0.12f), Circle(x - 0.32f, y - 0.28f, 0.12f));
                    float mouth = Mathf.Max(Ring(x, y - 0.06f, 0.45f, 0.11f), y + 0.04f);
                    return Mathf.Min(Face(x, y), Mathf.Min(eyes, mouth));
                }
                case EEmojiMark.Frown:
                {
                    // 같은 얼굴에 입만 아래로 휜다
                    float eyes = Mathf.Min(Circle(x + 0.32f, y - 0.28f, 0.12f), Circle(x - 0.32f, y - 0.28f, 0.12f));
                    float mouth = Mathf.Max(Ring(x, y + 0.72f, 0.45f, 0.11f), -0.70f - y);
                    return Mathf.Min(Face(x, y), Mathf.Min(eyes, mouth));
                }
                case EEmojiMark.ThumbUp:
                case EEmojiMark.ThumbDown:
                {
                    // 주먹 + 세운 엄지. 아래 엄지는 위아래를 뒤집은 같은 그림이다
                    if (mark == EEmojiMark.ThumbDown)
                        y = -y;

                    float fist = Mathf.Min(Box(x - 0.05f, y + 0.34f, 0.52f, 0.44f), Circle(x - 0.42f, y + 0.34f, 0.44f));
                    float thumb = Line(x, y, -0.30f, 0f, -0.30f, 0.48f, 0.22f);
                    float grooves = Mathf.Min(Box(x - 0.28f, y + 0.20f, 0.32f, 0.05f), Box(x - 0.28f, y + 0.48f, 0.32f, 0.05f));
                    return Mathf.Max(Mathf.Min(fist, thumb), -grooves);   // 손가락 마디를 홈으로 파서 주먹처럼 보이게
                }
                case EEmojiMark.Hand:
                {
                    // 손바닥 + 엄지. 손가락은 위에서 홈 두 줄을 파 가른다 (가는 선을 안 쓴다)
                    float palm = Mathf.Min(Box(x, y + 0.18f, 0.48f, 0.62f), Circle(x, y - 0.42f, 0.48f));
                    float thumb = Line(x, y, -0.46f, -0.30f, -0.88f, 0.14f, 0.17f);
                    float grooves = Mathf.Min(Line(x, y, -0.16f, 0.95f, -0.16f, 0.26f, 0.055f), Line(x, y, 0.16f, 0.95f, 0.16f, 0.26f, 0.055f));
                    return Mathf.Max(Mathf.Min(palm, thumb), -grooves);
                }
                case EEmojiMark.Heart:
                case EEmojiMark.HeartBroken:
                {
                    // 위쪽 동그라미 둘 + 아래로 뾰족한 몸
                    float lobes = Mathf.Min(Circle(x + 0.34f, y - 0.28f, 0.45f), Circle(x - 0.34f, y - 0.28f, 0.45f));
                    float heart = Mathf.Min(lobes, Cone(y, x, 0.28f, -0.90f, 0.82f));

                    if (mark == EEmojiMark.Heart)
                        return heart;

                    float crack = Mathf.Min(Line(x, y, 0.02f, 0.80f, -0.14f, 0.20f, 0.10f),
                        Mathf.Min(Line(x, y, -0.14f, 0.20f, 0.16f, -0.06f, 0.10f), Line(x, y, 0.16f, -0.06f, -0.06f, -0.95f, 0.10f)));
                    return Mathf.Max(heart, -crack);   // 지그재그로 갈라 놓는다
                }
                case EEmojiMark.Question:
                {
                    // 갈고리 + 아래 점
                    float hook = Mathf.Max(Ring(x, y - 0.42f, 0.36f, 0.14f), AngleGate(x, y, 0f, 0.42f, -40f, 215f));
                    float stem = Line(x, y, 0.28f, 0.20f, 0f, -0.18f, 0.13f);
                    return Mathf.Min(hook, Mathf.Min(stem, Circle(x, y + 0.62f, 0.17f)));
                }
                case EEmojiMark.Warning:
                {
                    // 꽉 찬 세모에서 느낌표를 파낸다
                    float triangle = Cone(y, x, -0.72f, 0.88f, 0.90f);
                    float bang = Mathf.Min(Box(x, y - 0.05f, 0.11f, 0.23f), Circle(x, y + 0.38f, 0.12f));
                    return Mathf.Max(triangle, -bang);
                }
                case EEmojiMark.Flag:
                {
                    // 장대 + 네모 깃
                    return Mathf.Min(Box(x + 0.55f, y, 0.12f, 0.90f), Box(x - 0.12f, y - 0.42f, 0.55f, 0.30f));
                }
                case EEmojiMark.Hourglass:
                {
                    // 위아래 막대 + 마주 본 세모 둘 + 가운데 목
                    float bars = Mathf.Min(Box(x, y - 0.82f, 0.62f, 0.12f), Box(x, y + 0.82f, 0.62f, 0.12f));
                    float bulbs = Mathf.Min(Cone(y, x, 0.70f, 0f, 0.56f), Cone(y, x, -0.70f, 0f, 0.56f));
                    return Mathf.Min(bars, Mathf.Min(bulbs, Box(x, y, 0.11f, 0.14f)));
                }
                case EEmojiMark.Explosion:
                {
                    // 뾰족한 날 여덟 개짜리 별 — 유닛 아이콘 별(다섯)과 갈린다
                    float angle = Mathf.Atan2(y, x);
                    return Mathf.Sqrt(x * x + y * y) - (0.52f + 0.38f * Mathf.Cos(angle * 8f));
                }
                case EEmojiMark.Skull:
                {
                    // 머리 + 턱에서 눈구멍 · 코 · 이 사이를 파낸다
                    float skull = Mathf.Min(Circle(x, y - 0.18f, 0.74f), Box(x, y + 0.64f, 0.40f, 0.26f));
                    float eyes = Mathf.Min(Circle(x + 0.30f, y - 0.24f, 0.23f), Circle(x - 0.30f, y - 0.24f, 0.23f));
                    float teeth = Mathf.Min(Box(x + 0.15f, y + 0.64f, 0.06f, 0.26f), Box(x - 0.15f, y + 0.64f, 0.06f, 0.26f));
                    return Mathf.Max(skull, -Mathf.Min(eyes, Mathf.Min(Cone(y, x, 0.24f, -0.04f, 0.15f), teeth)));
                }
                case EEmojiMark.Target:
                    return Mathf.Min(Ring(x, y, 0.84f, 0.14f), Mathf.Min(Ring(x, y, 0.46f, 0.14f), Circle(x, y, 0.16f)));
                case EEmojiMark.Crown:
                {
                    // 띠 + 뿔 세 개 (가운데가 높다)
                    float band = Box(x, y + 0.45f, 0.74f, 0.22f);
                    float middle = Cone(y, x, -0.30f, 0.78f, 0.26f);
                    float sides = Mathf.Min(Cone(y, x + 0.52f, -0.30f, 0.56f, 0.22f), Cone(y, x - 0.52f, -0.30f, 0.56f, 0.22f));
                    return Mathf.Min(band, Mathf.Min(middle, sides));
                }
                case EEmojiMark.Medal:
                {
                    // 목걸이 끈 두 줄 + 동그란 메달
                    float ribbon = Mathf.Min(Line(x, y, -0.48f, 0.88f, -0.14f, 0.10f, 0.14f), Line(x, y, 0.48f, 0.88f, 0.14f, 0.10f, 0.14f));
                    return Mathf.Min(Ring(x, y + 0.42f, 0.40f, 0.15f), ribbon);
                }
                case EEmojiMark.Coin:
                {
                    // 납작한 동전 세 개를 쌓았다
                    return Mathf.Min(Ellipse(x, y + 0.62f, 0.66f, 0.23f), Mathf.Min(Ellipse(x, y, 0.66f, 0.23f), Ellipse(x, y - 0.62f, 0.66f, 0.23f)));
                }
                case EEmojiMark.House:
                {
                    // 세모 지붕 + 몸 + 파낸 문
                    float house = Mathf.Min(Cone(y, x, 0.05f, 0.88f, 0.92f), Box(x, y + 0.44f, 0.62f, 0.46f));
                    return Mathf.Max(house, -Box(x, y + 0.62f, 0.16f, 0.30f));
                }
                case EEmojiMark.Animal:
                {
                    // 몸 + 머리 + 귀 + 꼬리 + 다리 둘
                    float body = Mathf.Min(Circle(x + 0.12f, y + 0.22f, 0.50f), Circle(x - 0.50f, y - 0.18f, 0.31f));
                    float ear = Circle(x - 0.62f, y - 0.52f, 0.16f);
                    float tail = Line(x, y, -0.58f, -0.16f, -0.96f, 0.26f, 0.10f);
                    float legs = Mathf.Min(Box(x + 0.32f, y + 0.74f, 0.11f, 0.20f), Box(x - 0.12f, y + 0.74f, 0.11f, 0.20f));
                    return Mathf.Min(body, Mathf.Min(ear, Mathf.Min(tail, legs)));
                }
                case EEmojiMark.Globe:
                {
                    // 둥근 테두리 + 적도 + 세로선 (둘 다 테두리 안쪽만 남긴다)
                    float inside = Circle(x, y, 0.84f);
                    float equator = Mathf.Max(Box(x, y, 0.95f, 0.09f), inside);
                    float meridian = Mathf.Max(EllipseRing(x, y, 0.36f, 0.84f, 0.09f), inside);
                    return Mathf.Min(Ring(x, y, 0.84f, 0.12f), Mathf.Min(equator, meridian));
                }
                case EEmojiMark.Helmet:
                {
                    // 반원 지붕 + 넓은 챙 (방패와 달리 아래가 넓다)
                    float dome = Mathf.Max(Circle(x, y + 0.10f, 0.74f), -0.10f - y);
                    return Mathf.Min(dome, Box(x, y + 0.30f, 0.90f, 0.18f));
                }
                case EEmojiMark.Castle:
                {
                    // 성벽 + 양쪽 탑 + 성가퀴, 가운데 성문을 파낸다
                    float wall = Box(x, y + 0.38f, 0.86f, 0.40f);
                    float towers = Mathf.Min(Box(x + 0.64f, y + 0.06f, 0.22f, 0.72f), Box(x - 0.64f, y + 0.06f, 0.22f, 0.72f));
                    float merlons = Mathf.Min(Box(x, y - 0.22f, 0.14f, 0.24f),
                        Mathf.Min(Box(x + 0.32f, y - 0.22f, 0.14f, 0.24f), Box(x - 0.32f, y - 0.22f, 0.14f, 0.24f)));
                    float gate = Mathf.Min(Box(x, y + 0.56f, 0.17f, 0.26f), Circle(x, y + 0.30f, 0.17f));
                    return Mathf.Max(Mathf.Min(wall, Mathf.Min(towers, merlons)), -gate);
                }
                case EEmojiMark.Swords:
                {
                    // 엇갈린 칼 두 자루 + 아래쪽 코등이
                    float blades = Mathf.Min(Line(x, y, -0.78f, -0.78f, 0.82f, 0.82f, 0.11f), Line(x, y, 0.78f, -0.78f, -0.82f, 0.82f, 0.11f));
                    float guards = Mathf.Min(Line(x, y, -0.78f, -0.34f, -0.34f, -0.78f, 0.10f), Line(x, y, 0.34f, -0.78f, 0.78f, -0.34f, 0.10f));
                    return Mathf.Min(blades, guards);
                }
                case EEmojiMark.ArrowUp: return Arrow(x, y, 0f);
                case EEmojiMark.ArrowUpRight: return Arrow(x, y, 45f);
                case EEmojiMark.ArrowRight: return Arrow(x, y, 90f);
                case EEmojiMark.ArrowDownRight: return Arrow(x, y, 135f);
                case EEmojiMark.ArrowDown: return Arrow(x, y, 180f);
                case EEmojiMark.ArrowDownLeft: return Arrow(x, y, 225f);
                case EEmojiMark.ArrowLeft: return Arrow(x, y, 270f);
                case EEmojiMark.ArrowUpLeft: return Arrow(x, y, 315f);
                case EEmojiMark.Check:
                {
                    // 꽉 찬 네모에서 굵은 체크를 파낸다 (가는 선만 쓰면 작을 때 안 보인다)
                    float plate = Box(x, y, 0.84f, 0.84f);
                    float tick = Mathf.Min(Line(x, y, -0.46f, 0.04f, -0.12f, -0.32f, 0.14f), Line(x, y, -0.12f, -0.32f, 0.50f, 0.40f, 0.14f));
                    return Mathf.Max(plate, -tick);
                }
                case EEmojiMark.PointUp:
                {
                    // 오른쪽 주먹 + 왼쪽에 세운 검지. 주먹에 마디 홈 두 줄
                    float fist = Mathf.Min(Box(x - 0.08f, y + 0.42f, 0.50f, 0.40f), Circle(x + 0.30f, y + 0.42f, 0.40f));
                    float finger = Line(x, y, -0.38f, -0.20f, -0.38f, 0.78f, 0.17f);
                    float grooves = Mathf.Min(Box(x - 0.30f, y + 0.30f, 0.22f, 0.05f), Box(x - 0.30f, y + 0.56f, 0.22f, 0.05f));
                    return Mathf.Max(Mathf.Min(fist, finger), -grooves);
                }
                case EEmojiMark.Pinch:
                {
                    // 왼쪽 손바닥에서 검지는 위로 굽어 내려오고 엄지는 곧게 뻗는다 — 두 끝 사이를 조금 띄운다
                    float palm = Mathf.Min(Box(x + 0.48f, y + 0.02f, 0.36f, 0.52f), Circle(x + 0.48f, y + 0.50f, 0.36f));
                    float index = Mathf.Min(Line(x, y, -0.20f, 0.30f, 0.34f, 0.62f, 0.16f), Line(x, y, 0.34f, 0.62f, 0.72f, 0.30f, 0.16f));
                    float thumb = Line(x, y, -0.20f, -0.30f, 0.66f, -0.18f, 0.16f);
                    return Mathf.Min(palm, Mathf.Min(index, thumb));
                }
                case EEmojiMark.Gear:
                    return UiSignedDistance(EUiIcon.Gear, x, y);
                case EEmojiMark.Door:
                {
                    // 세로 네모에서 오른쪽 손잡이 자리를 파낸다
                    return Mathf.Max(Box(x, y, 0.50f, 0.90f), -Circle(x - 0.26f, y, 0.12f));
                }
                case EEmojiMark.FastForward:
                    return Mathf.Min(Cone(x, y, -0.92f, 0.04f, 0.66f), Cone(x, y, -0.04f, 0.92f, 0.66f));
                case EEmojiMark.Pause:
                    return UiSignedDistance(EUiIcon.Pause, x, y);
                default:
                {
                    // Bubble — 모르는 이모지. 말풍선 + 점 세 개를 파낸다
                    float bubble = Mathf.Min(Box(x, y - 0.18f, 0.80f, 0.54f), Cone(y, x + 0.28f, -0.30f, -0.92f, 0.26f));
                    float dots = Mathf.Min(Circle(x, y - 0.18f, 0.13f),
                        Mathf.Min(Circle(x + 0.38f, y - 0.18f, 0.13f), Circle(x - 0.38f, y - 0.18f, 0.13f)));
                    return Mathf.Max(bubble, -dots);
                }
            }
        }
    }
}
