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
            var client = new CacheClient();
            client.SetServer("tcp://127.0.0.1:1234");

#if !DEBUG
            client.Bench();
#else
            Debug.Assert(client.Set("aa", 1234));
            Debug.Assert(client.ContainsKey("aa"));
            Debug.Assert(client.Get<Int32>("aa") == 1234);

            client.Set("bb", false);
            client.Set("cc", 3.14);
            client.Set("dd", "NewLife", 5);
            client.Set("ee", new { Name = "新生命", Year = 2002 });

            Console.WriteLine(client.Get<Int32>("aa"));
            Console.WriteLine(client.Get<Boolean>("bb"));
            Console.WriteLine(client.Get<Double>("cc"));
            Console.WriteLine(client.Get<String>("dd"));
            Console.WriteLine(client.Get<Object>("ee").ToJson());

            Console.WriteLine();
            Console.WriteLine("Count={0}", client.Count);
            Console.WriteLine("Keys={0}", client.Keys.Join());
            Thread.Sleep(2000);
            Console.WriteLine("Expire={0}", client.GetExpire("dd"));

            Console.WriteLine();
            client.Decrement("aa", 30);
            client.Increment("cc", 0.3);

            Console.WriteLine();
            var dic = client.GetAll<Object>(new[] { "aa", "cc", "ee" });
            foreach (var item in dic)
            {
                var val = item.Value;
                if (val != null && item.Value.GetType().GetTypeCode() == TypeCode.Object) val = val.ToJson();

                Console.WriteLine("{0}={1}", item.Key, val);
            }
#endif
        }
    }
}