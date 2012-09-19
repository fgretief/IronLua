using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
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
using Microsoft.Scripting.Actions;
using System.Linq;
using Microsoft.Scripting.Debugging.CompilerServices;
using Debugging = Microsoft.Scripting.Debugging;

namespace IronLua.Runtime
{
    public sealed class LuaContext : LanguageContext
    {
        private readonly DynamicCache _dynamicCache;
        
        public LuaContext(ScriptDomainManager manager, IDictionary<string, object> options = null)
            : base(manager)
        {
            // TODO: options
            if (options.ContainsKey("tracing"))
                EnableTracing = options["tracing"] is bool && (bool)options["tracing"];
            
            _binder = new LuaBinder(this);
            _dynamicCache = new DynamicCache(this);
            SetupLibraries();
        }

        internal DynamicCache DynamicCache
        {
            get { return _dynamicCache; }
        }


        #region Scope Operations Support

        public override bool ScopeTryGetVariable(Scope scope, string name, out dynamic value)
        {
            if (scope.Storage is LuaTable)
            {
                dynamic storage = scope.Storage;
                value = storage[name];
                return (object)value != null;
            }
            
            return base.ScopeTryGetVariable(scope, name, out value);
        }

        public override dynamic ScopeGetVariable(Scope scope, string name)
        {
            if (scope.Storage is LuaTable)
            {
                dynamic storage = scope.Storage;
                return storage[name];
            }
            return base.ScopeGetVariable(scope, name);
        }
        
        public override T ScopeGetVariable<T>(Scope scope, string name)
        {
            if (scope.Storage is LuaTable)
            {
                dynamic storage = scope.Storage;
                return storage[name];
            }
            return base.ScopeGetVariable<T>(scope, name);
        }

        public override void ScopeSetVariable(Scope scope, string name, object value)
        {
            if (scope.Storage is LuaTable)
            {
                dynamic storage = scope.Storage;
                storage[name] = value;
                return;
            }

            base.ScopeSetVariable(scope, name, value);
        }

        /// <inheritdoc/>
        public override Scope CreateScope()
        {
            var table = new LuaTable();            
            var scope = new Scope(table as IDynamicMetaObjectProvider);

            return scope;
        }

        #endregion

        #region Public API

        private static object ToLuaObject(object obj)
        {
            if (obj is Delegate)
                return obj;

            if (obj is double)
                return obj;

            if (obj is string)
                return obj;

            double d_temp = 0;
            if (double.TryParse(obj.ToString(), out d_temp))
                return d_temp;

            return obj;
        }

        #endregion

        #region Trace/Debug
        
        private Debugging.CompilerServices.DebugContext _debugContext;
        private Debugging.ITracePipeline _tracePipeline;

        //[ThreadStatic]
        //private static Stack<LuaTracebackListener> _tracebackListeners;
        //private static int _tracingThreads;

        //internal Debugging.CompilerServices.DebugContext DebugContext
        //{
        //    get
        //    {
        //        EnsureDebugContext();

        //        return _debugContext;
        //    }
        //}

        //internal void EnsureDebugContext()
        //{
        //    if (_debugContext == null || _tracePipeline == null)
        //    {
        //        lock (this)
        //        {
        //            if (_debugContext == null)
        //            {
        //                _debugContext = Debugging.CompilerServices.DebugContext.CreateInstance();
        //                _tracePipeline = Debugging.TracePipeline.CreateInstance(_debugContext);
        //            }
        //        }
        //    }

        //    if (_tracebackListeners == null)
        //    {
        //        _tracebackListeners = new Stack<LuaTracebackListener>();
        //        // push the default listener
        //        _tracebackListeners.Push(new LuaTracebackListener(this));
        //    }
        //}

        //internal Debugging.ITracePipeline TracePipeline
        //{
        //    get
        //    {
        //        return _tracePipeline;
        //    }
        //}

        public bool EnableTracing
        { get; set; }

        #endregion

        #region Interop Storage

        //Stores LuaTables which represent namespace/Type paths. For example:
        //System.Collections.Generic.Dictionary<TKey,TValue> would have
        //System                                    - LuaTable
        //System.Collections                        - LuaTable
        //System.Collections.Generic                - LuaTable
        //System.Collections.Generic.Dictionary<..> - LuaTable (CLR type wrapper)
        private readonly Dictionary<string, LuaTable> _clrNamespaces = new Dictionary<string, LuaTable>();

        internal LuaTable GetCLRNamespace(string @namespace)
        {
            if (_clrNamespaces.ContainsKey(@namespace))
                return _clrNamespaces[@namespace];

            var typeTable = InteropLibrary.GetTypeTable(Type.GetType(@namespace, false));
            if(typeTable != null)
                _clrNamespaces.Add(@namespace, typeTable);
            return typeTable;
        }

        #endregion

        #region Object Operations Support

        // These methods is called by the DynamicOperations class that can be
        // retrieved via the inherited Operations property of this class.

        public override UnaryOperationBinder CreateUnaryOperationBinder(ExpressionType operation)
        {
            return DynamicCache.GetUnaryOperationBinder(operation);
        }

        public override BinaryOperationBinder CreateBinaryOperationBinder(ExpressionType operation)
        {
            return DynamicCache.GetBinaryOperationBinder(operation);
        }

        public override ConvertBinder CreateConvertBinder(Type toType, bool? explicitCast)
        {
            ContractUtils.Requires(explicitCast == false, "explicitCast");
            return DynamicCache.GetConvertBinder(toType);
        }

        public override GetMemberBinder CreateGetMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return base.CreateGetMemberBinder(name, ignoreCase);

            return DynamicCache.GetGetMemberBinder(name);
        }

        public override SetMemberBinder CreateSetMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return base.CreateSetMemberBinder(name, ignoreCase);

            return DynamicCache.GetSetMemberBinder(name);
        }

        public override DeleteMemberBinder CreateDeleteMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return base.CreateDeleteMemberBinder(name, ignoreCase);

            // TODO: not implemented yet
            return base.CreateDeleteMemberBinder(name, ignoreCase);
        }

        public GetIndexBinder CreateGetIndexBinder(CallInfo callInfo)
        {
            return DynamicCache.GetGetIndexBinder();//callInfo);
        }

        public SetIndexBinder CreateSetIndexBinder(CallInfo callInfo)
        {
            return DynamicCache.GetSetIndexBinder();//callInfo);
        }

        public DeleteIndexBinder CreateDeleteIndexBinder()
        {
            throw new NotImplementedException();
        }

        public override InvokeMemberBinder CreateCallBinder(string name, bool ignoreCase, CallInfo callInfo)
        {
            ContractUtils.Requires(ignoreCase == false, "ignoreCase");
            return DynamicCache.GetInvokeMemberBinder(name, callInfo);
        }

        public override InvokeBinder CreateInvokeBinder(CallInfo callInfo)
        {
            return DynamicCache.GetInvokeBinder(callInfo);
        }

        public override CreateInstanceBinder CreateCreateBinder(CallInfo callInfo)
        {
            // TODO: not implemented yet
            return base.CreateCreateBinder(callInfo);
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
                var lexer = new Tokenizer(errorSink, luaOptions);
                lexer.Initialize(null, reader, sourceUnit, SourceLocation.MinValue);

                var parser = new Parser(lexer, errorSink);
                var ast = parser.Parse();

                var codeContext = new CodeContext(this);

                var gen = new Generator(codeContext);
                var exprLambda = gen.Compile(ast, sourceUnit);
                //sourceUnit.CodeProperties = ScriptCodeParseResult.Complete;
                return new LuaScriptCode(this, sourceUnit, exprLambda);
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

        /// <summary>
        /// Attempts to retrieve a string representation of the given object
        /// </summary>
        /// <param name="obj">The object to retrieve the string value for</param>
        /// <returns>Returns a string representing the object</returns>
        public string FormatObject(object obj)
        {
            return FormatObject(null, obj);
        }

        public override string FormatObject(DynamicOperations operations, object obj)
        {
            return BaseLibrary.ToStringEx(obj);
        }
        
        #region Lua base library management

        private readonly Dictionary<string, Func<CodeContext, Library.Library>> BaseLibraries = new Dictionary<string, Func<CodeContext,Library.Library>>();

        internal bool IsBaseLibrary(string name)
        {
            return BaseLibraries.Any(x => x.Key == name);
        }

        internal Library.Library GetLibraryInstance(string name, CodeContext context)
        {
            if (BaseLibraries.ContainsKey(name))
                return BaseLibraries[name](context);

            throw new LuaRuntimeException(context, "library not found '{0}'", name);
        }

        void SetupLibraries()
        {
            BaseLibraries.Add("clr", x => new InteropLibrary(x));
            BaseLibraries.Add("debug", x => new DebugLibrary(x));
            //TODO: IO Library
            BaseLibraries.Add("math", x => new BaseLibrary(x));
            BaseLibraries.Add("os", x => new OSLibrary(x));
            BaseLibraries.Add("package", x => new PackageLibrary(x));
            BaseLibraries.Add("string", x => new StringLibrary(x));
            //TODO: Table Library
        }

        #endregion
    }
}
