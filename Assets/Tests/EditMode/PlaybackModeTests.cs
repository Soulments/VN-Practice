using NUnit.Framework;

namespace VN.Tests
{
    /// <summary>오토/스킵 규칙. 화면이나 시간 없이 "언제 다음 줄로 넘어가는가"만 확인한다.</summary>
    public class PlaybackModeTests
    {
        private const float Base = 1.2f;
        private const float PerChar = 0.05f;
        private const float SkipDelay = 0.05f;

        [Test]
        public void StartsWithBothOff_AndNeverAdvancesOnItsOwn()
        {
            var mode = new PlaybackMode();

            Assert.That(mode.Auto, Is.False);
            Assert.That(mode.Skip, Is.False);
            Assert.That(mode.Active, Is.False);
            Assert.That(mode.ShouldAdvance(999f, 30, Base, PerChar, SkipDelay), Is.False);
        }

        [Test]
        public void TurningOnOne_TurnsOffTheOther()
        {
            var mode = new PlaybackMode();

            mode.SetAuto(true);
            mode.SetSkip(true);
            Assert.That(mode.Skip, Is.True);
            Assert.That(mode.Auto, Is.False);

            mode.SetAuto(true);
            Assert.That(mode.Auto, Is.True);
            Assert.That(mode.Skip, Is.False);
        }

        [Test]
        public void TurningOneOff_LeavesTheOtherAlone()
        {
            var mode = new PlaybackMode();
            mode.SetAuto(true);

            mode.SetSkip(false);

            Assert.That(mode.Auto, Is.True);
        }

        [Test]
        public void Toggle_FlipsTheMode()
        {
            var mode = new PlaybackMode();

            mode.ToggleAuto();
            Assert.That(mode.Auto, Is.True);
            mode.ToggleAuto();
            Assert.That(mode.Auto, Is.False);

            mode.ToggleSkip();
            Assert.That(mode.Skip, Is.True);
            mode.ToggleSkip();
            Assert.That(mode.Skip, Is.False);
        }

        [Test]
        public void Stop_TurnsBothOff()
        {
            var mode = new PlaybackMode();
            mode.SetSkip(true);

            mode.Stop();

            Assert.That(mode.Active, Is.False);
        }

        [Test]
        public void AutoWait_GrowsWithTheNumberOfCharacters()
        {
            Assert.That(PlaybackMode.AutoWait(0, Base, PerChar), Is.EqualTo(Base).Within(1e-6));
            Assert.That(PlaybackMode.AutoWait(40, Base, PerChar), Is.EqualTo(3.2f).Within(1e-6));
            Assert.That(PlaybackMode.AutoWait(40, Base, PerChar), Is.GreaterThan(PlaybackMode.AutoWait(10, Base, PerChar)));
            Assert.That(PlaybackMode.AutoWait(-5, Base, PerChar), Is.EqualTo(Base).Within(1e-6), "음수 글자 수는 0으로 친다");
        }

        [Test]
        public void Auto_AdvancesOnlyAfterTheLineIsReadLongEnough()
        {
            var mode = new PlaybackMode();
            mode.SetAuto(true);

            Assert.That(mode.ShouldAdvance(3.1f, 40, Base, PerChar, SkipDelay), Is.False);
            Assert.That(mode.ShouldAdvance(3.2f, 40, Base, PerChar, SkipDelay), Is.True);
            // 짧은 대사는 더 빨리 넘어간다
            Assert.That(mode.ShouldAdvance(1.6f, 5, Base, PerChar, SkipDelay), Is.True);
        }

        [Test]
        public void Skip_AdvancesAlmostImmediately_RegardlessOfLength()
        {
            var mode = new PlaybackMode();
            mode.SetSkip(true);

            Assert.That(mode.ShouldAdvance(0.04f, 80, Base, PerChar, SkipDelay), Is.False);
            Assert.That(mode.ShouldAdvance(0.05f, 80, Base, PerChar, SkipDelay), Is.True);
        }
    }
}
