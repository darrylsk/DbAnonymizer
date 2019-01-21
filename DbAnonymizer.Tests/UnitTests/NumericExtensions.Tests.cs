using DbAnonymizer.Console.Helpers;
using NUnit.Framework;

namespace DbAnonymizer.Tests.UnitTests
{
    internal class NumericExtensionsTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void RandomStringOfLength_WhenCalled_ShouldReturnStringOfExpectedLength()
        {
            // Arrange
            const int expected = 10;

            // Act
            var actual = expected.RandomStringOfLength(StringStyles.AlphaLower);

            // Assert
            Assert.AreEqual(expected, actual.Length);
        }

        [Test]
        public void Tweak_GivenSmallInt_ShouldReturnSlighlyDifferentIntegerValue()
        {
            // Arrange
            const int input = 10;

            // Act
            var actual = input.Tweak();

            // Assert
            Assert.IsInstanceOf<int>(actual);
            Assert.IsNotNull(actual);
        }
    }
}
