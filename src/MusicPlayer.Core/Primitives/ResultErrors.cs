namespace MusicPlayer.Core.Primitives;

public static class ResultErrors
{
    public static readonly Error NullValue =
        new("Result.NullValue", "Значение результата не может быть null.");

    public static readonly Error Uninitialized =
        new("Result.Uninitialized", "Структура Result не была инициализирована.");
}