using System;
using System.Diagnostics;
using IronLua.Hosting;
using System.Dynamic;
using System.Collections.Generic;
using System.Threading;
using IronLua;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var engine = Lua.CreateEngine();

            var context = engine.GetLuaContext();
            var scope = engine.CreateScope();

            string code =
@"
local function fib(n)
    if n <= 1 then
        do return n end
    else
        do return fib(n-1)+fib(n-2) end
    end
end

print('fib35',fib(35))
";

            engine.Execute(code);

            Console.WriteLine();

            WriteLine("Final Values:", ConsoleColor.Red);
            foreach (var entry in scope.GetVariableNames())
                Console.WriteLine("\t" + entry + ": " + context.FormatObject(scope.GetVariable(entry)));


            if (Debugger.IsAttached)
            {
                // Pause for debugger to see console output
                Console.WriteLine();
                Console.Write("Press ENTER to continue..");
                Console.ReadLine();
            }
        }

        static void WriteLine(string text, ConsoleColor color)
        {
            var temp = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = temp;
        }

    }
}
