using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting.Runtime;

namespace IronLua.Util
{
    static class ExceptionExtensions
    {
        public static object GetData(this Exception e, object key)
        {
            return e.Data[key];
        }

        public static void SetData(this Exception e, object key, object data)
        {
            e.Data[key] = data;
        }

        public static void RemoveData(this Exception e, object key)
        {
            e.Data.Remove(key);
        }

        public static List<DynamicStackFrame> GetFrames(this Exception e)
        {
            return e.GetData(typeof(DynamicStackFrame)) as List<DynamicStackFrame>;
        }
    }
}
