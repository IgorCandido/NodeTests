using System.Collections.Generic;
using NUnit.Framework;
using System.Text;

namespace Tests
{
    [TestFixture]
    public class Strings // http://redis.io/commands#string
    {
        [Test]
        public void Append()
        {
            using(var conn = Config.GetUnsecuredConnection(waitForOpen:true))
            {
                conn.Keys.Remove(2, "append");
                var l0 = conn.Features.StringLength ? conn.Strings.GetLength(2, "append") : null;

                var s0 = conn.Strings.GetString(2, "append");

                conn.Strings.Set(2, "append", "abc");
                var s1 = conn.Strings.GetString(2, "append");
                var l1 = conn.Features.StringLength ? conn.Strings.GetLength(2, "append") : null;

                var result = conn.Strings.Append(2, "append", Encode("defgh"));
                var s3 = conn.Strings.GetString(2, "append");
                var l2 = conn.Features.StringLength ? conn.Strings.GetLength(2, "append") : null;

                Assert.AreEqual(null, conn.Wait(s0));
                Assert.AreEqual("abc", conn.Wait(s1));
                Assert.AreEqual(8, conn.Wait(result));
                Assert.AreEqual("abcdefgh", conn.Wait(s3));

                if (conn.Features.StringLength)
                {
                    Assert.AreEqual(0, conn.Wait(l0));
                    Assert.AreEqual(3, conn.Wait(l1));
                    Assert.AreEqual(8, conn.Wait(l2));
                }
            }
        }
        [Test]
        public void Set()
        {
            using(var conn = Config.GetUnsecuredConnection())
            {
                conn.Keys.Remove(2, "set");

                conn.Strings.Set(2, "set", "abc");
                var v1 = conn.Strings.GetString(2, "set");

                conn.Strings.Set(2, "set", Encode("def"));
                var v2 = conn.Strings.Get(2, "set");

                Assert.AreEqual("abc", conn.Wait(v1));
                Assert.AreEqual("def", Decode(conn.Wait(v2)));
            }
        }
        [Test]
        public void SetNotExists()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Keys.Remove(2, "set");
                conn.Keys.Remove(2, "set2");
                conn.Keys.Remove(2, "set3");
                conn.Strings.Set(2, "set", "abc");

                var x0 = conn.Strings.SetIfNotExists(2, "set", "def");
                var x1 = conn.Strings.SetIfNotExists(2, "set", Encode("def"));
                var x2 = conn.Strings.SetIfNotExists(2, "set2", "def");
                var x3 = conn.Strings.SetIfNotExists(2, "set3", Encode("def"));

                var s0 = conn.Strings.GetString(2, "set");
                var s2 = conn.Strings.GetString(2, "set2");
                var s3 = conn.Strings.GetString(2, "set3");

                Assert.IsFalse(conn.Wait(x0));
                Assert.IsFalse(conn.Wait(x1));
                Assert.IsTrue(conn.Wait(x2));
                Assert.IsTrue(conn.Wait(x3));
                Assert.AreEqual("abc", conn.Wait(s0));
                Assert.AreEqual("def", conn.Wait(s2));
                Assert.AreEqual("def", conn.Wait(s3));
            }
        }
        
        [Test]
        public void Ranges()
        {
            using (var conn = Config.GetUnsecuredConnection(waitForOpen:true))
            {
                if (conn.Features.StringSetRange)
                {
                    conn.Keys.Remove(2, "range");

                    conn.Strings.Set(2, "range", "abcdefghi");
                    conn.Strings.Set(2, "range", 2, "xy");
                    conn.Strings.Set(2, "range", 4, Encode("z"));

                    var val = conn.Strings.GetString(2, "range");

                    Assert.AreEqual("abxyzfghi", conn.Wait(val));
                }
            }
        }

        [Test]
        public void IncrDecr()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Keys.Remove(2, "incr");

                conn.Strings.Set(2, "incr", "2");
                var v1 = conn.Strings.Increment(2, "incr");
                var v2 = conn.Strings.Increment(2, "incr", 5);
                var v3 = conn.Strings.Increment(2, "incr", -2);
                var v4 = conn.Strings.Decrement(2, "incr");
                var v5 = conn.Strings.Decrement(2, "incr", 5);
                var v6 = conn.Strings.Decrement(2, "incr", -2);
                var s = conn.Strings.GetString(2, "incr");

                Assert.AreEqual(3, conn.Wait(v1));
                Assert.AreEqual(8, conn.Wait(v2));
                Assert.AreEqual(6, conn.Wait(v3));
                Assert.AreEqual(5, conn.Wait(v4));
                Assert.AreEqual(0, conn.Wait(v5));
                Assert.AreEqual(2, conn.Wait(v6));
                Assert.AreEqual("2", conn.Wait(s));
            }
        }
        [Test]
        public void GetRange()
        {
            using (var conn = Config.GetUnsecuredConnection(waitForOpen:true))
            {   
                conn.Keys.Remove(2, "range");

                conn.Strings.Set(2, "range", "abcdefghi");
                var s = conn.Strings.GetString(2, "range", 2, 4);
                var b = conn.Strings.Get(2, "range", 2, 4);

                Assert.AreEqual("cde", conn.Wait(s));
                Assert.AreEqual("cde", Decode(conn.Wait(b)));
            }
        }

        static byte[] Encode(string value) { return Encoding.UTF8.GetBytes(value); }
        static string Decode(byte[] value) { return Encoding.UTF8.GetString(value); }
    }
}
