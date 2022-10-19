namespace IngameCoding
{
    internal class Program
    {
        static readonly bool HandleErrors = false;

        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                RunFile(args[0]);
            }
            else
            {
#if true
                RunFile("D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test2.bbc");
#else
                ConsoleColor.WriteLine("Wrong number of arguments was passed!", System.ConsoleColor.Red);
#endif
            }
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        static void RunFile(string path)
        {
            if (File.Exists(path))
            {
                var code = File.ReadAllText(path);
                var codeInterpreter = new CodeInterpeter();
                Action<Bytecode.Stack.Item>? onInputDone = null;

                codeInterpreter.RunCode_BBCode(code, (message, logType) =>
                {
                    switch (logType)
                    {
                        case Terminal.TerminalInterpreter.LogType.Normal:
                            Console.WriteLine(message);
                            break;
                        case Terminal.TerminalInterpreter.LogType.Warning:
                            ConsoleColor.WriteLine(message, System.ConsoleColor.Yellow);
                            break;
                        case Terminal.TerminalInterpreter.LogType.Error:
                            ConsoleColor.WriteLine(message, System.ConsoleColor.Red);
                            break;
                        case Terminal.TerminalInterpreter.LogType.DebugError:
                            ConsoleColor.WriteLine(message, System.ConsoleColor.DarkRed);
                            break;
                        case Terminal.TerminalInterpreter.LogType.Debug:
                            ConsoleColor.WriteLine(message, System.ConsoleColor.DarkGray);
                            break;
                    }
                }, (success) =>
                {
                    if (success)
                    {
                        Console.WriteLine("Code executed");
                    }
                    else
                    {
                        ConsoleColor.WriteLine("Failed to execute the code", System.ConsoleColor.Red);
                    }
                }, (msg) =>
                {
                    Console.WriteLine(msg);
                    var input = Console.ReadLine();
                    onInputDone?.Invoke(new Bytecode.Stack.Item(msg, "Console Input"));
                }, out onInputDone, HandleErrors);

                var time = DateTime.Now;

                while (codeInterpreter.currentlyRunningCode)
                {
                    if (HandleErrors)
                    {
                        try
                        {
                            codeInterpreter.Update((float)(DateTime.Now - time).TotalMilliseconds);
                        }
                        catch (EndlessLoopException)
                        {
                            ConsoleColor.WriteLine($"Endless loop!!!", System.ConsoleColor.Red);
                        }
                        catch (ParserException error)
                        {
                            ConsoleColor.WriteLine($"ParserException: {error.MessageAll}", System.ConsoleColor.Red);
                        }
                        catch (InternalException error)
                        {
                            ConsoleColor.WriteLine($"InternalException: {error.Message}", System.ConsoleColor.Red);
                        }
                        catch (RuntimeException error)
                        {
                            ConsoleColor.WriteLine($"RuntimeException: {error.MessageAll}", System.ConsoleColor.Red);
                        }
                        catch (SyntaxException error)
                        {
                            ConsoleColor.WriteLine($"SyntaxException: {error.MessageAll}", System.ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        codeInterpreter.Update((float)(DateTime.Now - time).TotalMilliseconds);
                    }
                    time = DateTime.Now;
                }
            }
            else
            {
                ConsoleColor.WriteLine("File does not exists!", System.ConsoleColor.Red);
            }
        }
    }
}