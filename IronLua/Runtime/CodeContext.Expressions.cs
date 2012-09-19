using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Linq.Expressions;
using Expr = System.Linq.Expressions.Expression;
using System.Dynamic;
using System.Reflection;
using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Runtime;
using IronLua.Compiler;

namespace IronLua.Runtime
{
    //Provides static methods which can be used to create LINQ Expressions
    //for performing different operations used by this CodeContext (as well
    //as the functions which back their usage).
    internal sealed partial class CodeContext
    {
        private static readonly Dictionary<string, MethodInfo> _MethodInfoCache = new Dictionary<string, MethodInfo>();
        /// <summary>
        /// Gets the private instance method represented by the given <paramref name="methodName"/>
        /// from a cache if it is present, otherwise adds it to the cache.
        /// </summary>
        private static MethodInfo GetBackingMethod(string methodName)
        {
            if (!_MethodInfoCache.ContainsKey(methodName))
                _MethodInfoCache.Add(methodName, typeof(CodeContext).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic));
            return _MethodInfoCache[methodName];
        }

        #region Start/Stop Execution

        /// <summary>
        /// Creates an expression which prepares the given <see cref="CodeContext"/>
        /// for execution of code within the given <paramref name="executingScope"/>
        /// </summary>
        /// <remarks>
        /// Since a CodeContext spans a compiled code instance, it may be reused each
        /// time its code is executed. As such, we need to prepare it for this execution
        /// including giving it a reference to the currently executing scope.
        /// 
        /// It also adds all the BaseLibrary stuff to this scope to make it accessible
        /// to Lua.
        /// </remarks>
        public static Expr OnStartExecute(CodeContext context, Expr executingScope)
        {
            return Expr.Call(Expr.Constant(context), GetBackingMethod("_OnStartExecute"), executingScope);
        }

        /// <summary>
        /// Releases any resources used by the given <see cref="CodeContext"/>
        /// for execution of the its code on the last scope set by <see cref="OnStartExecute"/>
        /// </summary>
        /// <remarks>
        /// Basically, this just does the opposite of OnStartExecute, and allows
        /// this CodeContext to clean out anything which is no longer being used
        /// after execution completes.
        /// 
        /// It also removes all the BaseLibrary references from the executing scope,
        /// as these are added during OnStartExecute, and should be removed afterwards
        /// as they are pure Lua primitives.
        /// </remarks>
        public static Expr OnFinishExecute(CodeContext context)
        {
            return Expr.Call(Expr.Constant(context), GetBackingMethod("_OnFinishExecute"));
        }


        private void _OnStartExecute(IDynamicMetaObjectProvider executingScope)
        {
            ContractUtils.Requires(executingScope is IDictionary<string, object>);

            ExecutingScope = executingScope as Scope;
            ExecutingScopeStorage = ExecutingScope.Storage;
            _EnvironmentMappings.Clear();   //We clear this here as opposed to in OnFinishExecute in case the last execution failed
            
            Language.BaseLibrary.Setup(executingScope as IDictionary<string, object>);
        }

        private void _OnFinishExecute()
        {
            Language.BaseLibrary.Clean(ExecutingScopeStorage as IDictionary<string, object>);

            ExecutingScope = null;
            ExecutingScopeStorage = null;
        }

        #endregion

        #region Execution Environment

        public static Expr GetExecutionEnvironment(CodeContext context, LuaScope scope)
        {
            LuaScope current = scope;
            while (!context.HasCustomEnvironment(current))
                current = current.GetParent();
        }

        #endregion
    }
}
