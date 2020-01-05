using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;

namespace Test
{
    class Program
    {
        static void Main(String[] args)
        {
            XTrace.UseConsole();

            try
            {
                Test1();
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            Console.WriteLine("OK");
            Console.Read();
        }

        static void Test1()
        {

        }
    }
}