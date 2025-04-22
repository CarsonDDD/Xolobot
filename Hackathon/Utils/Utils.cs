namespace Hackathon.Utils;

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


    public string[] DecodeTagFilter(string filterString)
    {
        string[] terms = new string[0];

        if (!string.IsNullOrWhiteSpace(filterString))
        {
            filterString = filterString.Replace("_", "");// sanitize

            terms = filterString
                .Replace("_", "")
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }

        return terms;
        /*if (terms == null)
        {
            //throw new Exception("Invalid filter string to decode: " + filterString);
        }
        else
        {
            return terms;
        }*/
    }

}
