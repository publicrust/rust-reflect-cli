using System.CommandLine;
using DotNetReflectCLI.Services;

namespace DotNetReflectCLI.Commands;

public static class DecompilationCommands
{
    public static Command CreateDecompileCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
    {
        var command = new Command("decompile", "Decompile .NET assembly to C# code");
        var outputOption = new Option<DirectoryInfo>(
            "--output",
            "Output directory for decompiled code"
        ) { IsRequired = true };

        command.AddOption(inputOption);
        command.AddOption(outputOption);

        command.SetHandler(async (FileInfo input, DirectoryInfo output) =>
        {
            try
            {
                Console.WriteLine($"Decompiling {input.FullName} to {output.FullName}");
                await decompilationService.DecompileAssembly(input.FullName, output.FullName);
                Console.WriteLine("Decompilation completed successfully");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during decompilation: {ex.Message}");
            }
        }, inputOption, outputOption);

        return command;
    }

    public static Command CreateDecompileTypeCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
    {
        var command = new Command("decompile-type", "Decompile specific type from assembly");
        var typeNameOption = new Option<string>(
            "--type",
            "Full name of the type to decompile"
        ) { IsRequired = true };

        command.AddOption(inputOption);
        command.AddOption(typeNameOption);

        command.SetHandler(async (FileInfo input, string typeName) =>
        {
            try
            {
                Console.WriteLine($"Decompiling type {typeName} from {input.FullName}");
                var result = await decompilationService.DecompileType(input.FullName, typeName);
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during type decompilation: {ex.Message}");
            }
        }, inputOption, typeNameOption);

        return command;
    }

    public static Command CreateDecompileMethodCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
    {
        var command = new Command("decompile-method", "Decompile specific method from assembly");
        var typeNameOption = new Option<string>(
            "--type",
            "Full name of the type containing the method"
        ) { IsRequired = true };
        var methodNameOption = new Option<string>(
            "--method",
            "Name of the method to decompile"
        ) { IsRequired = true };

        command.AddOption(inputOption);
        command.AddOption(typeNameOption);
        command.AddOption(methodNameOption);

        command.SetHandler(async (FileInfo input, string typeName, string methodName) =>
        {
            try
            {
                Console.WriteLine($"Decompiling method {methodName} from type {typeName} in {input.FullName}");
                var result = await decompilationService.DecompileMethod(input.FullName, typeName, methodName);
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during method decompilation: {ex.Message}");
            }
        }, inputOption, typeNameOption, methodNameOption);

        return command;
    }
} 