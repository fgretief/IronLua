using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Diagnostics;
using Microsoft.Scripting.Hosting;
using IronLua.Hosting;
using IronLua.Runtime;

namespace IronLua.Tests.Performance
{
    [TestFixture]
    class Performance_Calls
    {
        ScriptEngine engine;

        [OneTimeSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }
        
        [Test(Description = "Tests performance of function calls vs. their native counterparts")]
        public void Static_vs_Dynamic_Bindings()
        {
            dynamic mathlib = engine.Execute("return math");
            LuaTable mathlibS = (LuaTable)mathlib;
            dynamic sin = engine.Execute("return math.sin");

            Stopwatch stp = new Stopwatch();

            Console.WriteLine("Starting Static Access Tests (Global Table Access)");
            stp.Start();
            for (int i = 0; i < 10000; i++)            
                ((Func<double,double>)mathlibS.GetValue("sin"))(i);
            
            stp.Stop();
            Console.WriteLine("Static Access: " + stp.ElapsedMilliseconds + "ms");
            stp.Reset();

            Console.WriteLine("Starting Dynamic Access Tests");
            stp.Start();
            for (int i = 0; i < 10000; i++)
                mathlib.sin = mathlib.sin;

            stp.Stop();
            Console.WriteLine("Dynamic Access: " + stp.ElapsedMilliseconds + "ms");


        }

        [Test]
        public void Recursion_Fibonacci()
        {
            Stopwatch stp = new Stopwatch();
            stp.Start();
            engine.Execute(
@"
    local function fib(n) if n <= 1 then return n else return fib(n - 1) + fib(n - 2) end end
    for i = 0,30 do print('Fibonnaci from',i,'=',fib(i)) end
");
            stp.Stop();
            Console.WriteLine("Local Function: " + stp.ElapsedMilliseconds + "ms");

            stp.Restart();
            engine.Execute(
@"
    function fib(n) if n <= 1 then return n else return fib(n - 1) + fib(n - 2) end end
    for i = 0,30 do print('Fibonnaci from',i,'=',fib(i)) end
");
            stp.Stop();
            Console.WriteLine("Global Function: " + stp.ElapsedMilliseconds + "ms");
        }
    }
}
