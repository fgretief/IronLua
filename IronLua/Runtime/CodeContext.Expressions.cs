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
using Microsoft.Scripting;

namespace IronLua.Runtime
{
    //Provides static methods which can be used to create LINQ Expressions
    //for performing different operations used by this CodeContext (as well
    //as the functions which back their usage).
    internal sealed partial class CodeContext
    {
        static CodeContext()
        {
            FunctionStackVariable = Expr.Variable(typeof(List<FunctionStack>), "$function_stacks$");
        }

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
        
        /// <summary>
        /// Calls a backing method with the given arguments
        /// </summary>
        private static Expr CallBackingMethod(string methodName, CodeContext context, params Expr[] args)
        {
            return Expr.Call(Expr.Constant(context), GetBackingMethod(methodName), args);
        }

        /// <summary>
        /// Calls a backing method with the given arguments
        /// </summary>
        private static Expr CallBackingMethod(string methodName, Expression context, params Expr[] args)
        {
            return Expr.Call(context, GetBackingMethod(methodName), args);
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
        public static Expr OnStartExecute(CodeContext context, Expr executingScope, SourceUnit sourceUnit)
        {
            return CallBackingMethod("_OnStartExecute", context, executingScope, Expr.Constant(sourceUnit));
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
            return CallBackingMethod("_OnFinishExecute", context);
        }


        private void _OnStartExecute(IDynamicMetaObjectProvider executingScope, SourceUnit sourceUnit)
        {
            ContractUtils.Requires(executingScope is Scope);

            ExecutingScope = executingScope as Scope;
            ExecutingScopeStorage = ExecutingScope.Storage;

            //We should update the context under which this LuaTable is executing
            //so that it resolves metamethod overrides correctly.
            //TODO: Decide whether we should keep this if it is non-null, it may be that this is coming from a different
            //      CodeContext which has some metamethod stuff set particularly for this scope, or its contents, and we
            //      shouldn't change this.
            if (ExecutingScopeStorage is LuaTable)
                (ExecutingScopeStorage as LuaTable).Context = this;

            _EnvironmentMappings.Clear();   //We clear this here as opposed to in OnFinishExecute in case the last execution failed

            _BaseLibrary.Setup(ExecutingScopeStorage as IDictionary<string, object>);
        }

        private void _OnFinishExecute()
        {
            _BaseLibrary.Clean(ExecutingScopeStorage as IDictionary<string, object>);

            ExecutingScope = null;
            ExecutingScopeStorage = null;
            _LoadedPackages.Clear();
            _EnvironmentMappings.Clear();
            _FunctionStacks.Clear();
        }

        #endregion

        #region Execution Environment

        public static Expr GetExecutionEnvironment(CodeContext context, LuaScope scope)
        {
            return CallBackingMethod("_GetExecutionEnvironment", context, Expr.Constant(scope));
        }

        private IDynamicMetaObjectProvider _GetExecutionEnvironment(LuaScope scope)
        {
            LuaScope current = scope;
            while (current != null && !HasCustomEnvironment(current))
                current = current.GetParent();

            if (current != null)
                return GetFunctionEnvironment(scope);

            return ExecutingScopeStorage;
        }

        #endregion

        #region Execution Stack

        public static ParameterExpression FunctionStackVariable
        { get; private set; }
        
        public static Expr PushFunctionStack(Expression context, Expression function)
        {
            return CallBackingMethod("_PushFunctionStack", context, function);
        }

        public static Expr PushFunctionStack(CodeContext context, FunctionStack function)
        {
            return CallBackingMethod("_PushFunctionStack", context, Expr.Constant(function));
        }

        public static Expr PopFunctionStack(CodeContext context)
        {
            return CallBackingMethod("_PopFunctionStack", context);
        }

        public static Expr PopFunctionStack(Expression context)
        {
            return CallBackingMethod("_PopFunctionStack", context);
        }

        private List<FunctionStack> _PushFunctionStack(FunctionStack function)
        {
            _FunctionStacks.Add(function);
            return _FunctionStacks;
        }

        private void _PopFunctionStack()
        {
            _FunctionStacks.RemoveAt(_FunctionStacks.Count - 1);
        }

        #region Variable Access
        
        private void _UpdateLastVariableAccess(VariableAccess variable, object value)
        {
            switch (variable.Operation)
            {
                case AccessType.LocalGet:
                case AccessType.LocalSet:
                    //_FunctionStacks.Last().Locals.Push(variable);
                    break;
                case AccessType.GlobalGet:
                case AccessType.GlobalSet:
                    variableAccessStack.Clear();
                    break;
            }
            variableAccessStack.Push(variable);
        }

        public static Expr UpdateLastVariableAccess(CodeContext context, VariableAccess variable, Expr value)
        {
            return CallBackingMethod("_UpdateLastVariableAccess", context, Expr.Constant(variable), value);
        }

        public static Expr OnExceptionThrown(CodeContext context, Expr exception)
        {
            return CallBackingMethod("_OnExceptionThrown", context, exception);
        }

        private void _OnExceptionThrown(Exception ex)
        {
            throw ex;
        }

        private void _OnScopeEnter(LuaScope scope)
        {
            //accessStackExpectedSize.Push(variableAccessStack.Count);
        }

        private void _OnScopeLeave()
        {
            //var expectedSize = accessStackExpectedSize.Pop();

            //Stack<VariableAccess> backup = new Stack<VariableAccess>();
            //while (variableAccessStack.Count > expectedSize + accessStackSizeOffset)
            //{
            //    switch (GetRootOperation())
            //    {
            //        case AccessType.GlobalGet:
            //        case AccessType.GlobalSet:
            //            RemoveTopOperation(backup);
            //            break;
            //        default:
            //            RemoveTopOperation();
            //            break;
            //    }
            //}

            //while (_FunctionStacks.Any() && backup.Count > 0)
            //    _FunctionStacks.Last().Locals.Push(backup.Pop());
        }

        public static Expr OnScopeEnter(CodeContext context, LuaScope scope)
        {
            return CallBackingMethod("_OnScopeEnter", context, Expr.Constant(scope));
        }

        public static Expr OnScopeLeave(CodeContext context)
        {
            return CallBackingMethod("_OnScopeLeave", context);
        }

        #endregion

        #endregion

    }
}
