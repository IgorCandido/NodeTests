using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BookSleeve
{
    /// <summary>
    /// Represents a group of redis messages that will be sent as a single atomic 
    /// </summary>
    public sealed class RedisTransaction : RedisConnection
    {
        /// <summary>
        /// Features available to the redis server
        /// </summary>
        public override RedisFeatures Features { get { return parent.Features; } }
        /// <summary>
        /// The version of the connected redis server
        /// </summary>
        public override Version ServerVersion { get { return parent.ServerVersion; } }

        private RedisConnection parent;
        internal RedisTransaction(RedisConnection parent) : base(parent)
        {
            this.parent = parent;
        }
        /// <summary>
        /// Not supported, as nested transactions are not available.
        /// </summary>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Nested transactions are not supported", true)]
#pragma warning disable 809
        public override RedisTransaction CreateTransaction()
#pragma warning restore 809
        {
            throw new NotSupportedException("Nested transactions are not supported");
        }
        /// <summary>
        /// Release any resources held by this transaction.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            Discard();
        }
        /// <summary>
        /// Called before opening a connection
        /// </summary>
        protected override void OnOpening()
        {
            throw new InvalidOperationException("A transaction is linked to the parent connection, and does not require opening");
        }
        /// <summary>
        /// Sends all currently buffered commands to the redis server in a single unit; the transaction may subsequently be re-used to buffer additional blocks of commands if needed.
        /// </summary>
        public Task Execute(bool queueJump = false)
        {
            var all = DequeueAll();
            if (all.Length == 0)
            {
                TaskCompletionSource<bool> nix = new TaskCompletionSource<bool>();
                nix.SetResult(true);
                return nix.Task;
            }
            var multiMessage = new MultiMessage(parent, all);
            parent.EnqueueMessage(multiMessage, queueJump);
            return multiMessage.Completion;
        }
        /// <summary>
        /// Discards any buffered commands; the transaction may subsequently be re-used to buffer additional blocks of commands if needed.
        /// </summary>
        public void Discard()
        {
            CancelUnsent();
        }
    }
}

