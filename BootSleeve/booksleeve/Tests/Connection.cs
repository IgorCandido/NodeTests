using NUnit.Framework;
using System;

namespace Tests
{
    [TestFixture]
    public class Connections // http://redis.io/commands#connection
    {
        // AUTH is already tested by secured connection

        // QUIT is implicit in dispose

        // ECHO has little utility in an application

        [Test]
        public void TestGetSetOnDifferentDbHasDifferentValues()
        {
            // note: we don't expose SELECT directly, but we can verify that we have different DBs in play:

            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Strings.Set(1, "select", "abc");
                conn.Strings.Set(2, "select", "def");
                var x = conn.Strings.GetString(1, "select");
                var y = conn.Strings.GetString(2, "select");
                conn.WaitAll(x, y);
                Assert.AreEqual("abc", x.Result);
                Assert.AreEqual("def", y.Result);
            }
        }
        [Test, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestGetOnInvalidDbThrows()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Strings.GetString(-1, "select");                
            }
        }


        [Test]
        public void Ping()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                var ms = conn.Wait(conn.Server.Ping());
                Assert.GreaterOrEqual(ms, 0);
            }
        }

        [Test]
        public void CheckCounters()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                conn.Wait(conn.Strings.GetString(0, "select"));
                var first = conn.GetCounters();

                conn.Wait(conn.Strings.GetString(0, "select"));
                var second = conn.GetCounters();
                // +2 = ping + one select
                Assert.AreEqual(first.MessagesSent + 2, second.MessagesSent, "MessagesSent");
                Assert.AreEqual(first.MessagesReceived + 2, second.MessagesReceived, "MessagesReceived");
                Assert.AreEqual(0, second.ErrorMessages, "ErrorMessages");
                Assert.AreEqual(0, second.MessagesCancelled, "MessagesCancelled");
                Assert.AreEqual(0, second.SentQueue, "SentQueue");
                Assert.AreEqual(0, second.UnsentQueue, "UnsentQueue");
                Assert.AreEqual(0, second.QueueJumpers, "QueueJumpers");
                Assert.AreEqual(0, second.Timeouts, "Timeouts");
                Assert.IsTrue(second.Ping >= 0, "Ping");
                Assert.IsTrue(second.ToString().Length > 0, "ToString");
            }
        }

        
    }
}
