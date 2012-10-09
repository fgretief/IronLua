using System;
using IronLua.Runtime;
using System.Collections.Generic;

namespace IronLua.Library
{
    class MathLibrary : Library
    {
        public MathLibrary(CodeContext context)
            : base(context)
        {

        }

        private Random rand = new Random();

        public override void Setup(IDictionary<string,object> table)
        {
            const double Math_Tau = 2.0 * Math.PI; // http://tauday.com

            table.AddOrSet("huge", Double.MaxValue);

            // Basic operations
            table.AddOrSet("abs", (Func<double, double>)Math.Abs);
            table.AddOrSet("mod", (Func<double, double, double>)((a, b) => a % b));
            table.AddOrSet("modf", (Func<double, double, Varargs>)((a, b) =>
            {
                long r;
                long q = Math.DivRem((long) a, (long) b, out r);
                return new Varargs(q, r);
            }));
            table.AddOrSet("floor", (Func<double, double>) Math.Floor);
            table.AddOrSet("ceil", (Func<double, double>) Math.Ceiling);
            table.AddOrSet("min", (Func<double, double, double>) Math.Min);
            table.AddOrSet("max", (Func<double, double, double>) Math.Max);

            // Exponetial and logarithmic
            table.AddOrSet("sqrt", (Func<double, double>) Math.Sqrt);
            table.AddOrSet("pow", (Func<double, double, double>) Math.Pow);
            table.AddOrSet("exp", (Func<double, double>) Math.Exp);
            table.AddOrSet("log", (Func<double, double>) Math.Log);
            table.AddOrSet("log10", (Func<double, double>) Math.Log10);

            // Trigonometrical
            table.AddOrSet("pi", Math.PI);
            table.AddOrSet("tau", Math_Tau);
            table.AddOrSet("deg", (Func<double, double>)(r => r * 360.0 / Math_Tau));
            table.AddOrSet("rad", (Func<double, double>)(d => d / 360.0 * Math_Tau));
            table.AddOrSet("cos", (Func<double, double>) Math.Cos);
            table.AddOrSet("sin", (Func<double, double>) Math.Sin);
            table.AddOrSet("tan", (Func<double, double>)Math.Tan);
            table.AddOrSet("acos", (Func<double, double>)Math.Acos);
            table.AddOrSet("asin", (Func<double, double>)Math.Asin);
            table.AddOrSet("atan", (Func<double, double>)Math.Atan);
            table.AddOrSet("atan2", (Func<double, double, double>)Math.Atan2);
            
            // Splitting on powers of 2
            //table.AddOrSet("frexp", (Func<double, double>) Math.??);
            //table.AddOrSet("ldexp", (Func<double, double, double>) Math.??);

            // Pseudo-random numbers
            table.AddOrSet("randomseed", (Func<double, double>)(x => { rand = new Random((int)x); return rand.NextDouble(); }));
            table.AddOrSet("random", (Func<Varargs, double>)Random); // overloaded
        }

        private double Random(Varargs args = null)
        {
            if (args == null || args.Count == 0)
                return rand.NextDouble();
            else if (args.Count == 1)
                return rand.Next(1, 1 + Convert.ToInt32(args[0]));
            else if (args.Count >= 2)
                return rand.Next(Convert.ToInt32(args[0]), 1 + Convert.ToInt32(args[1]));
            return 0;
        }
    }
}