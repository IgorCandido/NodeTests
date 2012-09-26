using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookSleeve
{
    internal interface IMessageResult
    {
        void Complete(RedisResult result);
    }

    internal abstract class MessageResult<T> : IMessageResult
    {
        private readonly TaskCompletionSource<T> source = new TaskCompletionSource<T>();
        public Task<T> Task { get { return source.Task; } }
        public void Complete(RedisResult result)
        {
            if (result.IsCancellation)
            {
                source.SetCanceled();
            }
            else if (result.IsError)
            {
                source.SetException(result.Error());
            }
            else
            {
                T value;
                try
                {
                    value = GetValue(result);
                }
                catch (Exception ex)
                {
                    source.SetException(ex);
                    return;
                }
                source.SetResult(value);
            }
        }
        protected abstract T GetValue(RedisResult result);
    }
    internal sealed class MessageResultDouble : MessageResult<double>
    {
        protected override double  GetValue(RedisResult result) { return result.ValueDouble; }
    }
    internal sealed class MessageResultInt64 : MessageResult<long>
    {
        protected override long GetValue(RedisResult result) { return result.ValueInt64; }
    }
    internal sealed class MessageResultBoolean : MessageResult<bool>
    {
        protected override bool GetValue(RedisResult result) { return result.ValueBoolean; }
    }
    internal sealed class MessageResultString : MessageResult<string>
    {
        protected override string GetValue(RedisResult result) { return result.ValueString; }
    }
    internal sealed class MessageResultRaw : MessageResult<RedisResult>
    {
        protected override RedisResult GetValue(RedisResult result) { return result; }
    }
    internal sealed class MessageResultMultiString : MessageResult<string[]>
    {
        protected override string[] GetValue(RedisResult result) { return result.ValueItemsString(); }
    }
    internal sealed class MessageResultBytes : MessageResult<byte[]>
    {
        protected override byte[] GetValue(RedisResult result) { return result.ValueBytes; }
    }
    internal sealed class MessageResultMultiBytes : MessageResult<byte[][]>
    {
        protected override byte[][] GetValue(RedisResult result) { return result.ValueItemsBytes(); }
    }
    internal sealed class MessageResultPairs : MessageResult<KeyValuePair<byte[], double>[]>
    {
        protected override KeyValuePair<byte[], double>[] GetValue(RedisResult result) { return result.ExtractPairs(); }
    }
    internal sealed class MessageResultHashPairs : MessageResult<Dictionary<string, byte[]>>
    {
        protected override Dictionary<string, byte[]> GetValue(RedisResult result) { return result.ExtractHashPairs(); }
    }
    internal sealed class MessageResultStringPairs : MessageResult<Dictionary<string, string>>
    {
        protected override Dictionary<string, string> GetValue(RedisResult result) { return result.ExtractStringPairs(); }
    }
    internal sealed class MessageResultVoid : MessageResult<bool>
    {
        public new Task Task { get { return base.Task; } }
        protected override bool GetValue(RedisResult result) { result.Assert(); return true; }
    }
    internal sealed class MessageLockResult : MessageResult<bool>
    {
        protected override bool GetValue(RedisResult result)
        {
            var items = result.ValueItems;
            return items != null;
        }
    }
}
