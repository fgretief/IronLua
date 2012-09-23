using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting;
using System.Linq.Expressions;
using System.Reflection;
using System.Dynamic;
using IronLua.Compiler;
using Microsoft.Scripting.Utils;
using System.Runtime.CompilerServices;

namespace IronLua.Runtime
{
    //class LuaTrace
    //{
    //    LuaContext _context;

    //    public LuaTrace(LuaContext context)
    //    {
    //        _context = context;
    //        CallStack = new Stack<FunctionCall>();
    //    }

    //    #region Current File

    //    public string CurrentDocument
    //    { get; private set; }

    //    public void UpdateCurrentDocument(SymbolDocumentInfo document)
    //    {
    //        CurrentDocument = document == null ? "(chunk)" : document.FileName;
    //    }

    //    private static readonly MethodInfo UpdateCurrentDocumentMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentDocument");
    //    public static Expression MakeUpdateCurrentDocument(LuaContext context, SymbolDocumentInfo document)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), UpdateCurrentDocumentMethodInfo, Expression.Constant(document));
    //    }

    //    #endregion

    //    #region Current Source Span

    //    public SourceSpan CurrentSpan
    //    { get; private set; }

    //    public void UpdateSourceSpan(SourceSpan span)
    //    {
    //        CurrentSpan = span;
    //    }

    //    private static readonly MethodInfo UpdateSourceSpanMethodInfo = typeof(LuaTrace).GetMethod("UpdateSourceSpan");
    //    public static Expression MakeUpdateSourceSpan(LuaContext context, SourceSpan span)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), UpdateSourceSpanMethodInfo, Expression.Constant(span));
    //    }

    //    #endregion

    //    #region Function Call Stack

    //    public class FunctionCall
    //    {
    //        private FunctionCall()
    //        {
    //            Locals = new Stack<VariableAccess>();
    //        }

    //        public FunctionCall(SourceSpan functionLocation, FunctionType type, string identifier, string fileName = null)
    //            : this()
    //        {
    //            FunctionLocation = functionLocation;
    //            Type = type;
    //            FileName = fileName;
    //            MethodName = identifier;
    //        }

    //        public FunctionCall(SourceSpan functionLocation, FunctionType type, IEnumerable<string> identifiers, string fileName = null)
    //            : this()
    //        {
    //            FunctionLocation = functionLocation;
    //            Type = type;
    //            FileName = fileName;

    //            string temp = identifiers.First();
    //            foreach (var i in identifiers.Skip(1))
    //                temp += "." + i;

    //            MethodName = temp;
    //        }

    //        public string FileName { get; internal set; }
    //        public string MethodName { get; private set; }
    //        public SourceSpan FunctionLocation { get; private set; }
    //        public FunctionType Type { get; private set; }

    //        public Stack<VariableAccess> Locals
    //        { get; private set; }
    //    }

    //    public enum FunctionType
    //    {
    //        Lua,
    //        CLR,
    //        Chunk,
    //        Invoke
    //    }

    //    public Stack<FunctionCall> CallStack
    //    { get; private set; }

    //    private static readonly MethodInfo PushCallStackMethodInfo = typeof(LuaTrace).GetMethod("PushFunctionCall");
    //    private static readonly MethodInfo PopCallStackMethodInfo = typeof(LuaTrace).GetMethod("PopFunctionCall");

    //    public void PushFunctionCall(FunctionCall call)
    //    {
    //        if (call.FileName == null || call.FileName.Length == 0)
    //            call.FileName = CurrentDocument;
    //        CallStack.Push(call);
    //    }

    //    public void PopFunctionCall()
    //    {
    //        var call = CallStack.Pop();
    //        variableAccessStack.Clear();
    //        accessStackExpectedSize.Clear();
    //        accessStackSizeOffset = 0;
    //    }

    //    public static Expression MakePushFunctionCall(LuaContext context, FunctionCall call)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), PushCallStackMethodInfo, Expression.Constant(call));
    //    }

    //    public static Expression MakePopFunctionCall(LuaContext context)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), PopCallStackMethodInfo);
    //    }

    //    #endregion

    //    #region Variable Access Stack

    //    public enum AccessType
    //    {
    //        GlobalGet, GlobalSet,
    //        LocalGet, LocalSet,
    //        MemberGet, MemberSet,
    //        IndexGet, IndexSet
    //    }

    //    public class VariableAccess
    //    {
    //        public VariableAccess(string identifier, AccessType operation)
    //        {
    //            VariableName = identifier;
    //            Operation = operation;
    //        }

    //        public string VariableName
    //        { get; private set; }
            
    //        public AccessType Operation
    //        { get; private set; }

    //        public object Value
    //        { get; internal set; }
    //    }

    //    private Stack<VariableAccess> variableAccessStack = new Stack<VariableAccess>();

    //    //Holds a scope-depth stack of the expected sizes for the variable access stack
    //    private Stack<int> accessStackExpectedSize = new Stack<int>();
        
    //    //Holds the number of global variables defined within scopes, 
    //    //thus offsetting the expected variable access stack size by this value
    //    private int accessStackSizeOffset = 0;

    //    public VariableAccess LastVariableAccess
    //    { get { return variableAccessStack.Count > 0 ? variableAccessStack.Peek() : null; } }

    //    public string CurrentVariableIdentifier
    //    {
    //        get
    //        {
    //            string identifier = null;
    //            foreach (var v in variableAccessStack)
    //                switch (v.Operation)
    //                {
    //                    case AccessType.GlobalGet:
    //                    case AccessType.GlobalSet:
    //                    case AccessType.LocalGet:
    //                    case AccessType.LocalSet:
    //                        identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
    //                        return identifier;

    //                    case AccessType.MemberGet:
    //                    case AccessType.MemberSet:
    //                        identifier = v.VariableName + (identifier == null ? "" : ("." + identifier));
    //                        break;
                            
    //                    case AccessType.IndexGet:
    //                    case AccessType.IndexSet:
    //                        identifier = "[" + v.VariableName + "]" + (identifier == null ? "" : ("." + identifier));
    //                        break;
    //                }

    //            return "No current variable";
    //        }
    //    }

    //    public void UpdateLastVariableAccess(VariableAccess variable, object value)
    //    {
    //        variable.Value = value;

    //        switch(variable.Operation)
    //        {
    //            case AccessType.LocalGet:
    //            case AccessType.LocalSet:
    //                CallStack.Peek().Locals.Push(variable);
    //                break;
    //            case AccessType.GlobalGet:
    //            case AccessType.GlobalSet:
    //                variableAccessStack.Clear();
    //                break;
    //        }
    //        variableAccessStack.Push(variable);
    //    }

    //    private static readonly MethodInfo UpdateLastVariableAccessMethodInfo = typeof(LuaTrace).GetMethod("UpdateLastVariableAccess");
    //    public static Expression MakeUpdateLastVariableAccess(LuaContext context, VariableAccess variable, Expression value)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), UpdateLastVariableAccessMethodInfo, Expression.Constant(variable), value);
    //    }
        
        
    //    private static readonly MethodInfo OnScopeEnterMethodInfo = typeof(LuaTrace).GetMethod("OnScopeEnter");
    //    private static readonly MethodInfo OnScopeLeaveMethodInfo = typeof(LuaTrace).GetMethod("OnScopeLeave");
    //    public void OnScopeEnter()
    //    {
    //        accessStackExpectedSize.Push(variableAccessStack.Count);
    //    }

    //    public void OnScopeLeave()
    //    {
    //        var expectedSize = accessStackExpectedSize.Pop();

    //        Stack<VariableAccess> backup = new Stack<VariableAccess>();
    //        while (variableAccessStack.Count > expectedSize + accessStackSizeOffset)
    //        {
    //            switch(GetRootOperation())
    //            {
    //                case AccessType.GlobalGet:
    //                case AccessType.GlobalSet:
    //                    RemoveTopOperation(backup);
    //                    break;
    //                default:
    //                    RemoveTopOperation();
    //                    break;
    //            }
    //        }

    //        while (backup.Count > 0)
    //            CallStack.Peek().Locals.Push(backup.Pop());
    //    }

    //    private AccessType GetRootOperation()
    //    {
    //        Stack<VariableAccess> backup = new Stack<VariableAccess>(variableAccessStack.Reverse());
    //        while (backup.Count > 0)
    //        {
    //            var v = backup.Pop();
    //            switch(v.Operation)
    //            {
    //                case AccessType.GlobalSet:
    //                case AccessType.GlobalGet:
    //                case AccessType.LocalGet:
    //                case AccessType.LocalSet:
    //                    return v.Operation;
    //                default: continue;
    //            }
    //        }

    //        throw LuaRuntimeException.Create(_context, "No variables have been accessed yet");
    //    }

    //    private void RemoveTopOperation(Stack<VariableAccess> store = null)
    //    {
    //        var stack = CallStack.Peek().Locals;
    //        while (stack.Count > 0)
    //        {
    //            var v = stack.Pop();
    //            if (store != null)
    //                store.Push(v);
    //            switch (v.Operation)
    //            {
    //                case AccessType.GlobalSet:
    //                case AccessType.GlobalGet:
    //                case AccessType.LocalGet:
    //                case AccessType.LocalSet:
    //                    return;
    //                default: continue;
    //            }
    //        }

    //        throw LuaRuntimeException.Create(_context, "No variables have been accessed yet");
    //    }

    //    public static Expression MakeOnScopeEnter(LuaContext context)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), OnScopeEnterMethodInfo);
    //    }

    //    public static Expression MakeOnScopeLeave(LuaContext context)
    //    {
    //        return Expression.Call(Expression.Constant(context.Trace), OnScopeLeaveMethodInfo);
    //    }
        
    //    public VariableAccess GetVariableAccess(int stackLevel, int variableIndex)
    //    {
    //        if (CallStack.Count < stackLevel)
    //            throw LuaRuntimeException.Create(_context, "The given stack level was invalid");
    //        var callStackEntry =  CallStack.ElementAt(stackLevel - 1);

    //        if (callStackEntry.Locals.Count < variableIndex)
    //            return null;
    //        return callStackEntry.Locals.Reverse().ElementAt(variableIndex - 1);
    //    }

    //    #endregion

    //    #region Current Scope

    //    public IDynamicMetaObjectProvider CurrentScopeStorage
    //    { get; private set; }

    //    public void UpdateCurrentScopeStorage(IDynamicMetaObjectProvider scopeStorage)
    //    {
    //        CurrentScopeStorage = scopeStorage;
    //    }

    //    public LuaScope CurrentEvaluationScope
    //    { get; private set; }

    //    public void UpdateCurrentEvaluationScope(LuaScope scope)
    //    {
    //        CurrentEvaluationScope = scope;
    //    }


    //    private static readonly MethodInfo UpdateCurrentEvaluationScopeMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentEvaluationScope");
    //    private static readonly MethodInfo UpdateScopeStorageMethodInfo = typeof(LuaTrace).GetMethod("UpdateCurrentScopeStorage");
    //    public static Expression MakeUpdateCurrentEvaluationScope(LuaContext context, LuaScope scope)
    //    {
    //        return Expression.Block(
    //            Expression.Call(Expression.Constant(context.Trace), UpdateCurrentEvaluationScopeMethodInfo, Expression.Constant(scope)),
    //            Expression.Call(Expression.Constant(context.Trace), UpdateScopeStorageMethodInfo, scope.GetDlrGlobals()));
    //    }

    //    #endregion
    //}

    internal class FunctionStack
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public CodeContext Context;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly LuaScope UpScope;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly LuaScope ExecScope;

        public readonly Stack<VariableAccess> Locals;

        public readonly string[] LocalVariableNames;
        public IRuntimeVariables LocalVariables;
        public readonly string[] UpValueNames;
        public IRuntimeVariables UpValues;

        public readonly string Identifier;

        public FunctionStack(string identifier)
        {
            Identifier = identifier;

            Context = null;
            UpScope = null;
            ExecScope = null;
            UpValues = null;
            UpValueNames = null;
            LocalVariables = null;
            LocalVariableNames = null;
            Locals = null;
        }

        public FunctionStack(CodeContext context, LuaScope upScope, LuaScope execScope, string identifier)
        {
            ContractUtils.Requires(context != null);

            Context = context;
            UpScope = upScope;
            ExecScope = execScope;

            if (UpScope != null)
                UpValueNames = UpScope.GetLocalNames().ToArray();

            if (ExecScope != null)
                LocalVariableNames = ExecScope.GetLocalNames().ToArray();

            if (ExecScope != null && ExecScope.LocalsCount > 0)
                Locals = new Stack<VariableAccess>();
            else
                Locals = null;

            Identifier = identifier;

            LocalVariableNames = null;
            LocalVariables = null;
            UpValueNames = null;
            UpValues = null;
        }

        public FunctionStack(CodeContext context, LuaScope upScope, LuaScope execScope, IEnumerable<string> identifiers)
            : this(context, upScope, execScope, Flatten(identifiers, "."))
        { }

        static string Flatten(IEnumerable<string> items, string seperator)
        {
            using (var e = items.GetEnumerator())
            {
                string temp = "";
                while (e.MoveNext())
                {
                    if (temp.Length == 0)
                        temp = e.Current;
                    else
                        temp = seperator + e.Current;
                }
                return temp;
            }
        }
    }
    
    internal enum AccessType
    {
        GlobalGet, GlobalSet,
        LocalGet, LocalSet,
        MemberGet, MemberSet,
        IndexGet, IndexSet
    }

    internal class VariableAccess
    {
        public VariableAccess(string identifier, AccessType operation)
        {
            VariableName = identifier;
            Operation = operation;
        }

        public string VariableName
        { get; private set; }

        public AccessType Operation
        { get; private set; }
    }
}
