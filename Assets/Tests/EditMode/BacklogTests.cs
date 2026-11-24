using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static VN.Tests.TestSupport;

namespace VN.Tests
{
    /// <summary>백로그(지금까지 읽은 대사 기록). 실행기가 기록하고, 세이브에 실려 이어 해도 이어진다.</summary>
    public class BacklogTests
    {
        private static string Say(string speaker, string text) =>
            "{\"type\":\"say\",\"speaker\":\"" + speaker + "\",\"text\":\"" + text + "\"}";

        [Test]
        public void SaysAreRecordedInOrder_WithTheirSpeakers()
        {
            var data = Script(Say("지후", "하나"), Say("", "둘"), Say("미나", "셋"));
            var executor = Run(data, new FakeGame());

            Assert.That(executor.Backlog.Select(e => e.speaker + "|" + e.text),
                Is.EqualTo(new[] { "지후|하나", "|둘", "미나|셋" }));
        }

        [Test]
        public void ThePickedChoice_IsRecordedToo()
        {
            var data = Script(
                Say("A", "질문"),
                "{\"type\":\"choice\",\"options\":[{\"text\":\"가\",\"target\":\"a\"},{\"text\":\"나\",\"target\":\"b\"}]}",
                "{\"type\":\"label\",\"name\":\"a\"}",
                "{\"type\":\"end\",\"name\":\"A\"}",
                "{\"type\":\"label\",\"name\":\"b\"}",
                Say("A", "나를 골랐다"),
                "{\"type\":\"end\",\"name\":\"B\"}");

            var executor = Run(data, new FakeGame { Chooser = (call, count) => 1 });

            Assert.That(executor.Backlog.Select(e => e.text), Is.EqualTo(new[] { "질문", "▶ 나", "나를 골랐다" }));
            Assert.That(executor.Backlog[1].speaker, Is.Null.Or.Empty);
        }

        [Test]
        public void OnlyTheMostRecentHundredEntriesAreKept()
        {
            var commands = new List<string>();
            for (int i = 1; i <= 120; i++) commands.Add(Say("A", "줄" + i));

            var executor = Run(Script(commands.ToArray()), new FakeGame());

            Assert.That(executor.Backlog, Has.Count.EqualTo(100));
            Assert.That(executor.Backlog.First().text, Is.EqualTo("줄21"));
            Assert.That(executor.Backlog.Last().text, Is.EqualTo("줄120"));
        }

        [Test]
        public void StartingAgain_ClearsTheOldBacklog()
        {
            var data = Script(Say("A", "한 줄"));
            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));
            Drive(executor.Run(data));

            Assert.That(executor.Backlog, Has.Count.EqualTo(1));
        }

        [Test]
        public void ASave_KeepsTheLinesBeforeTheCurrentOne_ButNotTheCurrentOne()
        {
            var data = Script(Say("A", "첫째"), Say("A", "둘째"), Say("A", "셋째"));
            var game = new FakeGame();
            ScriptExecutor executor = null;
            SaveData saved = null;
            game.OnWait = () =>
            {
                if (game.Says.Count == 2) saved = executor.Capture(); // 셋째 줄을 기다리는 중
            };
            executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));

            Assert.That(saved.log.Select(e => e.text), Is.EqualTo(new[] { "첫째", "둘째" }));
        }

        [Test]
        public void ResumingFromASave_ContinuesTheBacklogWithoutDuplicatingTheCurrentLine()
        {
            var data = Script(Say("A", "첫째"), Say("A", "둘째"), Say("A", "셋째"));
            var game = new FakeGame();
            ScriptExecutor executor = null;
            SaveData saved = null;
            game.OnWait = () =>
            {
                if (game.Says.Count == 1) saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(executor.Capture()));
            };
            executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));

            var resumedGame = new FakeGame();
            var resumed = new ScriptExecutor(resumedGame, resumedGame, resumedGame, resumedGame);
            Drive(resumed.Run(data, saved));

            Assert.That(resumed.Backlog.Select(e => e.text), Is.EqualTo(new[] { "첫째", "둘째", "셋째" }));
        }

        [Test]
        public void ASaveWithoutALogField_StillLoads()
        {
            // 로그가 없던 옛 세이브(JSON에 log가 없음)도 이어 할 수 있어야 한다
            var data = Script(Say("A", "첫째"), Say("A", "둘째"));
            var game = new FakeGame();
            ScriptExecutor executor = null;
            SaveData saved = null;
            game.OnWait = () =>
            {
                if (game.Says.Count == 1) saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(executor.Capture()));
            };
            executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));
            saved.log = null;

            var resumedGame = new FakeGame();
            var resumed = new ScriptExecutor(resumedGame, resumedGame, resumedGame, resumedGame);
            Drive(resumed.Run(data, saved));

            Assert.That(resumed.Failed, Is.False);
            Assert.That(resumed.Backlog.Select(e => e.text), Is.EqualTo(new[] { "둘째" }));
        }
    }
}
