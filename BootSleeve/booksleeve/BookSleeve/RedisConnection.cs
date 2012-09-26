using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace BookSleeve
{
    /// <summary>
    /// A thread-safe, multiplexed connection to a Redis server; each connection
    /// should be cached and re-used (without synchronization) from multiple
    /// callers for maximum efficiency. Usually only a single RedisConnection
    /// is required
    /// </summary>
    public partial class RedisConnection : RedisConnectionBase
    {
        /// <summary>
        /// Constants representing the different storage devices in redis
        /// </summary>
        public static class ItemTypes
        {
            /// <summary>
            /// Returned for a key that does not exist
            /// </summary>
            public const string None = "none";
            /// <summary>
            /// Redis Lists are simply lists of strings, sorted by insertion order. It is possible to add elements to a Redis List pushing new elements on the head (on the left) or on the tail (on the right) of the list.
            /// </summary>
            /// <remarks>http://redis.io/topics/data-types#lists</remarks>
            public const string List = "list";
            /// <summary>
            /// Strings are the most basic kind of Redis value. Redis Strings are binary safe, this means that a Redis string can contain any kind of data, for instance a JPEG image or a serialized Ruby object.
            /// </summary>
            /// <remarks>http://redis.io/topics/data-types#strings</remarks>
            public const string String = "string";
            /// <summary>
            /// Redis Sets are an unordered collection of Strings. It is possible to add, remove, and test for existence of members in O(1) (constant time regardless of the number of elements contained inside the Set).
            /// </summary>
            /// <remarks>http://redis.io/topics/data-types#sets</remarks>
            public const string Set = "set";
            /// <summary>
            /// Redis Sorted Sets are, similarly to Redis Sets, non repeating collections of Strings. The difference is that every member of a Sorted Set is associated with score, that is used in order to take the sorted set ordered, from the smallest to the greatest score.
            /// </summary>
            /// <remarks>http://redis.io/topics/data-types#sorted-sets</remarks>
            public const string SortedSet = "zset";
            /// <summary>
            /// Redis Hashes are maps between string field and string values, so they are the perfect data type to represent objects (for instance Users with a number of fields like name, surname, age, and so forth)
            /// </summary>
            /// <remarks>http://redis.io/topics/data-types#hashes</remarks>
            public const string Hash = "hash";
        }
        internal const bool DefaultAllowAdmin = false;
        /// <summary>
        /// Creates a new RedisConnection to a designated server
        /// </summary>
        public RedisConnection(string host, int port = 6379, int ioTimeout = -1, string password = null, int maxUnsent = int.MaxValue, bool allowAdmin = DefaultAllowAdmin, int syncTimeout = DefaultSyncTimeout)
            : base(host, port, ioTimeout, password, maxUnsent, syncTimeout)
        {
            this.allowAdmin = allowAdmin;
            this.sent = new Queue<RedisMessage>();
        }
        /// <summary>
        /// Creates a child RedisConnection, such as for a RedisTransaction
        /// </summary>
        protected RedisConnection(RedisConnection parent) : base(
            parent.Host, parent.Port, parent.IOTimeout, parent.Password, int.MaxValue, parent.SyncTimeout)
        {
            this.allowAdmin = parent.allowAdmin;
            this.sent = new Queue<RedisMessage>();
        }
        /// <summary>
        /// Allows multiple commands to be buffered and sent to redis as a single atomic unit
        /// </summary>
        public virtual RedisTransaction CreateTransaction()
        {
            return new RedisTransaction(this);
        }
        private RedisSubscriberConnection subscriberChannel;

        private RedisSubscriberConnection SubscriberFactory()
        {
            var conn = new RedisSubscriberConnection(Host, Port, IOTimeout, Password, 100);
            conn.Error += OnError;
            conn.Open();
            return conn;
        }
        /// <summary>
        /// Configures an automatic keep-alive PING at a pre-determined interval.
        /// </summary>
        public new void SetKeepAlive(int seconds)
        {
            base.SetKeepAlive(seconds);
        }
        /// <summary>
        /// Called during connection init, but after the AUTH is sent (if needed)
        /// </summary>
        protected override void OnInitConnection()
        {
            base.OnInitConnection();

            var options = Server.GetConfig("timeout");
            options.ContinueWith(x =>
            {
                if(x.IsFaulted)
                {
                    var ex = x.Exception; // need to yank this to make TPL happy, but not going to get excited about it
                }
                else if(x.IsCompleted)
                {
                    int timeout;
                    string text;
                    if(x.Result.TryGetValue("timeout", out text) && int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out timeout))
                    {
                        SetKeepAlive(timeout - 15); // allow a few seconds contingency
                    }
                }
            });
        }

        /// <summary>
        /// Creates a pub/sub connection to the same redis server
        /// </summary>
        public RedisSubscriberConnection GetOpenSubscriberChannel()
        {
            // use (atomic) reference test for a lazy quick answer
            if (subscriberChannel != null) return subscriberChannel;
            RedisSubscriberConnection newValue = null;
            try
            {
                newValue = SubscriberFactory();
                if (Interlocked.CompareExchange(ref subscriberChannel, newValue, null) == null)
                {
                    // the field was null; we won the race; happy happy
                    var tmp = newValue;
                    newValue = null;
                    return tmp;
                }
                else
                {
                    // we lost the race; use Interlocked to be sure we report the right thing
                    return Interlocked.CompareExchange(ref subscriberChannel, null, null);
                }
            }
            finally
            {
                // if newValue still has a value, we failed to swap it; perhaps we
                // lost the thread race, or perhaps an exception was thrown; either way,
                // that sucka is toast
                using (newValue as IDisposable) 
                {
                }
            }
        }
        /// <summary>
        /// Releases any resources associated with the connection
        /// </summary>
        public override void Dispose()
        {
            var subscribers = subscriberChannel;
            if (subscribers != null) subscribers.Dispose();
            base.Dispose();
        }
        internal override object ProcessReply(ref RedisResult result)
        {
            RedisMessage message;
            lock (sent)
            {
                int count = sent.Count;
                if (count == 0) throw new RedisException("Data received with no matching message");
                message = sent.Dequeue();
                if (count == 1) Monitor.Pulse(sent); // in case the outbound stream is closing and needs to know we're up-to-date
            }
            return ProcessReply(ref result, message);
        }

        internal override object ProcessReply(ref RedisResult result, RedisMessage message)
        {
            byte[] expected;
            if (!result.IsError && (expected = message.Expected) != null)
            {
                result = result.IsMatch(expected)
                ? RedisResult.Pass : RedisResult.Error(result.ValueString);
            }


            if (result.IsError && message.MustSucceed)
            {
                throw new RedisException("A critical operation failed: " + message.ToString());
            }
            return message;
        }
        internal override void ProcessCallbacks(object ctx, RedisResult result)
        {
            CompleteMessage((RedisMessage)ctx, result);
        }
        /// <summary>
        /// Invoked when the server is terminating
        /// </summary>
        protected override void ShuttingDown(Exception error)
        {
            base.ShuttingDown(error);
            RedisMessage message;
            RedisResult result = null;

            lock (sent)
            {
                if (sent.Count > 0)
                {
                    result = RedisResult.Error(
                        error == null ? "The server terminated before a reply was received"
                        : ("Error processing data: " + error.Message));
                }
                while (sent.Count > 0)
                { // notify clients of things that just didn't happen

                    message = sent.Dequeue();
                    CompleteMessage(message, result);
                }
            }
        }
        private readonly Queue<RedisMessage> sent;
        private readonly bool allowAdmin;
        /// <summary>
        /// Query usage metrics for this connection
        /// </summary>
        public Counters GetCounters()
        {
            int messagesSent, messagesReceived, queueJumpers, messagesCancelled, unsent, errorMessages, timeouts;
            GetCounterValues(out messagesSent, out messagesReceived, out queueJumpers, out messagesCancelled, out unsent, out errorMessages, out timeouts);
            return new Counters(
                messagesSent, messagesReceived, queueJumpers, messagesCancelled,
                timeouts, unsent, errorMessages,
                GetSentCount(),
                GetDbUsage(),
                // important that ping happens last, as this may artificially drain the queues
                (int)Wait(Server.Ping())
            );
        }
        private int GetSentCount() { lock (sent) { return sent.Count; } }
        private DateTime opened;
        internal override void RecordSent(RedisMessage message, bool drainFirst)
        {
            base.RecordSent(message, drainFirst);

            lock (sent)
            {
                if (drainFirst && sent.Count != 0)
                {
                    // drain it down; the dequeuer will wake us
                    Monitor.Wait(sent);
                }
                sent.Enqueue(message);
            }
        }


        /// <summary>
        /// Give some information about the oldest incomplete (but sent) message on the server
        /// </summary>
        protected override string GetTimeoutSummary()
        {
            RedisMessage msg;
            lock (sent)
            {
                if (sent.Count == 0) return null;
                msg = sent.Peek();
            }
            return msg.ToString();
        }

        /// <summary>
        /// Called after opening a connection
        /// </summary>
        protected override void OnOpened()
        {
            base.OnOpened();
            this.opened = DateTime.UtcNow;
        }



        /// <summary>
        /// Takes a server out of "slave" mode, to act as a replication master.
        /// </summary>
        [Obsolete("Please use the Server API")]
        public Task PromoteToMaster()
        {
            return Server.MakeMaster();
        }
 
       

        /// <summary>
        /// Posts a message to the given channel.
        /// </summary>
        /// <returns>the number of clients that received the message.</returns>
        public Task<long> Publish(string key, string value, bool queueJump = false)
        {
            return ExecuteInt64(RedisMessage.Create(-1, RedisLiteral.PUBLISH, key, value), queueJump);
        }
        /// <summary>
        /// Posts a message to the given channel.
        /// </summary>
        /// <returns>the number of clients that received the message.</returns>
        public Task<long> Publish(string key, byte[] value, bool queueJump = false)
        {
            return ExecuteInt64(RedisMessage.Create(-1, RedisLiteral.PUBLISH, key, value), queueJump);
        }

        /// <summary>
        /// Indicates the number of messages that have not yet been sent to the server.
        /// </summary>
        public override int OutstandingCount
        {
            get
            {
                return base.OutstandingCount + GetSentCount();
            }
        }
    }
}
