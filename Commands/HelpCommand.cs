using System.CommandLine;
using System.Text;

namespace DotNetReflectCLI.Commands
{
    public class HelpCommand : Command
    {
        public HelpCommand() : base("help", "Показать подробную справку по использованию инструмента")
        {
            var commandOption = new Option<string>(
                "--command",
                "Название команды для получения подробной справки"
            );
            AddOption(commandOption);

            this.SetHandler((string? command) =>
            {
                if (string.IsNullOrEmpty(command))
                {
                    ShowGeneralHelp();
                }
                else
                {
                    ShowCommandHelp(command);
                }
            }, commandOption);
        }

        private void ShowGeneralHelp()
        {
            var help = new StringBuilder();
            help.AppendLine("rust-reflect-cli - Утилита для анализа и работы с декомпилированным Rust .NET кодом\n");
            help.AppendLine("Использование:");
            help.AppendLine("  rust-reflect <команда> [опции]\n");
            help.AppendLine("Доступные команды:");
            help.AppendLine("  search         Поиск в коде сборки");
            help.AppendLine("  decompile      Декомпиляция всей сборки");
            help.AppendLine("  decompile-type Декомпиляция конкретного типа");
            help.AppendLine("  analyze        Анализ использования типов");
            help.AppendLine("  help           Показать эту справку\n");
            help.AppendLine("Для получения подробной справки по команде используйте:");
            help.AppendLine("  rust-reflect help --command <название_команды>\n");
            help.AppendLine("Примеры:");
            help.AppendLine("  rust-reflect search --input Assembly.dll --string \"SearchText\"");
            help.AppendLine("  rust-reflect decompile --input Assembly.dll --output ./output");
            help.AppendLine("  rust-reflect analyze --input Assembly.dll --type \"MyNamespace.MyClass\"");

            Console.WriteLine(help.ToString());
        }

        private void ShowCommandHelp(string command)
        {
            var help = new StringBuilder();
            
            switch (command.ToLower())
            {
                case "search":
                    help.AppendLine("Поиск в коде сборки\n");
                    help.AppendLine("Использование:");
                    help.AppendLine("  rust-reflect search [опции]\n");
                    help.AppendLine("Опции:");
                    help.AppendLine("  --input <путь>      Путь к файлу сборки или директории со сборками (обязательно)");
                    help.AppendLine("  --string <текст>    Текст для поиска (обязательно)");
                    help.AppendLine("  --namespace <ns>    Фильтр по пространству имён (опционально)\n");
                    help.AppendLine("Примеры:");
                    help.AppendLine("  rust-reflect search --input Assembly.dll --string \"MyMethod\"");
                    help.AppendLine("  rust-reflect search --input ./Managed --string \"OnPlayerConnected\"");
                    help.AppendLine("  rust-reflect search --input Assembly.dll --string \"MyClass\" --namespace \"MyNamespace\"");
                    break;

                case "decompile":
                    help.AppendLine("Декомпиляция всей сборки\n");
                    help.AppendLine("Использование:");
                    help.AppendLine("  rust-reflect decompile [опции]\n");
                    help.AppendLine("Опции:");
                    help.AppendLine("  --input <путь>   Путь к файлу сборки (обязательно)");
                    help.AppendLine("  --output <путь>  Путь для сохранения декомпилированного кода (обязательно)\n");
                    help.AppendLine("Примеры:");
                    help.AppendLine("  rust-reflect decompile --input Assembly.dll --output ./output");
                    break;

                case "decompile-type":
                    help.AppendLine("Декомпиляция конкретного типа\n");
                    help.AppendLine("Использование:");
                    help.AppendLine("  rust-reflect decompile-type [опции]\n");
                    help.AppendLine("Опции:");
                    help.AppendLine("  --input <путь>  Путь к файлу сборки (обязательно)");
                    help.AppendLine("  --type <тип>    Полное имя типа для декомпиляции (обязательно)\n");
                    help.AppendLine("Примеры:");
                    help.AppendLine("  rust-reflect decompile-type --input Assembly.dll --type \"MyNamespace.MyClass\"");
                    break;

                case "analyze":
                    help.AppendLine("Анализ использования типов\n");
                    help.AppendLine("Использование:");
                    help.AppendLine("  rust-reflect analyze [опции]\n");
                    help.AppendLine("Опции:");
                    help.AppendLine("  --input <путь>  Путь к файлу сборки (обязательно)");
                    help.AppendLine("  --type <тип>    Полное имя типа для анализа (обязательно)\n");
                    help.AppendLine("Примеры:");
                    help.AppendLine("  rust-reflect analyze --input Assembly.dll --type \"MyNamespace.MyClass\"");
                    break;

                default:
                    help.AppendLine($"Неизвестная команда: {command}");
                    help.AppendLine("Используйте 'rust-reflect help' для просмотра списка доступных команд.");
                    break;
            }

            Console.WriteLine(help.ToString());
        }
    }
} 