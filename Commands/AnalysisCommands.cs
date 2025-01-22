using System.CommandLine;
using DotNetReflectCLI.Services;
using DotNetReflectCLI.Models;

namespace DotNetReflectCLI.Commands;

public static class AnalysisCommands
{
    public static Command CreateSearchCommand(AnalysisService analysisService)
    {
        var inputOption = new Option<string>(
            "--input",
            "Input assembly file or directory containing assemblies"
        ) { IsRequired = true };

        var searchOption = new Option<string>(
            new[] { "--string", "--pattern" },
            "Text or pattern to search for (supports wildcards)"
        ) { IsRequired = true };

        var command = new Command("search", "Search for types and members in assembly")
        {
            inputOption,
            searchOption
        };

        command.SetHandler(async (string input, string pattern) =>
        {
            try
            {
                var assemblies = new List<string>();
                
                if (Directory.Exists(input))
                {
                    assemblies.AddRange(Directory.GetFiles(input, "*.dll", SearchOption.AllDirectories));
                }
                else if (File.Exists(input))
                {
                    assemblies.Add(input);
                }
                else
                {
                    Console.WriteLine($"Error: Path not found: {input}");
                    return;
                }

                if (!assemblies.Any())
                {
                    Console.WriteLine($"No .NET assemblies found in {input}");
                    return;
                }

                var totalResults = new List<(string Assembly, List<SearchResult> Results)>();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var results = await analysisService.SearchInCode(assembly, pattern);
                        if (results.Any())
                        {
                            totalResults.Add((assembly, results));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to search in {assembly}: {ex.Message}");
                    }
                }

                if (!totalResults.Any())
                {
                    if (pattern.StartsWith("\"") && pattern.EndsWith("\""))
                    {
                        Console.WriteLine($"No string literals found containing '{pattern.Trim('\"')}' in any assembly.");
                    }
                    else
                    {
                        Console.WriteLine($"No matches found for '{pattern}' in any assembly.");
                    }
                    return;
                }

                foreach (var (assembly, results) in totalResults)
                {
                    Console.WriteLine($"\nIn assembly: {Path.GetFileName(assembly)}");
                    foreach (var result in results)
                    {
                        Console.WriteLine($"\nFound matches in type {result.Type}:");
                        foreach (var location in result.Locations)
                        {
                            if (location.MemberType == "StringLiteral")
                            {
                                Console.WriteLine($"\n  String literal in method: {location.Member}");
                            }
                            else
                            {
                                Console.WriteLine($"\n  {location.MemberType}: {location.Member}");
                            }
                            if (location.LineNumber > 0)
                            {
                                Console.WriteLine($"  Line: {location.LineNumber}");
                            }
                            Console.WriteLine("  Context:");
                            Console.WriteLine($"  {location.Context}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching assembly: {ex.Message}");
            }
        }, inputOption, searchOption);

        return command;
    }

    public static Command CreateMethodAnalysisCommand(AnalysisService analysisService)
    {
        var inputOption = new Option<string>(
            "--input",
            "Input assembly file or directory containing assemblies"
        ) { IsRequired = true };

        var typeOption = new Option<string>(
            "--type",
            "Full name of the type containing the method"
        ) { IsRequired = true };

        var methodOption = new Option<string>(
            "--method",
            "Name of the method to analyze"
        ) { IsRequired = true };

        var command = new Command("analyze-method", "Analyze method usage in assembly")
        {
            inputOption,
            typeOption,
            methodOption
        };

        command.SetHandler(async (string input, string typeName, string methodName) =>
        {
            try
            {
                typeName = typeName.Trim('"', '\'');
                methodName = methodName.Trim('"', '\'');

                var assemblies = new List<string>();
                
                if (Directory.Exists(input))
                {
                    assemblies.AddRange(Directory.GetFiles(input, "*.dll", SearchOption.AllDirectories));
                }
                else if (File.Exists(input))
                {
                    assemblies.Add(input);
                }
                else
                {
                    Console.WriteLine($"Error: Path not found: {input}");
                    return;
                }

                if (!assemblies.Any())
                {
                    Console.WriteLine($"No .NET assemblies found in {input}");
                    return;
                }

                var totalUsages = new List<(string Assembly, IEnumerable<MethodUsage> Usages)>();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var usages = await analysisService.FindMethodUsages(assembly, typeName, methodName);
                        if (usages.Any())
                        {
                            totalUsages.Add((assembly, usages));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to analyze {assembly}: {ex.Message}");
                    }
                }

                if (!totalUsages.Any())
                {
                    Console.WriteLine($"No usages of method '{typeName}.{methodName}' found in any assembly.");
                    return;
                }

                foreach (var (assembly, usages) in totalUsages)
                {
                    Console.WriteLine($"\nIn assembly: {Path.GetFileName(assembly)}");
                    foreach (var usage in usages)
                    {
                        Console.WriteLine($"Method '{methodName}' is used in:");
                        Console.WriteLine($"  Type: {usage.CallingType}");
                        Console.WriteLine($"  Method: {usage.CallingMethod}");
                        Console.WriteLine($"  Location: {usage.Location}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing method: {ex.Message}");
            }
        }, inputOption, typeOption, methodOption);

        return command;
    }
} 