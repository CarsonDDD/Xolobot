namespace Hackathon.Utility;

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
}
