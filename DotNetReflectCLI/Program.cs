using System.CommandLine;
using DotNetReflectCLI.Services;
using DotNetReflectCLI.Commands;

namespace DotNetReflectCLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CLI utility for .NET assembly analysis and decompilation");
        var decompilationService = new DecompilationService();
        var analysisService = new AnalysisService(decompilationService);

        var inputOption = new Option<FileInfo>(
            "--input",
            "Input assembly file to analyze"
        ) { IsRequired = true };

        // Добавляем команды декомпиляции
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileCommand(decompilationService, inputOption));
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileTypeCommand(decompilationService, inputOption));
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileMethodCommand(decompilationService, inputOption));

        // Добавляем команды анализа
        rootCommand.AddCommand(AnalysisCommands.CreateAnalyzeTypeCommand(analysisService, inputOption));
        rootCommand.AddCommand(AnalysisCommands.CreateAnalyzeMethodCommand(analysisService, inputOption));
        rootCommand.AddCommand(AnalysisCommands.CreateSearchCommand(analysisService, inputOption));

        return await rootCommand.InvokeAsync(args);
    }
}
