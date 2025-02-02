﻿using System.CommandLine;
using DotNetReflectCLI.Services;
using DotNetReflectCLI.Commands;

namespace DotNetReflectCLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("rust-reflect-cli - Утилита для анализа и работы с декомпилированным Rust .NET кодом");
        var decompilationService = new DecompilationService();
        var analysisService = new AnalysisService(decompilationService);

        var inputOption = new Option<FileInfo>(
            "--input",
            "Input assembly file or directory containing assemblies"
        ) { IsRequired = true };

        // Добавляем команды декомпиляции
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileCommand(decompilationService, inputOption));
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileTypeCommand(decompilationService, inputOption));
        rootCommand.AddCommand(DecompilationCommands.CreateDecompileMethodCommand(decompilationService, inputOption));

        // Добавляем команды анализа
        rootCommand.AddCommand(new AnalyzeCommand(analysisService));
        rootCommand.AddCommand(AnalysisCommands.CreateMethodAnalysisCommand(analysisService));
        rootCommand.AddCommand(AnalysisCommands.CreateSearchCommand(analysisService));
        rootCommand.AddCommand(new HelpCommand());

        return await rootCommand.InvokeAsync(args);
    }
}
