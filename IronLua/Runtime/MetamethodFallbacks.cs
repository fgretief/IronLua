using System;
using System.Dynamic;
using System.Linq;
using Expr = System.Linq.Expressions.Expression;
using LuaExpr = IronLua.Compiler.Expressions.LuaExpressions;
using ExprType = System.Linq.Expressions.ExpressionType;

namespace IronLua.Runtime
{
    static class MetamethodFallbacks
    {
        public static Expr WrapStackTrace(Expr expr, CodeContext context, FunctionStack callSite)
        {
            var tempVar = Expr.Variable(typeof(object), "$metamethod_result$");
            
            return LuaExpr.FunctionScope(context, callSite.ExecScope, callSite.UpScope, new[] { callSite.Identifier }, Expr.Assign(tempVar, expr));
        }

        public static Expr BinaryOp(CodeContext context, ExprType operation, DynamicMetaObject left, DynamicMetaObject right)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, ExprType, object, object, object>)LuaOps.BinaryOpMetamethod),
                Expr.Constant(context, typeof(LuaContext)),
                Expr.Constant(operation),
                Expr.Convert(left.Expression, typeof(object)),
                Expr.Convert(right.Expression, typeof(object)));
        }

        public static Expr Index(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] indexes)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object, object>)LuaOps.IndexMetamethod),
                Expr.Constant(context, typeof(LuaContext)),
                Expr.Convert(target.Expression, typeof(object)),
                Expr.Convert(indexes[0].Expression, typeof(object)));
        }

        public static Expr Call(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] args)
        {
            var expression = Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object[], object>)LuaOps.CallMetamethod),
                Expr.Constant(context, typeof(LuaContext)),
                Expr.Convert(target.Expression, typeof(object)),
                Expr.NewArrayInit(
                    typeof(object),
                    args.Select(arg => Expr.Convert(arg.Expression, typeof(object)))));

            return expression;
        }

        public static Expr NewIndex(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object, object, object>)LuaOps.NewIndexMetamethod),
                Expr.Constant(context, typeof(LuaContext)),
                Expr.Convert(target.Expression, typeof(object)),
                Expr.Convert(indexes[0].Expression, typeof(object)),
                Expr.Convert(value.Expression, typeof(object)));
        }

        public static Expr UnaryMinus(CodeContext context, DynamicMetaObject target)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object>)LuaOps.UnaryMinusMetamethod),
                Expr.Constant(context, typeof(LuaContext)),
                Expr.Convert(target.Expression, typeof(object)));
        }
    }
}
