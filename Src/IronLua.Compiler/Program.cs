using System;
using System.Diagnostics;

namespace IronLua
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                return new Program().Run(args);
            }
            finally
            {
                Pause();
            }
        }

        public int Run(string[] args)
        {
            Console.WriteLine("luac: no input files given");
            PrintUsage();
            return 0;
        }

        [Conditional("DEBUG")]
        public static void Pause()
        {
            if (Debugger.IsAttached)
            {
                Console.WriteLine();
                Console.Write("Press ENTER to continue ...");
                Console.ReadLine();
            }
        }
        
        public static void PrintUsage()
        {
            Console.WriteLine(@"
usage: luac [options] [filenames].
Available options are:
  -        process stdin
  -c       show command line
  -l       list
  -o name  output to file 'name' (default is "luac.out")
  -os      output to stdout
  -d dir   output package tree to directory `dir' (default is "".\"")
  -r dir   root dir of source packages (including trailing `')
  -x ext   filename extension of output files (default is ""luac"")
  -p       parse only
  -s       strip debug information
  -v       show version information
  --       stop handling options

-os can only be used when compiling 1 input file into 1 output.

d, r and x are used when compiling >1 file into >1 output. Output file names
are formed by removing the prefix specified by -r from the input file names
and replacing it with the value of the -d option. If the prefix cannot be
found the output is generated in the current directory using the input file
name. In all cases the input file name extension is replaced by the value of
the -x option. Multiple file output is triggered by the presence of the -r
option.");
        }
    }
}
