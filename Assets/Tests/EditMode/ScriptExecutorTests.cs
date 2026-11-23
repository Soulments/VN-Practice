using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static VN.Tests.TestSupport;

namespace VN.Tests
{
    public class ScriptExecutorTests
    {
        private static string Say(string text) => $"{{\"type\":\"say\",\"speaker\":\"\",\"text\":\"{text}\"}}";
        private static string Label(string name) => $"{{\"type\":\"label\",\"name\":\"{name}\"}}";
        private static string Jump(string target) => $"{{\"type\":\"jump\",\"target\":\"{target}\"}}";
        private static string End(string name = null) => name == null ? "{\"type\":\"end\"}" : $"{{\"type\":\"end\",\"name\":\"{name}\"}}";

        // ---------- 기본 흐름 ----------

        [Test]
        public void Say_ShowsLinesInOrder()
        {
            var game = new FakeGame();
            var executor = Run(Script(Say("a"), Say("b"), Say("c")), game);

            Assert.That(game.Says, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void Jump_SkipsToLabel()
        {
            var game = new FakeGame();
            Run(Script(Jump("b"), Say("skipped"), Label("b"), Say("arrived")), game);

            Assert.That(game.Says, Is.EqualTo(new[] { "arrived" }));
        }

        [Test]
        public void End_StopsAndReportsEndingId()
        {
            var game = new FakeGame();
            var executor = Run(Script(Say("a"), End("good"), Say("never")), game);

            Assert.That(game.Says, Is.EqualTo(new[] { "a" }));
            Assert.That(executor.EndingId, Is.EqualTo("good"));
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void End_WithoutName_HasNoEndingId()
        {
            var executor = Run(Script(End()), new FakeGame());

            Assert.That(executor.EndingId, Is.Null);
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void RunningOffTheEnd_IsNotAFailure()
        {
            var executor = Run(Script(Say("last")), new FakeGame());

            Assert.That(executor.EndingId, Is.Null);
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void Run_Again_ResetsEndingAndFailure()
        {
            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(Script(End("good"))));
            Assert.That(executor.EndingId, Is.EqualTo("good"));

            Drive(executor.Run(Script(Say("x"))));

            Assert.That(executor.EndingId, Is.Null);
            Assert.That(executor.Failed, Is.False);
        }

        // ---------- 연출 / 소리 ----------

        [Test]
        public void StageAndAudioCommands_ReachTheViews()
        {
            var game = new FakeGame();
            Run(Script(
                "{\"type\":\"bg\",\"name\":\"classroom\",\"duration\":0.5}",
                "{\"type\":\"show\",\"name\":\"a\",\"position\":\"left\"}",
                "{\"type\":\"bgm\",\"name\":\"daily\"}",
                "{\"type\":\"sfx\",\"name\":\"ding\"}",
                "{\"type\":\"hide\",\"position\":\"left\"}"), game);

            // 위치만 지정한 hide는 이름이 없어서 "hide:@left"로 기록된다
            Assert.That(game.Events, Is.EqualTo(new[] { "bg:classroom", "show:a@left", "bgm:daily", "sfx:ding", "hide:@left" }));
            Assert.That(game.AnyCharacterShown, Is.False);
        }

        [Test]
        public void Show_WithoutPosition_GoesToCenter()
        {
            var game = new FakeGame();
            Run(Script("{\"type\":\"show\",\"name\":\"a\"}"), game);

            Assert.That(game.Slots["center"], Is.EqualTo("a"));
        }

        [Test]
        public void Hide_ByName_FindsTheSlot()
        {
            var game = new FakeGame();
            Run(Script(
                "{\"type\":\"show\",\"name\":\"a\",\"position\":\"right\"}",
                "{\"type\":\"hide\",\"name\":\"a\"}"), game);

            Assert.That(game.Slots["right"], Is.Null);
            Assert.That(game.Problems, Is.Empty);
        }

        [Test]
        public void MissingStageView_SkipsStageCommands_WithOneWarning()
        {
            var game = new FakeGame();
            var executor = new ScriptExecutor(game); // 대사창만 연결

            LogAssert.Expect(LogType.Warning, new Regex("bg"));
            LogAssert.Expect(LogType.Warning, new Regex("show"));
            Drive(executor.Run(Script(
                "{\"type\":\"bg\",\"name\":\"x\"}",
                "{\"type\":\"bg\",\"name\":\"y\"}", // 같은 종류의 경고는 한 번만
                "{\"type\":\"show\",\"name\":\"a\"}",
                Say("still runs"))));

            Assert.That(game.Says, Is.EqualTo(new[] { "still runs" }));
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void UnknownCommand_IsSkippedWithWarning()
        {
            var game = new FakeGame();
            LogAssert.Expect(LogType.Warning, new Regex("teleport"));

            Run(Script("{\"type\":\"teleport\"}", Say("after")), game);

            Assert.That(game.Says, Is.EqualTo(new[] { "after" }));
        }

        // ---------- 선택지 ----------

        private const string TwoWayChoice =
            "{\"type\":\"choice\",\"options\":[" +
            "{\"text\":\"A\",\"target\":\"a\",\"set\":\"trust+1\"}," +
            "{\"text\":\"B\",\"target\":\"b\"}]}";

        private static ScriptData ChoiceScript()
        {
            return Script(TwoWayChoice, Label("a"), Say("chose A"), Jump("z"), Label("b"), Say("chose B"), Label("z"), End());
        }

        [TestCase(0, "chose A", 1)]
        [TestCase(1, "chose B", 0)]
        public void Choice_JumpsToChosenLabel_AndAppliesOnlyThatSet(int pick, string expectedLine, int expectedTrust)
        {
            var game = new FakeGame { Chooser = (call, count) => pick };
            var executor = Run(ChoiceScript(), game);

            Assert.That(game.Says, Is.EqualTo(new[] { expectedLine }));
            Assert.That(executor.Flags.Get("trust"), Is.EqualTo(expectedTrust));
        }

        [Test]
        public void Choice_ShowsAllOptionTexts()
        {
            var game = new FakeGame();
            Run(ChoiceScript(), game);

            Assert.That(game.Events, Does.Contain("choice:A|B"));
        }

        [Test]
        public void Choice_WithoutChoiceView_FallsThroughWithWarning()
        {
            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game); // 선택지 View 없음
            LogAssert.Expect(LogType.Warning, new Regex("choice"));

            Drive(executor.Run(ChoiceScript()));

            // 선택 없이 choice 다음 명령(라벨 a)으로 흘러가므로 첫 번째 가지의 대사만 나오고, jump z로 b 가지는 건너뛴다
            Assert.That(game.Says, Is.EqualTo(new[] { "chose A" }));
            Assert.That(executor.Failed, Is.False);
        }

        [Test]
        public void Choice_WithInvalidPick_FailsTheRun()
        {
            var game = new FakeGame { Chooser = (call, count) => 9 };
            LogAssert.Expect(LogType.Error, new Regex("올바르지 않은 번호"));

            var executor = Run(ChoiceScript(), game);

            Assert.That(executor.Failed, Is.True);
            Assert.That(game.Says, Is.Empty);
        }

        // ---------- 플래그 / 조건 ----------

        [Test]
        public void SetAndIf_BranchOnFlagValues()
        {
            var game = new FakeGame();
            Run(Script(
                "{\"type\":\"set\",\"name\":\"on\"}",
                "{\"type\":\"set\",\"name\":\"n\",\"value\":\"+3\"}",
                "{\"type\":\"if\",\"name\":\"on\",\"target\":\"yes\"}",
                Say("on-is-off"),
                Label("yes"),
                "{\"type\":\"if\",\"name\":\"n\",\"value\":\">=4\",\"target\":\"big\"}",
                Say("n<4"),
                Jump("z"),
                Label("big"),
                Say("n>=4"),
                Label("z"),
                "{\"type\":\"if\",\"name\":\"missing\",\"target\":\"bad\"}",
                Say("missing=0"),
                End(),
                Label("bad"),
                Say("BAD")), game);

            Assert.That(game.Says, Is.EqualTo(new[] { "n<4", "missing=0" }));
        }

        [Test]
        public void If_CanBuildALoop()
        {
            var game = new FakeGame();
            Run(Script(
                Label("top"),
                "{\"type\":\"set\",\"name\":\"c\",\"value\":\"+1\"}",
                Say("tick"),
                "{\"type\":\"if\",\"name\":\"c\",\"value\":\"<3\",\"target\":\"top\"}"), game);

            Assert.That(game.Says.Count, Is.EqualTo(3));
        }

        [Test]
        public void NewExecutor_StartsWithFreshFlags()
        {
            var first = new ScriptExecutor(new FakeGame());
            first.Flags.Set("trust", 5);

            Assert.That(new ScriptExecutor(new FakeGame()).Flags.Get("trust"), Is.EqualTo(0));
        }

        // ---------- 실패 처리 ----------

        [Test]
        public void InvalidScript_FailsWithoutRunningAnything()
        {
            var game = new FakeGame();
            LogAssert.Expect(LogType.Error, new Regex("nowhere"));

            var executor = Run(Script(Say("must not run"), Jump("nowhere")), game);

            Assert.That(executor.Failed, Is.True);
            Assert.That(game.Says, Is.Empty);
        }

        [Test]
        public void DuplicateLabel_IsReportedBeforeRunning()
        {
            var game = new FakeGame();
            LogAssert.Expect(LogType.Error, new Regex("중복 라벨"));

            var executor = Run(Script(Label("a"), Label("a"), Say("x")), game);

            Assert.That(executor.Failed, Is.True);
            Assert.That(game.Says, Is.Empty);
        }

        [Test]
        public void JumpLoopWithoutDialogue_IsAborted()
        {
            LogAssert.Expect(LogType.Error, new Regex("무한 루프"));

            var executor = Run(Script(Label("a"), Jump("a")), new FakeGame());

            Assert.That(executor.Failed, Is.True);
        }

        // ---------- 사전 검증 ----------

        [TestCase("{\"type\":\"jump\",\"target\":\"none\"}", "대상 라벨")]
        [TestCase("{\"type\":\"jump\"}", "target이 없음")]
        [TestCase("{\"type\":\"label\"}", "name이 없음")]
        [TestCase("{\"type\":\"choice\"}", "options가 없음")]
        [TestCase("{\"type\":\"choice\",\"options\":[{\"text\":\"\",\"target\":\"a\"}]}", "text가 없음")]
        [TestCase("{\"type\":\"choice\",\"options\":[{\"text\":\"A\",\"target\":\"a\",\"set\":\"k**\"}]}", "set을 해석할 수 없음")]
        [TestCase("{\"type\":\"set\"}", "name이 올바르지 않음")]
        [TestCase("{\"type\":\"set\",\"name\":\"a\",\"value\":\"abc\"}", "value를 해석할 수 없음")]
        [TestCase("{\"type\":\"if\",\"name\":\"a\",\"target\":\"a\"}", "대상 라벨")] // 라벨 a가 없음
        [TestCase("{\"type\":\"if\",\"target\":\"a\"}", "name이 올바르지 않음")]
        [TestCase("{\"type\":\"if\",\"name\":\"a\",\"value\":\">=x\",\"target\":\"a\"}", "조건")]
        [TestCase("{\"type\":\"bg\"}", "name이 없음")]
        [TestCase("{\"type\":\"show\",\"name\":\"a\",\"position\":\"middle\"}", "position")]
        [TestCase("{\"type\":\"bg\",\"name\":\"a\",\"duration\":-1}", "duration")]
        [TestCase("{\"type\":\"hide\"}", "name 또는 position")]
        [TestCase("{\"type\":\"bgm\"}", "name이 없음")]
        [TestCase("{\"type\":\"sfx\",\"name\":\"a\",\"duration\":-2}", "duration")]
        public void Validate_FindsProblem(string commandJson, string expectedPart)
        {
            var errors = ScriptExecutor.Validate(Script(commandJson));

            Assert.That(errors, Has.Some.Contain(expectedPart), "발견된 문제: " + string.Join(" / ", errors));
        }

        [Test]
        public void Validate_AcceptsAWellFormedScript()
        {
            var errors = ScriptExecutor.Validate(Script(
                "{\"type\":\"bg\",\"name\":\"a\",\"duration\":0.5}",
                "{\"type\":\"show\",\"name\":\"a\",\"position\":\"left\"}",
                "{\"type\":\"hide\",\"name\":\"a\"}",
                "{\"type\":\"hide\",\"position\":\"left\"}",
                "{\"type\":\"bgm\",\"name\":\"stop\"}",
                "{\"type\":\"set\",\"name\":\"k\",\"value\":\"=0\"}",
                "{\"type\":\"if\",\"name\":\"k\",\"value\":\">=1\",\"target\":\"z\"}",
                TwoWayChoice.Replace("\"a\"", "\"z\"").Replace("\"b\"", "\"z\""),
                Label("z"),
                End("e")));

            Assert.That(errors, Is.Empty, string.Join(" / ", errors));
        }
    }
}
