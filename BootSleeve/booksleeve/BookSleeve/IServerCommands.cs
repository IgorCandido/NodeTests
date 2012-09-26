
using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;
namespace BookSleeve
{
    /// <summary>
    /// Commands related to server operation and configuration, rather than data.
    /// </summary>
    /// <remarks>http://redis.io/commands#server</remarks>
    public interface IServerCommands
    {
        /// <summary>
        /// Delete all the keys of the currently selected DB.
        /// </summary>
        /// <remarks>http://redis.io/commands/flushdb</remarks>
        Task FlushDb(int db);

        /// <summary>
        /// Delete all the keys of all the existing databases, not just the currently selected one.
        /// </summary>
        /// <remarks>http://redis.io/commands/flushall</remarks>
        Task FlushAll();

        /// <summary>
        /// This command is often used to test if a connection is still alive, or to measure latency.
        /// </summary>
        /// <returns>The latency in milliseconds.</returns>
        /// <remarks>http://redis.io/commands/ping</remarks>
        Task<long> Ping(bool queueJump = false);

        /// <summary>
        /// Get all configuration parameters matching the specified pattern.
        /// </summary>
        /// <param name="pattern">All the configuration parameters matching this parameter are reported as a list of key-value pairs.</param>
        /// <returns>All matching configuration parameters.</returns>
        /// <remarks>http://redis.io/commands/config-get</remarks>
        Task<Dictionary<string,string>> GetConfig(string pattern);

        /// <summary>
        /// The CONFIG SET command is used in order to reconfigure the server at runtime without the need to restart Redis. You can change both trivial parameters or switch from one to another persistence option using this command.
        /// </summary>
        /// <remarks>http://redis.io/commands/config-set</remarks>
        Task SetConfig(string parameter, string value);

        /// <summary>
        /// The SLAVEOF command can change the replication settings of a slave on the fly. In the proper form SLAVEOF hostname port will make the server a slave of another server listening at the specified hostname and port.
        /// If a server is already a slave of some master, SLAVEOF hostname port will stop the replication against the old server and start the synchronization against the new one, discarding the old dataset.
        /// </summary>
        /// <remarks>http://redis.io/commands/slaveof</remarks>
        Task MakeSlave(string host, int port);
        /// <summary>
        /// The SLAVEOF command can change the replication settings of a slave on the fly. 
        /// If a Redis server is already acting as slave, the command SLAVEOF NO ONE will turn off the replication, turning the Redis server into a MASTER.
        /// The form SLAVEOF NO ONE will stop replication, turning the server into a MASTER, but will not discard the replication. So, if the old master stops working, it is possible to turn the slave into a master and set the application to use this new master in read/write. Later when the other Redis server is fixed, it can be reconfigured to work as a slave.
        /// </summary>
        Task MakeMaster();
    }
    partial class RedisConnection : IServerCommands
    {
        /// <summary>
        /// Commands related to server operation and configuration, rather than data.
        /// </summary>
        /// <remarks>http://redis.io/commands#server</remarks>
        public IServerCommands Server
        {
            get { return this; }
        }

        /// <summary>
        /// Delete all the keys of the currently selected DB.
        /// </summary>
        [Obsolete("Please use the Server API", false), EditorBrowsable(EditorBrowsableState.Never)]
        public Task FlushDb(int db)
        {
            return Server.FlushDb(db);
        }
        Task IServerCommands.FlushDb(int db)
        {
            if (allowAdmin)
            {
                return ExecuteVoid(RedisMessage.Create(db, RedisLiteral.FLUSHDB).ExpectOk().Critical(), false);
            }
            else
                throw new InvalidOperationException("Flush is not enabled");
        }
        /// <summary>
        /// Delete all the keys of all the existing databases, not just the currently selected one.
        /// </summary>
        [Obsolete("Please use the Server API", false), EditorBrowsable(EditorBrowsableState.Never)]
        public Task FlushAll()
        {
            return Server.FlushAll();
        }

        Task IServerCommands.FlushAll()
        {
            if (allowAdmin)
            {
                return ExecuteVoid(RedisMessage.Create(-1, RedisLiteral.FLUSHALL).ExpectOk().Critical(), false);
            }
            else
                throw new InvalidOperationException("Flush is not enabled");
        }

        Task IServerCommands.MakeMaster()
        {
            if (allowAdmin)
            {
                return ExecuteVoid(RedisMessage.Create(-1, RedisLiteral.SLAVEOF, RedisLiteral.NO, RedisLiteral.ONE).ExpectOk().Critical(), false);
            }
            else
                throw new InvalidOperationException("MakeMaster is not enabled");
        }
        Task IServerCommands.MakeSlave(string host, int port)
        {
            if (allowAdmin)
            {
                return ExecuteVoid(RedisMessage.Create(-1, RedisLiteral.SLAVEOF, host, port).ExpectOk().Critical(), false);
            }
            else
                throw new InvalidOperationException("MakeSlave is not enabled");
        }

        /// <summary>
        /// This command is often used to test if a connection is still alive, or to measure latency.
        /// </summary>
        /// <returns>The latency in milliseconds.</returns>
        /// <remarks>http://redis.io/commands/ping</remarks>
        [Obsolete("Please use the Server API", false), EditorBrowsable(EditorBrowsableState.Never)]
        public new Task<long> Ping(bool queueJump = false)
        {
            return Server.Ping(queueJump);
        }

        Task<long> IServerCommands.Ping(bool queueJump)
        {
            return base.Ping(queueJump);
        }

        Task<Dictionary<string, string>> IServerCommands.GetConfig(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) pattern = "*";
            return ExecuteStringPairs(RedisMessage.Create(-1, RedisLiteral.CONFIG, RedisLiteral.GET, pattern), false);
        }

        Task IServerCommands.SetConfig(string name, string value)
        {
            if (allowAdmin)
            {
                return
                    ExecuteVoid(RedisMessage.Create(-1, RedisLiteral.CONFIG, RedisLiteral.SET, name, value).ExpectOk(),
                                false);
            } else
            {
                throw new InvalidOperationException("SetConfig is not enabled");
            }
        }
    }
}
