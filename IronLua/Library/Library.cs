using IronLua.Runtime;
using Microsoft.Scripting.Utils;
using System.Dynamic;
using System.Collections.Generic;

namespace IronLua.Library
{
    abstract class Library
    {
        protected CodeContext Context { get; private set; }

        protected Library(CodeContext context)
        {
            ContractUtils.RequiresNotNull(context, "context");
            Context = context;
        }

        public abstract void Setup(IDictionary<string,object> table);

    }

}
