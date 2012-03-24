using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using NUnit.Framework;
using NUnit.Direct;

namespace RT.ParseCs.Tests
{
    class TestsProgram
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            NUnitDirect.RunTestsOnAssembly(typeof(TestsProgram).Assembly);

            if (args.Contains("--wait"))
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }
}
