using System.Text;

namespace CafeMenuConverter.Core.Parsing;

/// <summary>Склейка строк, разбитых при вёрстке.</summary>
public static class TextJoin
{
    /// <summary>
    /// Название блюда: дефис в конце строки — настоящий («ПО-|СТАРОРУССКИ»), поэтому он сохраняется и
    /// строки склеиваются без пробела. Так же со слэшем. В остальных случаях — через пробел.
    /// </summary>
    public static string JoinName(IEnumerable<string> lines) => Join(lines, removeTrailingHyphen: false);

    /// <summary>
    /// Состав: дефис в конце строки — перенос слова («полуфа-|брикатом»), он удаляется.
    /// </summary>
    public static string JoinComposition(IEnumerable<string> lines) => Join(lines, removeTrailingHyphen: true);

    private static string Join(IEnumerable<string> lines, bool removeTrailingHyphen)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (sb.Length > 0)
            {
                var last = sb[^1];
                if (last == '-' && removeTrailingHyphen)
                    sb.Length--;
                else if (last != '-' && last != '/')
                    sb.Append(' ');
            }

            sb.Append(line);
        }

        return sb.ToString();
    }
}
