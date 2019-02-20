﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronLua.Compiler;
using IronLua.Compiler.Parsing;
using IronLua.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using System.Runtime.CompilerServices;

namespace IronLua.Library
{
    class BaseLibrary : Library
    {
        public BaseLibrary(CodeContext context) 
            : base(context)
        {
        }

        public Varargs Assert(object v, object message = null, params object[] additional)
        {
            if ((v is bool ? (bool)v : v != null))
            {
                var returnValues = new List<object>(2 + additional.Length) { v };
                if (message != null)
                    returnValues.Add(message);
                returnValues.AddRange(additional);
                return new Varargs(returnValues);
            }

            if (message == null)
                throw LuaRuntimeException.Create(Context, "Assertion failed");
            throw LuaRuntimeException.Create(Context, message.ToString());
        }

        public void CollectGarbage(string opt, string arg = null)
        {
            throw LuaRuntimeException.Create(Context, ExceptionMessage.FUNCTION_NOT_IMPLEMENTED);
        }

        public object DoFile(string filename = null)
        {
            var source = filename == null ? Console.In.ReadToEnd() : File.ReadAllText(filename);
            try
            {
                return CompileString(Context, source)();
            }
            catch (SyntaxErrorException ex)
            {
                throw LuaRuntimeException.Create(Context, ex.Message, ex);
            }
            catch (LuaSyntaxException e)
            {
                throw LuaRuntimeException.Create(Context, e.Message, e);
            }
        }

        public void Error(object message, object level = null)
        {
            throw new LuaErrorException(Context, message, Convert.ToInt32(level ?? (object)1));
        }

        public object GetFEnv(object f = null)
        {
            if (f is double)
            {
                //stack level
                var function = Context.GetFunction(Convert.ToInt32(f) - 1);
                return Context.GetFunctionEnvironment(function.ExecScope);
            }
            else if (f is Delegate)
                return Context.GetFunctionEnvironment(f as Delegate);
            else
                return null;

        }

        public object GetMetatable(object obj)
        {
            var table = obj as LuaTable;

            if (table != null)
                table = table.Metatable;
            if (table == null)
                table = Context.Language.GetTypeMetatable(obj);
            if (table == null)
                return null;

            var table2 = table.GetValue(Constant.METATABLE_METAFIELD);
            return table2 ?? table;
        }

        public Varargs IPairs(LuaTable t)
        {
            var length = t.Length();
            Func<LuaTable, double, object> func =
                (table, index) =>
                    {
                        index++;
                        return index > length ? null : new Varargs(index, table.GetValue(index));
                    };

            return new Varargs(func, t, 0.0);
        }

        public Varargs Load(Delegate func, string chunkname = "=(load)")
        {
            var invoker = Context.DynamicCache.GetDynamicCall0();
            var sb = new StringBuilder(1024);

            while (true)
            {
                var result = invoker.Target(func);
                if (result == null)
                    break;

                var str = result.ToString();
                if (String.IsNullOrEmpty(str))
                    break;

                sb.Append(str);
            }

            try
            {
                return new Varargs(CompileString(Context, sb.ToString()));
            }
            catch (SyntaxErrorException ex)
            {
                return new Varargs(null, ex.Message);
            }
            catch (LuaSyntaxException e)
            {
                return new Varargs(null, e.Message);
            }
        }

        public Varargs LoadFile(string filename = null)
        {
            var source = filename == null ? Console.In.ReadToEnd() : File.ReadAllText(filename);
            try
            {
                return new Varargs(CompileString(Context, source));
            }
            catch (SyntaxErrorException ex)
            {
                return new Varargs(null, ex.Message);
            }
            catch (LuaSyntaxException e)
            {
                return new Varargs(null, e.Message);
            }
        }

        public Varargs LoadString(string str, string chunkname = "=(loadstring)")
        {
            try
            {
                return new Varargs(CompileString(Context, str));
            }
            catch (SyntaxErrorException ex)
            {
                return new Varargs(null, ex.Message);
            }
            catch (LuaSyntaxException e)
            {
                return new Varargs(null, e.Message);
            }
            catch (Exception ex)
            {
                return new Varargs(null, ex.Message);
            }
        }

        public Varargs Next(LuaTable table, object index = null)
        {
            if (table == null)
                throw LuaRuntimeException.Create(Context, ExceptionMessage.INVOKE_BAD_ARGUMENT_GOT, "next", "table", "nil");

            return table.Next(index);
        }

        public Varargs Pairs(LuaTable t)
        {
            return new Varargs((Func<LuaTable, object, Varargs>)Next, t, null);
        }

        public Varargs PCall(Delegate f, params object[] args)
        {
            try
            {
                var result = Context.DynamicCache.GetDynamicCall1().Target(f, new Varargs(args));
                return new Varargs(true, result);
            }
            catch (LuaErrorException e)
            {
                return new Varargs(false, e.Result);
            }
            catch (Exception e)
            {
                return new Varargs(false, e.Message);
            }
        }

        public void Print(params object[] args)
        {
            var writer = Context.EngineIO.OutputWriter;

            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0) 
                    writer.Write("\t");
                writer.Write(ToString(Context, args[i]));
            }
            writer.WriteLine();
        }

        public bool RawEqual(object v1, object v2)
        {
            return v1.Equals(v2);
        }

        public object RawGet(LuaTable table, object index)
        {
            return table.GetValue(index);
        }

        public LuaTable RawSet(LuaTable table, object index, object value)
        {
            table.SetValue(index, value);
            return table;
        }

        public Varargs Select(object index, params object[] args)
        {
            if (index.Equals("#"))
                return new Varargs(args.Length);

            var num = ConvertToNumber(index, 1);

            var numIndex = (int)Math.Round(num) - 1;
            if (numIndex >= args.Length || numIndex < 0)
                throw LuaRuntimeException.Create(Context, ExceptionMessage.INVOKE_BAD_ARGUMENT, 1, "index out of range");

            var returnArgs = new object[args.Length - numIndex];
            Array.Copy(args, numIndex, returnArgs, 0, args.Length - numIndex);
            return new Varargs(returnArgs);
        }

        public object SetFEnv(object f, LuaTable table)
        {
            if (f is double)
            {
                //stack level
                var function = Context.GetFunction(Convert.ToInt32(f) - 1);
                Context.SetFunctionEnvironment(function.ExecScope, table);
            }
            else if (f is Delegate)            
                Context.SetFunctionEnvironment(f as Delegate, table);
            else
                throw LuaRuntimeException.Create(Context, ExceptionMessage.FUNCTION_NOT_IMPLEMENTED);

            return table;
        }

        public LuaTable SetMetatable(LuaTable table, LuaTable metatable)
        {
            if (table.Metatable != null && table.Metatable.GetValue(Constant.METATABLE_METAFIELD) != null)
                throw LuaRuntimeException.Create(Context, ExceptionMessage.PROTECTED_METATABLE);

            table.Metatable = metatable;
            return table;
        }

        public object ToNumber(object obj, object @base = null)
        {
            return ToNumber(Context, obj, @base);
        }

        public static object ToNumber(CodeContext context, object obj, object @base = null)
        {
            var numBase = ConvertToNumber(context, @base, 2, 10.0);

            if (numBase == 10.0)
            {
                if (obj is double)
                    return obj;
            }
            else if (numBase < 2.0 || numBase > 36.0)
            {
                throw LuaRuntimeException.Create(context, ExceptionMessage.INVOKE_BAD_ARGUMENT, 2, "base out of range");
            }

            string stringStr;
            if ((stringStr = obj as string) == null)
                return null;

            var value = InternalToNumber(stringStr, numBase);
            return Double.IsNaN(value) ? null : (object)value;
        }

        public object ToString(object e)
        {
            return ToString(Context, e);
        }

        public static string ToString(CodeContext context, object v)
        {
            if (ReferenceEquals(v, null))
                return "nil";
            
            if (v is LuaTable)
            {
                var table = v as LuaTable;
                var metaToString = LuaOps.GetMetamethod(context, v, Constant.TOSTRING_METAFIELD);
                if (metaToString != null)
                    return ToString(context, context.DynamicCache.GetDynamicCall1().Target(metaToString, v));
                
                return String.Format("table [{0} entries]", (v as LuaTable).Count());
            }
            
            if (v is Delegate)
            {
                string functionFormat = "";
                var fn = v as Delegate;

                foreach (var p in fn.Method.GetParameters())
                    functionFormat += (p.Name ?? p.ParameterType.Name) + ",";
                functionFormat = functionFormat.Remove(functionFormat.Length - 1);
                functionFormat = string.Format("({0}) => {1}", functionFormat, fn.Method.ReturnType.Name);

                string functionName = fn.Method.Name;
                functionName = functionName.StartsWith(Constant.FUNCTION_PREFIX) ? "function " + functionName.Remove(0, Constant.FUNCTION_PREFIX.Length) : functionName;

                return String.Format("{0} {1}", functionName, functionFormat);
            }

            if (v is bool)            
                return ((bool)v).ToString().ToLower();
                        
            return v.ToString();
        }

        public static string Type(object v)
        {
            return ReferenceEquals(v, null) 
                ? "nil" 
                : TypeName(v.GetType());
        }

        public static string TypeName(Type t)
        {
            if (t == typeof(DynamicNull))
                return "nil";
            if (t == typeof(bool))
                return "boolean";
            if (t == typeof(double))
                return "number";
            if (t == typeof(string))
                return "string";
            if (t.IsSubclassOf(typeof(Delegate)))
                return "function";
            if (t == typeof(LuaTable))
                return "table";

            return t.FullName;
        }

        public Varargs Unpack(LuaTable list, object i = null, object j = null)
        {
            var listLength = list.Length();

            var startIndex = ConvertToNumber(i, 2, 1.0);
            var length = ConvertToNumber(j, 3, listLength);

            if (startIndex < 1)
                return Varargs.Empty;
            length = Math.Min(length, listLength - startIndex + 1);

            var array = new object[(int)length];
            var arrayIndex = 0;
            for (var k = startIndex; k < startIndex + length; k++)
                array[arrayIndex++] = list.GetValue(k);

            return new Varargs(array);
        }

        public Varargs XPCall(Delegate f, Delegate err)
        {
            try
            {
                var result = Context.DynamicCache.GetDynamicCall0().Target(f);
                return new Varargs(true, result);
            }
            catch (Exception e)
            {
                var result = Context.DynamicCache.GetDynamicCall1().Target(err, e.Message);
                return new Varargs(false, result);
            }
        }

        internal static double InternalToNumber(string str, double @base)
        {
            double result = 0;
            var intBase = (int)Math.Round(@base);

            if (intBase == 10)
            {
                if (str.StartsWith("0x") || str.StartsWith("0X"))
                {
                    if (NumberUtil.TryParseHexNumber(str.Substring(2), true, out result))
                        return result;
                }
                return NumberUtil.TryParseDecimalNumber(str, out result) ? result : Double.NaN;
            }

            if (intBase == 16)
            {
                if (str.StartsWith("0x") || str.StartsWith("0X"))
                {
                    if (NumberUtil.TryParseHexNumber(str.Substring(2), false, out result))
                        return result;
                }
                return Double.NaN;
            }

            var reversedStr = new String(str.Reverse().ToArray());
            for (var i = 0; i < reversedStr.Length; i++ )
            {
                var num = AlphaNumericToBase(reversedStr[i]);
                if (num == -1 || num >= intBase)
                    return Double.NaN;
                result += num * (intBase * i + 1);
            }
            return result;
        }

        double ConvertToNumber(object obj, int argumentIndex, double @default = Double.NaN)
        {
            return ConvertToNumber(Context, obj, argumentIndex, @default);
        }

        static double ConvertToNumber(CodeContext context, object obj, int argumentIndex, double @default = Double.NaN)
        {
            string tempString;

            if (obj == null && !Double.IsNaN(@default))
                return @default;
            if (obj is double)
                return Math.Round((double)obj);

            if ((tempString = obj as string) != null)
            {
                var num = Math.Round(InternalToNumber(tempString, 10.0));
                if (!Double.IsNaN(num))
                    return num;
            }

            throw LuaRuntimeException.Create(context, ExceptionMessage.INVOKE_BAD_ARGUMENT_GOT,
                                          argumentIndex, "number", Type(obj));
        }

        static int AlphaNumericToBase(char c)
        {
            if (c >= '0' && c >= '9')
                return c - '0';
            if (c >= 'A' && c <= 'Z')
                return c - 'A' + 10;
            if (c >= 'a' && c <= 'z')
                return c - 'a' + 10;

            return -1;
        }

        static Func<dynamic> CompileString(CodeContext context, string source)
        {
            ContractUtils.RequiresNotNull(context, "context");

            var sourceUnit = context.Language.CreateSnippet(source, SourceCodeKind.Statements);

            //var options = (LuaCompilerOptions)context.GetCompilerOptions();
            //var errorSink = context.GetCompilerErrorSink();
            //var lexer = context.GetService<TokenizerService>();

            var lexer = new Tokenizer(ErrorSink.Default, LuaCompilerOptions.Default);
            lexer.Initialize(null, sourceUnit.GetReader(), sourceUnit, SourceLocation.MinValue);

            var parser = new Parser(lexer, lexer.ErrorSink);
            var ast = parser.Parse();
            var gen = new Generator(context);

            var fnStack =context.FunctionStacks.LastOrDefault(x => x.ExecScope != null);
            if (fnStack == default(FunctionStack))
                fnStack = new FunctionStack(context, null, LuaScope.CreateRoot(context), "=(compiled code)");

            var expr = gen.CompileInline(ast, fnStack.ExecScope, context.ExecutingScopeStorage, sourceUnit);
            return expr.Compile();
        }

        public override void Setup(IDictionary<string,object> table)
        {
            table.AddNotPresent("assert", (Func<object, object, object[], Varargs>)Assert);
            table.AddNotPresent("collectgarbage", (Action<string, string>)CollectGarbage);
            table.AddNotPresent("dofile", (Func<string, object>)DoFile);
            table.AddNotPresent("error", (Action<object, object>)Error);
            table.AddNotPresent("_ENV", table);
            table.AddNotPresent("_G", table);
            table.AddNotPresent("getfenv", (Func<object, object>)GetFEnv);
            table.AddNotPresent("getmetatable", (Func<object, object>)GetMetatable);
            table.AddNotPresent("ipairs", (Func<LuaTable, Varargs>)IPairs);
            table.AddNotPresent("load", (Func<Delegate, string, Varargs>)Load);
            table.AddNotPresent("loadfile", (Func<string, Varargs>)LoadFile);
            table.AddNotPresent("loadstring", (Func<string, string, Varargs>)LoadString);
            table.AddNotPresent("next", (Func<LuaTable, object, Varargs>)Next);
            table.AddNotPresent("pairs", (Func<LuaTable, Varargs>)Pairs);
            table.AddNotPresent("pcall", (Func<Delegate, object[], Varargs>)PCall);
            table.AddNotPresent("print", (Action<object[]>)Print);
            table.AddNotPresent("rawequal", (Func<object, object, bool>)RawEqual);
            table.AddNotPresent("rawget", (Func<LuaTable, object, object>)RawGet);
            table.AddNotPresent("rawset", (Func<LuaTable, object, object, object>)RawSet);
            table.AddNotPresent("require", (Func<string, LuaTable>)Context.RequireLibrary);
            table.AddNotPresent("select", (Func<object, object[], Varargs>)Select);
            table.AddNotPresent("setfenv", (Func<object, LuaTable, object>)SetFEnv);
            table.AddNotPresent("setmetatable", (Func<LuaTable, LuaTable, LuaTable>)SetMetatable);
            table.AddNotPresent("tonumber", (Func<object, object, object>)ToNumber);
            table.AddNotPresent("tostring", (Func<object, object>)ToString);
            table.AddNotPresent("type", (Func<object, string>)Type);
            table.AddNotPresent("unpack", (Func<LuaTable, object, object, Varargs>)Unpack);
            table.AddNotPresent("_VERSION", Constant.LUA_VERSION);
            table.AddNotPresent("xpcall", (Func<Delegate, Delegate, Varargs>)XPCall);
        }

        public void Clean(IDictionary<string,object> table)
        {
            if (table == null)
                return;

            table.RemoveIfEqual("assert", (Func<object, object, object[], Varargs>)Assert);
            table.RemoveIfEqual("collectgarbage", (Action<string, string>)CollectGarbage);
            table.RemoveIfEqual("dofile", (Func<string, object>)DoFile);
            table.RemoveIfEqual("error", (Action<object, object>)Error);
            table.RemoveIfEqual("_ENV", table);
            table.RemoveIfEqual("_G", table);
            table.RemoveIfEqual("getfenv", (Func<object, object>)GetFEnv);
            table.RemoveIfEqual("getmetatable", (Func<object, object>)GetMetatable);
            table.RemoveIfEqual("ipairs", (Func<LuaTable, Varargs>)IPairs);
            table.RemoveIfEqual("load", (Func<Delegate, string, Varargs>)Load);
            table.RemoveIfEqual("loadfile", (Func<string, Varargs>)LoadFile);
            table.RemoveIfEqual("loadstring", (Func<string, string, Varargs>)LoadString);
            table.RemoveIfEqual("next", (Func<LuaTable, object, Varargs>)Next);
            table.RemoveIfEqual("pairs", (Func<LuaTable, Varargs>)Pairs);
            table.RemoveIfEqual("pcall", (Func<Delegate, object[], Varargs>)PCall);
            table.RemoveIfEqual("print", (Action<object[]>)Print);
            table.RemoveIfEqual("rawequal", (Func<object, object, bool>)RawEqual);
            table.RemoveIfEqual("rawget", (Func<LuaTable, object, object>)RawGet);
            table.RemoveIfEqual("rawset", (Func<LuaTable, object, object, object>)RawSet);
            table.RemoveIfEqual("select", (Func<object, object[], Varargs>)Select);
            table.RemoveIfEqual("setfenv", (Func<object, LuaTable, object>)SetFEnv);
            table.RemoveIfEqual("setmetatable", (Func<LuaTable, LuaTable, LuaTable>)SetMetatable);
            table.RemoveIfEqual("tonumber", (Func<object, object, object>)ToNumber);
            table.RemoveIfEqual("tostring", (Func<object, object>)ToString);
            table.RemoveIfEqual("type", (Func<object, string>)Type);
            table.RemoveIfEqual("unpack", (Func<LuaTable, object, object, Varargs>)Unpack);
            table.RemoveIfEqual("_VERSION", Constant.LUA_VERSION);
            table.RemoveIfEqual("xpcall", (Func<Delegate, Delegate, Varargs>)XPCall);
        }
    }
}
