using System.CommandLine;
using DotNetReflectCLI.Services;
using ICSharpCode.Decompiler.TypeSystem;

namespace DotNetReflectCLI.Commands
{
    public static class DecompilationCommands
    {
        public static Command CreateDecompileCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
        {
            var outputOption = new Option<DirectoryInfo>(
                "--output",
                "Output directory for decompiled files"
            ) { IsRequired = true };

            var command = new Command("decompile", "Decompile entire assembly")
            {
                inputOption,
                outputOption
            };

            command.SetHandler(async (FileInfo input, DirectoryInfo output) =>
            {
                try
                {
                    var decompiler = decompilationService.CreateDecompiler(input.FullName);
                    var code = await Task.Run(() => decompiler.DecompileWholeModuleAsSingleFile().ToString());
                    
                    if (!output.Exists)
                        output.Create();
                        
                    var outputPath = Path.Combine(output.FullName, $"{Path.GetFileNameWithoutExtension(input.Name)}.cs");
                    await File.WriteAllTextAsync(outputPath, code);
                    Console.WriteLine($"Assembly decompiled successfully to {outputPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decompiling assembly: {ex.Message}");
                }
            }, inputOption, outputOption);

            return command;
        }

        public static Command CreateDecompileTypeCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
        {
            var typeOption = new Option<string>(
                "--type",
                "Full name of the type to decompile"
            ) { IsRequired = true };

            var command = new Command("decompile-type", "Decompile specific type")
            {
                inputOption,
                typeOption
            };

            command.SetHandler(async (FileInfo input, string typeName) =>
            {
                try
                {
                    var decompiler = decompilationService.CreateDecompiler(input.FullName);
                    var type = decompiler.TypeSystem.MainModule.TypeDefinitions
                        .FirstOrDefault(t => t.FullName == typeName)?.GetDefinition();
                    
                    if (type == null)
                    {
                        Console.WriteLine($"Type '{typeName}' not found in assembly");
                        return;
                    }

                    var code = decompilationService.DecompileType(input.FullName, type);
                    Console.WriteLine(code);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decompiling type: {ex.Message}");
                }
            }, inputOption, typeOption);

            return command;
        }

        public static Command CreateDecompileMethodCommand(DecompilationService decompilationService, Option<FileInfo> inputOption)
        {
            var typeOption = new Option<string>(
                "--type",
                "Full name of the type containing the method"
            ) { IsRequired = true };

            var methodOption = new Option<string>(
                "--method",
                "Name of the method to decompile"
            ) { IsRequired = true };

            var command = new Command("decompile-method", "Decompile specific method")
            {
                inputOption,
                typeOption,
                methodOption
            };

            command.SetHandler(async (FileInfo input, string typeName, string methodName) =>
            {
                try
                {
                    var decompiler = decompilationService.CreateDecompiler(input.FullName);
                    var type = decompiler.TypeSystem.MainModule.TypeDefinitions
                        .FirstOrDefault(t => t.FullName == typeName)?.GetDefinition();
                    
                    if (type == null)
                    {
                        Console.WriteLine($"Type '{typeName}' not found in assembly");
                        return;
                    }

                    var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
                    if (method == null)
                    {
                        Console.WriteLine($"Method '{methodName}' not found in type '{typeName}'");
                        return;
                    }

                    var code = decompilationService.DecompileMethod(input.FullName, method);
                    Console.WriteLine(code);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decompiling method: {ex.Message}");
                }
            }, inputOption, typeOption, methodOption);

            return command;
        }
    }
} 