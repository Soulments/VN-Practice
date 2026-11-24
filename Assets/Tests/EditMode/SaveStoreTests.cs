using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VN.Tests
{
    /// <summary>세이브 파일 읽기/쓰기. 시험마다 새 임시 폴더를 쓰므로 실제 세이브에 영향이 없다.</summary>
    public class SaveStoreTests
    {
        private string dir;
        private SaveStore store;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "vn-save-test-" + System.Guid.NewGuid().ToString("N"));
            store = new SaveStore(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        private static SaveData Sample()
        {
            return new SaveData
            {
                scriptName = "story",
                pc = 12,
                lineKey = "say|지후|안녕",
                background = "classroom",
                left = "jihu_normal",
                right = "mina_smile",
                bgm = "daily",
                flags = new[] { new SaveData.FlagEntry { name = "trust", value = 2 } },
                savedAt = "2026-11-24 22:10",
                preview = "지후: 안녕",
            };
        }

        [Test]
        public void SaveThenLoad_ReturnsTheSameContent()
        {
            Assert.That(store.Save(1, Sample()), Is.True);

            var loaded = store.Load(1);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.scriptName, Is.EqualTo("story"));
            Assert.That(loaded.pc, Is.EqualTo(12));
            Assert.That(loaded.lineKey, Is.EqualTo("say|지후|안녕"));
            Assert.That(loaded.background, Is.EqualTo("classroom"));
            Assert.That(loaded.left, Is.EqualTo("jihu_normal"));
            Assert.That(loaded.center, Is.Null.Or.Empty);
            Assert.That(loaded.right, Is.EqualTo("mina_smile"));
            Assert.That(loaded.bgm, Is.EqualTo("daily"));
            Assert.That(loaded.flags, Has.Length.EqualTo(1));
            Assert.That(loaded.flags[0].name, Is.EqualTo("trust"));
            Assert.That(loaded.flags[0].value, Is.EqualTo(2));
            Assert.That(loaded.preview, Is.EqualTo("지후: 안녕"));
        }

        [Test]
        public void EmptySlot_LoadsAsNull_WithoutAnyLog()
        {
            Assert.That(store.Load(2), Is.Null);
            Assert.That(store.Exists(2), Is.False);
        }

        [Test]
        public void Save_OverwritesTheSameSlot_AndLeavesOthersAlone()
        {
            var first = Sample();
            var second = Sample();
            second.pc = 30;

            store.Save(1, first);
            store.Save(2, first);
            store.Save(1, second);

            Assert.That(store.Load(1).pc, Is.EqualTo(30));
            Assert.That(store.Load(2).pc, Is.EqualTo(12));
        }

        [Test]
        public void Save_LeavesNoTemporaryFileBehind()
        {
            store.Save(1, Sample());
            store.Save(1, Sample());

            Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty);
        }

        [TestCase(0)]
        [TestCase(SaveStore.SlotCount + 1)]
        [TestCase(-1)]
        public void SlotsOutsideTheRange_AreRejected(int slot)
        {
            Assert.That(store.Save(slot, Sample()), Is.False);
            Assert.That(store.Load(slot), Is.Null);
            Assert.That(store.Exists(slot), Is.False);
        }

        [Test]
        public void CorruptFile_IsIgnoredWithAWarning()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "slot1.json"), "{ 이건 JSON이 아니다");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("세이브 파일"));
            Assert.That(store.Load(1), Is.Null);
        }

        [Test]
        public void FileWithoutScriptName_IsIgnoredWithAWarning()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "slot1.json"), "{\"version\":1,\"pc\":3}");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("올바르지 않아"));
            Assert.That(store.Load(1), Is.Null);
        }

        [Test]
        public void UnknownVersion_IsIgnoredWithAWarning()
        {
            var data = Sample();
            data.version = 99;
            store.Save(1, data);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("버전"));
            Assert.That(store.Load(1), Is.Null);
        }

        [Test]
        public void Delete_EmptiesTheSlot()
        {
            store.Save(1, Sample());
            store.Delete(1);

            Assert.That(store.Exists(1), Is.False);
            Assert.That(store.Load(1), Is.Null);
        }
    }
}
