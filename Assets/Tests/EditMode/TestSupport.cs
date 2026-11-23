using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VN.Tests
{
    /// <summary>
    /// 화면과 소리 없이 ScriptExecutor를 시험하기 위한 가짜 View.
    /// 실행기가 요청한 일을 기록하고, 캐릭터 자리(left/center/right)와 BGM 상태를 실제 구현(StageManager, AudioManager)과 같은 규칙으로 흉내 낸다.
    /// </summary>
    public class FakeGame : IDialogueView, IStageView, IAudioView, IChoiceView
    {
        public readonly List<string> Says = new List<string>();
        public readonly List<string> Events = new List<string>();
        public readonly List<string> Problems = new List<string>();
        public readonly List<int> OptionCounts = new List<int>();
        public readonly List<int> PicksUsed = new List<int>();

        public readonly Dictionary<string, string> Slots = new Dictionary<string, string>
        {
            { "left", null }, { "center", null }, { "right", null },
        };

        public string Bg;
        public string Bgm;

        /// <summary>true면 배경/캐릭터/소리 이름이 Resources에 실제로 있는지도 검사해서 Problems에 남긴다.</summary>
        public bool CheckResources;

        /// <summary>선택지가 나올 때마다 (몇 번째 선택지인지, 선택지 개수) -> 고를 번호. 기본은 항상 0번.</summary>
        public Func<int, int, int> Chooser = (call, count) => 0;

        public bool AnyCharacterShown
        {
            get
            {
                foreach (var name in Slots.Values) if (name != null) return true;
                return false;
            }
        }

        public IEnumerator ShowLine(string speaker, string text)
        {
            Says.Add(text);
            Events.Add("say:" + text);
            yield break;
        }

        public IEnumerator SetBackground(string name, float duration)
        {
            Bg = name;
            Events.Add("bg:" + name);
            if (CheckResources && Resources.Load<Sprite>("Backgrounds/" + name) == null)
                Problems.Add("배경 없음: " + name);
            yield break;
        }

        public IEnumerator ShowCharacter(string name, string position, float duration)
        {
            var slot = string.IsNullOrEmpty(position) ? "center" : position;
            Slots[slot] = name;
            Events.Add($"show:{name}@{slot}");
            if (CheckResources && Resources.Load<Sprite>("Characters/" + name) == null)
                Problems.Add("캐릭터 없음: " + name);
            yield break;
        }

        public IEnumerator HideCharacter(string name, string position, float duration)
        {
            string slot = position;
            if (string.IsNullOrEmpty(slot))
            {
                foreach (var pair in Slots)
                {
                    if (pair.Value == name) slot = pair.Key;
                }
            }

            Events.Add($"hide:{name}@{slot}");
            if (slot == null || Slots[slot] == null)
                Problems.Add($"비어 있는 자리를 hide: name={name} position={position}");
            else
                Slots[slot] = null;
            yield break;
        }

        public void PlayBgm(string name, float duration)
        {
            Bgm = name;
            Events.Add("bgm:" + name);
            if (CheckResources && name != AudioManager.StopName && Resources.Load<AudioClip>("Audio/Bgm/" + name) == null)
                Problems.Add("BGM 없음: " + name);
        }

        public void PlaySfx(string name)
        {
            Events.Add("sfx:" + name);
            if (CheckResources && Resources.Load<AudioClip>("Audio/Sfx/" + name) == null)
                Problems.Add("효과음 없음: " + name);
        }

        public IEnumerator ShowChoices(IList<string> options, Action<int> onChosen)
        {
            int call = OptionCounts.Count;
            OptionCounts.Add(options.Count);
            int pick = Chooser(call, options.Count);
            PicksUsed.Add(pick);
            Events.Add("choice:" + string.Join("|", options));
            yield return null; // 실제 ChoiceManager처럼 한 프레임 이상 기다린다
            onChosen(pick);
        }
    }

    public static class TestSupport
    {
        /// <summary>코루틴(IEnumerator)을 Unity 엔진처럼 안쪽 코루틴까지 끝까지 실행한다.</summary>
        public static void Drive(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) stack.Push(inner);
            }
        }

        public static ScriptData Script(params string[] commandJson)
        {
            return Parse("{\"id\":\"test\",\"commands\":[" + string.Join(",", commandJson) + "]}");
        }

        public static ScriptData Parse(string json)
        {
            return JsonUtility.FromJson<ScriptData>(json);
        }

        public static ScriptData Load(string name)
        {
            var data = ScriptLoader.Load(name);
            if (data == null) throw new InvalidOperationException("시나리오를 읽을 수 없음: " + name);
            return data;
        }

        /// <summary>모든 화면 구성요소가 연결된 실행기로 시나리오를 끝까지 실행한다.</summary>
        public static ScriptExecutor Run(ScriptData data, FakeGame game)
        {
            var executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));
            return executor;
        }

        public class PathResult
        {
            public List<int> Picks;
            public List<string> Says;
            public string EndingId;
            public bool Failed;
            public Dictionary<string, int> Flags;
            public bool LeftoverCharacters;
            public string FinalBgm;
            public List<string> Problems;

            public override string ToString() => "선택 [" + string.Join(",", Picks) + "] → 엔딩 " + (EndingId ?? "(없음)");
        }

        /// <summary>
        /// 선택지의 모든 조합을 하나도 빠짐없이 실행해 본다 (선택지 개수가 달라도 동작).
        /// 처음에는 전부 0번을 고르고, 선택지가 나온 자리마다 다른 번호를 골라 가지를 늘려 간다.
        /// </summary>
        public static List<PathResult> ExploreAllPaths(ScriptData data, int maxPaths = 1000)
        {
            var results = new List<PathResult>();
            var pending = new Stack<List<int>>();
            pending.Push(new List<int>());

            while (pending.Count > 0)
            {
                if (results.Count >= maxPaths)
                    throw new InvalidOperationException($"경로가 {maxPaths}개를 넘어 탐색을 멈춤 (선택지가 너무 많거나 순환이 있음)");

                var prefix = pending.Pop();
                var game = new FakeGame { CheckResources = true };
                game.Chooser = (call, count) => call < prefix.Count ? prefix[call] : 0;

                var executor = Run(data, game);
                results.Add(new PathResult
                {
                    Picks = new List<int>(game.PicksUsed),
                    Says = game.Says,
                    EndingId = executor.EndingId,
                    Failed = executor.Failed,
                    Flags = new Dictionary<string, int>(executor.Flags.All),
                    LeftoverCharacters = game.AnyCharacterShown,
                    FinalBgm = game.Bgm,
                    Problems = game.Problems,
                });

                // 이번 실행에서 prefix 뒤로 처음 나온 선택지들의 다른 선택을 새 경로로 추가
                for (int position = prefix.Count; position < game.OptionCounts.Count; position++)
                {
                    for (int alternative = 1; alternative < game.OptionCounts[position]; alternative++)
                    {
                        var next = new List<int>(prefix);
                        while (next.Count < position) next.Add(0);
                        next.Add(alternative);
                        pending.Push(next);
                    }
                }
            }
            return results;
        }
    }
}
