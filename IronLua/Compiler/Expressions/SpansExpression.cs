using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using Microsoft.Scripting;
using Microsoft.Scripting.Ast;

namespace IronLua.Compiler.Expressions
{
    class SpansExpression : Expression
    {
        private readonly SourceSpan _span;
        private readonly Expression _body;
        private readonly SymbolDocumentInfo _document;

        /// <summary>
        /// Wraps an expression in <see cref="DebugInfoExpression"/>s which indicate the <see cref="SourceSpan"/> where it is defined
        /// </summary>
        public SpansExpression(SymbolDocumentInfo document, SourceSpan span, Expression body)
        {
            _span = span;
            _body = body;
            _document = document;
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
            if (!_span.IsValid || !_span.Start.IsValid || !_span.End.IsValid)
                return _body.Reduce();

#if DEBUG
            return _body.Reduce();
#else
            return Utils.AddDebugInfo(_body.Reduce(), _document, _span.Start, _span.End);     
#endif

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
