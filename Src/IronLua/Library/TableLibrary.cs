using System;
using System.Collections.Generic;
using IronLua.Runtime;

namespace IronLua.Library
{
    class TableLibrary : Library
    {
        public TableLibrary(CodeContext context) 
            : base(context)
        {
        }

        public object SortTable(LuaTable t)
        {
            throw new NotImplementedException();
        }

        public override void Setup(IDictionary<string, object> table)
        {
            //table.AddOrSet("concat"); // TODO: not implemented yet
            //table.AddOrSet("insert"); // TODO: not implemented yet
            //table.AddOrSet("pack"); // TODO: not implemented yet
            //table.AddOrSet("remove"); // TODO: not implemented yet
            table.AddOrSet("sort", (Func<LuaTable, object>)SortTable);
            //table.AddOrSet("unpack"); // TODO: not implemented yet
        }
    }
}