using System;
using System.Net.Sockets;
using BookSleeve;
using NUnit.Framework;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tests
{
    [TestFixture(Description="Validates that the test environment is configured and responding")]
    public class Config
    {
        static Config()
        {
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Trace.WriteLine(args.Exception,"UnobservedTaskException");
                args.SetObserved();
            };
        }

        const string host = "127.0.0.1";
        const int unsecuredPort = 6379, securedPort = 6380;

        internal static RedisConnection GetUnsecuredConnection(bool open = true, bool allowAdmin = false, bool waitForOpen = false)
        {
            var conn = new RedisConnection(host, unsecuredPort, syncTimeout: 5000, ioTimeout: 5000, allowAdmin: allowAdmin);
            conn.Error += (s, args) =>
            {
                Trace.WriteLine(args.Exception.Message, args.Cause);
            };
            if (open)
            {
                var openAsync = conn.Open();
                if (waitForOpen) conn.Wait(openAsync);
            }
            return conn;
        }

        internal static RedisSubscriberConnection GetSubscriberConnection()
        {
            var conn = new RedisSubscriberConnection(host, unsecuredPort);
            conn.Error += (s, args) =>
            {
                Trace.WriteLine(args.Exception.Message, args.Cause);
            };
            conn.Open();
            return conn;
        }
        internal static RedisConnection GetSecuredConnection(bool open = true)
        {
            var conn = new RedisConnection(host, securedPort, password: "changeme", syncTimeout: 60000, ioTimeout: 5000);
            conn.Error += (s, args) =>
            {
                Trace.WriteLine(args.Exception.Message, args.Cause);
            };
            if (open) conn.Open();
            return conn;
        }

        [Test]
        public void CanOpenUnsecuredConnection()
        {
            using (var conn = GetUnsecuredConnection(false))
            {
                Assert.IsNull(conn.ServerVersion);
                conn.Wait(conn.Open());
                Assert.IsNotNull(conn.ServerVersion);
            }
        }

        [Test]
        public void CanOpenSecuredConnection()
        {
            using (var conn = GetSecuredConnection(false))
            {
                Assert.IsNull(conn.ServerVersion);
                conn.Wait(conn.Open());
                Assert.IsNotNull(conn.ServerVersion);
            }
        }

        [Test, ExpectedException(typeof(SocketException))]
        public void CanNotOpenNonsenseConnection()
        {
            using (var conn = new RedisConnection("127.0.0.1", 6500))
            {
                conn.Wait(conn.Open());
            }
        }
    }
}

