using System.Collections.Generic;
using NUnit.Framework;

namespace VN.Tests
{
    /// <summary>글자 속도와 음량 설정. 저장소는 메모리에만 두는 가짜를 써서 PlayerPrefs를 건드리지 않는다.</summary>
    public class GameSettingsTests
    {
        private class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
            public int SaveCalls;

            public int GetInt(string key, int defaultValue) => Values.TryGetValue(key, out var v) ? v : defaultValue;
            public void SetInt(string key, int value) => Values[key] = value;
            public void Save() => SaveCalls++;
        }

        [Test]
        public void Defaults_MatchTheValuesTheGameShippedWith()
        {
            var settings = new GameSettings();

            Assert.That(settings.TextSpeedName, Is.EqualTo("보통"));
            Assert.That(settings.TypeInterval, Is.EqualTo(0.05f).Within(1e-6));
            Assert.That(settings.BgmVolume, Is.EqualTo(0.6f).Within(1e-6));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f).Within(1e-6));
        }

        [Test]
        public void TextSpeed_GetsFasterWithEachStep_AndInstantMeansNoDelay()
        {
            var settings = new GameSettings();
            var intervals = new List<float>();
            settings.StepTextSpeed(-10);
            for (int i = 0; i < GameSettings.TextSpeedNames.Length; i++)
            {
                intervals.Add(settings.TypeInterval);
                settings.StepTextSpeed(1);
            }

            Assert.That(intervals, Is.Ordered.Descending);
            Assert.That(intervals[intervals.Count - 1], Is.EqualTo(0f));
        }

        [Test]
        public void Steps_StopAtBothEnds_AndReportWhetherAnythingChanged()
        {
            var settings = new GameSettings();

            Assert.That(settings.StepTextSpeed(+100), Is.True);
            Assert.That(settings.TextSpeed, Is.EqualTo(GameSettings.TextSpeedNames.Length - 1));
            Assert.That(settings.StepTextSpeed(+1), Is.False);

            Assert.That(settings.StepBgm(-100), Is.True);
            Assert.That(settings.BgmVolume, Is.EqualTo(0f));
            Assert.That(settings.StepBgm(-1), Is.False);

            Assert.That(settings.StepSfx(+1), Is.False, "효과음은 기본이 최대라 더 올라가지 않는다");
            Assert.That(settings.StepSfx(-1), Is.True);
            Assert.That(settings.SfxVolume, Is.EqualTo(0.9f).Within(1e-6));
        }

        [Test]
        public void SaveThenLoad_RestoresTheSameValues()
        {
            var store = new MemoryStore();
            var settings = new GameSettings();
            settings.StepTextSpeed(+2);
            settings.StepBgm(-3);
            settings.StepSfx(-5);
            settings.Save(store);

            var loaded = new GameSettings();
            loaded.Load(store);

            Assert.That(loaded.TextSpeed, Is.EqualTo(settings.TextSpeed));
            Assert.That(loaded.BgmStep, Is.EqualTo(settings.BgmStep));
            Assert.That(loaded.SfxStep, Is.EqualTo(settings.SfxStep));
            Assert.That(store.SaveCalls, Is.EqualTo(1));
        }

        [Test]
        public void Load_WithNothingStored_KeepsTheDefaults()
        {
            var loaded = new GameSettings();
            loaded.Load(new MemoryStore());

            Assert.That(loaded.TextSpeed, Is.EqualTo(GameSettings.DefaultTextSpeed));
            Assert.That(loaded.BgmStep, Is.EqualTo(GameSettings.DefaultBgmStep));
            Assert.That(loaded.SfxStep, Is.EqualTo(GameSettings.DefaultSfxStep));
        }

        [Test]
        public void Load_ClampsValuesThatAreOutOfRange()
        {
            var store = new MemoryStore();
            store.Values[GameSettings.TextSpeedKey] = 99;
            store.Values[GameSettings.BgmKey] = -4;
            store.Values[GameSettings.SfxKey] = 1000;

            var loaded = new GameSettings();
            loaded.Load(store);

            Assert.That(loaded.TextSpeed, Is.EqualTo(GameSettings.TextSpeedNames.Length - 1));
            Assert.That(loaded.BgmStep, Is.EqualTo(0));
            Assert.That(loaded.SfxStep, Is.EqualTo(GameSettings.MaxVolumeStep));
        }

        [Test]
        public void Volumes_AreAlwaysBetweenZeroAndOne()
        {
            var settings = new GameSettings();
            for (int step = -2; step <= 2 * GameSettings.MaxVolumeStep; step++)
            {
                settings.StepBgm(1);
                Assert.That(settings.BgmVolume, Is.InRange(0f, 1f));
            }
        }
    }
}
