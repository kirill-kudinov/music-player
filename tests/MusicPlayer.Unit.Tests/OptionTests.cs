using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Unit.Tests;

public class OptionTests
{
    public class None
    {
        [Fact]
        public void ShouldChangeIsNone_True()
        {
            Option<int> expected = Option.None;

            Assert.True(expected.IsNone);
        }
    }

    public class Some
    {
        [Fact]
        public void WhenValueIsNull_ReturnNoneOption()
        {
            const string expectedValue = null!;
            var expectedResult = Option.Some(expectedValue);

            Assert.Equal(expectedResult, Option.None);
        }

        [Fact]
        public void WhenValueIsNoNull_ReturnTrue()
        {
            const int expectedValue = 30;
            var expectedResult = Option.Some(expectedValue);
            
            Assert.True(expectedResult.HasValue);
        }
    }

    public class TryGetSome
    {
        [Fact]
        public void WhenValueIsNull_ReturnFalse()
        {
            const string expectedValue = null!;
            var expectedResult = Option.Some(expectedValue);

            var hasValue = expectedResult.TryGetSome(out var value);
            
            Assert.False(hasValue);
            Assert.Null(value);
        }

        [Fact]
        public void WhenValueIsNotNull_ReturnTrue()
        {
            const string expectedValue = "Test";
            var expectedResult = Option.Some(expectedValue);

            var hasValue = expectedResult.TryGetSome(out var value); 
            
            Assert.True(hasValue);
            Assert.Equal(expectedValue, value);
        }
    }
}