using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Runtime.Serialization;

namespace BookSleeve
{
    internal abstract class RedisResult
    {
        internal static RedisResult Message(byte[] value) { return new MessageRedisResult(value); }
        internal static RedisResult Error(string value) { return new ErrorRedisResult(value); }
        internal static RedisResult Integer(long value) { return new Int64RedisResult(value); }
        internal static RedisResult Bytes(byte[] value) { return new BytesRedisResult(value); }
        internal static RedisResult Assert(string error)
        {
            return error == null ? RedisResult.Pass : new ErrorRedisResult(error);
        }
        internal void Assert()
        {
            if (IsError) throw Error();
        }
        public bool IsMatch(byte[] expected)
        {
            var bytes = ValueBytes;
            if (expected == null && bytes == null) return true;
            if (expected == null || bytes == null || expected.Length != bytes.Length) return false;
            for (int i = 0; i < bytes.Length; i++)
                if (expected[i] != bytes[i]) return false;
            return true;
        }
        public virtual Exception Error() { return new InvalidOperationException("This operation is not supported by " + GetType().Name); }
        public virtual long ValueInt64 { get { return int.Parse(ValueString); } }
        public bool ValueBoolean { get { return ValueInt64 != 0; } }
        public virtual string ValueString
        {
            get
            {
                byte[] bytes;
                return (bytes = ValueBytes) == null ? null :
                    bytes.Length == 0 ? "" :
                    Encoding.UTF8.GetString(bytes);
            }
        }
        public virtual byte[] ValueBytes { get { throw Error(); } }
        public virtual RedisResult[] ValueItems { get { return null; } }
        public virtual bool IsError { get { return false; } }
        public virtual bool IsCancellation { get { return false; } }
        public virtual double ValueDouble { get { return double.Parse(ValueString, CultureInfo.InvariantCulture); } }

        private class Int64RedisResult : RedisResult
        {
            internal Int64RedisResult(long value) { this.value = value; }
            private readonly long value;
            public override long ValueInt64 { get { return value; } }
            public override string ValueString { get { return value.ToString(); } }
            public override double ValueDouble { get { return value; } }
        }
        private class MessageRedisResult : RedisResult
        {
            internal MessageRedisResult(byte[] value) { this.value = value; }
            private readonly byte[] value;
            public override byte[] ValueBytes { get { return value; } }
        }
        internal class TimingRedisResult : RedisResult
        {
            private readonly TimeSpan send, receive;
            internal TimingRedisResult(TimeSpan send, TimeSpan receive) { this.send = send; this.receive = receive; }
            internal TimeSpan Send { get { return send; } }
            internal TimeSpan Receive { get { return receive; } }
            public override long ValueInt64{ get{return (long)(send.TotalMilliseconds + receive.TotalMilliseconds); } }
            public override string ValueString{ get{ return ValueInt64.ToString(); } }
            public override byte[] ValueBytes { get { return Encoding.UTF8.GetBytes(ValueString); } }
        }
        private class ErrorRedisResult : RedisResult
        {

            internal ErrorRedisResult(string message) { this.message = message; }
            private readonly string message;
            public override bool IsError { get { return true; } }
            public override Exception Error() { return new RedisException(message); }
            public override RedisResult[] ValueItems { get { throw Error(); } }
        }
        private class BytesRedisResult : RedisResult
        {
            internal BytesRedisResult(byte[] value) { this.value = value; }
            private readonly byte[] value;
            public override byte[] ValueBytes { get { return value; } }
        }
        public static readonly RedisResult Pass = new PassRedisResult(),
            TimeoutNotSent = new TimeoutRedisResult("Timeout; the messsage was not sent"),
            TimeoutSent = new TimeoutRedisResult("Timeout; the messsage was sent and may still have effect"),
            Cancelled = new CancellationRedisResult();

        private class PassRedisResult : RedisResult
        {
            internal PassRedisResult() { }
        }
        private class CancellationRedisResult : RedisResult
        {
            public override bool IsCancellation
            {
                get
                {
                    return true;
                }
            }
            public override bool IsError
            {
                get
                {
                    return true;
                }
            }
            public override Exception Error()
            {
                return new InvalidOperationException("The message was cancelled");
            }
        }
        private class TimeoutRedisResult : RedisResult
        {
            private readonly string message;
            public TimeoutRedisResult(string message) { this.message = message; }
            public override bool IsError { get { return true; } }
            public override Exception Error() { return new TimeoutException(message); }
            public override RedisResult[] ValueItems {get { throw Error(); }}
        }

        internal static RedisResult Multi(RedisResult[] inner)
        {
            return new MultiRedisResult(inner);
        }

        public string[] ValueItemsString()
        {
            var items = ValueItems;
            if (items == null) return null;
            string[] result = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                result[i] = items[i].ValueString;
            }
            return result;
        }
        public byte[][] ValueItemsBytes()
        {
            var items = ValueItems;
            if (items == null) return null;
            byte[][] result = new byte[items.Length][];
            for (int i = 0; i < items.Length; i++)
            {
                result[i] = items[i].ValueBytes;
            }
            return result;
        }
        public KeyValuePair<byte[], double>[] ExtractPairs()
        {
            var items = this.ValueItems;
            KeyValuePair<byte[], double>[] pairs = new KeyValuePair<byte[], double>[items.Length / 2];
            int index = 0;
            for (int i = 0; i < pairs.Length; i++)
            {
                var itemKey = items[index++].ValueBytes;
                var itemScore = items[index++].ValueDouble;
                pairs[i] = new KeyValuePair<byte[], double>(itemKey, itemScore);
            }
            return pairs;
        }
        public Dictionary<string, byte[]> ExtractHashPairs()
        {
            var items = this.ValueItems;
            int count = items.Length / 2;
            var dict = new Dictionary<string, byte[]>(count);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                var itemKey = items[index++].ValueString;
                var itemValue = items[index++].ValueBytes;
                dict.Add(itemKey, itemValue);
            }
            return dict;
        }
        public Dictionary<string, string> ExtractStringPairs()
        {
            var items = this.ValueItems;
            int count = items.Length / 2;
            var dict = new Dictionary<string, string>(count);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                var itemKey = items[index++].ValueString;
                var itemValue = items[index++].ValueString;
                dict.Add(itemKey, itemValue);
            }
            return dict;
        }
    }
    internal class MultiRedisResult : RedisResult
    {
        private readonly RedisResult[] items;
        public override RedisResult[] ValueItems { get { return items; } }
        public MultiRedisResult(RedisResult[] items) { this.items = items; }
    }
    /// <summary>
    /// An redis-related exception; this could represent a message from the server,
    /// or a protocol error talking to the server.
    /// </summary>
    [Serializable]
    public sealed class RedisException : Exception
    {
        /// <summary>
        /// Create a new RedisException
        /// </summary>
        public RedisException() {}
        /// <summary>
        /// Create a new RedisException
        /// </summary>
        public RedisException(string message) : base(message)  { }
        /// <summary>
        /// Create a new RedisException
        /// </summary>
        public RedisException(string message, Exception innerException) : base(message, innerException) { }
        private RedisException(SerializationInfo info, StreamingContext context)  : base(info, context) {}
    }

    internal enum MessageState
    {
        NotSent, Sent, Complete, Cancelled
    }
}
