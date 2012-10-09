using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using IronLua.Compiler.Ast;
using IronLua.Runtime;
using IronLua.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using Expr = System.Linq.Expressions.Expression;
using ParamExpr = System.Linq.Expressions.ParameterExpression;
using ExprType = System.Linq.Expressions.ExpressionType;
using Expression = IronLua.Compiler.Ast.Expression;
using LuaExpr = IronLua.Compiler.Expressions.LuaExpressions;
using Microsoft.Scripting.Debugging.CompilerServices;

namespace IronLua.Compiler
{
    class Generator : IStatementVisitor<Expr>, IExpressionVisitor<Expr>,
                      IVariableVisitor<VariableVisit>, IPrefixExpressionVisitor<Expr>, IFunctionCallVisitor<Expr>,
                      IArgumentsVisitor<Expr[]>, IFieldVisitor<FieldVisit>
    {
        static readonly Dictionary<BinaryOp, ExprType> binaryExprTypes =
            new Dictionary<BinaryOp, ExprType>
                {
                    {BinaryOp.Or,           ExprType.Or/*Else*/},
                    {BinaryOp.And,          ExprType.And/*Also*/},
                    {BinaryOp.Equal,        ExprType.Equal},
                    {BinaryOp.NotEqual,     ExprType.NotEqual},
                    {BinaryOp.Less,         ExprType.LessThan},
                    {BinaryOp.Greater,      ExprType.GreaterThan},
                    {BinaryOp.LessEqual,    ExprType.LessThanOrEqual},
                    {BinaryOp.GreaterEqual, ExprType.GreaterThanOrEqual},
                    {BinaryOp.Add,          ExprType.Add},
                    {BinaryOp.Subtract,     ExprType.Subtract},
                    {BinaryOp.Multiply,     ExprType.Multiply},
                    {BinaryOp.Divide,       ExprType.Divide},
                    {BinaryOp.Mod,          ExprType.Modulo},
                    {BinaryOp.Power,        ExprType.Power}
                };

        static readonly Dictionary<UnaryOp, ExprType> unaryExprTypes =
            new Dictionary<UnaryOp, ExprType>
                {
                    {UnaryOp.Negate, ExprType.Negate},
                    {UnaryOp.Not,    ExprType.Not}
                };

        LuaScope scope;
        readonly CodeContext context;
        SymbolDocumentInfo _document;

        public Generator(CodeContext context)
        {
            ContractUtils.RequiresNotNull(context, "context");
            this.context = context;
        }
        
        public Expression<Func<IDynamicMetaObjectProvider, dynamic>> Compile(Block block, SourceUnit sourceUnit = null)
        {
            if (sourceUnit != null)
                _document = sourceUnit.Document ?? Expr.SymbolDocument("(chunk)", sourceUnit.LanguageContext.LanguageGuid, sourceUnit.LanguageContext.VendorGuid);

            scope = LuaScope.CreateRoot(context);
            ParameterExpression dlrGlobals = scope.AddParameter("$DLR_Scope$", typeof(IDynamicMetaObjectProvider)); //Expr.Parameter(typeof(IDynamicMetaObjectProvider), "$DLR_Scope$");
            

            var blockExpr = Visit(block).Reduce();

            var expr = Expr.Block(
                LuaExpr.ExecutionContext(context, dlrGlobals, sourceUnit, 
                    //LuaTrace.MakePushFunctionCall(context, new LuaTrace.FunctionCall(block.Span, LuaTrace.FunctionType.Chunk, "main chunk", _document.FileName)),
                    blockExpr
                    //LuaTrace.MakePopFunctionCall(context),
                ),
                Expr.Label(scope.GetReturnLabel(), Expr.Constant(null)));

            var ex = Expr.Parameter(typeof(Exception),"$ex$");

            var safeExpr = Expr.TryCatch(expr, Expr.Catch(ex, Expr.Block(CodeContext.OnExceptionThrown(context, ex), Expr.Constant(null))));

            
            return scope.ToLambda<Func<IDynamicMetaObjectProvider, dynamic>>(expr);
            //return Expr.Lambda<Func<IDynamicMetaObjectProvider, dynamic>>(safeExpr, dlrGlobals);
        }

        public Expression<Func<dynamic>> CompileInline(Block block, LuaScope evaluationScope, IDynamicMetaObjectProvider runtimeScope, SourceUnit sourceUnit = null)
        {
            if (sourceUnit != null)
                _document = sourceUnit.Document ?? Expr.SymbolDocument("(chunk)", sourceUnit.LanguageContext.LanguageGuid, sourceUnit.LanguageContext.VendorGuid);

            ParameterExpression dlrGlobals = Expr.Parameter(typeof(IDynamicMetaObjectProvider), "$DLR_Scope$");
            scope = evaluationScope.GetParent() ?? evaluationScope;

            var blockExpr = Visit(block);
            var expr = Expr.Block(new [] { dlrGlobals }, 
                Expr.Assign(dlrGlobals, Expr.Constant(runtimeScope)), 
                blockExpr, 
                Expr.Label(scope.GetReturnLabel(), 
                Expr.Constant(null)));

            return Expr.Lambda<Func<dynamic>>(expr);
        }

        Expr Visit(Block block)
        {
            var statementExprs = new List<Expr>();

            //statementExprs.Add(LuaTrace.MakeUpdateCurrentEvaluationScope(context, scope));
            //statementExprs.Add(LuaTrace.MakeOnScopeEnter(context));

            if (block.Statements.Count > 0)                
                statementExprs.AddRange(block.Statements.Select(s => LuaExpr.SourceSpan(_document, s.Span, s.Visit(this))));
               
            //statementExprs.Add(LuaTrace.MakeOnScopeLeave(context));
            //statementExprs.Add(LuaTrace.MakeUpdateCurrentEvaluationScope(context, parentScope));

            if (statementExprs.Count == 0)
                return LuaExpr.SourceSpan(_document, block.Span, Expr.Constant(null)).Reduce();
            else if (statementExprs.Count == 1 && scope.LocalsCount == 0)
                // Don't output blocks if we don't declare any locals and it's a single statement
                return LuaExpr.SourceSpan(_document, block.Span, statementExprs.First()).Reduce();
            else
                return LuaExpr.Scope(context, scope, LuaExpr.SourceSpan(_document, block.Span, Expr.Block(scope.GetLocals(), statementExprs))).Reduce();
            
        }

        Expr Visit(FunctionName name, FunctionBody function)
        {
            var parentScope = scope;
            try
            {
                scope = LuaScope.CreateFunctionChildFrom(scope, name.Identifiers.Aggregate((x,y) => x + "." + y));

                var parameters = new List<ParamExpr>();
                if (name.HasTableMethod)
                    parameters.Add(scope.AddLocal("self"));
                parameters.AddRange(
                    function.Parameters.Select(p => scope.AddParameter(p)));
                if (function.HasVarargs)
                    parameters.Add(scope.AddLocal(Constant.VARARGS, typeof(Varargs)));
                
                var bodyExpr = Expr.Block(
                                Visit(function.Body),
                                Expr.Label(scope.GetReturnLabel(), Expr.Constant(null)));

                var funcName = Constant.FUNCTION_PREFIX + name.Identifiers.Last();

                return scope.ToLambda(LuaExpr.FunctionScope(context, scope, parentScope, name.Identifiers, bodyExpr));

                //return LuaExpr.FunctionDefinitionExpression(context, scope, name.Identifiers, 
                //    scope.ToLambda(LuaExpr.FunctionScope(context, scope, parentScope, name.Identifiers, bodyExpr)),
                //    funcName, true, parameters);
            }
            finally
            {
                scope = parentScope;
            }
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.Assign statement)
        {
            var variables = statement.Variables.Select(v => v.Visit(this)).ToList();
            var values = WrapWithVarargsFirst(statement.Values);
            var access = variables.Select(v => new VariableAccess(v.Identifier, AccessType.GlobalSet)).ToList();

            var lastValue = statement.Values.Last();
            if (lastValue.IsVarargs() || lastValue.IsFunctionCall())
                return LuaExpr.SourceSpan(_document,statement.Span, VarargsExpandAssignment(variables, values));

            return LuaExpr.SourceSpan(_document, statement.Span, AssignWithTemporaries(variables, values, Assign, access));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.Do statement)
        {
            var parentScope = scope;
            try
            {
                scope = LuaScope.CreateChildFrom(parentScope);
                return LuaExpr.SourceSpan(_document, statement.Span, Visit(statement.Body));
            }
            finally
            {
                scope = parentScope;
            }
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.For statement)
        {
            var parentScope = scope;
            try
            {
                scope = LuaScope.CreateChildFrom(parentScope);

                var step = statement.Step == null
                               ? new Expression.Number(1.0).Visit(this)
                               : ExprHelpers.ConvertToNumber(context, statement.Step.Visit(this));

                var loopVariable = scope.AddLocal(statement.Identifier);
                var varVar = scope.AddHidden("current", typeof(double)); //Expr.Variable(typeof(double));
                var limitVar = scope.AddHidden("limit", typeof(double)); //Expr.Variable(typeof(double));
                var stepVar = scope.AddHidden("step", typeof(double));  //Expr.Variable(typeof(double));

                var breakConditionExpr = ForLoopBreakCondition(limitVar, stepVar, varVar);
                                
                return LuaExpr.SourceSpan(_document, statement.Span, 
                        Expr.Block(
                            new[] { loopVariable, varVar, limitVar, stepVar },
                            Expr.Assign(varVar, ExprHelpers.ConvertToNumber(context, statement.Var.Visit(this))),
                            Expr.Assign(limitVar, ExprHelpers.ConvertToNumber(context, statement.Limit.Visit(this))),
                            Expr.Assign(stepVar, step),
                            ExprHelpers.CheckNumberForNan(context, varVar, String.Format(ExceptionMessage.FOR_VALUE_NOT_NUMBER, "inital value")),
                            ExprHelpers.CheckNumberForNan(context, limitVar, String.Format(ExceptionMessage.FOR_VALUE_NOT_NUMBER, "limit")),
                            ExprHelpers.CheckNumberForNan(context, stepVar, String.Format(ExceptionMessage.FOR_VALUE_NOT_NUMBER, "step")),
                            ForLoop(statement, stepVar, loopVariable, varVar, breakConditionExpr)));
            }
            finally
            {
                scope = parentScope;
            }
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.ForIn statement)
        {

            var parentScope = scope;
            scope = LuaScope.CreateChildFrom(scope);

            var iterFuncVar = scope.AddHidden("itterator"); //Expr.Variable(typeof(object));
            var iterStateVar = scope.AddHidden("state"); //Expr.Variable(typeof(object));
            var iterableVar = scope.AddHidden("current"); //Expr.Variable(typeof(object));
            var iterVars = new[] {iterFuncVar, iterStateVar, iterableVar};

            var valueExprs = statement.Values.Select(v => Expr.Convert(v.Visit(this), typeof(object)));
            var assignIterVars = VarargsExpandAssignment(iterVars, valueExprs);

            var locals = statement.Identifiers.Select(id => scope.AddLocal(id)).ToList();

            var invokeIterFunc = Expr.Dynamic(context.CreateInvokeBinder(new CallInfo(2)),
                                              typeof(object), iterFuncVar, iterStateVar, iterableVar);
            var loop =
                Expr.Loop(
                    Expr.Block(
                        locals,
                        VarargsExpandAssignment(
                            locals,
                            new[] {invokeIterFunc}),
                        Expr.IfThen(Expr.Equal(locals[0], Expr.Constant(null)), Expr.Break(scope.BreakLabel())),
                        Expr.Assign(iterableVar, locals[0]),
                        LuaExpr.SourceSpan(_document, statement.Body.Span, Visit(statement.Body))),
                    scope.BreakLabel());

            var expr = LuaExpr.SourceSpan(_document, statement.Span,Expr.Block(iterVars,assignIterVars, loop));

            scope = parentScope;
            return expr;
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.Function statement)
        {
            Expr bodyExpr = Visit(statement.Name, statement.Body);

            if (statement.IsLocal)
            {
                var localExpr = scope.AddLocal(statement.Name.Identifiers.Last());
                return LuaExpr.SourceSpan(_document, statement.Span, Expr.Assign(localExpr, bodyExpr));
            }

            return LuaExpr.SourceSpan(_document, statement.Span, AssignToIdentifierList(statement.Name.Identifiers, bodyExpr));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.FunctionCall statement)
        {
            return LuaExpr.SourceSpan(_document, statement.Span, statement.Call.Visit(this));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.If statement)
        {
            var binder = context.CreateConvertBinder(typeof(bool), false);
            Expr expr = statement.ElseBody != null
                     ? LuaExpr.SourceSpan(_document, statement.Span, Visit(statement.ElseBody))
                     : Expr.Block(Expr.Empty());

            var list = statement.IfList;
            for (int i = list.Count - 1; i >= 0; --i)
            {
                var ifThen = list[i];
                expr = Expr.IfThenElse(
                            Expr.Dynamic(binder, typeof(bool), LuaExpr.SourceSpan(_document, statement.Span, ifThen.Test.Visit(this))),
                         LuaExpr.SourceSpan(_document, statement.Span, Visit(ifThen.Body)),
                         expr);
            }

            return expr;
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.LocalAssign statement)
        {
            var values = (statement.Values != null && statement.Values.Count > 0) 
                       ? WrapWithVarargsFirst(statement.Values) : new List<Expr>();
            var locals = statement.Identifiers.Select(v => scope.AddLocal(v)).ToList();
            var access = statement.Identifiers.Select(v => new VariableAccess(v, AccessType.LocalSet)).ToList();

            if (statement.Values != null && statement.Values.Count > 0)
            {
                var lastValue = statement.Values.Last();
                if (lastValue.IsVarargs() || lastValue.IsFunctionCall())
                    return LuaExpr.SourceSpan(_document, statement.Span, VarargsExpandAssignment(locals, values));
            }

            return LuaExpr.SourceSpan(_document, statement.Span, AssignWithTemporaries(locals, values, Expr.Assign, access));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.LocalFunction statement)
        {
            var localExpr = scope.AddLocal(statement.Name.Identifiers.Last());

            var bodyExpr = Visit(statement.Name, statement.Body);

            return LuaExpr.SourceSpan(_document, statement.Span,
                    Expr.Assign(localExpr, bodyExpr));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.Repeat statement)
        {
            var stats = statement.Body.Statements;

            // Temporarily rewrite the AST so that the test expression 
            // can be evaluated in the same scope as the body.
            stats.Add(new Statement.If(statement.Test, 
                new Block(new LastStatement.Break() { Span = statement.Test.Span }))
            {
                Span = statement.Test.Span
            });

            var breakLabel = scope.BreakLabel();
            var expr = Expr.Loop(
                LuaExpr.SourceSpan(_document, statement.Body.Span, Visit(statement.Body)
                    ),
                breakLabel);

            // Remove the temporary statement we added.
            stats.RemoveAt(stats.Count - 1);
            return LuaExpr.SourceSpan(_document, statement.Span, expr);
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.While statement)
        {
            var breakLabel = scope.BreakLabel();
            var stat = Expr.Loop(
                Expr.IfThenElse(
                    Expr.Dynamic(
                        context.CreateConvertBinder(typeof(bool), false),
                        typeof(bool),
                        LuaExpr.SourceSpan(_document, statement.Test.Span, statement.Test.Visit(this))),
                    LuaExpr.SourceSpan(_document, statement.Body.Span, Visit(statement.Body)),
                    Expr.Break(breakLabel)),
                breakLabel);
            return LuaExpr.SourceSpan(_document, statement.Span, stat);
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.Goto statement)
        {
            ContractUtils.RequiresNotNull(statement.LabelName, "LabelName");

            if (statement.LabelName == "@break")
            {
                return Expr.Break(scope.BreakLabel());
            }

            return LuaExpr.SourceSpan(_document, statement.Span, Expr.Goto(scope.AddLabel(statement.LabelName)));
        }

        Expr IStatementVisitor<Expr>.Visit(Statement.LabelDecl statement)
        {
            return Expr.Label(scope.AddLabel(statement.LabelName));
        }

        Expr IStatementVisitor<Expr>.Visit(LastStatement.Break statement)
        {
            return LuaExpr.SourceSpan(_document, statement.Span, Expr.Break(scope.BreakLabel()));
        }

        Expr IStatementVisitor<Expr>.Visit(LastStatement.Return statement)
        {
            var returnLabel = scope.GetReturnLabel();

            if (returnLabel == null)
                return Expr.Empty();

            var returnValues = statement.Values
                .Select(expr => Expr.Convert(expr.Visit(this), typeof(object))).ToArray();

            if (returnValues.Length == 0)
                return Expr.Return(returnLabel, Expr.Constant(null)); // hack!
                //return Expr.Return(returnLabel); // FIXME: how do we get the return label to be void in this case?
            if (returnValues.Length == 1)
                return Expr.Return(returnLabel, returnValues[0]);

            return LuaExpr.SourceSpan(_document, statement.Span, Expr.Return(
                        returnLabel,
                        Expr.New(MemberInfos.NewVarargs, Expr.NewArrayInit(typeof(object), returnValues))));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.BinaryOp expression)
        {
            var left = expression.Left.Visit(this);
            var right = expression.Right.Visit(this);
            ExprType operation;
            if (binaryExprTypes.TryGetValue(expression.Operation, out operation))
                return LuaExpr.SourceSpan(_document, expression.Span, Expr.Dynamic(context.CreateBinaryOperationBinder(operation),
                                    typeof(object), left, right));

            // BinaryOp have to be Concat at this point which can't be represented as a binary operation in the DLR
            return
                LuaExpr.SourceSpan(_document, expression.Span, Expr.Invoke(
                        Expr.Constant((Func<CodeContext, object, object, object>)LuaOps.Concat),
                        Expr.Constant(context),
                        Expr.Convert(left, typeof(object)),
                        Expr.Convert(right, typeof(object))));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Boolean expression)
        {
            return Expr.Constant(expression.Literal);
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Function expression)
        {
            return LuaExpr.SourceSpan(_document, expression.Span, 
                Visit(new FunctionName("lambda_" + Guid.NewGuid().ToString()), expression.Body));            
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Nil expression)
        {
            return Expr.Constant(null, typeof(DynamicNull));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Number expression)
        {
            return Expr.Constant(expression.Literal, typeof(object));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Prefix expression)
        {
            return expression.Expression.Visit(this);
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.String expression)
        {
            return Expr.Constant(expression.Literal, typeof(object));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Table expression)
        {
            var newTableExpr = Expr.New(MemberInfos.NewLuaTable, Expr.Constant(context, typeof(CodeContext)));
            var tableVar = Expr.Variable(typeof(LuaTable));
            var tableAssign = Expr.Assign(tableVar, newTableExpr);

            double intIndex = 1;
            var fieldInitsExprs = expression.Fields
                .Select(f => TableSetValue(tableVar, f.Visit(this), ref intIndex))
                .ToArray();

            var exprs = new Expr[fieldInitsExprs.Length + 2];
            exprs[0] = tableAssign;
            exprs[exprs.Length - 1] = tableVar;
            Array.Copy(fieldInitsExprs, 0, exprs, 1, fieldInitsExprs.Length);

            return LuaExpr.SourceSpan(_document, expression.Span, Expr.Block(new [] {tableVar}, exprs));
        }

        Expr TableSetValue(Expr table, FieldVisit field, ref double intIndex)
        {
            switch (field.Type)
            {
                case FieldVisitType.Implicit:
                    return Expr.Call(table, MemberInfos.LuaTableSetValue,
                                     Expr.Constant(intIndex++, typeof(object)),
                                     Expr.Convert(field.Value, typeof(object)));
                case FieldVisitType.Explicit:
                    return Expr.Call(table, MemberInfos.LuaTableSetValue,
                                     Expr.Convert(field.Member, typeof(object)),
                                     Expr.Convert(field.Value, typeof(object)));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.UnaryOp expression)
        {
            var operand = expression.Operand.Visit(this);
            ExprType operation;
            if (unaryExprTypes.TryGetValue(expression.Operation, out operation))
                return LuaExpr.SourceSpan(_document, expression.Span, Expr.Dynamic(context.CreateUnaryOperationBinder(operation),
                                    typeof(object), operand));

            // UnaryOp have to be Length at this point which can't be represented as a unary operation in the DLR
            return LuaExpr.SourceSpan(_document, expression.Span, Expr.Invoke(
                        Expr.Constant((Func<CodeContext, object, object>)LuaOps.Length),
                        Expr.Constant(context),
                        Expr.Convert(operand, typeof(object))));
        }

        Expr IExpressionVisitor<Expr>.Visit(Expression.Varargs expression)
        {
            ParamExpr param;
            if (scope.TryGetLocal(Constant.VARARGS, out param))
                return param;
            return Expr.Constant(null);
        }

        VariableVisit IVariableVisitor<VariableVisit>.Visit(Variable.Identifier variable)
        {
            return VariableVisit.CreateIdentifier(variable.Value);
        }

        VariableVisit IVariableVisitor<VariableVisit>.Visit(Variable.MemberExpr variable)
        {
            return VariableVisit.CreateMemberExpr(
                variable.Prefix.Visit(this),
                variable.Member.Visit(this));
        }

        VariableVisit IVariableVisitor<VariableVisit>.Visit(Variable.MemberId variable)
        {
            return VariableVisit.CreateMemberId(
                variable.Prefix.Visit(this),
                variable.Member);
        }

        Expr IPrefixExpressionVisitor<Expr>.Visit(PrefixExpression.Expression prefixExpr)
        {
            return prefixExpr.Expr.Visit(this);
        }

        Expr IPrefixExpressionVisitor<Expr>.Visit(PrefixExpression.FunctionCall prefixExpr)
        {
            return prefixExpr.Call.Visit(this);
        }


        Expr CreateGlobalGetMember(string identifier, ScopeStorage globals, Expr libraries, LuaScope scope)
        {
            var temp = Expr.Parameter(typeof(object));

            var ex = Expr.Parameter(typeof(Exception), "$ex$");
            var target = Expr.Parameter(typeof(IDynamicMetaObjectProvider), "$global_get_target$");


            Func<Expr, Expr> makeVariableAccess = t => LuaExpr.VariableAccess(context, Expr.Assign(temp, Expr.Dynamic(context.CreateGetMemberBinder(identifier, false),
                                    typeof(object), t)), new VariableAccess(identifier, AccessType.GlobalGet));

            if (globals.HasValue(identifier, false))
                return Expr.Block(
                    typeof(object),
                    scope.AllLocals().Add(temp),
                    Expr.Assign(temp, Expr.Constant(null)),
                    Expr.TryCatch(makeVariableAccess(Expr.Constant(globals)),
                                    Expr.Catch(ex, Expr.Block(CodeContext.OnExceptionThrown(context, ex), Expr.Constant(null)))),
                    temp);
                        

            var scopeGlobals = scope.GetDlrGlobals();

            //We can assume that base libraries are static here, for a runtime boost in performance
            //Basically, if we only access the libraries table each time instead of checking if the global
            //table has the library name, we can shift 3 expensive operations into 1 (+1 compile time one).
            //DOWNSIDE: If you redefine a library's name (e.g. os = null) it will have no effect (unless we
            //          implement a way of allowing you to change global library definitions, which we will need
            //          to reset at the begining of each run).

            if(context.IsLibraryIdentifier(identifier))
                return Expr.Block(
                    typeof(object),
                    scope.AllLocals().Add(temp),
                    Expr.Assign(temp, Expr.Constant(null)),
                    makeVariableAccess(libraries),
                    //Expr.TryCatch(makeVariableAccess(libraries),
                    //                Expr.Catch(ex, Expr.Block(CodeContext.OnExceptionThrown(context, ex), Expr.Constant(null)))),
                    temp);
            else
                return Expr.Block(
                    typeof(object),
                    scope.AllLocals().Add(temp),
                    Expr.Assign(temp, Expr.Constant(null)),
                    makeVariableAccess(scopeGlobals),
                    //Expr.TryCatch(makeVariableAccess(scopeGlobals),
                    //                Expr.Catch(ex, Expr.Block(CodeContext.OnExceptionThrown(context, ex), Expr.Constant(null)))),
                    temp);

            //Does runtime checking for where we should grab an identifier from, SLOW

            //return Expr.Block(
            //        typeof(object),
            //        scope.AllLocals().Add(temp).Add(target),
            //        Expr.Assign(temp, Expr.Constant(null)),
            //        Expr.Assign(target, Expr.Condition(Expr.Equal(makeVariableAccess(scopeGlobals), Expr.Constant(null)), libraries, scopeGlobals, typeof(IDynamicMetaObjectProvider))),
            //        Expr.TryCatch(makeVariableAccess(target),
            //                        Expr.Catch(ex, Expr.Block(CodeContext.OnExceptionThrown(context, ex), Expr.Constant(null)))),
            //        temp);

            
        }

        Expr IPrefixExpressionVisitor<Expr>.Visit(PrefixExpression.Variable prefixExpr)
        {
            var variable = prefixExpr.Var.Visit(this);
            switch (variable.Type)
            {
                case VariableType.Identifier:
                    ParamExpr local;
                    if (scope.TryGetLocal(variable.Identifier, out local))
                        return local;

                    return CreateGlobalGetMember(variable.Identifier, context.Language.DomainManager.Globals.Storage, CodeContext.GetLibraries(context), scope);
                    
                    //return Expr.Dynamic(context.CreateGetMemberBinder(variable.Identifier, false),
                    //                    typeof(object), Expr.Constant(context.Globals));

                    //return Expr.Dynamic(context.CreateGetMemberBinder(variable.Identifier, false),
                    //                    typeof(object), scope.GetDlrGlobals());

                case VariableType.MemberId:
                    return LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateGetMemberBinder(variable.Identifier, false),
                                        typeof(object), variable.Object), new VariableAccess(variable.Identifier, AccessType.MemberGet));

                case VariableType.MemberExpr:
                    return LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateGetIndexBinder(new CallInfo(1)),
                                        typeof(object), variable.Object, variable.Member), new VariableAccess(variable.Identifier, AccessType.IndexGet));

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Expr IFunctionCallVisitor<Expr>.Visit(FunctionCall.Normal functionCall)
        {
            var funcExpr = functionCall.Prefix.Visit(this);
            var argExprs = functionCall.Arguments.Visit(this);

            var invokeArgs = new Expr[argExprs.Length + 1];
            invokeArgs[0] = funcExpr;
            Array.Copy(argExprs, 0, invokeArgs, 1, argExprs.Length);
                        
            return Expr.Dynamic(context.CreateInvokeBinder(new CallInfo(argExprs.Length)),
                                typeof(object), invokeArgs);
        }

        Expr IFunctionCallVisitor<Expr>.Visit(FunctionCall.Table functionCall)
        {
            var tableExpr = functionCall.Prefix.Visit(this);
            var tableVar = Expr.Variable(typeof(object));
            var assignExpr = Expr.Assign(tableVar, tableExpr);

            var tableGetMember = Expr.Dynamic(context.CreateGetMemberBinder(functionCall.MethodName, false),
                                              typeof(object), tableVar);

            var argExprs = functionCall.Arguments.Visit(this);
            var invokeArgs = new Expr[argExprs.Length + 2];
            invokeArgs[0] = tableGetMember;
            invokeArgs[1] = tableVar;
            Array.Copy(argExprs, 0, invokeArgs, 2, argExprs.Length);

            var invokeExpr = Expr.Dynamic(context.CreateInvokeBinder(new CallInfo(argExprs.Length)),
                                          typeof(object), invokeArgs);

            return
                Expr.Block(
                    new[] {tableVar},
                    assignExpr,
                    invokeExpr);
        }

        Expr[] IArgumentsVisitor<Expr[]>.Visit(Arguments.Normal arguments)
        {
            return arguments.Arguments.Select(e => e.Visit(this)).ToArray();
        }

        Expr[] IArgumentsVisitor<Expr[]>.Visit(Arguments.String arguments)
        {
            return new[] {arguments.Literal.Visit(this)};
        }

        Expr[] IArgumentsVisitor<Expr[]>.Visit(Arguments.Table arguments)
        {
            return new[] {arguments.Value.Visit(this)};
        }

        FieldVisit IFieldVisitor<FieldVisit>.Visit(Field.MemberExpr field)
        {
            return FieldVisit.CreateExplicit(field.Member.Visit(this), field.Value.Visit(this));
        }

        FieldVisit IFieldVisitor<FieldVisit>.Visit(Field.MemberId field)
        {
            return FieldVisit.CreateExplicit(Expr.Constant(field.Member), field.Value.Visit(this));
        }

        FieldVisit IFieldVisitor<FieldVisit>.Visit(Field.Normal field)
        {
            return FieldVisit.CreateImplicit(field.Value.Visit(this));
        }

        Expr AssignWithTemporaries<T>(List<T> variables, List<Expr> values, Func<T, Expr, Expr> assigner, List<VariableAccess> access)
        {
            // Assign values to temporaries
            var tempVariables = values.Select(expr => Expr.Variable(expr.Type, "assign_temp")).ToList();
            var tempAssigns = tempVariables.Zip(values, Expr.Assign);

            // Shrink or pad temporary's list with nil to match variables's list length
            // and cast temporaries to object type
            var tempVariablesResized = tempVariables
                .Resize(variables.Count, new Expression.Nil().Visit(this))
                .Select(tempVar => Expr.Convert(tempVar, typeof(object)));

            // Assign temporaries to globals
            var realAssigns = variables
                .Zip(tempVariablesResized, assigner)
                .Zip(access, (assign, varAccess) => LuaExpr.VariableAccess(context, assign, varAccess));
            return Expr.Block(tempVariables, tempAssigns.Concat(realAssigns));
        }
        
        Expr CreateGlobalSetMember(string identifier, LuaScope scope, Expr value)
        {
            var scopeAssign = Expr.Dynamic(context.CreateSetMemberBinder(identifier, false),
                                    typeof(object), scope.GetDlrGlobals(), value);

            var scopeDelete = Expr.TryCatch(Expr.Dynamic(context.CreateDeleteMemberBinder(identifier, false),
                                    typeof(void), scope.GetDlrGlobals()), Expr.Catch(Expr.Parameter(typeof(Exception)), Expr.Empty()));

            return Expr.Condition(Expr.Equal(value, Expr.Constant(null)), Expr.Block(scopeDelete, Expr.Constant(null)), scopeAssign);
        }

        Expr Assign(VariableVisit variable, Expr value)
        {
            switch (variable.Type)
            {
                case VariableType.Identifier:
                    ParamExpr local;
                    if (scope.TryGetLocal(variable.Identifier, out local))
                        return Expr.Assign(local, value);


                    return CreateGlobalSetMember(variable.Identifier, scope, value);

                    //return Expr.Dynamic(context.CreateSetMemberBinder(variable.Identifier, false),
                    //                    typeof(object), Expr.Constant(context.Globals), value);

                    //return Expr.Dynamic(context.CreateSetMemberBinder(variable.Identifier, false),
                    //                    typeof(object), scope.GetDlrGlobals(), value);

                case VariableType.MemberId:
                    return LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateSetMemberBinder(variable.Identifier, false),
                                        typeof(object), variable.Object, value), new VariableAccess(variable.Identifier,AccessType.MemberSet));

                case VariableType.MemberExpr:
                    return LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateSetIndexBinder(new CallInfo(1)),
                                        typeof(object), variable.Object, variable.Member, value), 
                                        new VariableAccess(variable.Identifier, AccessType.IndexSet));

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Expr AssignToIdentifierList(List<string> identifiers, Expr value)
        {
            Expr expr;
            var firstId = identifiers.First();

            ParamExpr local;
            bool isLocal = scope.TryGetLocal(firstId, out local);

            // If there is just a single identifier return the assignment to it
            if (identifiers.Count == 1)
            {
                if (isLocal)
                    return LuaExpr.VariableAccess(context, Expr.Assign(local, value), new VariableAccess(identifiers[0], AccessType.LocalSet));

                return CreateGlobalSetMember(firstId, scope, value);
                //return Expr.Dynamic(context.CreateSetMemberBinder(firstId, false),
                //                            typeof(object),
                //                            Expr.Constant(context.Globals),
                //                            value);
            }

            // First element can be either a local or global variable
            if (isLocal)
                expr = LuaExpr.VariableAccess(context, local, new VariableAccess(identifiers[0], AccessType.LocalGet));
            else
                expr = CreateGlobalGetMember(firstId, context.EngineGlobals.Storage, CodeContext.GetLibraries(context), scope);
                    //Expr.Dynamic(context.CreateGetMemberBinder(firstId, false),
                    //                        typeof(object),
                    //                        Expr.Constant(context.Globals));

            // Loop over all elements except the first and the last and perform get member on them
            expr = identifiers
                .Skip(1).Take(identifiers.Count - 2)
                .Aggregate(expr, (e, id) =>
                    LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateGetMemberBinder(id, false),
                                         typeof (object), e), new VariableAccess(id, AccessType.MemberGet)));

            // Do the assignment on the last identifier
            return LuaExpr.VariableAccess(context, Expr.Dynamic(context.CreateSetMemberBinder(identifiers.Last(), false),
                                        typeof(object), expr, value), new VariableAccess(identifiers.Last(), AccessType.MemberSet));
        }

        List<Expr> WrapWithVarargsFirst(List<Expression> values)
        {
            // Try to wrap all values except the last with varargs select
            return values
                .Take(values.Count - 1)
                .Select(TryWrapWithVarargsFirst)
                .Add(values.Last().Visit(this))
                .ToList();
        }

        Expr TryWrapWithVarargsFirst(Expression value)
        {
            // If expr is a varargs or function call expression we need to return the first element in
            // the Varargs list if the value is of type Varargs or do nothing
            if (value.IsVarargs())
            {
                var varargsExpr = Expr.Convert(value.Visit(this), typeof(Varargs));

                return Expr.Call(varargsExpr, MemberInfos.VarargsFirst);
            }

            var valueExpr = Expr.Convert(value.Visit(this), typeof(object));

            if (value.IsFunctionCall())
            {
                var variable = Expr.Variable(typeof(object));

                return
                    Expr.Block(
                        new[] { variable },
                        Expr.Assign(variable, valueExpr),
                        Expr.Condition(
                            Expr.TypeIs(variable, typeof(Varargs)),
                            Expr.Call(Expr.Convert(variable, typeof(Varargs)), MemberInfos.VarargsFirst),
                            variable));
            }

            return valueExpr;
        }

        Expr VarargsExpandAssignment(IEnumerable<ParameterExpression> locals, IEnumerable<Expr> values)
        {
            return Expr.Invoke(
                Expr.Constant((Action<IRuntimeVariables, object[]>)LuaOps.VarargsAssign),
                Expr.RuntimeVariables(locals),
                Expr.NewArrayInit(
                    typeof(object),
                    values.Select(value => Expr.Convert(value, typeof(object)))));
        }

        Expr VarargsExpandAssignment(List<VariableVisit> variables, IEnumerable<Expr> values)
        {
            var valuesVar = Expr.Variable(typeof(object[]));
            var invokeExpr =
                Expr.Invoke(
                    Expr.Constant((Func<int, object[], object[]>)LuaOps.VarargsAssign),
                    Expr.Constant(variables.Count),
                    Expr.NewArrayInit(
                        typeof(object),
                        values.Select(value => Expr.Convert(value, typeof(object)))));
            var valuesAssign = Expr.Assign(valuesVar, invokeExpr);

            var varAssigns = variables
                .Select((var, i) => Assign(var, Expr.ArrayIndex(valuesVar, Expr.Constant(i))))
                .ToArray();

            var exprs = new Expr[varAssigns.Length + 1];
            exprs[0] = valuesAssign;
            Array.Copy(varAssigns, 0, exprs, 1, varAssigns.Length);

            return
                Expr.Block(
                    new[] { valuesVar },
                    exprs);
        }

        LoopExpression ForLoop(Statement.For statement, ParameterExpression stepVar, ParameterExpression loopVariable,
                               ParameterExpression varVar, BinaryExpression breakConditionExpr)
        {
            var loopExpr = Expr.Loop(
                            LuaExpr.SourceSpan(_document, statement.Span, Expr.Block(
                                Expr.IfThen(breakConditionExpr, Expr.Break(scope.BreakLabel())),
                                Expr.Assign(loopVariable, Expr.Convert(varVar, typeof(object))),
                                LuaExpr.SourceSpan(_document, statement.Body.Span, Visit(statement.Body)),
                                Expr.AddAssign(varVar, stepVar))),
                            scope.BreakLabel());

            return loopExpr;
        }

        BinaryExpression ForLoopBreakCondition(ParameterExpression limitVar, ParameterExpression stepVar, ParameterExpression varVar)
        {
            var breakConditionExpr =
                Expr.MakeBinary(
                    ExprType.OrElse,
                    Expr.MakeBinary(
                        ExprType.AndAlso,
                        Expr.GreaterThan(stepVar, Expr.Constant(0.0)),
                        Expr.GreaterThan(varVar, limitVar)),
                    Expr.MakeBinary(
                        ExprType.AndAlso,
                        Expr.LessThanOrEqual(stepVar, Expr.Constant(0.0)),
                        Expr.LessThan(varVar, limitVar)));
            return breakConditionExpr;
        }
    }
}
