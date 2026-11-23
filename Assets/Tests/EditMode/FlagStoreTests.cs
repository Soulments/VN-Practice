using NUnit.Framework;

namespace VN.Tests
{
    public class FlagStoreTests
    {
        [Test]
        public void UnsetFlag_IsZero()
        {
            Assert.That(new FlagStore().Get("never_set"), Is.EqualTo(0));
        }

        // set의 value 문법: 생략 = 1로 켜기, "3"/"=3" = 지정, "+2"/"-1" = 더하기
        [TestCase("", 0, 1)]
        [TestCase("3", 0, 3)]
        [TestCase("=5", 0, 5)]
        [TestCase("+2", 5, 7)]
        [TestCase("-10", 7, -3)]
        [TestCase("=-4", 9, -4)]
        [TestCase(" + 1 ", 1, 2)]
        [TestCase("+1", 0, 1)] // 없던 이름에 더하기는 0에서 시작
        public void Apply_ParsesAllValueForms(string value, int start, int expected)
        {
            var flags = new FlagStore();
            flags.Set("a", start);

            Assert.That(flags.TryApply("a", value), Is.True);
            Assert.That(flags.Get("a"), Is.EqualTo(expected));
        }

        [TestCase("abc")]
        [TestCase("=")]
        [TestCase("1.5")]
        [TestCase("++1")]
        [TestCase("+")]
        public void Apply_RejectsBadValue_AndKeepsOldValue(string value)
        {
            var flags = new FlagStore();
            flags.Set("a", 7);

            Assert.That(flags.TryApply("a", value), Is.False);
            Assert.That(flags.Get("a"), Is.EqualTo(7));
        }

        [TestCase("kindness", true)]
        [TestCase("route2", true)]
        [TestCase("_hidden", true)]
        [TestCase("호감도", true)]
        [TestCase("1abc", false)]
        [TestCase("a b", false)]
        [TestCase("a-b", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValidName(string name, bool expected)
        {
            Assert.That(FlagStore.IsValidName(name), Is.EqualTo(expected));
        }

        [Test]
        public void Apply_RejectsBadName()
        {
            var flags = new FlagStore();
            Assert.That(flags.TryApply("1abc", "1"), Is.False);
            Assert.That(flags.All.Count, Is.EqualTo(0));
        }

        // 선택지의 set: 이름과 값이 붙은 한 덩어리 문자열
        [TestCase("kindness+1", "kindness", 1)]
        [TestCase("kindness-1", "kindness", -1)]
        [TestCase("route=2", "route", 2)]
        [TestCase("route2+1", "route2", 1)] // 이름 끝의 숫자는 이름의 일부
        [TestCase("metMina", "metMina", 1)]
        [TestCase(" kindness + 1 ", "kindness", 1)]
        public void ApplyAssignment_ParsesNameAndValue(string expression, string name, int expected)
        {
            var flags = new FlagStore();

            Assert.That(flags.TryApplyAssignment(expression), Is.True);
            Assert.That(flags.Get(name), Is.EqualTo(expected));
        }

        [TestCase("kind**")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("+1")]
        public void ApplyAssignment_RejectsBadExpression(string expression)
        {
            var flags = new FlagStore();

            Assert.That(flags.TryApplyAssignment(expression), Is.False);
            Assert.That(flags.All.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyAssignment_Accumulates()
        {
            var flags = new FlagStore();
            flags.TryApplyAssignment("trust+1");
            flags.TryApplyAssignment("trust+1");
            flags.TryApplyAssignment("trust-1");

            Assert.That(flags.Get("trust"), Is.EqualTo(1));
        }

        // if의 조건 문법: 숫자 = 같음, ==, !=, >, >=, <, <=, 생략 = 0이 아니면 참
        [TestCase("2", 2, true)]
        [TestCase("2", 3, false)]
        [TestCase("==2", 2, true)]
        [TestCase("!=2", 2, false)]
        [TestCase("!=2", 5, true)]
        [TestCase(">2", 2, false)]
        [TestCase(">2", 3, true)]
        [TestCase(">=2", 2, true)]
        [TestCase(">=2", 1, false)]
        [TestCase("<2", 1, true)]
        [TestCase("<2", 2, false)]
        [TestCase("<=2", 2, true)]
        [TestCase("<=2", 3, false)]
        [TestCase(">=-1", 0, true)]
        [TestCase(">= 2", 2, true)] // 공백 무시
        [TestCase("", 2, true)]
        [TestCase("", 0, false)]
        public void Evaluate_Conditions(string condition, int current, bool expected)
        {
            var flags = new FlagStore();
            flags.Set("k", current);

            Assert.That(flags.Evaluate("k", condition), Is.EqualTo(expected));
        }

        [Test]
        public void Evaluate_UnsetFlagCountsAsZero()
        {
            var flags = new FlagStore();

            Assert.That(flags.Evaluate("missing", ""), Is.False);
            Assert.That(flags.Evaluate("missing", "0"), Is.True);
            Assert.That(flags.Evaluate("missing", "<1"), Is.True);
        }

        [TestCase("abc")]
        [TestCase(">=x")]
        [TestCase(">>2")]
        public void Evaluate_BadCondition_IsFalse(string condition)
        {
            var flags = new FlagStore();
            flags.Set("k", 2);

            Assert.That(flags.Evaluate("k", condition), Is.False);
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            var flags = new FlagStore();
            flags.Set("a", 1);
            flags.Clear();

            Assert.That(flags.All.Count, Is.EqualTo(0));
            Assert.That(flags.Get("a"), Is.EqualTo(0));
        }
    }
}
