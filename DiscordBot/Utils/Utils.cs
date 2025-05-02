namespace Hackathon.Utility;

using System.Globalization;

public class Utils
{
    private static Utils _instance;

    public static Utils Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new Utils();
            }

            return _instance;
        }
    }

    public static string[] DecodeFilter(string filterString)
    {
        string[] terms = new string[0];

        if (!string.IsNullOrWhiteSpace(filterString))
        {
            terms = filterString
                .Replace("_", "") // sanitize
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }

        return terms;
    }

    public static string FormatUIList(IEnumerable<string> items)
    {
        const int LIST_MAX_WIDTH = 4;
        var textInfo = CultureInfo.InvariantCulture.TextInfo;

        return !items.Any()
            ? "-# ***Nothing***"
            : string.Join(
                '\n',
                items
                    .Select((lbl, idx) => new { lbl, idx })
                    .GroupBy(x => x.idx / LIST_MAX_WIDTH) // 4 per line
                    .Select(g =>
                        "-# "
                        + // line prefix
                        string.Join(
                            ", ",
                            g.Select(x => $"***{textInfo.ToTitleCase(x.lbl.ToLower())}***")
                        )
                    )
            );
    }
}
