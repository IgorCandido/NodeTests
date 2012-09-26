using NUnit.Framework;
using System.Threading;
using System.Text;

namespace Tests
{
    [TestFixture]
    public class PubSub // http://redis.io/commands#pubsub
    {
        [Test]
        public void TestPublishWithNoSubscribers()
        {
            using (var conn = Config.GetUnsecuredConnection())
            {
                Assert.AreEqual(0, conn.Wait(conn.Publish("channel", "message")));
            }
        }
     
        [Test]
        public void TestPublishWithSubscribers()
        {
            using(var listenA = Config.GetSubscriberConnection())
            using(var listenB = Config.GetSubscriberConnection())
            using (var conn = Config.GetUnsecuredConnection())
            {
                listenA.Subscribe("channel", delegate { });
                listenB.Subscribe("channel", delegate { });
                int count = 0;
                while (listenA.SubscriptionCount == 0 || listenB.SubscriptionCount == 0)
                {
                    if (++count == 10) Assert.Fail();
                    Thread.Sleep(50);
                }
                Assert.AreEqual(2, conn.Wait(conn.Publish("channel", "message")));
            }
        }

        [Test]
        public void TestMultipleSubscribersGetMessage()
        {
            using (var listenA = Config.GetSubscriberConnection())
            using (var listenB = Config.GetSubscriberConnection())
            using (var conn = Config.GetUnsecuredConnection())
            {
                int gotA = 0, gotB = 0;
                listenA.Subscribe("channel", (s, msg) => { if (Encoding.UTF8.GetString(msg) == "message") Interlocked.Increment(ref gotA); });
                listenB.Subscribe("channel", (s, msg) => { if (Encoding.UTF8.GetString(msg) == "message") Interlocked.Increment(ref gotB); });
                Thread.Sleep(50);
                Assert.AreEqual(2, conn.Wait(conn.Publish("channel", "message")));
                Thread.Sleep(50);
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotA, 0, 0));
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotB, 0, 0));

                // and unsubscibe...
                listenA.Unsubscribe("channel");
                Thread.Sleep(50);
                Assert.AreEqual(1, conn.Wait(conn.Publish("channel", "message")));
                Thread.Sleep(50);
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotA, 0, 0));
                Assert.AreEqual(2, Interlocked.CompareExchange(ref gotB, 0, 0));
            }
        }

        [Test]
        public void TestPartialSubscriberGetMessage()
        {
            using (var listenA = Config.GetSubscriberConnection())
            using (var listenB = Config.GetSubscriberConnection())
            using (var conn = Config.GetUnsecuredConnection())
            {
                int gotA = 0, gotB = 0;
                listenA.Subscribe("channel", (s, msg) => { if (s=="channel" && Encoding.UTF8.GetString(msg) == "message") Interlocked.Increment(ref gotA); });
                listenB.PatternSubscribe("chann*", (s, msg) => { if (s=="channel" && Encoding.UTF8.GetString(msg) == "message") Interlocked.Increment(ref gotB); });
                Thread.Sleep(50);
                Assert.AreEqual(2, conn.Wait(conn.Publish("channel", "message")));
                Thread.Sleep(50);
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotA, 0, 0));
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotB, 0, 0));

                // and unsubscibe...
                listenB.PatternUnsubscribe("chann*");
                Thread.Sleep(50);
                Assert.AreEqual(1, conn.Wait(conn.Publish("channel", "message")));
                Thread.Sleep(50);
                Assert.AreEqual(2, Interlocked.CompareExchange(ref gotA, 0, 0));
                Assert.AreEqual(1, Interlocked.CompareExchange(ref gotB, 0, 0));
            }
        }

        [Test]
        public void TestSubscribeUnsubscribeAndSubscribeAgain()
        {
            using(var pub = Config.GetUnsecuredConnection())
            using(var sub = Config.GetSubscriberConnection())
            {
                int x = 0, y = 0;
                sub.Subscribe("abc", delegate { Interlocked.Increment(ref x); });
                sub.PatternSubscribe("ab*", delegate { Interlocked.Increment(ref y); });
                Thread.Sleep(50);
                pub.Publish("abc", "");
                Thread.Sleep(50);
                Assert.AreEqual(1, Thread.VolatileRead(ref x));
                Assert.AreEqual(1, Thread.VolatileRead(ref y));
                sub.Unsubscribe("abc");
                sub.PatternUnsubscribe("ab*");
                Thread.Sleep(50);
                pub.Publish("abc", "");
                Assert.AreEqual(1, Thread.VolatileRead(ref x));
                Assert.AreEqual(1, Thread.VolatileRead(ref y));
                sub.Subscribe("abc", delegate { Interlocked.Increment(ref x); });
                sub.PatternSubscribe("ab*", delegate { Interlocked.Increment(ref y); });
                Thread.Sleep(50);
                pub.Publish("abc", "");
                Thread.Sleep(50);
                Assert.AreEqual(2, Thread.VolatileRead(ref x));
                Assert.AreEqual(2, Thread.VolatileRead(ref y));
                
            }
        }
    }
}
