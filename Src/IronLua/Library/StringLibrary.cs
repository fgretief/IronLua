using System;
using System.Linq;
using System.Text;
using IronLua.Runtime;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace IronLua.Library
{
    class StringLibrary : Library
    {
        public StringLibrary(CodeContext context) 
            : base(context)
        {
        }

        public static string Subst(string s, double i, double j = -1)
        {
            int m = (int)Math.Round(i, MidpointRounding.ToEven);
            int n = (int)Math.Round(j, MidpointRounding.ToEven);

            m = (m < 0) ? s.Length + m : m - 1;
            n = (n < 0) ? s.Length + n : n - 1;

            if (m > n)
                return String.Empty;

            int k = Math.Max(0, m);
            int l = n - k + 1;

            if (k >= s.Length || l <= 0)
                return String.Empty;

            return s.Substring(k, Math.Min(l, s.Length - k));
        }

        public static double[] Byte(string s, double i = 1, double j = -1)
        {
            return Subst(s, i, j).Select(c => (double) c).ToArray();
        }

        public static string Char(params double[] varargs)
        {
            if (varargs.Length <= 0)
                return String.Empty;

            var sb = new StringBuilder(varargs.Length);
            
            foreach (double arg in varargs)
                sb.Append((char)arg);
            
            return sb.ToString();
        }

        public static string Dump(object function)
        {
            throw new NotImplementedException();
        }


        private static Regex EscapeMatchingRegex = new Regex("%([aAcCdDlLpPsSuUwWxXzZ\\(\\).+\\-*?\\[\\^$%])", RegexOptions.Compiled);

        public static object[] Find(string str, string pattern, int? init = 1, bool? plain = false)
        {
            if (plain.HasValue && plain.Value && init.HasValue)
            {
                var index = str.Substring(init.Value).IndexOf(pattern, StringComparison.Ordinal);
                return index != -1 ? new object[] { index, index + pattern.Length } : null;
            }
            else
            {
                //RegExp matching, using % instead of \
                return null;
            }
        }

        public static string Format(string format, params object[] varargs)
        {
            return StringFormatter.Format(format, varargs);
        }

        public override void Setup(IDictionary<string, object> table)
        {
            table.AddOrSet("len", (Func<string, double>) (s => s.Length));
            table.AddOrSet("upper", (Func<string, string>) (s => s.ToUpperInvariant()));
            table.AddOrSet("lower", (Func<string, string>)(s => s.ToLowerInvariant()));
            table.AddOrSet("rep", (Func<string, double, string>) ((s, r) => s.Repeat((int)Math.Round(r, MidpointRounding.ToEven))));

            table.AddOrSet("sub", (Func<string, double, double, string>)Subst); // TODO: varargs
            table.AddOrSet("char", (Func<double[], string>) Char); // TODO: varargs
            table.AddOrSet("byte", (Func<string, double, double, double[]>) Byte); // TODO: varargs

            table.AddOrSet("find", (Func<string, string, int?, bool?, object[]>)Find);
        }
    }

    static class StringUtils
    {
        public static string Repeat(this string s, int r)
        {
            if (r < 1)
                return String.Empty;

            var sb = new StringBuilder(r * s.Length);
            do 
                sb.Append(s);
            while (--r > 0);
            return sb.ToString();
        }
    }
}
