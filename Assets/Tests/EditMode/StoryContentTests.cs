using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static VN.Tests.TestSupport;

namespace VN.Tests
{
    /// <summary>
    /// 실제 시나리오(Resources/Scripts)가 어떤 선택 조합으로 해도 끝까지 정상 진행되는지 확인한다.
    /// 선택지의 모든 조합을 자동으로 돌려 보므로 시나리오를 고치거나 늘려도 그대로 동작한다.
    /// (story 전용 규칙을 확인하는 테스트는 시나리오의 엔딩 조건을 바꾸면 함께 고쳐야 한다.)
    /// </summary>
    public class StoryContentTests
    {
        private static IEnumerable<string> ScriptNames => new[] { "story", "sample" };

        [TestCaseSource(nameof(ScriptNames))]
        public void EveryPath_EndsNormally(string scriptName)
        {
            var paths = ExploreAllPaths(Load(scriptName));

            Assert.That(paths, Is.Not.Empty);
            foreach (var path in paths)
            {
                Assert.That(path.Failed, Is.False, path.ToString());
                Assert.That(path.EndingId, Is.Not.Null.And.Not.Empty, "엔딩 이름이 없는 경로: " + path);
            }
        }

        [TestCaseSource(nameof(ScriptNames))]
        public void EveryPath_LeavesNothingOnScreen_AndStopsTheMusic(string scriptName)
        {
            foreach (var path in ExploreAllPaths(Load(scriptName)))
            {
                Assert.That(path.LeftoverCharacters, Is.False, "캐릭터가 화면에 남은 채 끝남: " + path);
                Assert.That(path.FinalBgm, Is.EqualTo(AudioManager.StopName), "BGM이 꺼지지 않고 끝남: " + path);
            }
        }

        [TestCaseSource(nameof(ScriptNames))]
        public void EveryPath_UsesOnlyExistingResources_AndNeverHidesAnEmptySlot(string scriptName)
        {
            foreach (var path in ExploreAllPaths(Load(scriptName)))
            {
                Assert.That(path.Problems, Is.Empty, path + " : " + string.Join(" / ", path.Problems));
            }
        }

        [TestCaseSource(nameof(ScriptNames))]
        public void EveryDialogueLine_CanBeReached(string scriptName)
        {
            var data = Load(scriptName);
            var reached = new HashSet<string>();
            foreach (var path in ExploreAllPaths(data)) reached.UnionWith(path.Says);

            var unreached = data.commands.Where(c => c.type == "say" && !reached.Contains(c.text)).Select(c => c.text).ToList();
            Assert.That(unreached, Is.Empty, "어떤 선택으로도 나오지 않는 대사: " + string.Join(" / ", unreached));
        }

        [TestCaseSource(nameof(ScriptNames))]
        public void ThereAreAtLeastTwoDifferentEndings(string scriptName)
        {
            var endings = ExploreAllPaths(Load(scriptName)).Select(p => p.EndingId).Distinct().ToList();

            Assert.That(endings.Count, Is.GreaterThanOrEqualTo(2), "도달 가능한 엔딩: " + string.Join(", ", endings));
        }

        // ---------- story 전용 규칙 ----------

        [Test]
        public void Story_EndingFollowsTheTrustRule()
        {
            foreach (var path in ExploreAllPaths(Load("story")))
            {
                int trust = path.Flags.TryGetValue("trust", out var value) ? value : 0;
                string expected = trust >= 2 ? "good" : "normal";

                Assert.That(path.EndingId, Is.EqualTo(expected), $"{path} (trust={trust})");
            }
        }

        [Test]
        public void Story_BothEndingsAreReachable_AndSplitEvenly()
        {
            var paths = ExploreAllPaths(Load("story"));

            Assert.That(paths.Count, Is.EqualTo(8), "선택지 3곳 x 2개 = 8가지 조합");
            Assert.That(paths.Count(p => p.EndingId == "good"), Is.EqualTo(4));
            Assert.That(paths.Count(p => p.EndingId == "normal"), Is.EqualTo(4));
        }

        [Test]
        public void Story_EachChoiceCanRaiseTrustAtMostOnce()
        {
            foreach (var path in ExploreAllPaths(Load("story")))
            {
                Assert.That(path.Flags["trust"], Is.InRange(0, 3), path.ToString());
            }
        }

        // ---------- 경로 탐색기 자체 ----------

        [Test]
        public void PathExplorer_FindsEveryCombination()
        {
            // 선택지 2개(2가지)와 3개(3가지)가 이어지면 6가지 경로
            var data = Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"a\",\"target\":\"x\"},{\"text\":\"b\",\"target\":\"x\"}]}",
                "{\"type\":\"label\",\"name\":\"x\"}",
                "{\"type\":\"choice\",\"options\":[{\"text\":\"c\",\"target\":\"y\"},{\"text\":\"d\",\"target\":\"y\"},{\"text\":\"e\",\"target\":\"y\"}]}",
                "{\"type\":\"label\",\"name\":\"y\"}",
                "{\"type\":\"end\",\"name\":\"e\"}");

            var paths = ExploreAllPaths(data);

            Assert.That(paths.Count, Is.EqualTo(6));
            Assert.That(paths.Select(p => string.Join(",", p.Picks)).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void PathExplorer_ScriptWithoutChoices_HasOnePath()
        {
            var paths = ExploreAllPaths(Script("{\"type\":\"say\",\"speaker\":\"\",\"text\":\"x\"}", "{\"type\":\"end\",\"name\":\"e\"}"));

            Assert.That(paths.Count, Is.EqualTo(1));
        }

        [Test]
        public void PathExplorer_FollowsBranchesThatChangeHowManyChoicesFollow()
        {
            // 첫 선택이 a면 두 번째 선택지가 더 나오고, b면 바로 끝난다 → 경로 3개 (a-c, a-d, b)
            var data = Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"a\",\"target\":\"A\"},{\"text\":\"b\",\"target\":\"B\"}]}",
                "{\"type\":\"label\",\"name\":\"A\"}",
                "{\"type\":\"choice\",\"options\":[{\"text\":\"c\",\"target\":\"B\"},{\"text\":\"d\",\"target\":\"B\"}]}",
                "{\"type\":\"label\",\"name\":\"B\"}",
                "{\"type\":\"end\",\"name\":\"e\"}");

            Assert.That(ExploreAllPaths(data).Count, Is.EqualTo(3));
        }
    }
}
