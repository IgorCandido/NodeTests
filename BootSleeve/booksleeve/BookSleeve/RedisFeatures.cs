using System;
using System.Text;

namespace BookSleeve
{
    /// <summary>
    /// Provides basic information about the features available on a particular version of Redis
    /// </summary>
    public sealed class RedisFeatures
    {
        private readonly Version version;
        /// <summary>
        /// The Redis version of the server
        /// </summary>
        public Version Version { get { return version; } }
        /// <summary>
        /// Create a new RedisFeatures instance for the given version
        /// </summary>
        public RedisFeatures(Version version)
        {
            if (version == null) throw new ArgumentNullException("version");
            this.version = version;
        }

        internal static readonly Version v2_1_0 = new Version("2.1.0"),
                                         v2_1_1 = new Version("2.1.1"),
                                         v2_1_2 = new Version("2.1.2"),
                                         v2_1_3 = new Version("2.1.3"),
                                         v2_1_8 = new Version("2.1.8"),
                                         v2_2_0 = new Version("2.2.0"),
                                         v2_4_0 = new Version("2.4.0");
        /// <summary>
        /// Is the PERSIST operation supported?
        /// </summary>
        public bool Persist { get { return version >= v2_1_2; } }
        /// <summary>
        /// Can EXPIRE be used to set expiration on a key that is already volatile (i.e. has an expiration)?
        /// </summary>
        public bool ExpireOverwrite { get { return version >= v2_1_3; } }
        /// <summary>
        /// Does HDEL support varadic usage?
        /// </summary>
        public bool HashVaradicDelete { get { return version > v2_2_0; } }
        /// <summary>
        /// Is STRLEN available?
        /// </summary>
        public bool StringLength { get { return version >= v2_1_2; } }
        /// <summary>
        /// Is SETRANGE available?
        /// </summary>
        public bool StringSetRange { get { return version >= v2_1_8; } }
        /// <summary>
        /// Is RPUSHX and LPUSHX available?
        /// </summary>
        public bool PushIfNotExists { get { return version >= v2_1_1; } }
        /// <summary>
        /// Does SADD support varadic usage?
        /// </summary>
        public bool SetVaradicAddRemove { get { return version >= v2_4_0; } }
        /// <summary>
        /// Create a string representation of the available features
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder().Append("Features in ").Append(version).AppendLine()
                .Append("ExpireOverwrite: ").Append(ExpireOverwrite).AppendLine()
                .Append("Persist: ").Append(Persist).AppendLine();

            return sb.ToString();
        }
        /// <summary>
        /// Is LINSERT available?
        /// </summary>
        public bool ListInsert { get { return version >= v2_1_1; } }
    }
}
