using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using System.Linq;

namespace DotNetReflectCLI.Services
{
    public class AnalysisService
    {
        private readonly DecompilationService _decompilationService;

        public AnalysisService(DecompilationService decompilationService)
        {
            _decompilationService = decompilationService;
        }

        private string ExtractContextAroundMatch(string code, string searchText, int lineNumber)
        {
            var lines = code.Split('\n');
            if (lineNumber <= 0 || lineNumber > lines.Length)
            {
                return "";
            }

            var contextLines = new List<string>();
            var methodStart = -1;
            var methodEnd = -1;

            // Ищем начало метода
            for (int i = lineNumber - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Contains("public") || line.Contains("private") || line.Contains("protected"))
                {
                    methodStart = i;
                    break;
                }
            }

            // Ищем конец метода
            if (methodStart != -1)
            {
                var braceCount = 0;
                var foundFirstBrace = false;
                for (int i = methodStart; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("{"))
                    {
                        foundFirstBrace = true;
                        braceCount++;
                    }
                    if (line.Contains("}"))
                    {
                        braceCount--;
                        if (foundFirstBrace && braceCount == 0)
                        {
                            methodEnd = i;
                            break;
                        }
                    }
                }
            }

            if (methodStart != -1 && methodEnd != -1)
            {
                // Проверяем наличие атрибутов
                if (methodStart > 0 && lines[methodStart - 1].Trim().StartsWith("["))
                {
                    contextLines.Add($"{methodStart} --> {lines[methodStart - 1].Trim()}");
                }
                
                // Добавляем объявление метода
                contextLines.Add($"{methodStart + 1} --> {lines[methodStart].Trim()} {{");

                // Если есть код до контекста, добавляем многоточие
                if (lineNumber - methodStart > 3)
                {
                    contextLines.Add("    ....");
                }

                // Добавляем 2 строки до совпадения
                if (lineNumber - 2 > methodStart)
                {
                    contextLines.Add($"{lineNumber - 1} --> {lines[lineNumber - 2].Trim()}");
                }
                if (lineNumber - 1 > methodStart)
                {
                    contextLines.Add($"{lineNumber} --> {lines[lineNumber - 1].Trim()}");
                }

                // Добавляем строку с совпадением
                contextLines.Add($"{lineNumber + 1} --> {lines[lineNumber].Trim()}");

                // Добавляем 1 строку после совпадения
                if (lineNumber + 1 < methodEnd)
                {
                    contextLines.Add($"{lineNumber + 2} --> {lines[lineNumber + 1].Trim()}");
                }

                // Если есть код после контекста, добавляем многоточие
                if (methodEnd - lineNumber > 2)
                {
                    contextLines.Add("    ....");
                }

                contextLines.Add("}");
            }
            else
            {
                // Для обычного кода (не метод) добавляем контекст
                if (lineNumber > 1)
                {
                    contextLines.Add($"{lineNumber - 1} --> {lines[lineNumber - 2].Trim()}");
                }
                contextLines.Add($"{lineNumber} --> {lines[lineNumber - 1].Trim()}");
                if (lineNumber < lines.Length)
                {
                    contextLines.Add($"{lineNumber + 1} --> {lines[lineNumber].Trim()}");
                }
            }

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

        private IEnumerable<string> GetAssemblyFiles(string path)
        {
            if (File.Exists(path))
            {
                if (IsValidAssembly(path))
                {
                    yield return path;
                }
                else
                {
                    throw new ArgumentException($"File '{path}' is not a valid .NET assembly");
                }
            }
            else if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (IsValidAssembly(file))
                    {
                        yield return file;
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Path '{path}' does not exist or is not accessible");
            }
        }

        public async Task<IEnumerable<TypeUsage>> FindTypeUsages(string path, string typeName)
        {
            var results = new List<TypeUsage>();
            foreach (var assemblyPath in GetAssemblyFiles(path))
            {
                try
                {
                    var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
                    var targetType = decompiler.TypeSystem.FindType(new FullTypeName(typeName))?.GetDefinition();
                    
                    if (targetType == null) continue;

                    var syntaxTree = await Task.Run(() => decompiler.DecompileWholeModuleAsSingleFile());

                    foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
                    {
                        try
                        {
                            var typeNode = await Task.Run(() => decompiler.DecompileTypeAsString(new FullTypeName(type.FullName)));
                            var typeUsage = new TypeUsage
                            {
                                UsingType = type.FullName,
                                Locations = new List<string>()
                            };

                            // Analyze fields
                            foreach (var field in type.Fields)
                            {
                                if (field.ReturnType.FullName == targetType.FullName)
                                {
                                    typeUsage.Locations.Add($"Field: {field.Name}");
                                }
                            }

                            // Analyze methods
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
                                results.Add(typeUsage);
                            }
                        }
                        catch
                        {
                            // Skip types that couldn't be analyzed
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to analyze assembly '{assemblyPath}': {ex.Message}");
                }
            }

            return results;
        }

        public async Task<IEnumerable<MethodUsage>> FindMethodUsages(string path, string typeName, string methodName)
        {
            var results = new List<MethodUsage>();
            foreach (var assemblyPath in GetAssemblyFiles(path))
            {
                try
                {
                    var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
                    var type = decompiler.TypeSystem.FindType(new FullTypeName(typeName))?.GetDefinition();
                    
                    if (type == null) continue;
                        
                    var targetMethod = type.Methods.FirstOrDefault(m => m.Name == methodName);
                    if (targetMethod == null) continue;

                    // Get all interfaces implemented by the type
                    var interfaces = type.DirectBaseTypes
                        .Where(t => t.Kind == TypeKind.Interface)
                        .Select(t => t.GetDefinition())
                        .Where(t => t != null)
                        .ToList();

                    // Get all base types
                    var baseTypes = new List<ITypeDefinition>();
                    var currentType = type;
                    while (currentType != null)
                    {
                        baseTypes.Add(currentType);
                        currentType = currentType.DirectBaseTypes
                            .FirstOrDefault(t => t.Kind == TypeKind.Class)?
                            .GetDefinition();
                    }

                    foreach (var searchType in decompiler.TypeSystem.MainModule.TypeDefinitions)
                    {
                        try
                        {
                            // Check if this type is derived from any of the base types
                            var isDerived = searchType.DirectBaseTypes
                                .Any(t => baseTypes.Any(bt => bt.FullName == t.GetDefinition()?.FullName));

                            // Check if this type implements any of the target type's interfaces
                            var implementsInterface = interfaces.Any(i => 
                                searchType.DirectBaseTypes.Any(t => t.GetDefinition()?.FullName == i.FullName));

                            // Check for fields and properties of target type
                            var hasTargetTypeMembers = false;
                            foreach (var field in searchType.Fields)
                            {
                                if (baseTypes.Any(bt => field.ReturnType.FullName == bt.FullName) ||
                                    interfaces.Any(i => field.ReturnType.FullName == i.FullName))
                                {
                                    hasTargetTypeMembers = true;
                                    break;
                                }
                            }

                            if (!hasTargetTypeMembers)
                            {
                                foreach (var property in searchType.Properties)
                                {
                                    if (baseTypes.Any(bt => property.ReturnType.FullName == bt.FullName) ||
                                        interfaces.Any(i => property.ReturnType.FullName == i.FullName))
                                    {
                                        hasTargetTypeMembers = true;
                                        break;
                                    }
                                }
                            }

                            // Check for methods returning target type
                            var methodsReturningTargetType = new HashSet<string>();
                            foreach (var m in searchType.Methods)
                            {
                                if (baseTypes.Any(bt => m.ReturnType.FullName == bt.FullName) ||
                                    interfaces.Any(i => m.ReturnType.FullName == i.FullName))
                                {
                                    methodsReturningTargetType.Add(m.Name);
                                }
                            }

                            foreach (var method in searchType.Methods)
                            {
                                try
                                {
                                    // Check if this is an override of the target method
                                    var isOverride = method.Name == methodName && 
                                        method.Parameters.Count == targetMethod.Parameters.Count &&
                                        method.ReturnType.FullName == targetMethod.ReturnType.FullName;

                                    var methodBody = await Task.Run(() => decompiler.DecompileAsString(method.MetadataToken));

                                    // Check for direct calls
                                    var hasDirectCall = baseTypes.Any(bt => 
                                        methodBody.Contains($"{bt.FullName}.{methodName}") || 
                                        methodBody.Contains($"{bt.Name}.{methodName}")) ||
                                        methodBody.Contains($"base.{methodName}") ||
                                        methodBody.Contains($"this.{methodName}(") ||
                                        methodBody.Contains($"{methodName}(");

                                    // Check for calls through variables
                                    var hasVariableCall = false;
                                    var localVariables = new HashSet<string>();
                                    var memberVariables = new HashSet<string>();
                                    var methodChains = new HashSet<string>();

                                    // Add fields and properties to member variables
                                    if (hasTargetTypeMembers)
                                    {
                                        foreach (var field in searchType.Fields)
                                        {
                                            if (baseTypes.Any(bt => field.ReturnType.FullName == bt.FullName) ||
                                                interfaces.Any(i => field.ReturnType.FullName == i.FullName))
                                            {
                                                memberVariables.Add(field.Name);
                                            }
                                        }

                                        foreach (var property in searchType.Properties)
                                        {
                                            if (baseTypes.Any(bt => property.ReturnType.FullName == bt.FullName) ||
                                                interfaces.Any(i => property.ReturnType.FullName == i.FullName))
                                            {
                                                memberVariables.Add(property.Name);
                                            }
                                        }
                                    }

                                    if (methodBody.Contains(methodName))
                                    {
                                        var lines = methodBody.Split('\n');
                                        foreach (var line in lines)
                                        {
                                            // Check for variable declarations and parameters
                                            var hasTypeReference = baseTypes.Any(bt =>
                                                line.Contains($": {bt.Name}") || 
                                                line.Contains($": {bt.FullName}") ||
                                                line.Contains($"as {bt.Name}") ||
                                                line.Contains($"as {bt.FullName}") ||
                                                line.Contains($"is {bt.Name}") ||
                                                line.Contains($"is {bt.FullName}") ||
                                                line.Contains($"({bt.Name} ") ||
                                                line.Contains($"({bt.FullName} ") ||
                                                line.Contains($", {bt.Name} ") ||
                                                line.Contains($", {bt.FullName} ")) ||
                                            interfaces.Any(i => 
                                                line.Contains($": {i.Name}") ||
                                                line.Contains($"as {i.Name}") ||
                                                line.Contains($"is {i.Name}") ||
                                                line.Contains($"({i.Name} ") ||
                                                line.Contains($", {i.Name} "));

                                            // Extract variable names
                                            if (hasTypeReference)
                                            {
                                                var match = System.Text.RegularExpressions.Regex.Match(line, @"(\w+)\s*[=:]");
                                                if (match.Success)
                                                {
                                                    localVariables.Add(match.Groups[1].Value);
                                                }
                                            }

                                            // Extract method chains
                                            if (line.Contains("."))
                                            {
                                                var parts = line.Split('.');
                                                var chain = "";
                                                for (var i = 0; i < parts.Length; i++)
                                                {
                                                    var part = parts[i].Trim();
                                                    if (part.Contains("("))
                                                    {
                                                        part = part.Substring(0, part.IndexOf("("));
                                                    }
                                                    if (!string.IsNullOrWhiteSpace(part))
                                                    {
                                                        chain = string.IsNullOrEmpty(chain) ? part : chain + "." + part;
                                                        methodChains.Add(chain);
                                                    }
                                                }
                                            }

                                            // Check for method calls
                                            var hasMethodCall = line.Contains(methodName) &&
                                                (hasTypeReference || 
                                                 line.Contains("base.") || 
                                                 line.Contains("this.") ||
                                                 localVariables.Any(v => line.Contains($"{v}.{methodName}")) ||
                                                 memberVariables.Any(v => line.Contains($"{v}.{methodName}")) ||
                                                 methodsReturningTargetType.Any(m => 
                                                    methodChains.Any(c => c.EndsWith(m)) && 
                                                    line.Contains($"{m}().{methodName}")));

                                            if (hasMethodCall)
                                            {
                                                hasVariableCall = true;
                                                break;
                                            }
                                        }
                                    }

                                    // Check for method parameters of target type
                                    var hasTargetTypeParameter = method.Parameters
                                        .Any(p => baseTypes.Any(bt => 
                                            p.Type.FullName == bt.FullName) ||
                                            interfaces.Any(i => p.Type.FullName == i.FullName));

                                    if (isOverride || isDerived || implementsInterface || hasDirectCall || hasVariableCall || hasTargetTypeParameter || hasTargetTypeMembers)
                                    {
                                        results.Add(new MethodUsage
                                        {
                                            CallingType = searchType.FullName,
                                            CallingMethod = method.Name,
                                            Location = $"{searchType.FullName}.{method.Name}"
                                        });
                                    }
                                }
                                catch
                                {
                                    // Skip methods that couldn't be analyzed
                                    continue;
                                }
                            }
                        }
                        catch
                        {
                            // Skip types that couldn't be analyzed
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to analyze assembly '{assemblyPath}': {ex.Message}");
                }
            }

            return results;
        }

        public async Task<IEnumerable<SearchResult>> SearchInCode(string path, string searchText, string? @namespace = null)
        {
            var results = new Dictionary<string, SearchResult>();
            foreach (var assemblyPath in GetAssemblyFiles(path))
            {
                try
                {
                    var decompiler = _decompilationService.CreateDecompiler(assemblyPath);

                    foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
                    {
                        // Skip types from other namespaces if namespace filter is specified
                        if (@namespace != null && !type.FullName.StartsWith(@namespace))
                            continue;

                        try
                        {
                            var typeCode = await Task.Run(() => decompiler.DecompileTypeAsString(new FullTypeName(type.FullName)));
                            var typeLines = typeCode.Split('\n');
                            
                            // Search in methods
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

                            // Search in fields
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

                            // Search in properties
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
                            // Skip types that couldn't be decompiled
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to analyze assembly '{assemblyPath}': {ex.Message}");
                }
            }

            return results.Values;
        }

        public async Task<List<SearchResult>> SearchInCode(string assemblyPath, string pattern)
        {
            var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
            var results = new List<SearchResult>();
            var isStringLiteral = pattern.StartsWith("\"") && pattern.EndsWith("\"");

            var searchText = isStringLiteral ? pattern.Trim('"') : pattern.Trim('"', '\'');
            var regex = new Regex(isStringLiteral ? Regex.Escape(searchText) : pattern.Replace("*", ".*"), RegexOptions.IgnoreCase);

            foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
            {
                var searchResult = new SearchResult { Type = type.FullName };

                foreach (var method in type.Methods)
                {
                    try
                    {
                        var methodBody = decompiler.DecompileAsString(method.MetadataToken);
                        var lines = methodBody.Split('\n').Select(l => l.TrimEnd()).ToList();
                        
                        // Поиск в строковых литералах
                        for (int i = 0; i < lines.Count; i++)
                        {
                            var line = lines[i];
                            var matches = Regex.Matches(line, "(?:@|\"|\\$\")(?:[^\"\\\\]|\\\\.)*\"");
                            foreach (Match match in matches)
                            {
                                var literal = match.Value.TrimStart('@', '$').Trim('"');
                                literal = Regex.Unescape(literal);
                                
                                if (regex.IsMatch(literal))
                                {
                                    var context = ExtractContextAroundMatch(methodBody, literal, i + 1);
                                    if (!string.IsNullOrEmpty(context))
                                    {
                                        searchResult.Locations.Add(new CodeLocation
                                        {
                                            Type = type.FullName,
                                            Member = method.Name,
                                            MemberType = "StringLiteral",
                                            Context = context,
                                            LineNumber = i + 1
                                        });
                                    }
                                    break;
                                }
                            }
                        }

                        // Поиск по имени метода
                        if (!isStringLiteral && regex.IsMatch(method.Name))
                        {
                            var methodStart = -1;
                            for (int i = 0; i < lines.Count; i++)
                            {
                                if (lines[i].Contains(method.Name) && 
                                    (lines[i].Contains("public") || lines[i].Contains("private") || lines[i].Contains("protected")))
                                {
                                    methodStart = i;
                                    break;
                                }
                            }

                            if (methodStart != -1)
                            {
                                var context = ExtractContextAroundMatch(methodBody, method.Name, methodStart + 1);
                                if (!string.IsNullOrEmpty(context))
                                {
                                    searchResult.Locations.Add(new CodeLocation
                                    {
                                        Type = type.FullName,
                                        Member = method.Name,
                                        MemberType = "Method",
                                        Context = context,
                                        LineNumber = methodStart + 1
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                if (searchResult.Locations.Any())
                {
                    results.Add(searchResult);
                }
            }

            return results;
        }

        private int GetLineNumber(string code, string searchText)
        {
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(searchText))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private List<(string Text, int LineNumber)> ExtractStringLiterals(string code)
        {
            var results = new List<(string, int)>();
            var lines = code.Split('\n');
            var stringLiteralRegex = new Regex("\"([^\"\\\\]|\\\\.)*\"");

            for (int i = 0; i < lines.Length; i++)
            {
                var matches = stringLiteralRegex.Matches(lines[i]);
                foreach (Match match in matches)
                {
                    // Убираем кавычки и экранирование
                    var literal = match.Value.Trim('"').Replace("\\\"", "\"");
                    results.Add((literal, i + 1));
                }
            }

            return results;
        }
    }
} 