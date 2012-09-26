using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Reflection;

namespace Tests
{
    class Program
    {
        static void Main()
        {
            // why is this here:
            // many test runners have problems when logging data from lots of threads, which made
            // it hard to inspect issues that arose during testing
            // this just allows me to have a *simple* test run I can use at the console, so I
            // can add console logging from any thread to inspect state etc
            
            var tests = from type in typeof(Program).Assembly.GetTypes()
                        where Attribute.IsDefined(type, typeof(TestFixtureAttribute))
                        let obj = Activator.CreateInstance(type)
                        from method in type.GetMethods()
                        where Attribute.IsDefined(method, typeof(TestAttribute))
                        select Tuple.Create(method, obj);

            foreach (var tuple in tests)
            {
                Console.WriteLine("{0}:{1}", tuple.Item1.DeclaringType.Name, tuple.Item1.Name);
                try
                {
                    tuple.Item1.Invoke(tuple.Item2, null);
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine("#" + ex.InnerException.GetType().Name+":"+ex.InnerException.Message);
                }


            }
        }
    }
}
