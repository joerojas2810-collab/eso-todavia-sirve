// Runner local: ejecuta los [Test] (respetando [Order]) igual que el Test Runner de Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
class Runner
{
    static int Main()
    {
        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        var types = asm.GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(NUnit.Framework.TestFixtureAttribute), false).Length > 0
                     || t.GetMethods().Any(m => m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length > 0))
            .OrderBy(t => t.Name).ToList();
        foreach (var type in types)
        {
            object inst;
            try { inst = Activator.CreateInstance(type); }
            catch (Exception e) { Console.WriteLine("FIXTURE-FAIL " + type.Name + ": " + e.Message); fail++; continue; }
            var methods = type.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length > 0)
                .OrderBy(m => { var o = m.GetCustomAttributes(typeof(NUnit.Framework.OrderAttribute), false).FirstOrDefault() as NUnit.Framework.OrderAttribute; return o != null ? o.Order : int.MaxValue; })
                .ThenBy(m => m.Name).ToList();
            var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes(typeof(NUnit.Framework.SetUpAttribute), false).Length > 0);
            var tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes(typeof(NUnit.Framework.TearDownAttribute), false).Length > 0);
            foreach (var m in methods)
            {
                try { if (setUp != null) setUp.Invoke(inst, null); m.Invoke(inst, null); pass++; Console.WriteLine("PASS " + type.Name + "." + m.Name); }
                catch (Exception e)
                {
                    fail++;
                    var inner = e is TargetInvocationException ? e.InnerException : e;
                    Console.WriteLine("FAIL " + type.Name + "." + m.Name + ": " + inner.Message);
                }
                finally { try { if (tearDown != null) tearDown.Invoke(inst, null); } catch (Exception te) { Console.WriteLine("TEARDOWN-FAIL " + type.Name + "." + m.Name + ": " + (te is TargetInvocationException ? te.InnerException : te).Message); } }
            }
        }
        Console.WriteLine("\n" + pass + " passed, " + fail + " failed");
        return fail > 0 ? 1 : 0;
    }
}
