using System.Text;

namespace MusicPlayer.Core.Playlist;

public static class NameNormalizer
{
    public static string ForFolder(string entityName)
    {
        StringBuilder stringBuilder = new();

        var words = entityName.Split(' ');

        for (var i = 0; i < words.Length; i++)
        {
            if (i == words.Length)
            {
                stringBuilder.Append($"{words[i]}");
                continue;
            }

            stringBuilder.Append($"{words[i]}_");
        }

        return stringBuilder.ToString();
    }

    public static string ForEntity(string directoryName)
    {
        StringBuilder stringBuilder = new();

        var words = directoryName.Split('_');

        for (var i = 0; i < words.Length; i++)
        {
            if (i == words.Length)
            {
                stringBuilder.Append($"{words[i]}");
                continue;
            }

            stringBuilder.Append($"{words[i]} ");
        }

        return stringBuilder.ToString();
    }
}