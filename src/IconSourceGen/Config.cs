namespace IconSourceGen;

class Config
{
    public Config(string rootNameSpace, string rootFolder)
    {
        RootNameSpace = rootNameSpace;
        RootFolder = rootFolder;
    }

    public string RootNameSpace { get; }
    public string RootFolder { get; }
}