using System.CommandLine;
using DotNetReflectCLI.Services;

namespace DotNetReflectCLI.Commands
{
    public class AnalyzeCommand : Command
    {
        public AnalyzeCommand(AnalysisService analysisService) : base("analyze", "Analyze assembly for type usages")
        {
            var inputOption = new Option<FileInfo>(
                "--input",
                "Input assembly file to analyze"
            ) { IsRequired = true };

            var typeOption = new Option<string>(
                "--type",
                "Full name of the type to find usages of"
            ) { IsRequired = true };

            AddOption(inputOption);
            AddOption(typeOption);

            this.SetHandler(async (FileInfo input, string typeName) =>
            {
                try
                {
                    var usages = await analysisService.FindTypeUsages(input.FullName, typeName);
                    foreach (var usage in usages)
                    {
                        Console.WriteLine($"Type '{usage.UsingType}' uses '{typeName}' in:");
                        foreach (var location in usage.Locations)
                        {
                            Console.WriteLine($"  - {location}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error analyzing assembly: {ex.Message}");
                }
            }, inputOption, typeOption);
        }
    }
} 