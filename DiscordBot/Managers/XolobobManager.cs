using MongoDB.Bson.IO;

namespace Hackathon.Managers;

public class XolotbobManager
{
    public enum ResponseOptions
    {
        None,
        Word,
        Sentance,
        Breif,
        Short,
        Paragraph,
    }

    private static XolotbobManager _instance;

    private XolotbobManager() { }

    public static XolotbobManager Instance => _instance ??= new XolotbobManager();

    public string XolobobResponse(
        string objective,
        ResponseOptions type = ResponseOptions.None,
        object? extraContext = null
    )
    {
        return type switch
        {
            ResponseOptions.Paragraph => Paragraph(objective),
            ResponseOptions.Short => Short(objective),
            ResponseOptions.Breif => Breif(objective),
            ResponseOptions.Sentance => Sentance(objective),
            ResponseOptions.Word => Word(objective),
            _ => objective,
        };
        //objective is what we are trying to say, ex: "New invetory amount of items is 8 from 10"
        // later, we will run this through some processing
    }

    private string Word(String input)
    {
        return "";
    }

    private string Sentance(String input)
    {
        return "";
    }

    private string Breif(String input)
    {
        return "";
    }

    private string Short(String input)
    {
        return "";
    }

    private string Paragraph(String input)
    {
        return "";
    }
}
