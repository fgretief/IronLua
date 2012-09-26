using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting.Runtime;
using System.CodeDom;

namespace IronLua.Hosting
{
    class LuaCodeDomGen : CodeDomCodeGen
    {
        //TODO: Add the rest of the code gen here

        protected override string QuoteString(string val)
        {
            return string.Format("\"{0}\"", val.Replace("\"","\\"));
        }

        protected override void WriteExpressionStatement(System.CodeDom.CodeExpressionStatement s)
        {
            throw new NotImplementedException();
        }

        protected override void WriteFunctionDefinition(System.CodeDom.CodeMemberMethod func)
        {
            if (func.Attributes.HasFlag(MemberAttributes.Private))
                Writer.Write("local ");
            Writer.Write("function ");
            if (!string.IsNullOrEmpty(func.Name))
                Writer.Write(func.Name + " ");
            Writer.Write("(");

            for (int i = 0; i < func.Parameters.Count; ++i)
            {
                if (i != 0)                
                    Writer.Write(",");
                
                Writer.Write(func.Parameters[i].Name);
            }
            Writer.Write(")\n");
        }
    }
}
