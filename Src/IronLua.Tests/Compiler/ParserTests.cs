using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IronLua.Compiler;
using IronLua.Compiler.Parsing;
using IronLua.Hosting;
using Microsoft.Scripting;
using NUnit.Framework;

namespace IronLua.Tests.Compiler
{
    //
    // These tests uses the "official" Lua Test Suite available at:
    //   http://www.lua.org/tests/5.2
    // compressed in these files:
    //   http://www.lua.org/tests/5.2/lua-5.2.0-tests.tar.gz
    //   http://www.inf.puc-rio.br/~roberto/lua/lua5.1-tests.tar.gz
    //
    // These tests were written for the Lua environment, but we use
    // them here to test the Lexer/Parser/Generator. Since we don't
    // execute them here, the test files are only an indication that
    // the compiler can work on the files without throwing syntax
    // errors. They don't test the error cases and error messages.
    //
    // Usage:
    // - Download the files described above
    // - Unpack them to a folder.
    // - Update the two paths in the LuaTestSuiteSource class (see below)
    // - Compile and execute the test in NUnit or RSharper.
    //
    [TestFixture]
    public class ParserTests
    {
        public void ParserErrorReportTests(string luaFile, bool useLua52, bool useLua53 = false)
        {
            var options = new LuaCompilerOptions()
            {
                SkipFirstLine = true,
                UseLua52Features = useLua52,
            };

            var engine = Lua.CreateEngine();
            var context = Lua.GetLuaContext(engine);
            var sourceUnit = context.CreateFileUnit(luaFile);

            //var reader = TestUtils.OpenReaderOrIgnoreTest(() => File.OpenText(luaFile));
            var reader = TestUtils.OpenReaderOrIgnoreTest(sourceUnit.GetReader);

            var tokenizer = new Tokenizer(ErrorSink.Default, options);
            tokenizer.Initialize(null, reader, sourceUnit, SourceLocation.MinValue);
            var parser = new Parser(tokenizer, tokenizer.ErrorSink, options);

            TestUtils.AssertSyntaxError(() =>
            {
                var ast = parser.Parse();
                Assert.That(ast, Is.Not.Null);
            });
        }

        [TestCase("assert((1 << (numbits - 1)) == math.mininteger)")]
        [TestCase("assert(bit32.arshift(0x12345678, 1) == 0x12345678 // 2)")]
        public void SimpleCodeSnippetTest(string code)
        {
            var options = new LuaCompilerOptions()
            {
                SkipFirstLine = true,
                UseLua52Features = true,
                UseLua53Features = true
            };

            var engine = Lua.CreateEngine();
            var context = Lua.GetLuaContext(engine);
            var sourceUnit = context.CreateSnippet(code, SourceCodeKind.SingleStatement);
            var reader = sourceUnit.GetReader();

            var tokenizer = new Tokenizer(ErrorSink.Default, options);
            tokenizer.Initialize(null, reader, sourceUnit, SourceLocation.MinValue);
            var parser = new Parser(tokenizer, tokenizer.ErrorSink, options);

            TestUtils.AssertSyntaxError(() =>
            {
                var ast = parser.Parse();
                Assert.That(ast, Is.Not.Null);
            });
        }

        [Test, TestCaseSource(typeof(LuaTestSuiteSource), nameof(LuaTestSuiteSource.Lua53TestCases))]
        public void ParserTestOnLua53TestSuite(string luaFile)
        {
            ParserErrorReportTests(luaFile, useLua52:true, useLua53:true);
        }

        [Test, TestCaseSource(typeof(LuaTestSuiteSource), nameof(LuaTestSuiteSource.Lua52TestCases))]
        public void ParserTestOnLua52TestSuite(string luaFile)
        {
            ParserErrorReportTests(luaFile, useLua52:true);
        }

        [Test, TestCaseSource(typeof(LuaTestSuiteSource), nameof(LuaTestSuiteSource.Lua51TestCases))]
        public void ParserTestOnLua51TestSuite(string luaFile)
        {
            ParserErrorReportTests(luaFile, useLua52:false);
        }

        public static class LuaTestSuiteSource
        {
            public static readonly string Lua53TestSuitePath = TestUtils.GetTestPath(@"IronLua.Tests\\Scripts\\lua-5.3.4-tests");
            public static readonly string Lua52TestSuitePath = TestUtils.GetTestPath(@"IronLua.Tests\\Scripts\\lua-5.2.2-tests");
            public static readonly string Lua51TestSuitePath = TestUtils.GetTestPath(@"IronLua.Tests\\Scripts\\lua-5.1-tests");

            public static string[] LuaTestSuiteFiles = new[]
            {
                "all.lua",
                "api.lua",
                "attrib.lua",
                "big.lua",
                "calls.lua",
                "checktable.lua", // not in Lua 5.3
                "closure.lua",
                "code.lua",
                "constructs.lua",
                "db.lua",
                "errors.lua",
                "events.lua",
                "files.lua",
                "gc.lua",
                "literals.lua",
                "locals.lua",
                "main.lua",
                "math.lua",
                "nextvar.lua",
                "pm.lua",
                "sort.lua",
                "strings.lua",
                "vararg.lua",
                "verybig.lua",
                // Lua 5.2, 5.3 specific files
                "bitwise.lua",
                "coroutine.lua",
                "goto.lua",
                // Lua 5.3 specific files
                //"tpack.lua",
                "utf8.lua"
            };

            public static IEnumerable<TestCaseData> LuaTestCases(string path)
            {
                return LuaTestSuiteFiles
                    .Select(f => new TestCaseData(Path.Combine(path, f)).SetName(f));
            }

            public static IEnumerable<TestCaseData> Lua53TestCases()
            {
                return LuaTestCases(Lua53TestSuitePath);
            }

            public static IEnumerable<TestCaseData> Lua52TestCases()
            {
                return LuaTestCases(Lua52TestSuitePath);
            }

            public static IEnumerable<TestCaseData> Lua51TestCases()
            {
                return LuaTestCases(Lua51TestSuitePath).Take(24);
            }
        }
    }
}
