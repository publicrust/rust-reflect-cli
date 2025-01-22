using System.CommandLine;
using DotNetReflectCLI.Services;

namespace DotNetReflectCLI.Commands;

public static class AnalysisCommands
{
    public static Command CreateAnalyzeTypeCommand(AnalysisService analysisService, Option<FileInfo> inputOption)
    {
        var command = new Command("analyze", "Analyze type usage in assembly");
        var typeNameOption = new Option<string>(
            "--type",
            "Full name of the type to analyze"
        ) { IsRequired = true };

        command.AddOption(inputOption);
        command.AddOption(typeNameOption);

        command.SetHandler(async (FileInfo input, string typeName) =>
        {
            try
            {
                Console.WriteLine($"Analyzing type usage {typeName} in {input.FullName}");
                var usages = await analysisService.FindTypeUsages(input.FullName, typeName);
                
                foreach (var usage in usages)
                {
                    Console.WriteLine($"\nType: {usage.UsingType}");
                    foreach (var location in usage.Locations)
                    {
                        Console.WriteLine($"  - {location}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during type analysis: {ex.Message}");
            }
        }, inputOption, typeNameOption);

        return command;
    }

    public static Command CreateAnalyzeMethodCommand(AnalysisService analysisService, Option<FileInfo> inputOption)
    {
        var command = new Command("analyze-method", "Analyze method usage in assembly");
        var methodNameOption = new Option<string>(
            "--method",
            "Full name of the method to analyze (Format: Namespace.Type.Method)"
        ) { IsRequired = true };

        command.AddOption(inputOption);
        command.AddOption(methodNameOption);

        command.SetHandler(async (FileInfo input, string methodFullName) =>
        {
            try
            {
                var lastDot = methodFullName.LastIndexOf('.');
                if (lastDot == -1)
                    throw new ArgumentException("Method name should be in format: Namespace.Type.Method");

                var typeName = methodFullName.Substring(0, lastDot);
                var methodName = methodFullName.Substring(lastDot + 1);

                Console.WriteLine($"Analyzing method usage {methodName} in type {typeName} from {input.FullName}");
                var usages = await analysisService.FindMethodUsages(input.FullName, typeName, methodName);
                
                foreach (var usage in usages)
                {
                    Console.WriteLine($"\nCalled from: {usage.Location}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during method analysis: {ex.Message}");
            }
        }, inputOption, methodNameOption);

        return command;
    }

    public static Command CreateSearchCommand(AnalysisService analysisService, Option<FileInfo> inputOption)
    {
        var command = new Command("search", "Search in decompiled code");
        
        var searchTextOption = new Option<string>(
            "--string",
            "Text to search for"
        ) { IsRequired = true };

        var namespaceOption = new Option<string>(
            "--namespace",
            "Optional namespace to limit search scope"
        );

        var pathOption = new Option<string>(
            "--input",
            "Path to a file or directory to search in"
        ) { IsRequired = true };

        command.AddOption(pathOption);
        command.AddOption(searchTextOption);
        command.AddOption(namespaceOption);

        command.SetHandler(async (string path, string searchText, string? @namespace) =>
        {
            try
            {
                IEnumerable<SearchResult> results;

                if (Directory.Exists(path))
                {
                    Console.WriteLine($"Searching for '{searchText}' in directory {path}" + 
                        (@namespace != null ? $" (namespace: {@namespace})" : ""));
                    results = await analysisService.SearchInDirectory(path, searchText, @namespace);
                }
                else if (File.Exists(path))
                {
                    Console.WriteLine($"Searching for '{searchText}' in {path}" + 
                        (@namespace != null ? $" (namespace: {@namespace})" : ""));
                    results = await analysisService.SearchInCode(path, searchText, @namespace);
                }
                else
                {
                    Console.Error.WriteLine($"Path {path} does not exist or is not accessible");
                    return;
                }

                var foundResults = false;
                foreach (var result in results)
                {
                    foundResults = true;
                    Console.WriteLine($"\nType: {result.Type}");
                    foreach (var location in result.Locations)
                    {
                        Console.WriteLine($"  {location.MemberType}: {location.Member}");
                        if (location.LineNumber > 0)
                        {
                            Console.WriteLine($"  Line: {location.LineNumber}");
                        }
                        Console.WriteLine("  Context:");
                        foreach (var line in location.Context.Split('\n'))
                        {
                            Console.WriteLine($"    {line}");
                        }
                        Console.WriteLine();
                    }
                }

                if (!foundResults)
                {
                    Console.WriteLine("No matches found.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during code search: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner error: {ex.InnerException.Message}");
                }
            }
        }, pathOption, searchTextOption, namespaceOption);

        return command;
    }
} 