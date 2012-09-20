using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Actions;
using IronLua.Compiler;
using Microsoft.Scripting.Utils;
using System.Linq.Expressions;
using Microsoft.Scripting;
using IronLua.Library;
using IronLua.Runtime.Binder;
using System.Runtime.CompilerServices;

namespace IronLua.Runtime
{
    internal sealed partial class CodeContext
    {
        public CodeContext(LuaContext language)
        {
            Language = language;
            _metatables = SetupMetatables();
            _BaseLibrary = new BaseLibrary(this);


            _binder = new LuaBinder(this);
            _dynamicCache = new DynamicCache(this);
        }

        #region CodeContext Properties and Shortcuts

        /// <summary>
        /// Gets the language context which is handling this code context
        /// </summary>
        public LuaContext Language
        { get; private set; }

        /// <summary>
        /// Gets the SourceUnit from which this CodeContext was generated
        /// </summary>
        public SourceUnit Source
        { get; private set; }

        /// <summary>
        /// Gets the currently executing scope (if one is executing, otherwise null)
        /// </summary>
        public Scope ExecutingScope
        { get; private set; }

        /// <summary>
        /// Gets the currently executing scope's storage (if one is executing, otherwise null)
        /// </summary>
        public IDynamicMetaObjectProvider ExecutingScopeStorage
        { get; private set; }

        /// <summary>
        /// Gets the scope containing the engine's currently defined global values
        /// </summary>
        public Scope EngineGlobals
        { get { return Language.DomainManager.Globals; } }

        /// <summary>
        /// Gets the <see cref="SharedIO"/> object used by this language's engine
        /// </summary>
        public SharedIO EngineIO
        { get { return Language.DomainManager.SharedIO; } }

        /// <summary>
        /// Gets the function which can be executed to run this CodeContext's compiled code
        /// </summary>
        public Func<IDynamicMetaObjectProvider, dynamic> Execute
        { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether tracing is enabled on an engine level
        /// </summary>
        public bool EnableTracing
        {
            get { return Language.EnableTracing; }
        }

        #endregion

        #region Binders
                
        private readonly DynamicCache _dynamicCache;

        internal DynamicCache DynamicCache
        {
            get { return _dynamicCache; }
        }


        readonly LuaBinder _binder;
        internal LuaBinder Binder
        {
            get { return _binder; }
        }


        #endregion

        #region Execution Environment

        //Stores Function => Environment mappings to support custom environments for function execution
        private readonly Dictionary<LuaScope, LuaTable> _EnvironmentMappings = new Dictionary<LuaScope, LuaTable>();
        
        /// <summary>
        /// Determines whether or not the given function has a custom execution environment
        /// set for it.
        /// </summary>
        public bool HasCustomEnvironment(LuaScope function)
        {
            return _EnvironmentMappings.ContainsKey(function);
        }

        /// <summary>
        /// Gets the custom execution environment for the given function, or null if no environment was set
        /// </summary>
        public LuaTable GetFunctionEnvironment(LuaScope function)
        {
            if (!HasCustomEnvironment(function))
                return null;
            return _EnvironmentMappings[function];
        }
        
        #endregion

        #region Metatable management

        readonly Dictionary<Type, LuaTable> _metatables;

        Dictionary<Type, LuaTable> SetupMetatables()
        {
            return new Dictionary<Type, LuaTable>()
            {
                {typeof(bool), new LuaTable(this)},
                {typeof(double), new LuaTable(this)},
                {typeof(string), new LuaTable(this)},
                {typeof(Delegate), new LuaTable(this)},
            };
        }

        internal LuaTable GetTypeMetatable(object obj)
        {
            if (obj == null)
                return null;

            LuaTable table;

            if (obj is BoundMemberTracker)
            {
                var tracker = obj as BoundMemberTracker;

                if (tracker.ObjectInstance is LuaTable)
                {
                    if ((tracker.ObjectInstance as LuaTable).Metatable != null)
                        return (tracker.ObjectInstance as LuaTable).Metatable;
                }

                if (_metatables.TryGetValue(tracker.ObjectInstance.GetType(), out table))
                    return table;
            }

            var objType = obj.GetType();

            if (_metatables.TryGetValue(objType, out table))
                return table;

            throw new LuaRuntimeException(this, "Could not find metatable for '{0}'", objType.FullName);
        }

        internal LuaTable SetTypeMetatable(Type type, LuaTable metatable)
        {
            if (type == null || metatable == null)
                return null;

            LuaTable table;
            if (_metatables.TryGetValue(type, out table))
                return table;

            _metatables.Add(type, metatable);
            return metatable;
        }

        #endregion


        #region Object Operations Support

        // These methods is called by the DynamicOperations class that can be
        // retrieved via the inherited Operations property of this class.

        public  UnaryOperationBinder CreateUnaryOperationBinder(ExpressionType operation)
        {
            return DynamicCache.GetUnaryOperationBinder(operation);
        }

        public BinaryOperationBinder CreateBinaryOperationBinder(ExpressionType operation)
        {
            return DynamicCache.GetBinaryOperationBinder(operation);
        }

        public ConvertBinder CreateConvertBinder(Type toType, bool? explicitCast)
        {
            ContractUtils.Requires(explicitCast == false, "explicitCast");
            return DynamicCache.GetConvertBinder(toType);
        }

        public GetMemberBinder CreateGetMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return Language.CreateGetMemberBinder(name, ignoreCase);

            return DynamicCache.GetGetMemberBinder(name);
        }

        public SetMemberBinder CreateSetMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return Language.CreateSetMemberBinder(name, ignoreCase);

            return DynamicCache.GetSetMemberBinder(name);
        }

        public DeleteMemberBinder CreateDeleteMemberBinder(string name, bool ignoreCase)
        {
            if (ignoreCase)
                return Language.CreateDeleteMemberBinder(name, ignoreCase);

            // TODO: not implemented yet
            return Language.CreateDeleteMemberBinder(name, ignoreCase);
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

        public InvokeMemberBinder CreateCallBinder(string name, bool ignoreCase, CallInfo callInfo)
        {
            ContractUtils.Requires(ignoreCase == false, "ignoreCase");
            return DynamicCache.GetInvokeMemberBinder(name, callInfo);
        }

        public InvokeBinder CreateInvokeBinder(CallInfo callInfo)
        {
            return DynamicCache.GetInvokeBinder(callInfo);
        }

        public CreateInstanceBinder CreateCreateBinder(CallInfo callInfo)
        {
            // TODO: not implemented yet
            return Language.CreateCreateBinder(callInfo);
        }

        #endregion


        #region Execution Stack

        private readonly List<FunctionStack> _FunctionStacks = new List<FunctionStack>();

        internal IEnumerable<FunctionStack> FunctionStacks
        { get { return _FunctionStacks; } }

        #region Variable Access Stack

        private Stack<VariableAccess> variableAccessStack = new Stack<VariableAccess>();

        //Holds a scope-depth stack of the expected sizes for the variable access stack
        private Stack<int> accessStackExpectedSize = new Stack<int>();

        //Holds the number of global variables defined within scopes, 
        //thus offsetting the expected variable access stack size by this value
        private int accessStackSizeOffset = 0;

        public VariableAccess LastVariableAccess
        { get { return variableAccessStack.Count > 0 ? variableAccessStack.Peek() : null; } }

        public string CurrentVariableIdentifier
        {
            get
            {
                string identifier = null;
                foreach (var v in variableAccessStack)
                    switch (v.Operation)
                    {
                        case AccessType.GlobalGet:
                        case AccessType.GlobalSet:
                        case AccessType.LocalGet:
                        case AccessType.LocalSet:
                            identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
                            return identifier;

                        case AccessType.MemberGet:
                        case AccessType.MemberSet:
                            identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
                            break;

                        case AccessType.IndexGet:
                        case AccessType.IndexSet:
                            identifier = "[" + v.VariableName + "]" + (identifier == null ? "" : ("." + identifier));
                            break;
                    }

                return "No current variable";
            }
        }
        
        private AccessType GetRootOperation()
        {
            Stack<VariableAccess> backup = new Stack<VariableAccess>(variableAccessStack.Reverse());
            while (backup.Count > 0)
            {
                var v = backup.Pop();
                switch (v.Operation)
                {
                    case AccessType.GlobalSet:
                    case AccessType.GlobalGet:
                    case AccessType.LocalGet:
                    case AccessType.LocalSet:
                        return v.Operation;
                    default: continue;
                }
            }

            throw new LuaRuntimeException(this, "No variables have been accessed yet");
        }

        private void RemoveTopOperation(Stack<VariableAccess> store = null)
        {
            //var stack = _FunctionStacks.Last().Locals;
            var stack = variableAccessStack;
            while (stack.Count > 0)
            {
                var v = stack.Pop();
                if (store != null)
                    store.Push(v);
                switch (v.Operation)
                {
                    case AccessType.GlobalSet:
                    case AccessType.GlobalGet:
                    case AccessType.LocalGet:
                    case AccessType.LocalSet:
                        return;
                    default: continue;
                }
            }

            throw new LuaRuntimeException(this, "No variables have been accessed yet");
        }

        /// <summary>
        /// Gets an array of locally defined variables, as well as their current values from within a function
        /// </summary>
        /// <param name="stackLevel">The stack level of the function who's variables to get</param>
        public IRuntimeVariables GetLocalVariables(int stackLevel)
        {
            if (_FunctionStacks.Count <= stackLevel)
                throw new LuaRuntimeException(this, "The given stack level was invalid");
            var callStackEntry = _FunctionStacks[_FunctionStacks.Count - stackLevel];

            return callStackEntry.LocalVariables;
        }
        
        public string GetLocalVariableName(int stackLevel, int index)
        {
            if (_FunctionStacks.Count <= stackLevel)
                throw new LuaRuntimeException(this, "The given stack level was invalid");
            var callStackEntry = _FunctionStacks[_FunctionStacks.Count - stackLevel];

            if (callStackEntry.LocalVariableNames.Length <= index)
                return null;

            return callStackEntry.LocalVariableNames[index];
        }

        /// <summary>
        /// Gets an array of up values, as well as their current values for a function
        /// </summary>
        /// <param name="stackLevel">The stack level of the function who's variables to get</param>
        public IRuntimeVariables GetUpValues(int stackLevel)
        {
            if (_FunctionStacks.Count <= stackLevel)
                throw new LuaRuntimeException(this, "The given stack level was invalid");
            var callStackEntry = _FunctionStacks[_FunctionStacks.Count - stackLevel];

            return callStackEntry.UpValues;
        }

        public string GetUpValueName(int stackLevel, int index)
        {
            if (_FunctionStacks.Count <= stackLevel)
                throw new LuaRuntimeException(this, "The given stack level was invalid");
            var callStackEntry = _FunctionStacks[_FunctionStacks.Count - stackLevel];

            if (callStackEntry.UpValueNames.Length <= index)
                return null;

            return callStackEntry.UpValueNames[index];
        }
        #endregion

        #endregion

        #region Libraries

        private readonly BaseLibrary _BaseLibrary;

        //Stores a list of loaded libraries which are implemented in CLR code
        private readonly Dictionary<string, Library.Library> _LoadedBaseLibraries = new Dictionary<string, Library.Library>();

        //Stores a list of loaded packages, these are both CLR libraries and Lua code pieces which have been executed with "require"
        //In the case of packages loaded with "require" this just stores the table returned from that call, which should represent the library
        private readonly Dictionary<string, IDictionary<string, object>> _LoadedPackages = new Dictionary<string, IDictionary<string, object>>();

        /// <summary>
        /// Loads the given library, attempting first to locate a CLR library with that name, and if that fails, then falls back on
        /// searching for a Lua file with the given name.
        /// 
        /// If a Lua file is found with that name, it is executed and its output cached for future access
        /// </summary>
        public LuaTable RequireLibrary(string libraryName)
        {
            //We can perform any library specific initialization stuff here (which may require things to be set outside of the library's table)
            switch(libraryName)
            {
                case "debug":
                    //Enable debug library features
                    break;
            }

            if (Language.IsBaseLibrary(libraryName) && !_LoadedBaseLibraries.ContainsKey(libraryName))
            {
                _LoadedBaseLibraries.Add(libraryName, Language.GetLibraryInstance(libraryName, this));
            }

            if (Language.IsBaseLibrary(libraryName))
            {
                LuaTable temp = new LuaTable(this);
                _LoadedBaseLibraries[libraryName].Setup(temp);

                //Now we need to set the global variable that this library uses
                (ExecutingScopeStorage as IDictionary<string, object>).AddOrSet(libraryName, temp);

                return temp;
            }
            else if(_LoadedPackages.ContainsKey(libraryName))
            {
                LuaTable temp = new LuaTable(this);
                foreach (var v in _LoadedPackages[libraryName]) temp.Add(v);
                return temp;
            }

            return null;
        }

        #endregion
    }
}
