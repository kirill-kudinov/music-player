using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Unit.Tests;

public class ResultTests
{
    private const string ExpectedNullValue = null!;
    private const int ExpectedValue = 30;

    public class Ok
    {
        [Fact]
        public void WhenValueIsNull_ReturnResultWithNullValueError()
        {
            var actual = Result.Ok(ExpectedNullValue);
            var actualError = actual.Error;

            Assert.True(actual.IsError);
            Assert.Equal(ResultErrors.NullValue, actualError);
        }

        [Fact]
        public void WhenValueIsNotNull_ReturnResultWithCorrectValue()
        {
            var actual = Result.Ok(ExpectedValue);

            var hasValue = actual.TryGetOk(out var value);

            Assert.True(hasValue);
            Assert.True(actual.IsOk);
            Assert.Equal(Core.Primitives.Error.None, actual.Error);
            Assert.Equal(ExpectedValue, value);
        }
    }

    public class Fail
    {
        
        [Fact]
        public void WhenErrorIsNotNull_ReturnNonNullError()
        {
            var error = new Error("Test.Test", "Test");
            Result<string> result = Result.Fail(error);
            Assert.Equal(error, result.Error);
        }
    }


    public class TryGetOk
    {
        [Fact]
        public void WhenValueIsNull_ReturnFalse()
        {
            var result = Result.Ok(ExpectedNullValue);

            var hasValue = result.TryGetOk(out var value);

            Assert.False(hasValue);
            Assert.Equal(ExpectedNullValue, value);
        }

        [Fact]
        public void WhenValueIsNotNull_ReturnTrue()
        {
            var result = Result.Ok(ExpectedValue);

            var hasValue = result.TryGetOk(out var value);

            Assert.True(hasValue);
            Assert.Equal(ExpectedValue, value);
        }
    }
}