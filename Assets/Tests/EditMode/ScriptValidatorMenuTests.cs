using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VN.Tests
{
    /// <summary>
    /// VN > Validate Scripts 메뉴가 프로젝트의 실제 시나리오 전체를 검사해서 문제가 없다고 보고하는지 확인한다.
    /// EditMode 테스트는 빈 씬에서 시작하므로, 엔딩 화면 정의(GameFlow.endings)까지 검사하려고 프로젝트 씬을 추가로 열어 둔다.
    /// </summary>
    public class ScriptValidatorMenuTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private Scene scene;

        [SetUp]
        public void OpenProjectScene()
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void CloseProjectScene()
        {
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void ValidateAll_ProjectScripts_HaveNoErrorsOrWarnings()
        {
            Assert.That(Object.FindFirstObjectByType<GameFlow>(), Is.Not.Null, "엔딩 연결 검사를 하려면 씬에 GameFlow가 있어야 함");

            var report = ScriptValidatorMenu.ValidateAll();

            Assert.That(report.Scripts, Is.GreaterThanOrEqualTo(2), "Resources/Scripts 아래 시나리오를 찾지 못함");
            Assert.That(report.Errors, Is.EqualTo(0));
            Assert.That(report.Warnings, Is.EqualTo(0));
        }

        [Test]
        public void GameFlow_PlaysAnExistingScript_AndDefinesTheEndingsItUses()
        {
            // 씬의 GameFlow가 재생하는 시나리오가 있고, 그 시나리오의 엔딩이 모두 엔딩 화면에 정의돼 있어야 한다
            var flow = Object.FindFirstObjectByType<GameFlow>();
            Assert.That(flow, Is.Not.Null, "씬에 GameFlow가 없음");

            var data = TestSupport.Load(flow.scriptName);
            var defined = new System.Collections.Generic.List<string>();
            foreach (var entry in flow.endings) defined.Add(entry.id);

            var issues = ScriptAnalyzer.Analyze(data, defined);
            Assert.That(issues, Is.Empty, string.Join(" / ", issues));
        }

        [Test]
        public void ValidateAll_ReportsAnEndingThatTheSceneDoesNotDefine()
        {
            // 엔딩 정의가 빠진 상황을 흉내 내서, 검사가 실제로 엔딩 연결까지 보는지 확인한다
            var flow = Object.FindFirstObjectByType<GameFlow>();
            var original = flow.endings;
            flow.endings = new[] { new GameFlow.EndingEntry { id = "good", title = "x", description = "x" } }; // normal이 없음
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("normal"));
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("오류 1개"));

                var report = ScriptValidatorMenu.ValidateAll();

                Assert.That(report.Errors, Is.EqualTo(1));
            }
            finally
            {
                flow.endings = original;
            }
        }
    }
}
