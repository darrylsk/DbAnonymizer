using DbAnonymizer.Console.Helpers;
using NUnit.Framework;

namespace DbAnonymizer.Tests.UnitTests
{
    [TestFixture]
    class StringExtensionsTests
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
        public void RandomOfLength_WhenCalled_ShouldReturnStringOfExpectedLength()
        {
            // Arrange
            const int expected = 10;

            // Act
            var actual = StringExtentions.RandomOfLength(expected);

            // Assert
            Assert.AreEqual(expected, actual.Length);
        }
    }
}
