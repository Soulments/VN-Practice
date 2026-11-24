using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static VN.Tests.TestSupport;

namespace VN.Tests
{
    /// <summary>
    /// 입력을 기다리는 자리(대사, 선택지)에서 저장하고 그 세이브로 이어 했을 때,
    /// 처음부터 끝까지 한 번에 한 것과 같은 결과가 나오는지 확인한다.
    /// </summary>
    public class ScriptExecutorSaveTests
    {
        private class Point
        {
            public SaveData Save;
            public int SaysBefore;
            public int ChoicesBefore;
            public Dictionary<string, string> Slots;
            public string Bg, Bgm;
            public Dictionary<string, int> Flags;
        }

        /// <summary>시나리오를 picks대로 끝까지 실행하면서, 입력을 기다리는 자리마다 세이브와 그때의 화면 상태를 모은다.</summary>
        private static (FakeGame game, ScriptExecutor executor, List<Point> points) RunCollecting(ScriptData data, List<int> picks)
        {
            var game = new FakeGame();
            game.Chooser = (call, count) => call < picks.Count ? picks[call] : 0;
            var points = new List<Point>();
            ScriptExecutor executor = null;
            game.OnWait = () => points.Add(new Point
            {
                // 파일에 쓰고 읽는 과정까지 거치도록 JSON으로 한 번 변환한다
                Save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(executor.Capture())),
                SaysBefore = game.Says.Count,
                ChoicesBefore = game.OptionCounts.Count,
                Slots = new Dictionary<string, string>(game.Slots),
                Bg = game.Bg,
                Bgm = game.Bgm,
                Flags = new Dictionary<string, int>(executor.Flags.All),
            });
            executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data));
            return (game, executor, points);
        }

        private static IEnumerable<string> ScriptNames => new[] { "story", "sample" };

        [TestCaseSource(nameof(ScriptNames))]
        public void ResumingFromAnyWaitingPoint_GivesTheSameRestAsPlayingStraightThrough(string scriptName)
        {
            var data = Load(scriptName);
            int resumes = 0;

            foreach (var path in ExploreAllPaths(data))
            {
                var (original, originalExecutor, points) = RunCollecting(data, path.Picks);
                Assert.That(points, Is.Not.Empty);

                foreach (var point in points)
                {
                    string where = $"{path} / 대사 {point.SaysBefore}번째 지점(#{point.Save.pc})";

                    var game = new FakeGame();
                    game.Chooser = (call, count) => original.PicksUsed[point.ChoicesBefore + call];

                    bool checkedState = false;
                    ScriptExecutor executor = null;
                    game.OnWait = () =>
                    {
                        if (checkedState) return;
                        checkedState = true;
                        // 이어 하기 직후, 저장했을 때와 같은 화면/소리/플래그여야 한다
                        Assert.That(game.Bg, Is.EqualTo(point.Bg), "배경: " + where);
                        Assert.That(game.Bgm, Is.EqualTo(point.Bgm), "BGM: " + where);
                        Assert.That(game.Slots, Is.EquivalentTo(point.Slots), "캐릭터 자리: " + where);
                        Assert.That(executor.Flags.All, Is.EquivalentTo(point.Flags), "플래그: " + where);
                    };
                    executor = new ScriptExecutor(game, game, game, game);
                    Drive(executor.Run(data, point.Save));

                    Assert.That(executor.Failed, Is.False, where);
                    Assert.That(checkedState, Is.True, where);
                    Assert.That(executor.EndingId, Is.EqualTo(originalExecutor.EndingId), "엔딩: " + where);
                    Assert.That(game.Says, Is.EqualTo(original.Says.Skip(point.SaysBefore).ToList()), "남은 대사: " + where);
                    Assert.That(executor.Flags.All, Is.EquivalentTo(originalExecutor.Flags.All), "최종 플래그: " + where);
                    Assert.That(game.Bgm, Is.EqualTo(original.Bgm), "최종 BGM: " + where);
                    resumes++;
                }
            }

            Assert.That(resumes, Is.GreaterThan(0));
        }

        [Test]
        public void Capture_IsNull_BeforeAndAfterRunning()
        {
            var data = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}");
            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game, game);
            Assert.That(executor.Capture(), Is.Null);

            Drive(executor.Run(data));
            Assert.That(executor.CanSave, Is.False);
            Assert.That(executor.Capture(), Is.Null);
        }

        [Test]
        public void Capture_IsNull_WhileAStageEffectIsRunning()
        {
            // 배경 페이드 같은 연출 중에는 대사도 선택지도 기다리고 있지 않으므로 저장할 수 없다
            var data = Script(
                "{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}",
                "{\"type\":\"bg\",\"name\":\"classroom\",\"duration\":1}",
                "{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"둘\"}");
            var game = new BlockingStage();
            var executor = new ScriptExecutor(game, game, game, game);
            var driver = new Stack<IEnumerator>();
            driver.Push(executor.Run(data));

            // 첫 대사에서 한 번 멈춘다
            Step(driver);
            Assert.That(executor.CanSave, Is.True);

            // 대사를 넘기면 배경 연출로 들어가고, 거기서도 한 번 멈춘다
            Step(driver);
            Assert.That(executor.CanSave, Is.False);
            Assert.That(executor.Capture(), Is.Null);
        }

        /// <summary>코루틴을 다음 yield return null(한 프레임 쉼)까지만 진행한다.</summary>
        private static void Step(Stack<IEnumerator> stack)
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                return;
            }
        }

        [Test]
        public void Resume_RestoresFlagsSoLaterConditionsSeeThem()
        {
            var data = Script(
                "{\"type\":\"set\",\"name\":\"trust\",\"value\":\"=2\"}",
                "{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"저장 지점\"}",
                "{\"type\":\"if\",\"name\":\"trust\",\"value\":\">=2\",\"target\":\"good\"}",
                "{\"type\":\"end\",\"name\":\"bad\"}",
                "{\"type\":\"label\",\"name\":\"good\"}",
                "{\"type\":\"end\",\"name\":\"good\"}");

            var (_, _, points) = RunCollecting(data, new List<int>());
            Assert.That(points, Has.Count.EqualTo(1));

            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data, points[0].Save));

            Assert.That(executor.EndingId, Is.EqualTo("good"));
            Assert.That(executor.Flags.Get("trust"), Is.EqualTo(2));
        }

        [Test]
        public void Resume_AtAChoice_ShowsTheChoiceAgain_AndAppliesThePick()
        {
            var data = Script(
                "{\"type\":\"choice\",\"options\":[{\"text\":\"가\",\"target\":\"a\",\"set\":\"x+1\"},{\"text\":\"나\",\"target\":\"b\"}]}",
                "{\"type\":\"label\",\"name\":\"a\"}",
                "{\"type\":\"end\",\"name\":\"A\"}",
                "{\"type\":\"label\",\"name\":\"b\"}",
                "{\"type\":\"end\",\"name\":\"B\"}");

            var (_, _, points) = RunCollecting(data, new List<int>());
            Assert.That(points[0].Save.lineKey, Does.StartWith("choice|"));

            var game = new FakeGame { Chooser = (call, count) => 1 };
            var executor = new ScriptExecutor(game, game, game, game);
            Drive(executor.Run(data, points[0].Save));

            Assert.That(game.OptionCounts, Is.EqualTo(new[] { 2 }));
            Assert.That(executor.EndingId, Is.EqualTo("B"));
            Assert.That(executor.Flags.Get("x"), Is.EqualTo(0));
        }

        [Test]
        public void CheckResume_AcceptsASaveMadeFromTheSameScript()
        {
            var data = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}");
            var (_, _, points) = RunCollecting(data, new List<int>());

            Assert.That(ScriptExecutor.CheckResume(data, points[0].Save), Is.Null);
        }

        [Test]
        public void CheckResume_RejectsASaveWhenTheLineWasEditedAfterwards()
        {
            var original = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}");
            var edited = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나였던 것\"}");
            var (_, _, points) = RunCollecting(original, new List<int>());

            Assert.That(ScriptExecutor.CheckResume(edited, points[0].Save), Is.Not.Null);
        }

        [Test]
        public void CheckResume_RejectsBadPositionsAndVersions()
        {
            var data = Script(
                "{\"type\":\"label\",\"name\":\"x\"}",
                "{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}");
            var (_, _, points) = RunCollecting(data, new List<int>());

            var outOfRange = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(points[0].Save));
            outOfRange.pc = 99;
            Assert.That(ScriptExecutor.CheckResume(data, outOfRange), Does.Contain("범위"));

            var notALine = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(points[0].Save));
            notALine.pc = 0; // label
            Assert.That(ScriptExecutor.CheckResume(data, notALine), Does.Contain("대사나 선택지가 아님"));

            var oldVersion = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(points[0].Save));
            oldVersion.version = 0;
            Assert.That(ScriptExecutor.CheckResume(data, oldVersion), Does.Contain("버전"));
        }

        [Test]
        public void Run_WithAnUnusableSave_FailsWithoutShowingAnything()
        {
            var original = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"하나\"}");
            var edited = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"바뀐 대사\"}");
            var (_, _, points) = RunCollecting(original, new List<int>());

            var game = new FakeGame();
            var executor = new ScriptExecutor(game, game, game, game);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("시나리오가 바뀌어"));
            Drive(executor.Run(edited, points[0].Save));

            Assert.That(executor.Failed, Is.True);
            Assert.That(game.Events, Is.Empty);
        }

        [Test]
        public void Capture_RecordsTheStageTheWayTheScriptLeftIt()
        {
            var data = Script(
                "{\"type\":\"bg\",\"name\":\"classroom\"}",
                "{\"type\":\"show\",\"name\":\"jihu_normal\",\"position\":\"left\"}",
                "{\"type\":\"show\",\"name\":\"mina_smile\",\"position\":\"right\"}",
                "{\"type\":\"show\",\"name\":\"mina_worried\",\"position\":\"center\"}",
                "{\"type\":\"hide\",\"name\":\"mina_smile\"}",
                "{\"type\":\"bgm\",\"name\":\"daily\"}",
                "{\"type\":\"say\",\"speaker\":\"지후\",\"text\":\"확인용 대사\"}");

            var (_, _, points) = RunCollecting(data, new List<int>());
            var save = points[0].Save;

            Assert.That(save.background, Is.EqualTo("classroom"));
            Assert.That(save.left, Is.EqualTo("jihu_normal"));
            Assert.That(save.center, Is.EqualTo("mina_worried"));
            Assert.That(save.right, Is.Null.Or.Empty, "이름만 주고 hide한 자리는 비어야 한다");
            Assert.That(save.bgm, Is.EqualTo("daily"));
            Assert.That(save.preview, Is.EqualTo("지후: 확인용 대사"));
        }

        [Test]
        public void Capture_RecordsBgmStopAsNoMusic()
        {
            var data = Script(
                "{\"type\":\"bgm\",\"name\":\"daily\"}",
                "{\"type\":\"bgm\",\"name\":\"stop\"}",
                "{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"조용\"}");

            var (_, _, points) = RunCollecting(data, new List<int>());

            Assert.That(points[0].Save.bgm, Is.Null.Or.Empty);
        }

        [Test]
        public void Preview_IsShortenedForLongLines()
        {
            string longText = new string('가', 60);
            var data = Script("{\"type\":\"say\",\"speaker\":\"A\",\"text\":\"" + longText + "\"}");

            var (_, _, points) = RunCollecting(data, new List<int>());

            Assert.That(points[0].Save.preview.Length, Is.LessThan(40));
            Assert.That(points[0].Save.preview, Does.EndWith("…"));
        }

        /// <summary>대사와 배경 연출에서 한 프레임씩 멈추도록 만든 View.</summary>
        private class BlockingStage : IDialogueView, IStageView, IAudioView, IChoiceView
        {
            public IEnumerator ShowLine(string speaker, string text) { yield return null; }
            public IEnumerator SetBackground(string name, float duration) { yield return null; }
            public IEnumerator ShowCharacter(string name, string position, float duration) { yield break; }
            public IEnumerator HideCharacter(string name, string position, float duration) { yield break; }
            public void PlayBgm(string name, float duration) { }
            public void PlaySfx(string name) { }
            public IEnumerator ShowChoices(IList<string> options, System.Action<int> onChosen) { onChosen(0); yield break; }
        }
    }
}
