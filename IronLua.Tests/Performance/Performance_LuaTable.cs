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
    class Performance_LuaTable
    {
        ScriptEngine engine;

        [TestFixtureSetUp]
        public void PrepareEngine()
        {
            engine = Lua.CreateEngine();
        }

        [Test]
        public void Performance_LuaTables_All()
        {
            Stopwatch stp = new Stopwatch();

            #region Global Library Access

            string code =
@"
for i = 0,math.tau,0.001 do
    assert(math.cos(i) == math.cos(-i))
    assert(math.sin(i) == -math.sin(-i))
end
";
            stp.Start();
            engine.Execute(code);
            stp.Stop();
            long firstRun = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            engine.Execute(code);
            stp.Stop();
            long onTheFly = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            var source = engine.Compile(code);
            stp.Stop();
            long compilation = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            source.Run();
            stp.Stop();
            long compiled = stp.ElapsedMilliseconds;
            stp.Reset();

            Console.WriteLine("GLOBAL LIBRARY ACCESS");
            Console.WriteLine("First Run: " + firstRun + "ms");
            Console.WriteLine("On The Fly: " + onTheFly + "ms");
            Console.WriteLine("Compilation: " + compilation + "ms");
            Console.WriteLine("Compiled: " + compiled + "ms");
            Console.WriteLine();

            #endregion

            #region Local Variable Access

            code = @"
local cos = math.cos
local sin = math.sin
local a = assert
local tau = math.tau

for i = 0, tau,0.001 do
    a(cos(i) == cos(-i))
    a(sin(i) == -sin(-i))
end
";

            stp.Start();
            engine.Execute(code);
            stp.Stop();
            firstRun = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            engine.Execute(code);
            stp.Stop();
            onTheFly = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            source = engine.Compile(code);
            stp.Stop();
            compilation = stp.ElapsedMilliseconds;
            stp.Reset();

            stp.Start();
            source.Run();
            stp.Stop();
            compiled = stp.ElapsedMilliseconds;
            stp.Reset();

            Console.WriteLine("LOCAL VARIABLE ACCESS");
            Console.WriteLine("First Run: " + firstRun + "ms");
            Console.WriteLine("On The Fly: " + onTheFly + "ms");
            Console.WriteLine("Compilation: " + compilation + "ms");
            Console.WriteLine("Compiled: " + compiled + "ms");
            Console.WriteLine();

            #endregion

            stp.Start();
            for (double i = 0; i < 2 * Math.PI; i += 0.001)
            {
                Assert.That(Math.Cos(i) == Math.Cos(-i));
                Assert.That(Math.Sin(i) == -Math.Sin(-i));
            }
            stp.Stop();
            long native = stp.ElapsedMilliseconds;
            stp.Reset();

            Console.WriteLine("NATIVE");
            Console.WriteLine("Local Variables: " + native + "ms");

            LuaTable mathlib = engine.Execute("return math");

            stp.Start();
            for (double i = 0; i < (double)mathlib.GetValue("tau"); i += 0.001)
            {
                Assert.That(((Func<double, double>)mathlib.GetValue("sin"))(i) == -((Func<double, double>)mathlib.GetValue("sin"))(-i));
                Assert.That(((Func<double, double>)mathlib.GetValue("cos"))(i) == ((Func<double, double>)mathlib.GetValue("cos"))(-i));
            }
            stp.Stop();
            long nativeGlobals = stp.ElapsedMilliseconds;
            stp.Reset();
            Console.WriteLine("Global Variables (LuaTable): " + nativeGlobals + "ms");
        }

        [Test(Description = "Tests dynamic (late bound) LuaTable bindings vs their static counterparts in performance")]
        public void Static_vs_Dynamic_Bindings()
        {
            dynamic mathlib = engine.Execute("return math");
            LuaTable mathlibS = (LuaTable)mathlib;

            Stopwatch stp = new Stopwatch();

            Console.WriteLine("Starting Static Access Tests");
            stp.Start();
            for (int i = 0; i < 50000; i++)            
                mathlibS.SetValue("sin", mathlibS.GetValue("sin"));
            
            stp.Stop();
            Console.WriteLine("Static Access: " + stp.ElapsedMilliseconds + "ms");
            stp.Reset();

            Console.WriteLine("Starting Dynamic Access Tests");
            stp.Start();
            for (int i = 0; i < 50000; i++)
                mathlib.sin = mathlib.sin;

            stp.Stop();
            Console.WriteLine("Dynamic Access: " + stp.ElapsedMilliseconds + "ms");


            Console.WriteLine("Starting Lua Access Tests");
            stp.Start();
            engine.Execute(
@"
    --local m = math
    for i=0,50000,1 do math.sin = math.sin end
");

            stp.Stop();
            Console.WriteLine("Lua Access: " + stp.ElapsedMilliseconds + "ms");


        }

        [Test(Description = "Tests the ideal case (identical keys being requested) vs. worst case (different keys being requested each time)")]
        public void Ideal_vs_Worst()
        {
            Stopwatch stp = new Stopwatch();
            stp.Start();
            engine.Execute(
@"
    for i = 0,10000,1 do math.sin(i) math.sin(i) end
");
            stp.Stop();
            Console.WriteLine("Ideal Case: " + stp.ElapsedMilliseconds + "ms");

            stp.Restart();
            engine.Execute(@"
    for i = 0,10000,1 do math.sin(i) math.cos(i) end
");
            stp.Stop();
            Console.WriteLine("Worst Case: " + stp.ElapsedMilliseconds + "ms");
        }
    }
}
