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
        public static Expr WrapStackTrace(Expr expr, LuaTable parentTable, FunctionStack callSite)
        {
            var tempVar = Expr.Variable(typeof(object), "$metamethod_result$");

            var context = Expr.Property(Expr.Constant(parentTable), "Context");

            return Expr.Block(new [] { tempVar },                 
                LuaExpr.FunctionScope(context, callSite.Identifier, Expr.Assign(tempVar, expr)));
        }

        public static Expr WrapStackTrace(Expr expr, CodeContext context, FunctionStack callSite)
        {
            var tempVar = Expr.Variable(typeof(object), "$metamethod_result$");


            return Expr.Block(new[] { tempVar },
                LuaExpr.FunctionScope(context, callSite.ExecScope, callSite.UpScope, new[] { callSite.Identifier }, Expr.Assign(tempVar, expr)));
        }

        public static Expr BinaryOp(Expr context, ExprType operation, DynamicMetaObject left, DynamicMetaObject right)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, ExprType, object, object, object>)LuaOps.BinaryOpMetamethod),
                context,
                Expr.Constant(operation),
                Expr.Convert(left.Expression, typeof(object)),
                Expr.Convert(right.Expression, typeof(object)));
        }

        public static Expr BinaryOp(CodeContext context, ExprType operation, DynamicMetaObject left, DynamicMetaObject right)
        {
            return BinaryOp(Expr.Constant(context, typeof(CodeContext)), operation, left, right);
        }

        public static Expr BinaryOp(LuaTable context, ExprType operation, DynamicMetaObject left, DynamicMetaObject right)
        {
            return BinaryOp(Expr.Property(Expr.Constant(context, typeof(LuaTable)), "Context"), operation, left, right);
        }

        public static Expr Index(Expr context, DynamicMetaObject target, DynamicMetaObject[] indexes)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object, object>)LuaOps.IndexMetamethod),
                context,
                Expr.Convert(target.Expression, typeof(object)),
                Expr.Convert(indexes[0].Expression, typeof(object)));
        }
        
        public static Expr Index(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] indexes)
        {
            return Index(Expr.Constant(context, typeof(CodeContext)), target, indexes);
        }

        public static Expr Index(LuaTable context, DynamicMetaObject target, DynamicMetaObject[] indexes)
        {
            return Index(Expr.Property(Expr.Constant(context, typeof(LuaTable)), "Context"), target, indexes);
        }

        public static Expr Call(Expr context, DynamicMetaObject target, DynamicMetaObject[] args)
        {
            var expression = Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object[], object>)LuaOps.CallMetamethod),
                context,
                Expr.Convert(target.Expression, typeof(object)),
                Expr.NewArrayInit(
                    typeof(object),
                    args.Select(arg => Expr.Convert(arg.Expression, typeof(object)))));

            return expression;
        }

        public static Expr Call(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] args)
        {
            return Call(Expr.Constant(context, typeof(CodeContext)), target, args);
        }

        public static Expr Call(LuaTable context, DynamicMetaObject target, DynamicMetaObject[] args)
        {
            return Call(Expr.Property(Expr.Constant(context, typeof(LuaTable)), "Context"), target, args);
        }

        public static Expr NewIndex(Expr context, DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object, object, object>)LuaOps.NewIndexMetamethod),
                context,
                Expr.Convert(target.Expression, typeof(object)),
                Expr.Convert(indexes[0].Expression, typeof(object)),
                Expr.Convert(value.Expression, typeof(object)));
        }

        public static Expr NewIndex(CodeContext context, DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return NewIndex(Expr.Constant(context, typeof(CodeContext)), target, indexes, value);
        }

        public static Expr NewIndex(LuaTable context, DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return NewIndex(Expr.Property(Expr.Constant(context, typeof(LuaTable)), "Context"), target, indexes, value);
        }

        public static Expr UnaryMinus(Expr context, DynamicMetaObject target)
        {
            return Expr.Invoke(
                Expr.Constant((Func<CodeContext, object, object>)LuaOps.UnaryMinusMetamethod),
                context,
                Expr.Convert(target.Expression, typeof(object)));
        }

        public static Expr UnaryMinus(CodeContext context, DynamicMetaObject target)
        {
            return UnaryMinus(Expr.Constant(context, typeof(CodeContext)), target);
        }

        public static Expr UnaryMinus(LuaTable context, DynamicMetaObject target)
        {
            return UnaryMinus(Expr.Property(Expr.Constant(context, typeof(LuaTable)), "Context"), target);
        }
    }
}
