# DotNetReflectCLI

A CLI utility for analyzing and working with decompiled .NET code.

## Features

- Decompile .NET assemblies to C#
- Analyze decompiled code
- Search through code and IL instructions
- Find type and method usages
- Support for searching in both individual files and directories
- Smart context-aware search results

## Installation

```bash
git clone https://github.com/yourusername/DotNetReflectCLI.git
cd DotNetReflectCLI
dotnet build
```

## Usage

### Code Search

```bash
# Search in a file
dotnet run search --input path/to/Assembly.dll --string "SearchText"

# Search in a directory
dotnet run search --input path/to/Managed --string "SearchText"

# Search with namespace filtering
dotnet run search --input path/to/Assembly.dll --string "SearchText" --namespace "MyNamespace"
```

### Decompilation

```bash
# Decompile entire assembly
dotnet run decompile --input Assembly.dll --output ./output

# Decompile specific type
dotnet run decompile-type --input Assembly.dll --type "MyNamespace.MyClass"

# Decompile method
dotnet run decompile-method --input Assembly.dll --type "MyNamespace.MyClass" --method "MyMethod"
```

### Usage Analysis

```bash
# Find type usages
dotnet run analyze --input Assembly.dll --type "MyNamespace.MyClass"

# Find method calls
dotnet run analyze-method --input Assembly.dll --method "MyNamespace.MyClass.MyMethod"
```

## Search Results Format

The search results are displayed in a tree-like structure:

```
Type: MyNamespace.MyClass
  Method: MyMethod
  Line: 42
  Context:
    ...
    public void Initialize()
    >>> MySearchedText("param1", param2);
    AnotherMethod();
    ...
```

Where:
- `>>>` indicates the exact line where the match was found
- `...` indicates omitted code
- Context shows 2 lines before and after the match

## Requirements

- .NET 8.0 SDK
- ICSharpCode.Decompiler
- System.CommandLine

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details. 