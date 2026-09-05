using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// UI 프리팹의 <b>「어느 <c>Image</c> 에 어느 주소를 넣을지」 표</b>.
    ///
    /// <para>
    /// ★ <b>UI 프리팹은 Resources · 아트는 어드레서블</b>이라(확정표 10-c) 프리팹이 스프라이트를
    /// 직접 물면 <b>빌드에 중복 편입</b>되고 카테고리 로드/해제를 우회한다 (ResourceRule).
    /// 그래서 <b>프리팹에는 «주소»만 굽고 스프라이트는 런타임에 주입</b>한다.
    /// </para>
    ///
    /// <para>
    /// 패스 ③(배선)이 할 일은 두 줄이다 —
    /// <c>ArtLoader.LoadSpritesAsync(binder.CollectAddresses())</c> → <c>binder.Apply(...)</c>.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>지연 로드 아트는 다시 물어야 한다</b>(ResourceRule) — 창이 열릴 때 한 번만 물면
    /// 아직 안 온 것이 평생 <c>null</c> 로 남는다. <see cref="Apply"/> 는 <b>몇 번 불러도 안전</b>하다.
    /// </para>
    /// </summary>
    public sealed class BlumgiUiArtBinder : MonoBehaviour
    {
        [Serializable]
        public struct Binding
        {
            public Image Target;

            /// <summary><see cref="BlumgiArtAddress"/> 의 값이어야 한다. 오타는 감사기가 잡는다.</summary>
            public string Address;

            /// <summary>
            /// <b>어떻게 그릴 것인가.</b> 대부분 <c>Simple</c>(늘어남) · 튜토 패널 판은 <c>Sliced</c> ·
            /// 레벨 도트 바는 <c>Tiled</c>(칸이 반복된다).
            ///
            /// <para>
            /// ⚠ <b>[정정 2026-08-30] 예전에는 <c>bool Sliced</c> 였다.</b> 그래서 <see cref="Apply"/> 가
            /// «Sliced 아니면 무조건 <c>Simple</c>»으로 덮어써, <b>빌더가 구운 <c>Tiled</c> 가 런타임에 지워졌다</b> —
            /// 도트 바가 5칸 반복이 아니라 <b>2칸짜리 그림을 통째로 늘린 «큰 도트 2개»</b>로 떴다.
            /// 프리팹에는 <c>Tiled</c> 가 «저장돼 있어서» 파일을 봐도 안 보였고, 폭 검사도 통과했다 —
            /// <b>캡처로만 보이는 결함</b>이었다 (재발방지 #60).
            /// </para>
            /// </summary>
            public Image.Type Draw;
        }

        [SerializeField] private Binding[] _bindings;

        /// <summary>이 창이 필요로 하는 주소 전수. <b>중복 없이</b> 돌려준다.</summary>
        public string[] CollectAddresses()
        {
            if (_bindings.IsNullOrEmpty())
                return Array.Empty<string>();

            var unique = new List<string>(_bindings.Length);

            for (int i = 0; i < _bindings.Length; i++)
            {
                string address = _bindings[i].Address;

                if (address.IsNullOrEmpty() || unique.Contains(address))
                    continue;

                unique.Add(address);
            }

            return unique.ToArray();
        }

        /// <summary>주소 → 스프라이트 표를 받아 물린다. 없는 주소는 <b>조용히 넘기지 않고 오류</b>다.</summary>
        public void Apply(IReadOnlyDictionary<string, Sprite> sprites)
        {
            if (_bindings.IsNullOrEmpty() || sprites == null)
                return;

            for (int i = 0; i < _bindings.Length; i++)
            {
                Binding binding = _bindings[i];

                if (binding.Target == null || binding.Address.IsNullOrEmpty())
                    continue;

                if (sprites.TryGetValue(binding.Address, out Sprite sprite) == false || sprite == null)
                {
                    Log.Error($"UI 아트를 못 물렸다: {binding.Address} ({name})");
                    continue;
                }

                binding.Target.sprite = sprite;
                binding.Target.type = binding.Draw;
            }
        }

#if UNITY_EDITOR
        /// <summary>빌더가 표를 굽는 자리. 런타임에는 쓰지 않는다.</summary>
        public void EditorSetBindings(Binding[] bindings)
        {
            _bindings = bindings;
        }
#endif
    }
}
