// Shim local de NUnit.Framework para verificar los tests FUERA de Unity (Mono).
// NO va al repositorio: en Unity compilan contra el NUnit real (paquete test-framework).
using System;
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public class TearDownAttribute : Attribute {}
    [AttributeUsage(AttributeTargets.Method)] public class OrderAttribute : Attribute { public int Order; public OrderAttribute(int o){Order=o;} }
    public class AssertionException : Exception { public AssertionException(string m) : base(m) {} }
    public static class Assert
    {
        public static void AreEqual(object expected, object actual, string msg = null)
        {
            if (!Eq(expected, actual)) throw new AssertionException("AreEqual: esperado <" + Fmt(expected) + "> actual <" + Fmt(actual) + "> " + msg);
        }
        public static void AreEqual(double e, double a, double tol, string msg = null)
        {
            if (Math.Abs(e - a) > tol) throw new AssertionException("AreEqual(tol): " + e + " vs " + a + " " + msg);
        }
        public static void IsTrue(bool c, string msg = null) { if (!c) throw new AssertionException("IsTrue: " + msg); }
        public static void IsFalse(bool c, string msg = null) { if (c) throw new AssertionException("IsFalse: " + msg); }
        public static void IsNull(object o, string msg = null) { if (o != null) throw new AssertionException("IsNull: " + msg); }
        public static void IsNotNull(object o, string msg = null) { if (o == null) throw new AssertionException("IsNotNull: " + msg); }

        public static void Greater(double a, double b, string msg = null) { if (!(a > b)) throw new AssertionException("Greater: " + a + " <= " + b + " " + msg); }
        public static void Less(double a, double b, string msg = null) { if (!(a < b)) throw new AssertionException("Less: " + a + " >= " + b + " " + msg); }
        public static void Fail(string msg = null) { throw new AssertionException("Fail: " + msg); }
        static string Fmt(object o) { return o == null ? "null" : o.ToString(); }
        static bool Eq(object e, object a)
        {
            if (e == null || a == null) return Equals(e, a);
            if (e is string || a is string) return e.Equals(a);
            if (e is IConvertible && a is IConvertible)
            {
                try { return Convert.ToDecimal(e) == Convert.ToDecimal(a); } catch {}
            }
            return e.Equals(a);
        }
    }
}
