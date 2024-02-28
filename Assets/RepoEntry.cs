using System.Collections.Generic;
using System;

public class RepoEntry
{
    public string Name { get; private set; }

    public RepoEntry(Dictionary<string, object> Data)
    {
        Name = (string)Data["Name"];
    }
}