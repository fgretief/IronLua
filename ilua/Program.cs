﻿using System;
using System.Diagnostics;
using IronLua.Hosting;

namespace IronLua
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            int status = new LuaConsoleHost().Run(args);
            if (status != 0 && Debugger.IsAttached)
            {
                // Pause for errors
                System.Console.WriteLine();
                System.Console.Write("Press ENTER to continue ...");
                System.Console.ReadLine();
            }
            return status;
        }
    }
}
