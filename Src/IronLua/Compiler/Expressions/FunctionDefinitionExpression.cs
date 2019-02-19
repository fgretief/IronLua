using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Expr = System.Linq.Expressions.Expression;
using System.Linq.Expressions;
using IronLua.Runtime;
using Microsoft.Scripting.Debugging.CompilerServices;

namespace IronLua.Compiler.Expressions
{
    class FunctionDefinitionExpression : Expr
    {
        private readonly CodeContext _context;
        private readonly LambdaExpression _body;
        private readonly LuaScope _scope;
        private readonly string _identifier;

        /// <summary>
        /// Wraps a new function definition, so that the function can be registered as a Lua function with the relevant
        /// CodeContext. This allows the function to benefit from custom execution environments.
        /// </summary>
        public FunctionDefinitionExpression(CodeContext context, LuaScope scope, IEnumerable<string> identifiers, LambdaExpression function)
        {
            _context = context;
            _scope = scope;
            _body = function;

            _identifier = identifiers.Merge((s1, s2) => s1 + "." + s2);
        }

        public override bool CanReduce
        {
            get
            {
                return true;
            }
        }

        public override ExpressionType NodeType
        {
            get
            {
                return ExpressionType.Extension;
            }
        }

        public Expression Body
        {
            get
            {
                return _body;
            }
        }

        public override Expression Reduce()
        {
            var temp = Expr.Variable(typeof(Delegate), "$function$");

            Expr function = _body;

            if (_context.EnableTracing)
            {
                var aliases = _scope.AllLocals().Zip(_scope.GetLocalNames(), (p, n) => new KeyValuePair<ParameterExpression, string>(p, n)).ToDictionary(x => x.Key, y => y.Value);
                var debugInfo = new DebugLambdaInfo(null, _identifier, false, _scope.AllLocals(), aliases, _scope);
                function = _context.Language.DebugContext.TransformLambda(_body).Reduce();
            }
            else
                function = _body.Reduce();

            return  Expression.Block(new[] { temp },
                        Expr.Assign(temp, function),
                        CodeContext.RegisterFunction(_context, temp, _scope),                
                        temp);
        }

        public override Type Type
        {
            get
            {
                return _body.Type;
            }
        }
    }
}
