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
            var codeLines = code.Split('\n');
            if (lineNumber >= codeLines.Length) return "";

            var contextLines = new List<string>();
            var targetLine = codeLines[lineNumber].Trim();

            // Take 2 lines before and after the match
            var contextStart = Math.Max(0, lineNumber - 2);
            var contextEnd = Math.Min(codeLines.Length - 1, lineNumber + 2);

            // Add marker for start of fragment if not at file start
            if (contextStart > 0)
                contextLines.Add("...");

            for (int i = contextStart; i <= contextEnd; i++)
            {
                var line = codeLines[i].Trim();
                if (i == lineNumber)
                {
                    contextLines.Add(">>> " + line); // Mark the found line
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    contextLines.Add("    " + line);
                }
            }

            // Add marker for end of fragment if not at file end
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

            if (!results.Any())
            {
                Console.WriteLine($"No usages of type '{typeName}' found in the specified path.");
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

            if (!results.Any())
            {
                Console.WriteLine($"No usages of method '{typeName}.{methodName}' found in the specified path.");
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

            if (!results.Any())
            {
                Console.WriteLine($"No matches found for '{searchText}' in the specified path.");
            }

            return results.Values;
        }

        public async Task<List<SearchResult>> SearchInCode(string assemblyPath, string pattern)
        {
            var decompiler = _decompilationService.CreateDecompiler(assemblyPath);
            var results = new List<SearchResult>();
            var isStringLiteral = pattern.StartsWith("\"") && pattern.EndsWith("\"");

            // Если это не поиск строкового литерала, удаляем кавычки (если они есть)
            var searchText = isStringLiteral ? pattern.Trim('"') : pattern.Trim('"', '\'');
            var regex = new Regex(isStringLiteral ? Regex.Escape(searchText) : pattern.Replace("*", ".*"), RegexOptions.IgnoreCase);

            foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions)
            {
                var searchResult = new SearchResult { Type = type.FullName };

                // Поиск строковых литералов (всегда для поиска без кавычек, и только для поиска с кавычками)
                if (!isStringLiteral || isStringLiteral)
                {
                    foreach (var method in type.Methods)
                    {
                        try
                        {
                            var methodBody = decompiler.DecompileAsString(method.MetadataToken);
                            var lines = methodBody.Split('\n');
                            for (int i = 0; i < lines.Length; i++)
                            {
                                var line = lines[i];
                                // Ищем строки вида: "текст" или @"текст" или $"текст"
                                var matches = Regex.Matches(line, "(?:@|\"|\\$\")(?:[^\"\\\\]|\\\\.)*\"");
                                foreach (Match match in matches)
                                {
                                    var literal = match.Value;
                                    // Убираем префиксы (@, $) и кавычки
                                    literal = literal.TrimStart('@', '$').Trim('"');
                                    // Раскодируем экранированные символы
                                    literal = Regex.Unescape(literal);
                                    
                                    if (regex.IsMatch(literal))
                                    {
                                        searchResult.Locations.Add(new CodeLocation
                                        {
                                            Type = type.FullName,
                                            Member = method.Name,
                                            MemberType = "StringLiteral",
                                            Context = line.Trim(),
                                            LineNumber = i + 1
                                        });
                                        break; // Один метод - одно совпадение
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Пропускаем методы, которые не удалось декомпилировать
                            continue;
                        }
                    }
                }

                // Поиск по именам типов, методов, свойств и полей (только для поиска без кавычек)
                if (!isStringLiteral)
                {
                    if (regex.IsMatch(type.Name))
                    {
                        searchResult.Locations.Add(new CodeLocation
                        {
                            Type = type.FullName,
                            Member = type.Name,
                            MemberType = "Type",
                            Context = $"Type name matches pattern: {type.Name}"
                        });
                    }

                    foreach (var method in type.Methods)
                    {
                        if (regex.IsMatch(method.Name))
                        {
                            var methodBody = decompiler.DecompileAsString(method.MetadataToken);
                            searchResult.Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = method.Name,
                                MemberType = "Method",
                                Context = ExtractContextAroundMatch(methodBody, method.Name, GetLineNumber(methodBody, method.Name)),
                                LineNumber = GetLineNumber(methodBody, method.Name)
                            });
                        }
                    }

                    foreach (var property in type.Properties)
                    {
                        if (regex.IsMatch(property.Name))
                        {
                            searchResult.Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = property.Name,
                                MemberType = "Property",
                                Context = $"Property name matches pattern: {property.Name}"
                            });
                        }
                    }

                    foreach (var field in type.Fields)
                    {
                        if (regex.IsMatch(field.Name))
                        {
                            searchResult.Locations.Add(new CodeLocation
                            {
                                Type = type.FullName,
                                Member = field.Name,
                                MemberType = "Field",
                                Context = $"Field name matches pattern: {field.Name}"
                            });
                        }
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