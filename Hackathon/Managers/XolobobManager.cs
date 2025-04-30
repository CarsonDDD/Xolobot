namespace Hackathon.Managers;

public enum XOLOBOBOPTIONS
{
    None = 0,
}

public class XolotbobManager
{
    private static XolotbobManager _instance;

    private XolotbobManager() { }

    public static XolotbobManager Instance => _instance ??= new XolotbobManager();

    public string XolobobResponse(
        string objective,
        XOLOBOBOPTIONS type = XOLOBOBOPTIONS.None,
        string? extraContext = null
    )
    {
        //objective is what we are trying to say, ex: "New invetory amount of items is 8 from 10"
        // later, we will run this through some processing
        return objective;
    }
}
