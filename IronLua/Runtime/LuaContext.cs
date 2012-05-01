using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using IronLua.Compiler;
using IronLua.Compiler.Parsing;
using IronLua.Hosting;
using IronLua.Library;
using IronLua.Runtime.Binder;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronLua.Runtime
{
    public sealed class LuaContext : LanguageContext
    {
        private readonly DynamicCache _dynamicCache;
        private readonly LuaTable _globals;

        public LuaContext(ScriptDomainManager manager, IDictionary<string, object> options = null)
            : base(manager)
        {
            // TODO: options

            _binder = new LuaBinder(this);
            _dynamicCache = new DynamicCache(this);
            _globals = SetupLibraries(new LuaTable());
            _metatables = SetupMetatables();
        }

        internal LuaTable Globals
        {
            get { return _globals; }
        }

        internal DynamicCache DynamicCache
        {
            get { return _dynamicCache; }
        }

        #region Metatable management

        readonly Dictionary<Type, LuaTable> _metatables;

        Dictionary<Type, LuaTable> SetupMetatables()
        {
            return new Dictionary<Type, LuaTable>()
            {
                {typeof(bool), new LuaTable()},
                {typeof(double), new LuaTable()},
                {typeof(string), new LuaTable()},
                {typeof(Delegate), new LuaTable()},
            };
        }

        internal LuaTable GetTypeMetatable(object obj)
        {
            if (obj == null)
                return null;

            LuaTable table;
            if (_metatables.TryGetValue(obj.GetType(), out table))
                return table;

            throw new ArgumentOutOfRangeException("obj", "Argument is of non-supported type");
        }

        #endregion

        public override ScriptCode CompileSourceCode(SourceUnit sourceUnit, CompilerOptions options, ErrorSink errorSink)
        {
            ContractUtils.RequiresNotNull(sourceUnit, "sourceUnit");
            ContractUtils.RequiresNotNull(options, "options");
            ContractUtils.RequiresNotNull(errorSink, "errorSink");
            ContractUtils.Requires(sourceUnit.LanguageContext == this, "Language mismatch.");

            //Console.WriteLine("This is where we 'compile' the source code");

            var luaOptions = options as LuaCompilerOptions;
            if (luaOptions == null)
                throw new ArgumentException("Compiler context required", "options");
            
            SourceCodeReader reader;
            try
            {
                reader = sourceUnit.GetReader();
            }
            catch (IOException ex)
            {
                errorSink.Add(sourceUnit, ex.Message, SourceSpan.Invalid, 0, Severity.Error);
                throw;
            }

            using (reader)
            {
#if false
                var source = reader.ReadToEnd();
                var input = new Input(source);
                var lexer = new Lexer(input);
#else
                var lexer = new Tokenizer(errorSink, luaOptions);
                lexer.Initialize(null, reader, sourceUnit, SourceLocation.MinValue);
#endif
                var parser = new Parser(lexer, errorSink);
                var ast = parser.Parse();
                var gen = new Generator(this);
                var expr = gen.Compile(ast);
                var lamda = expr.Compile();

                //sourceUnit.CodeProperties = ScriptCodeParseResult.Complete;
                return new LuaScriptCode(sourceUnit, lamda);
            }
        }

        #region Lua Information

        private static readonly Guid LuaLanguageGuid = new Guid("03ed4b80-d10b-442f-ad9a-47dae85b2051");

        private static readonly Lazy<Version> LuaLanguageVersion = new Lazy<Version>(GetLuaVersion);

        public override Guid LanguageGuid
        {
            get { return LuaLanguageGuid; }
        }

        public override Version LanguageVersion
        {
            get { return LuaLanguageVersion.Value; }
        }

        internal static Version GetLuaVersion()
        {
            return new AssemblyName(typeof(LuaContext).Assembly.FullName).Version;
        }

        #endregion

        Lazy<LuaCompilerOptions> _compilerOptions = 
            new Lazy<LuaCompilerOptions>(() => new LuaCompilerOptions());

        public override CompilerOptions GetCompilerOptions()
        {
            return _compilerOptions.Value;
        }

        public override TService GetService<TService>(params object[] args)            
        {
            if (typeof(TService) == typeof(TokenizerService))
            {
                return (TService)(object)new Tokenizer(ErrorSink.Null, (LuaCompilerOptions)GetCompilerOptions());
            }
            else if (typeof(TService) == typeof(LuaService))
            {
                return (TService)(object)GetLuaService((ScriptEngine)args[0]);
            }
            else
            {
                return base.GetService<TService>(args);
            }
        }

        #region LuaService

        LuaService _luaService;

        internal LuaService GetLuaService(ScriptEngine engine)
        {
            if (_luaService == null)
            {
                var service = new LuaService(this, engine);
                Interlocked.CompareExchange(ref _luaService, service, null);
            }
            return _luaService;
        }

        #endregion

        readonly LuaBinder _binder;
        internal LuaBinder Binder
        {
            get { return _binder; }
        }

        public override string FormatObject(DynamicOperations operations, object obj)
        {
            if (obj == null) 
                return "nil";

            if (obj is bool)
                return (bool) obj ? "true" : "false";

            if (obj is LuaTable)
                return "table: 00000000";

            if (obj is Delegate)
                return "function: 00000000";
            
            return obj.ToString();
        }

        #region Lua base library management

        LuaTable SetupLibraries(LuaTable globals)
        {
            ContractUtils.RequiresNotNull(globals, "globals");

            BaseLibrary = new BaseLibrary(this);
            BaseLibrary.Setup(globals);

            PackageLibrary = new PackageLibrary(this);
            var packagelibTable = new LuaTable();
            PackageLibrary.Setup(packagelibTable);
            globals.SetValue("package", packagelibTable);

            //TableLibrary = new TableLibrary();
            var tablelibTable = new LuaTable();
            //TableLibrary.Setup(tablelibTable);
            globals.SetValue("table", tablelibTable);

            MathLibrary = new MathLibrary(this);
            var mathlibTable = new LuaTable();
            MathLibrary.Setup(mathlibTable);
            globals.SetValue("math", mathlibTable);

            StringLibrary = new StringLibrary(this);
            var strlibTable = new LuaTable();
            StringLibrary.Setup(strlibTable);
            globals.SetValue("string", strlibTable);

            //IoLibrary = new IoLibrary(this);
            var iolibTable = new LuaTable();
            //IoLibrary.Setup(iolibTable);
            globals.SetValue("io", iolibTable);

            OSLibrary = new OSLibrary(this);
            var oslibTable = new LuaTable();
            OSLibrary.Setup(oslibTable);
            globals.SetValue("os", oslibTable);

            //DebugLibrary = new DebugLibrary(this);
            var debuglibTable = new LuaTable();
            //DebugLibrary.Setup(debuglibTable);
            globals.SetValue("debug", debuglibTable);

            return globals;
        }

        internal BaseLibrary BaseLibrary;
        internal StringLibrary StringLibrary;
        internal MathLibrary MathLibrary;
        internal OSLibrary OSLibrary;
        internal PackageLibrary PackageLibrary;

        #endregion
    }
}
