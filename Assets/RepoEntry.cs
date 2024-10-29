using System.Collections.Generic;

public class RepoEntry
{
    public string Name { get; private set; }
    public bool Translation { get; private set; }

    public RepoEntry(Dictionary<string, object> Data)
    {
        Name = (string)Data["Name"];
        Translation = Data.ContainsKey("TranslationOf") ? true : false;
    }
}