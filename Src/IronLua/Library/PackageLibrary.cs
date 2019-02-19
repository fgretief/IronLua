using System;
using System.Text;
using IronLua.Runtime;
using System.Collections.Generic;

namespace IronLua.Library
{
    class PackageLibrary : Library
    {
        static readonly string ConfigStr = new StringBuilder()
            .AppendLine("\\")
            .AppendLine(";")
            .AppendLine("?")
            .AppendLine("!")
            .AppendLine("-")
            .ToString();

        public PackageLibrary(CodeContext context) 
            : base(context)
        {
        }

        public static object Loadlib(string libName, string funcName)
        {
            //Maybe fall back on using InteropLibrary?
            throw new NotImplementedException();   
        }

        public static object SearchPath(string name, string path, string sep, string rep)
        {
            throw new NotImplementedException();    
        }

        public override void Setup(IDictionary<string, object> table)
        {
            table.AddOrSet("config", ConfigStr);

            table.AddOrSet("cpath", 
                Environment.GetEnvironmentVariable("LUA_CPATH_5_2") ??
                    Environment.GetEnvironmentVariable("LUA_CPATH") ??
                        String.Join(";", new[]
                        {
                            "!\\?.dll", 
                            "!\\loadall.dll", 
                            ".\\?.dll"
                        }));

            table.AddOrSet("path",
                Environment.GetEnvironmentVariable("LUA_PATH_5_2") ??
                    Environment.GetEnvironmentVariable("LUA_PATH") ??
                        String.Join(";", new[] 
                        { 
                            "!\\lua\\" + "?.lua",
                            "!\\lua\\" + "?\\init.lua",
                            "!\\" + "?.lua",
                            "!\\" + "?\\init.lua",
                            ".\\?.lua"
                        }));

            table.AddOrSet("loaded", new LuaTable(Context));
            table.AddOrSet("preload", new LuaTable(Context));
            table.AddOrSet("searchers", new LuaTable(Context)); // TODO: fill with searchers

            table.AddOrSet("loadlib", (Func<string, string, object>)Loadlib);
            table.AddOrSet("searchpath", (Func<string, string, string, string, object>) SearchPath);
        }
    }
}