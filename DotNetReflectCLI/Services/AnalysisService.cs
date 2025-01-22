using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.CSharp.Syntax;
using DotNetReflectCLI.Models;
using System.Text.RegularExpressions;
using System.Reflection.PortableExecutable;

namespace DotNetReflectCLI.Services;

public class AnalysisService
{
    private readonly DecompilationService _decompilationService;

    public AnalysisService(DecompilationService decompilationService)
    {
        _decompilationService = decompilationService;
    }

    private string ExtractContextAroundMatch(string code, string searchText, int lineNumber)
    {
        var codeLines = code.Split('\n');
        if (lineNumber >= codeLines.Length) return "";

        var contextLines = new List<string>();
        var targetLine = codeLines[lineNumber].Trim();

        // Берем только 2 строки до и 2 строки после найденной строки
        var contextStart = Math.Max(0, lineNumber - 2);
        var contextEnd = Math.Min(codeLines.Length - 1, lineNumber + 2);

        // Добавляем маркер начала фрагмента, если это не начало файла
        if (contextStart > 0)
            contextLines.Add("...");

        for (int i = contextStart; i <= contextEnd; i++)
        {
            var line = codeLines[i].Trim();
            if (i == lineNumber)
            {
                contextLines.Add(">>> " + line); // Маркируем найденную строку
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                contextLines.Add("    " + line);
            }
        }

        // Добавляем маркер конца фрагмента, если это не конец файла
        if (contextEnd < codeLines.Length - 1)
            contextLines.Add("...");

        return string.Join("\n", contextLines);
    }

    private bool IsValidAssembly(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var peReader = new PEReader(stream);
            var metadataReader = peReader.GetMetadataReader();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<SearchResult>> SearchInDirectory(string directoryPath, string searchText, string? @namespace = null)
    {
        var results = new List<SearchResult>();
        var files = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            if (IsValidAssembly(file))
            {
                try
                {
                    var fileResults = await SearchInCode(file, searchText, @namespace);
                    results.AddRange(fileResults);
                }
                catch
                {
                    // Пропускаем файлы, которые не удалось проанализировать
                    continue;
                }
            }
        }

        return results;
    }

    public async Task<IEnumerable<TypeUsage>> FindTypeUsages(string assemblyPath, string typeName)
    {
        var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
        var targetType = decompiler.TypeSystem.FindType(new FullTypeName(typeName))?.GetDefinition();
        
        if (targetType == null)
            throw new ArgumentException($"Type {typeName} not found");

        var usages = new List<TypeUsage>();
        var syntaxTree = await Task.Run(() => decompiler.DecompileWholeModuleAsSingleFile());

        foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            try
            {
                var typeNode = decompiler.DecompileTypeAsString(new FullTypeName(type.FullName));
                var typeUsage = new TypeUsage
                {
                    UsingType = type.FullName,
                    Locations = new List<string>()
                };

                // Анализируем использование типа в полях
                foreach (var field in type.Fields)
                {
                    if (field.ReturnType.FullName == targetType.FullName)
                    {
                        typeUsage.Locations.Add($"Field: {field.Name}");
                    }
                }

                // Анализируем использование типа в методах
                foreach (var method in type.Methods)
                {
                    var methodBody = await Task.Run(() => decompiler.DecompileAsString(method.MetadataToken));
                    if (methodBody.Contains(targetType.FullName))
                    {
                        typeUsage.Locations.Add($"Method: {method.Name}");
                    }
                }

                if (typeUsage.Locations.Any())
                {
                    usages.Add(typeUsage);
                }
            }
            catch
            {
                // Пропускаем типы, которые не удалось проанализировать
                continue;
            }
        }

        return usages;
    }

    public async Task<IEnumerable<MethodUsage>> FindMethodUsages(string assemblyPath, string typeName, string methodName)
    {
        var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
        var type = decompiler.TypeSystem.FindType(new FullTypeName(typeName))?.GetDefinition();
        
        if (type == null)
            throw new ArgumentException($"Type {typeName} not found");
            
        var targetMethod = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (targetMethod == null)
            throw new ArgumentException($"Method {methodName} not found in type {typeName}");

        var usages = new List<MethodUsage>();

        foreach (var searchType in decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            try
            {
                foreach (var method in searchType.Methods)
                {
                    var methodBody = await Task.Run(() => decompiler.DecompileAsString(method.MetadataToken));
                    if (methodBody.Contains($"{typeName}.{methodName}") || 
                        methodBody.Contains($"{type.Name}.{methodName}"))
                    {
                        usages.Add(new MethodUsage
                        {
                            CallingType = searchType.FullName,
                            CallingMethod = method.Name,
                            Location = $"{searchType.FullName}.{method.Name}"
                        });
                    }
                }
            }
            catch
            {
                // Пропускаем методы, которые не удалось проанализировать
                continue;
            }
        }

        return usages;
    }

    public async Task<IEnumerable<SearchResult>> SearchInCode(string assemblyPath, string searchText, string? @namespace = null)
    {
        var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
        var results = new Dictionary<string, SearchResult>();

        foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            // Если указан namespace, пропускаем типы из других пространств имен
            if (@namespace != null && !type.FullName.StartsWith(@namespace))
                continue;

            try
            {
                var typeCode = await Task.Run(() => decompiler.DecompileTypeAsString(new FullTypeName(type.FullName)));
                var typeLines = typeCode.Split('\n');
                
                // Ищем совпадения в методах
                foreach (var method in type.Methods)
                {
                    try
                    {
                        var methodCode = await Task.Run(() => decompiler.DecompileAsString(method.MetadataToken));
                        if (methodCode.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            var methodLines = methodCode.Split('\n');
                            var lineNumber = Array.FindIndex(methodLines, 
                                line => line.Contains(searchText, StringComparison.OrdinalIgnoreCase));

                            if (!results.ContainsKey(type.FullName))
                            {
                                results[type.FullName] = new SearchResult 
                                { 
                                    Type = type.FullName,
                                    Locations = new List<CodeLocation>()
                                };
                            }

                            results[type.FullName].Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = method.Name,
                                MemberType = "Method",
                                Context = ExtractContextAroundMatch(methodCode, searchText, lineNumber),
                                LineNumber = lineNumber + 1
                            });
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Ищем совпадения в полях
                foreach (var field in type.Fields)
                {
                    try
                    {
                        var fieldCode = field.ToString() ?? "";
                        if (fieldCode.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!results.ContainsKey(type.FullName))
                            {
                                results[type.FullName] = new SearchResult 
                                { 
                                    Type = type.FullName,
                                    Locations = new List<CodeLocation>()
                                };
                            }

                            results[type.FullName].Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = field.Name,
                                MemberType = "Field",
                                Context = fieldCode,
                                LineNumber = 0
                            });
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Ищем совпадения в свойствах
                foreach (var property in type.Properties)
                {
                    try
                    {
                        var propertyCode = property.ToString() ?? "";
                        if (propertyCode.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!results.ContainsKey(type.FullName))
                            {
                                results[type.FullName] = new SearchResult 
                                { 
                                    Type = type.FullName,
                                    Locations = new List<CodeLocation>()
                                };
                            }

                            results[type.FullName].Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = property.Name,
                                MemberType = "Property",
                                Context = propertyCode,
                                LineNumber = 0
                            });
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // Пропускаем типы, которые не удалось декомпилировать
                continue;
            }
        }

        return results.Values;
    }
}

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