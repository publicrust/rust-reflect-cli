namespace DotNetReflectCLI.Models;

public class TypeUsage
{
    public string UsingType { get; set; } = "";
    public List<string> Locations { get; set; } = new();
}

public class MethodUsage
{
    public string CallingType { get; set; } = "";
    public string CallingMethod { get; set; } = "";
    public string Location { get; set; } = "";
}

public class SearchResult
{
    public string Type { get; set; } = "";
    public List<CodeLocation> Locations { get; set; } = new();
}

public class CodeLocation
{
    public string Type { get; set; } = "";
    public string Member { get; set; } = "";
    public string MemberType { get; set; } = ""; // Method, Field, Property, etc.
    public string Context { get; set; } = "";
    public int LineNumber { get; set; }
} 