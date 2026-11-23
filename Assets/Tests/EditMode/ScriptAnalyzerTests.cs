using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static VN.Tests.TestSupport;

namespace VN.Tests
{
    public class ScriptAnalyzerTests
    {
        private static string Say(string text) => $"{{\"type\":\"say\",\"speaker\":\"\",\"text\":\"{text}\"}}";
        private static string Label(string name) => $"{{\"type\":\"label\",\"name\":\"{name}\"}}";
        private static string Jump(string target) => $"{{\"type\":\"jump\",\"target\":\"{target}\"}}";
        private static string End(string name = null) => name == null ? "{\"type\":\"end\"}" : $"{{\"type\":\"end\",\"name\":\"{name}\"}}";

        private static List<ScriptIssue> Analyze(ScriptData data, params string[] definedEndings)
        {
            return ScriptAnalyzer.Analyze(data, definedEndings.Length == 0 ? null : definedEndings);
        }

        private static IEnumerable<ScriptIssue> Of(List<ScriptIssue> issues, IssueSeverity severity)
        {
            return issues.Where(i => i.Severity == severity);
        }

        private static string Describe(List<ScriptIssue> issues) => string.Join(" / ", issues.Select(i => i.ToString()));

        // ---------- 문제 없는 시나리오 ----------

        [Test]
        public void CleanScript_HasNoIssues()
        {
            var issues = Analyze(Script(
                "{\"type\":\"bg\",\"name\":\"classroom\"}",
                "{\"type\":\"show\",\"name\":\"mina_normal\",\"position\":\"left\"}",
                "{\"type\":\"bgm\",\"name\":\"daily\"}",
                Say("hello"),
                "{\"type\":\"sfx\",\"name\":\"ding\"}",
                "{\"type\":\"bgm\",\"name\":\"stop\"}",
                End("good")), "good");

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        // ---------- 오류 ----------

        [Test]
        public void StructuralErrors_AreReportedAsErrors_AndStopFurtherAnalysis()
        {
            var issues = Analyze(Script(Jump("nowhere"), "{\"type\":\"bg\",\"name\":\"does_not_exist\"}"));

            Assert.That(Of(issues, IssueSeverity.Error).Count(), Is.EqualTo(1), Describe(issues));
            Assert.That(issues[0].Message, Does.Contain("nowhere"));
        }

        [TestCase("{\"type\":\"bg\",\"name\":\"nope\"}", "Backgrounds/nope")]
        [TestCase("{\"type\":\"show\",\"name\":\"nope\"}", "Characters/nope")]
        [TestCase("{\"type\":\"bgm\",\"name\":\"nope\"}", "Audio/Bgm/nope")]
        [TestCase("{\"type\":\"sfx\",\"name\":\"nope\"}", "Audio/Sfx/nope")]
        public void MissingResource_IsAnError(string commandJson, string expectedPath)
        {
            var issues = Analyze(Script(commandJson, End()));

            Assert.That(Of(issues, IssueSeverity.Error), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains(expectedPath)), Describe(issues));
        }

        [Test]
        public void BgmStop_IsNotLookedUpAsAFile()
        {
            var issues = Analyze(Script("{\"type\":\"bgm\",\"name\":\"stop\"}", End()));

            Assert.That(Of(issues, IssueSeverity.Error), Is.Empty, Describe(issues));
        }

        [Test]
        public void EndingNotDefinedOnTheEndingScreen_IsAnError()
        {
            var issues = Analyze(Script(Say("x"), End("secret")), "good");

            Assert.That(Of(issues, IssueSeverity.Error), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("secret")), Describe(issues));
        }

        [Test]
        public void EndingDefinedButNeverUsed_IsAWarning()
        {
            var issues = Analyze(Script(Say("x"), End("good")), "good", "bad");

            Assert.That(Of(issues, IssueSeverity.Error), Is.Empty, Describe(issues));
            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("'bad'")), Describe(issues));
        }

        [Test]
        public void EndingsAreNotChecked_WhenNoDefinitionsAreGiven()
        {
            var issues = ScriptAnalyzer.Analyze(Script(Say("x"), End("anything")), null);

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        // ---------- 경고: 제어 흐름 ----------

        [Test]
        public void CodeAfterJump_IsUnreachable()
        {
            var issues = Analyze(Script(Jump("b"), Say("dead1"), Say("dead2"), Label("b"), Say("alive"), End()));

            var warnings = Of(issues, IssueSeverity.Warning).ToList();
            Assert.That(warnings.Count, Is.EqualTo(1), Describe(issues));       // 연속된 구간은 한 건으로 묶인다
            Assert.That(warnings[0].CommandIndex, Is.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("#1~#2"));
        }

        [Test]
        public void ChoiceBranchesThatAreTargeted_AreReachable()
        {
            var issues = Analyze(Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"A\",\"target\":\"a\"},{\"text\":\"B\",\"target\":\"b\"}]}",
                Label("a"), Say("A"), Jump("z"),
                Label("b"), Say("B"),
                Label("z"), End()));

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        [Test]
        public void CodeAfterChoice_ThatNoOptionTargets_IsUnreachable()
        {
            // choice는 반드시 어느 라벨로 가므로 바로 다음 줄로는 흘러가지 않는다
            var issues = Analyze(Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"A\",\"target\":\"a\"}]}",
                Say("dead"),
                Label("a"), End()));

            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.CommandIndex == 1), Describe(issues));
        }

        [Test]
        public void IfBranch_KeepsBothSidesReachable()
        {
            var issues = Analyze(Script(
                "{\"type\":\"set\",\"name\":\"k\",\"value\":\"=1\"}",
                "{\"type\":\"if\",\"name\":\"k\",\"target\":\"yes\"}",
                Say("no"), Jump("z"),
                Label("yes"), Say("yes"),
                Label("z"), End()));

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        [Test]
        public void PathThatRunsOffTheEnd_IsAWarning()
        {
            var issues = Analyze(Script(Say("last line without end")));

            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("end")), Describe(issues));
        }

        [Test]
        public void ScriptThatEndsWithEnd_DoesNotWarnAboutRunningOff()
        {
            var issues = Analyze(Script(Say("x"), End()));

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        // ---------- 경고: 대사 ----------

        [Test]
        public void EmptyDialogue_IsAWarning()
        {
            var issues = Analyze(Script(Say(""), End()));

            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.CommandIndex == 0 && i.Message.Contains("빈 대사")), Describe(issues));
        }

        [Test]
        public void VeryLongDialogue_IsAWarning_ButTheLimitItselfIsFine()
        {
            var atLimit = Analyze(Script(Say(new string('가', ScriptAnalyzer.LongTextLength)), End()));
            var tooLong = Analyze(Script(Say(new string('가', ScriptAnalyzer.LongTextLength + 1)), End()));

            Assert.That(atLimit, Is.Empty, Describe(atLimit));
            Assert.That(Of(tooLong, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("넘칠 수")), Describe(tooLong));
        }

        // ---------- 경고: 플래그 ----------

        [Test]
        public void FlagReadButNeverSet_IsAWarning()
        {
            var issues = Analyze(Script(
                "{\"type\":\"if\",\"name\":\"ghost\",\"target\":\"z\"}",
                Label("z"), End()));

            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("'ghost'")), Describe(issues));
        }

        [Test]
        public void FlagSetButNeverRead_IsAWarning()
        {
            var issues = Analyze(Script("{\"type\":\"set\",\"name\":\"unused\",\"value\":\"=1\"}", End()));

            Assert.That(Of(issues, IssueSeverity.Warning), Has.Some.Matches<ScriptIssue>(i => i.Message.Contains("'unused'")), Describe(issues));
        }

        [Test]
        public void FlagSetByAChoice_CountsAsSet()
        {
            var issues = Analyze(Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"A\",\"target\":\"a\",\"set\":\"trust+1\"}]}",
                Label("a"),
                "{\"type\":\"if\",\"name\":\"trust\",\"value\":\">=1\",\"target\":\"z\"}",
                Say("no"),
                Label("z"), End()));

            Assert.That(issues, Is.Empty, Describe(issues));
        }

        // ---------- 실제 시나리오 ----------

        [TestCase("story")]
        [TestCase("sample")]
        public void RealScripts_HaveNoErrors(string scriptName)
        {
            var issues = Analyze(Load(scriptName));

            Assert.That(Of(issues, IssueSeverity.Error), Is.Empty, Describe(issues));
        }

        [TestCase("story")]
        [TestCase("sample")]
        public void RealScripts_HaveNoWarnings(string scriptName)
        {
            var issues = Analyze(Load(scriptName));

            Assert.That(issues, Is.Empty, Describe(issues));
        }
    }
}
