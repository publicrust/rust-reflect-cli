# DotNetReflectCLI

CLI-утилита для анализа и работы с декомпилированным .NET-кодом.

## Возможности

- Декомпиляция .NET сборок в C#
- Анализ декомпилированного кода
- Поиск по коду и IL-инструкциям
- Поиск использований типов и методов
- Поддержка поиска как в отдельных файлах, так и в директориях

## Установка

```bash
git clone https://github.com/yourusername/DotNetReflectCLI.git
cd DotNetReflectCLI
dotnet build
```

## Использование

### Поиск в коде

```bash
# Поиск в файле
dotnet run search --input path/to/Assembly.dll --string "SearchText"

# Поиск в директории
dotnet run search --input path/to/Managed --string "SearchText"

# Поиск с фильтрацией по namespace
dotnet run search --input path/to/Assembly.dll --string "SearchText" --namespace "MyNamespace"
```

### Декомпиляция

```bash
# Декомпилировать всю сборку
dotnet run decompile --input Assembly.dll --output ./output

# Декомпилировать конкретный тип
dotnet run decompile-type --input Assembly.dll --type "MyNamespace.MyClass"

# Декомпилировать метод
dotnet run decompile-method --input Assembly.dll --type "MyNamespace.MyClass" --method "MyMethod"
```

### Анализ использований

```bash
# Найти использования типа
dotnet run analyze --input Assembly.dll --type "MyNamespace.MyClass"

# Найти вызовы метода
dotnet run analyze-method --input Assembly.dll --method "MyNamespace.MyClass.MyMethod"
```

## Требования

- .NET 8.0 SDK
- ICSharpCode.Decompiler
- System.CommandLine 