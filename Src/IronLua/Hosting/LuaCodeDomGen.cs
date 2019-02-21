#define FEATURE_CODEDOM

using System;
using Microsoft.Scripting.Runtime;
using System.CodeDom;
using System.Collections.Generic;

#if FEATURE_CODEDOM
namespace IronLua.Hosting
{
    class LuaCodeDomGen : CodeDomCodeGen
    {
        // Tracks the indents found in user provided snippets
        private Stack<int> _indents = new Stack<int>(new[] { 0 });

        // Tracks the indent level that is required by generated
        // nesting levels (that the user can't see)
        private int _generatedIndent;

        protected override void WriteFunctionDefinition(CodeMemberMethod func)
        {
            if (func.Attributes.HasFlag(MemberAttributes.Private))
                Writer.Write("local ");

            Writer.Write("function ");
            Writer.Write(func.Name ?? string.Empty);
            Writer.Write('(');
            if (func.Parameters.Count > 0)
            {
                Writer.Write(func.Parameters[0].Name);
                for (var i = 1; i < func.Parameters.Count; ++i)
                {
                    Writer.Write(",");
                    Writer.Write(func.Parameters[i].Name);
                }
            }
            Writer.Write(')');
            Writer.Write('\n');

            var baseIndent = _indents.Peek();
            _generatedIndent += 4;

            foreach (CodeStatement stmt in func.Statements)
            {
                WriteStatement(stmt);
            }

            _generatedIndent -= 4;
            while (_indents.Peek() > baseIndent)
            {
                _indents.Pop();
            }

            Writer.Write("end");
            Writer.Write('\n');
        }

        protected override void WriteSnippetStatement(CodeSnippetStatement s)
        {
            string snippet = s.Value;

            // Finally, append the snippet. Make sure that it is indented properly if
            // it has nested newlines
            Writer.Write(IndentSnippetStatement(snippet));
            Writer.Write('\n');

            // See if the snippet changes our indent level
            var lastLine = snippet.Substring(snippet.LastIndexOf('\n') + 1);
            // If the last line is only whitespace, then we have a new indent level
            if (string.IsNullOrEmpty(lastLine.Trim('\t', ' ')))
            {
                lastLine = lastLine.Replace("\t", "        ");
                int indentLen = lastLine.Length;
                if (indentLen > _indents.Peek())
                {
                    _indents.Push(indentLen);
                }
                else
                {
                    while (indentLen < _indents.Peek())
                    {
                        _indents.Pop();
                    }
                }
            }
        }

        protected override void WriteExpressionStatement(CodeExpressionStatement s)
        {
            Writer.Write(new string(' ', _generatedIndent + _indents.Peek()));
            WriteExpression(s.Expression);
            Writer.Write('\n');
        }

        protected override void WriteSnippetExpression(CodeSnippetExpression e)
        {
            Writer.Write(IndentSnippet(e.Value));
        }

        protected override string QuoteString(string val)
        {
            return $"\"{val.Replace("\"", "\\")}\"";
        }

        private string IndentSnippet(string block)
        {
            return block.Replace("\n", "\n" + new string(' ', _generatedIndent));
        }

        private string IndentSnippetStatement(string block)
        {
            return new string(' ', _generatedIndent) + IndentSnippet(block);
        }
    }
}
#endif
