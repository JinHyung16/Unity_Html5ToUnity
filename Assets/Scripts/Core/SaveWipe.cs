using System;
using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.Core
{
    /// <summary>
    /// <b>로컬에 남긴 것을 지운다</b> — 게임마다 «지우는 버튼» 하나를 달기 위한 공용 자리.
    ///
    /// <para>
    /// ★★ <b>로컬에 무엇이든 저장하는 게임은 «지우는 길»을 반드시 같이 낸다.</b>
    /// 최고 기록·해금·설정이 <see cref="PlayerPrefs"/> 에 남으면 <b>지우는 방법이 없는 한</b>
    /// 「처음 켠 사람」의 화면을 다시 볼 수 없다 — 원본과 대조할 수도, 사람이 확인할 수도 없다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>키 목록이 아니라 «지우는 일»을 등록한다</b> — 게임마다 <c>World_3</c> 처럼
    /// <b>이름이 그때그때 만들어지는 키</b>가 있어서, 바깥에서 키를 다 알 수 없다.
    /// 대신 <paramref name="keys"/> 로 <b>대표 키</b>를 같이 받아 <b>검사가 「정말 지워졌나」를 볼 수 있게</b> 한다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <see cref="PlayerPrefs"/> 는 <b>키를 훑을 수 없다</b> — 그래서 접두사만으로는 못 지운다.
    /// 지우는 책임은 <b>키를 아는 게임 쪽</b>에 있다.
    /// </para>
    /// </summary>
    public static class SaveWipe
    {
        private sealed class Entry
        {
            public string Owner;
            public Action Wipe;
            public string[] Keys;
        }

        private static readonly List<Entry> Entries = new List<Entry>(4);

        /// <summary>등록된 게임 수 — <b>검사가 이 값을 센다</b>.</summary>
        public static int OwnerCount
        {
            get { return Entries.Count; }
        }

        /// <summary>
        /// 지우는 일을 등록한다. <b>같은 <paramref name="owner"/> 로 두 번 부르면 덮어쓴다</b> —
        /// 씬을 다시 열 때 두 벌이 쌓이지 않는다.
        /// </summary>
        public static void Register(string owner, Action wipe, params string[] keys)
        {
            if (string.IsNullOrEmpty(owner) || wipe == null)
            {
                Log.Error("지우는 일을 등록하려면 이름과 «지우는 함수»가 둘 다 있어야 한다");
                return;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Owner != owner)
                    continue;

                Entries[i].Wipe = wipe;
                Entries[i].Keys = keys ?? Array.Empty<string>();
                return;
            }

            Entries.Add(new Entry { Owner = owner, Wipe = wipe, Keys = keys ?? Array.Empty<string>() });
        }

        /// <summary>대표 키 전부 — 검사가 「지워졌나」를 확인하는 데 쓴다.</summary>
        public static IReadOnlyList<string> SampleKeys
        {
            get
            {
                var all = new List<string>(8);

                for (int i = 0; i < Entries.Count; i++)
                    all.AddRange(Entries[i].Keys);

                return all;
            }
        }

        /// <summary>
        /// 등록된 것을 <b>전부</b> 지우고 즉시 기록한다.
        /// <para>⚠ <see cref="PlayerPrefs.Save"/> 를 여기서 한 번만 부른다 — 게임마다 부르면 디스크를 여러 번 친다.</para>
        /// </summary>
        public static void WipeAll()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                try
                {
                    Entries[i].Wipe();
                }
                catch (Exception e)
                {
                    Log.Error($"{Entries[i].Owner} 의 저장을 지우다 실패했다 — {e.Message}");
                }
            }

            PlayerPrefs.Save();
            Log.Success($"로컬 저장을 지웠다 — {Entries.Count}개 게임");
        }

        /// <summary>등록을 비운다 — 재생을 다시 시작할 때 쓴다.</summary>
        public static void Clear()
        {
            Entries.Clear();
        }
    }
}
