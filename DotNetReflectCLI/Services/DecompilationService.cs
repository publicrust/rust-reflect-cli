using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;

namespace DotNetReflectCLI.Services;

public class DecompilationService
{
    public CSharpDecompiler CreateDecompiler(string assemblyPath)
    {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
        
        // Создаем загрузчик сборок, который будет искать зависимости в той же директории
        var assemblyResolver = new UniversalAssemblyResolver(assemblyPath, false, "netstandard2.0");
        
        // Добавляем директорию со сборкой в пути поиска
        assemblyResolver.AddSearchDirectory(assemblyDirectory);
        
        // Загружаем все DLL из директории
        foreach (var dllFile in Directory.GetFiles(assemblyDirectory, "*.dll"))
        {
            try
            {
                using var stream = File.OpenRead(dllFile);
                using var peReader = new PEReader(stream);
                var metadataReader = peReader.GetMetadataReader();
                assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(dllFile)!);
            }
            catch
            {
                // Игнорируем файлы, которые не являются валидными сборками .NET
                continue;
            }
        }

        var settings = new DecompilerSettings 
        { 
            ThrowOnAssemblyResolveErrors = false,
            LoadInMemory = true
        };

        return new CSharpDecompiler(assemblyPath, assemblyResolver, settings);
    }

    public async Task DecompileAssembly(string inputPath, string outputPath)
    {
        var decompiler = CreateDecompiler(inputPath);
        
        // Получаем все типы из сборки
        var types = decompiler.TypeSystem.MainModule.TypeDefinitions;
        
        foreach (var type in types)
        {
            var typeOutput = Path.Combine(outputPath, $"{type.FullName}.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(typeOutput)!);
            
            // Декомпилируем каждый тип
            var decompiled = decompiler.DecompileTypeAsString(new FullTypeName(type.FullName));
            await File.WriteAllTextAsync(typeOutput, decompiled);
        }
    }

    public Task<string> DecompileType(string assemblyPath, string typeName)
    {
        var decompiler = CreateDecompiler(assemblyPath);
        var result = decompiler.DecompileTypeAsString(new FullTypeName(typeName));
        return Task.FromResult(result);
    }

    public Task<string> DecompileMethod(string assemblyPath, string typeName, string methodName)
    {
        var decompiler = CreateDecompiler(assemblyPath);
        var type = decompiler.TypeSystem.FindType(new FullTypeName(typeName)).GetDefinition();
        
        if (type == null)
            throw new ArgumentException($"Type {typeName} not found");
            
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method == null)
            throw new ArgumentException($"Method {methodName} not found in type {typeName}");
            
        var result = decompiler.DecompileAsString(method.MetadataToken);
        return Task.FromResult(result);
    }
} 