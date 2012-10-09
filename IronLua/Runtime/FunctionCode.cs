using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting.Runtime;
using System.Linq.Expressions;
using Microsoft.Scripting;
using System.Threading;
using System.Diagnostics;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Generation;
using IronLua.Compiler;
using Microsoft.Scripting.Utils;
using System.Runtime.CompilerServices;

namespace IronLua.Runtime
{
    public delegate object LookupCompilationDelegate(CodeContext context, FunctionCode code);

    /// <summary>
    /// Represents a piece of code.  This can reference either a CompiledCode
    /// object or a Function.   The user can explicitly call FunctionCode by
    /// passing it into exec or eval.
    /// </summary>
    public class FunctionCode : IExpressionSerializable
    {
        internal Delegate Target, LightThrowTarget;                 // the current target for the function.  This can change based upon adaptive compilation, recursion enforcement, and tracing.
        internal Delegate _normalDelegate;                          // the normal delegate - this can be a compiled or interpreted delegate.
        private Compiler.Ast.Statement.Function _lambda;                  // the original DLR lambda that contains the code
        internal readonly string _initialDoc;                       // the initial doc string
        private readonly int _localCount;                           // the number of local variables in the code
        private readonly int _argCount;                             // cached locally because it's used during calls w/ defaults
        private bool _compilingLight;                               // true if we're compiling for light exceptions
        private int _exceptionCount;
        internal LuaScope _compilerScope;                           // the compiler scope under which this function is created

        // debugging/tracing support
        private LambdaExpression _tracingLambda;                    // the transformed lambda used for tracing/debugging
        internal Delegate _tracingDelegate;                         // the delegate used for tracing/debugging, if one has been created.  This can be interpreted or compiled.

        /// <summary>
        /// This is both the lock that is held while enumerating the threads or updating the thread accounting
        /// information.  It's also a marker CodeList which is put in place when we are enumerating the thread
        /// list and all additions need to block.
        /// 
        /// This lock is also acquired whenever we need to calculate how a function's delegate should be created 
        /// so that we don't race against sys.settrace/sys.setprofile.
        /// </summary>
        private static CodeList _CodeCreateAndUpdateDelegateLock = new CodeList();

        internal Compiler.Ast.Statement.Function LuaCode
        {
            get { return _lambda; }
        }

        internal LuaScope CompilerScope
        { get { return _compilerScope; } }

        public SourceSpan Span
        { get { return _lambda.Span; } }
        
        internal static void CleanFunctionCodes(LuaContext context, bool synchronous)
        {
            if (synchronous)            
                CodeCleanup(context);
            
            else            
                ThreadPool.QueueUserWorkItem(CodeCleanup, context);
            
        }

        private static void CodeCleanup(object state)
        {
            LuaContext context = (LuaContext)state;

            // only allow 1 thread at a time to do cleanup (other threads can continue adding)
            lock (context._codeCleanupLock)
            {
                // the bulk of the work is in scanning the list, this proceeeds lock free
                int removed = 0, kept = 0;
                CodeList prev = null;
                CodeList cur = GetRootCodeNoUpdating(context);

                while (cur != null)
                {
                    if (!cur.Code.IsAlive)
                    {
                        if (prev == null)
                        {
                            if (Interlocked.CompareExchange(ref context._allCodes, cur.Next, cur) != cur)
                            {
                                // someone else snuck in and added a new code entry, spin and try again.
                                cur = GetRootCodeNoUpdating(context);
                                continue;
                            }
                            cur = cur.Next;
                            removed++;
                            continue;
                        }
                        else
                        {
                            // remove from the linked list, we're the only one who can change this.
                            Debug.Assert(prev.Next == cur);
                            removed++;
                            cur = prev.Next = cur.Next;
                            continue;
                        }
                    }
                    else
                    {
                        kept++;
                    }
                    prev = cur;
                    cur = cur.Next;
                }

                // finally update our bookkeeping statistics which requires locking but is fast.
                lock (_CodeCreateAndUpdateDelegateLock)
                {
                    // calculate the next cleanup, we want each pass to remove ~50% of all entries
                    const double removalGoal = .50;

                    if (context._codeCount == 0)
                    {
                        // somehow we would have had to queue a bunch of function codes, have 1 thread
                        // clean them up all up, and a 2nd queued thread waiting to clean them up as well.
                        // At the same time there would have to be no live functions defined which means
                        // we're executing top-level code which is causing this to happen.
                        context._nextCodeCleanup = 200;
                        return;
                    }
                    //Console.WriteLine("Removed {0} entries, {1} remain", removed, context._codeCount);

                    Debug.Assert(removed <= context._codeCount);
                    double pctRemoved = (double)removed / (double)context._codeCount; // % of code removed
                    double targetRatio = pctRemoved / removalGoal;                    // how we need to adjust our last goal

                    // update the total and next node cleanup
                    int newCount = Interlocked.Add(ref context._codeCount, -removed);
                    Debug.Assert(newCount >= 0);

                    // set the new target for cleanup
                    int nextCleanup = targetRatio != 0 ? newCount + (int)(context._nextCodeCleanup / targetRatio) : -1;
                    if (nextCleanup > 0)
                    {
                        // we're making good progress, use the newly calculated next cleanup point
                        context._nextCodeCleanup = nextCleanup;
                    }
                    else
                    {
                        // none or very small amount cleaned up, just schedule a cleanup for sometime in the future.
                        context._nextCodeCleanup += 500;
                    }

                    Debug.Assert(context._nextCodeCleanup >= context._codeCount, String.Format("{0} vs {1} ({2})", context._nextCodeCleanup, context._codeCount, targetRatio));
                }
            }
        }

        private static CodeList GetRootCodeNoUpdating(LuaContext context)
        {
            CodeList cur = context._allCodes;
            if (cur == _CodeCreateAndUpdateDelegateLock)
            {
                lock (_CodeCreateAndUpdateDelegateLock)
                {
                    // wait until enumerating thread is done, but it's alright
                    // if we got cur and then an enumeration started (because we'll
                    // just clear entries out)
                    cur = context._allCodes;
                    Debug.Assert(cur != _CodeCreateAndUpdateDelegateLock);
                }
            }
            return cur;
        }

        internal void SetTarget(Delegate target)
        {
            Target = LightThrowTarget = target;
        }

        internal void LightThrowCompile(CodeContext context)
        {
            if (++_exceptionCount > 20)
            {
                if (!_compilingLight && (object)Target == (object)LightThrowTarget)
                {
                    _compilingLight = true;
                    if (!IsOnDiskCode)
                    {
                        ThreadPool.QueueUserWorkItem(x =>
                        {
                            var Ctx = context.Language;

                            bool enableTracing;
                            lock (Ctx._codeUpdateLock)                            
                                enableTracing = context.Language.EnableTracing;                            

                            Delegate target;
                            if (enableTracing)                            
                                target = ((LambdaExpression)LightExceptions.Rewrite(GetGeneratorOrNormalLambdaTracing(Ctx).Reduce())).Compile();
                            
                            else                            
                                target = ((LambdaExpression)LightExceptions.Rewrite(GetGeneratorOrNormalLambda().Reduce())).Compile();

                            

                            lock (Ctx._codeUpdateLock)
                            {
                                if (context.Language.EnableTracing == enableTracing)                                
                                    LightThrowTarget = target;                                
                            }
                        });
                    }
                }
            }
        }

        private bool IsOnDiskCode
        {
            get
            {
                //TODO: Add logic here when we implement expression tree serialization/deserialization (i.e. compiling)
                return false;
            }
        }

        
        internal string[] ArgNames
        {
            get
            {
                return _lambda.Body.Parameters.ToArray();
            }
        }



        #region Internal API Surface

        internal LightLambdaExpression Code
        {
            get
            {
                return _lambda.GetLambda();
            }
        }

        internal object Call(CodeContext/*!*/ context)
        {
            if (Target == null || (Target.GetMethodInfo() != null && Target.GetMethodInfo().DeclaringType == typeof(PythonCallTargets)))            
                UpdateDelegate(context.Language, true);
            
            Func<CodeContext, CodeContext> classTarget = Target as Func<CodeContext, CodeContext>;
            if (classTarget != null)
                return classTarget(context);

            LookupCompilationDelegate moduleCode = Target as LookupCompilationDelegate;
            if (moduleCode != null)
                return moduleCode(context, this);

            Func<FunctionCode, object> optimizedModuleCode = Target as Func<FunctionCode, object>;
            if (optimizedModuleCode != null)
                return optimizedModuleCode(this);

            var func = new LuaFunction(context, this, null, ArrayUtils.EmptyObjects, new MutableTuple<object>());
            CallSite<Func<CallSite, CodeContext, LuaFunction, object>> site = context.Language.FunctionCallSite;
            return site.Target(site, context, func);
        }

        /// <summary>
        /// Creates a FunctionCode object for exec/eval/execfile'd/compile'd code.
        /// 
        /// The code is then executed in a specific CodeContext by calling the .Call method.
        /// 
        /// If the code is being used for compile (vs. exec/eval/execfile) then it needs to be
        /// registered incase our tracing mode changes.
        /// </summary>
        internal static FunctionCode FromSourceUnit(SourceUnit sourceUnit, CompilerOptions options, bool register)
        {
            var code = LuaContext.CompileLuaCode(sourceUnit, options, ThrowingErrorSink.Default);

            return ((RunnableScriptCode)code).GetFunctionCode(register);
        }

        #endregion


        /// <summary>
        /// Called the 1st time a function is invoked by our OriginalCallTarget* methods
        /// over in PythonCallTargets.  This computes the real delegate which needs to be
        /// created for the function.  Usually this means starting off interpretering.  It 
        /// also involves adding the wrapper function for recursion enforcement.
        /// 
        /// Because this can race against sys.settrace/setprofile we need to take our 
        /// _ThreadIsEnumeratingAndAccountingLock to ensure no one is actively changing all
        /// of the live functions.
        /// </summary>
        internal void LazyCompileFirstTarget()
        {
            lock (_CodeCreateAndUpdateDelegateLock)
            {
                UpdateDelegate(_compilerScope.GetContext().Language, true);
            }
        }

        /// <summary>
        /// Updates the delegate based upon current Lua context settings for tracing.
        /// </summary>
        internal void UpdateDelegate(LuaContext context, bool forceCreation)
        {
            Delegate finalTarget;

            if (context.EnableTracing && _lambda != null)
            {
                if (_tracingLambda == null)
                {
                    if (!forceCreation)
                    {
                        // the user just called sys.settrace(), don't force re-compilation of every method in the system.  Instead
                        // we'll just re-compile them as they're invoked.
                        PythonCallTargets.GetPythonTargetType(_lambda.Body.Parameters.Count > PythonCallTargets.MaxArgs, _lambda.Body.Parameters.Count, out Target);
                        LightThrowTarget = Target;
                        return;
                    }
                    _tracingLambda = GetGeneratorOrNormalLambdaTracing(context);
                }

                if (_tracingDelegate == null)
                {
                    _tracingDelegate = CompileLambda(_tracingLambda, new TargetUpdaterForCompilation(context, this).SetCompiledTargetTracing);
                }

                finalTarget = _tracingDelegate;
            }
            else
            {
                if (_normalDelegate == null)
                {
                    if (!forceCreation)
                    {
                        // we cannot create the delegate when forceCreation is false because we hold the
                        // _CodeCreateAndUpdateDelegateLock and compiling the delegate may create a FunctionCode
                        // object which requires the lock.
                        PythonCallTargets.GetPythonTargetType(_lambda.ParameterNames.Length > PythonCallTargets.MaxArgs, _lambda.ParameterNames.Length, out Target);
                        LightThrowTarget = Target;
                        return;
                    }
                    _normalDelegate = CompileLambda(GetGeneratorOrNormalLambda(), new TargetUpdaterForCompilation(context, this).SetCompiledTarget);
                }

                finalTarget = _normalDelegate;
            }

            SetTarget(finalTarget);
        }

        /// <summary>
        /// Called to set the initial target delegate when the user has passed -X:Debug to enable
        /// .NET style debugging.
        /// </summary>
        internal void SetDebugTarget(LuaContext context, Delegate target)
        {
            _normalDelegate = target;

            SetTarget(target);
        }

        /// <summary>
        /// Gets the LambdaExpression for tracing.  
        /// 
        /// If this is a generator function code then the lambda gets tranformed into the correct generator code.
        /// </summary>
        private LambdaExpression GetGeneratorOrNormalLambdaTracing(LuaContext context)
        {
            var debugProperties = new LuaDebuggingPayload(this);

            var debugInfo = new Microsoft.Scripting.Debugging.CompilerServices.DebugLambdaInfo(
                null,                                                               // IDebugCompilerSupport
                _lambda.Name.Identifiers.Aggregate((a,b) => a + "." + b),           // lambda alias
                false,                                                              // optimize for leaf frames
                null,                                                               // hidden variables
                null,                                                               // variable aliases
                debugProperties                                                     // custom payload
            );
            
            
            return context.DebugContext.TransformLambda((LambdaExpression)Compiler.Ast.Node.RemoveFrame(_lambda.GetLambda()), debugInfo);            
        }


        /// <summary>
        /// Gets the correct final LambdaExpression for this piece of code.
        /// 
        /// This is either just _lambda or _lambda re-written to be a generator expression.
        /// </summary>
        private LightLambdaExpression GetGeneratorOrNormalLambda()
        {
            return Code;
        }

        private Delegate CompileLambda(LightLambdaExpression code, EventHandler<LightLambdaCompileEventArgs> handler)
        {
#if EMIT_PDB
            if (_lambda.EmitDebugSymbols) {
                return CompilerHelpers.CompileToMethod((LambdaExpression)code.Reduce(), DebugInfoGenerator.CreatePdbGenerator(), true);
            }
#endif
            if (_lambda.ShouldInterpret)
            {
                Delegate result = code.Compile();

                // If the adaptive compiler decides to compile this function, we
                // want to store the new compiled target. This saves us from going
                // through the interpreter stub every call.
                var lightLambda = result.Target as LightLambda;
                if (lightLambda != null)
                {
                    lightLambda.Compile += handler;
                }

                return result;
            }

            return code.Compile();
        }

        private Delegate CompileLambda(LambdaExpression code, EventHandler<LightLambdaCompileEventArgs> handler)
        {
#if EMIT_PDB
            if (_lambda.EmitDebugSymbols) {
                return CompilerHelpers.CompileToMethod(code, DebugInfoGenerator.CreatePdbGenerator(), true);
            } 
#endif
            if (_lambda.ShouldInterpret)
            {
                Delegate result = CompilerHelpers.LightCompile(code);

                // If the adaptive compiler decides to compile this function, we
                // want to store the new compiled target. This saves us from going
                // through the interpreter stub every call.
                var lightLambda = result.Target as LightLambda;
                if (lightLambda != null)
                {
                    lightLambda.Compile += handler;
                }

                return result;
            }

            return code.Compile();
        }





        /// <summary>
        /// Extremely light weight linked list of weak references used for tracking
        /// all of the FunctionCode objects which get created and need to be updated
        /// for purposes of recursion enforcement or tracing.
        /// </summary>
        internal class CodeList
        {
            public readonly WeakReference Code;
            public CodeList Next;

            public CodeList() { }

            public CodeList(WeakReference code, CodeList next)
            {
                Code = code;
                Next = next;
            }
        }


        class TargetUpdaterForCompilation
        {
            private readonly LuaContext _context;
            private readonly FunctionCode _code;

            public TargetUpdaterForCompilation(LuaContext context, FunctionCode code)
            {
                _code = code;
                _context = context;
            }

            public void SetCompiledTarget(object sender, Microsoft.Scripting.Interpreter.LightLambdaCompileEventArgs e)
            {
                _code.SetTarget(_code._normalDelegate = e.Compiled);
            }

            public void SetCompiledTargetTracing(object sender, Microsoft.Scripting.Interpreter.LightLambdaCompileEventArgs e)
            {
                _code.SetTarget(_code._tracingDelegate = e.Compiled);
            }
        }

        public Expression CreateExpression()
        {
            throw new NotImplementedException();
        }
    }
    internal class LuaDebuggingPayload
    {
        public FunctionCode Code;
        private Dictionary<int, bool> _handlerLocations;
        private Dictionary<int, List<int>> _loopLocations;

        public LuaDebuggingPayload(FunctionCode code)
        {
            Code = code;
        }

        public Dictionary<int, bool> HandlerLocations
        {
            get
            {
                if (_handlerLocations == null)
                {
                    GatherLocations();
                }

                return _handlerLocations;
            }
        }

        public Dictionary<int, List<int>> LoopLocations
        {
            get
            {
                if (_loopLocations == null)
                {
                    GatherLocations();
                }

                return _loopLocations;
            }
        }

        private void GatherLocations()
        {
            var walker = new TracingWalker();

            Code.LuaCode.Visit(walker);

            _loopLocations = walker.LoopLocations;
            _handlerLocations = walker.HandlerLocations;
        }

        class TracingWalker : Compiler.Ast.IStatementVisitor<bool>
        {
            private bool _inLoop;
            private int _loopId;
            public Dictionary<int, bool> HandlerLocations = new Dictionary<int, bool>();
            public Dictionary<int, List<int>> LoopLocations = new Dictionary<int, List<int>>();
            private List<int> _loopIds = new List<int>();
            
            private void UpdateLoops(Compiler.Ast.Statement stmt)
            {
                if (_inLoop)
                {
                    if (!LoopLocations.ContainsKey(stmt.Span.Start.Line))                    
                        LoopLocations.Add(stmt.Span.Start.Line, new List<int>(LoopIds));
                    
                }
            }

            public List<int> LoopIds
            {
                get
                {
                    if (_loopIds == null)
                        _loopIds = new List<int>();
                    
                    return _loopIds;
                }
            }
            
            #region Statement Visitor

            public bool Visit(Compiler.Ast.Statement.Assign statement)
            {
                UpdateLoops(statement);
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.Do statement)
            {
                UpdateLoops(statement);
                statement.Body.Statements.Select(x => x.Visit(this)).Run();
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.For statement)
            {
                UpdateLoops(statement);
                WalkLoopBody(statement.Body);
                
                return false;
            }

            private void WalkLoopBody(Compiler.Ast.Block body)
            {
                bool inLoop = _inLoop;

                int loopId = ++_loopId;
                _inLoop = true;
                _loopIds.Add(loopId);

                body.Statements.Select(x => x.Visit(this)).Run();

                _inLoop = inLoop;
                LoopIds.Remove(loopId);
            }

            public bool Visit(Compiler.Ast.Statement.ForIn statement)
            {
                UpdateLoops(statement);
                WalkLoopBody(statement.Body);

                return false;
            }

            public bool Visit(Compiler.Ast.Statement.Function statement)
            {
                UpdateLoops(statement);
                statement.Body.Body.Statements.Select(x => x.Visit(this)).Run();
                return statement.Visit(this);
            }

            public bool Visit(Compiler.Ast.Statement.FunctionCall statement)
            {
                UpdateLoops(statement);
                //statement.Call.Visit(this);   //--Not an IFunctionCallVisitor<T>
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.If statement)
            {
                UpdateLoops(statement);


                foreach (var ifelif in statement.IfList)                
                    ifelif.Body.Statements.Select(x => x.Visit(this)).Run();

                statement.ElseBody.Statements.Select(x => x.Visit(this)).Run();
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.LocalAssign statement)
            {
                UpdateLoops(statement);
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.LocalFunction statement)
            {
                UpdateLoops(statement);
                statement.Body.Body.Statements.Select(x => x.Visit(this)).Run();
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.Repeat statement)
            {
                UpdateLoops(statement);

                WalkLoopBody(statement.Body);

                return false;
            }

            public bool Visit(Compiler.Ast.Statement.While statement)
            {
                UpdateLoops(statement);

                WalkLoopBody(statement.Body);

                return false;
            }

            public bool Visit(Compiler.Ast.Statement.Goto statement)
            {
                UpdateLoops(statement);
                return false;
            }

            public bool Visit(Compiler.Ast.Statement.LabelDecl statement)
            {
                UpdateLoops(statement);
                return false;
            }

            public bool Visit(Compiler.Ast.LastStatement.Break lastStatement)
            {
                UpdateLoops(lastStatement);
                return false;
            }

            public bool Visit(Compiler.Ast.LastStatement.Return lastStatement)
            {
                UpdateLoops(lastStatement);
                return false;
            }


            #endregion
        }
    }
}
