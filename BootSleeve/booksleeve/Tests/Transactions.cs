using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class Transactions // http://redis.io/commands#transactions
    {
        [Test]
        public void TestBasicMultiExec()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Keys.Remove(1, "tran");
                conn.Keys.Remove(2, "tran");

                using (var tran = conn.CreateTransaction())
                {
                    var s1 = tran.Strings.Set(1, "tran", "abc");
                    var s2 = tran.Strings.Set(2, "tran", "def");
                    var g1 = tran.Strings.GetString(1, "tran");
                    var g2 = tran.Strings.GetString(2, "tran");

                    var outsideTran = conn.Strings.GetString(1, "tran");

                    var exec = tran.Execute();

                    Assert.IsNull(conn.Wait(outsideTran));
                    Assert.AreEqual("abc", conn.Wait(g1));
                    Assert.AreEqual("def", conn.Wait(g2));
                    conn.Wait(s1);
                    conn.Wait(s2);
                    conn.Wait(exec);
                }

            }
        }

        [Test]
        public void TestRollback()
        {
            using (var conn = Config.GetUnsecuredConnection())
            using (var tran = conn.CreateTransaction())
            {
                var task = tran.Strings.Set(4, "abc", "def");
                tran.Discard();

                Assert.IsTrue(task.IsCanceled, "should be cancelled");
                try
                {
                    conn.Wait(task);
                }
                catch (TaskCanceledException)
                { }// ok, else boom!

            }
        }

        [Test]
        public void TestDispose()
        {
            Task task;
            using (var conn = Config.GetUnsecuredConnection())
            {
                using (var tran = conn.CreateTransaction())
                {
                    task = tran.Strings.Set(4, "abc", "def");
                }
                Assert.IsTrue(task.IsCanceled, "should be cancelled");
                try
                {
                    conn.Wait(task);
                }
                catch (TaskCanceledException)
                { }// ok, else boom!
            }
        }

        [Test]
        public void BlogDemo()
        {
            int db = 8;
            using(var conn = Config.GetUnsecuredConnection())
            {
                conn.Keys.Remove(db, "foo"); // just to reset
                using(var tran = conn.CreateTransaction())
                {   // deliberately ignoring INCRBY here
                    tran.Strings.Increment(db, "foo");
                    tran.Strings.Increment(db, "foo");
                    var val = tran.Strings.GetString(db, "foo");

                    tran.Execute(); // this *still* returns a Task

                    Assert.AreEqual("2", conn.Wait(val));
                }
            }
        }
    }
}

