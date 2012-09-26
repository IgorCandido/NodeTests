using System.Linq;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class SortedSets // http://redis.io/commands#sorted_set
    {
        [Test]
        public void Range() // http://code.google.com/p/booksleeve/issues/detail?id=12
        {
            using(var conn = Config.GetUnsecuredConnection())
            {
                const double value = 634614442154715;
                conn.SortedSets.Add(3, "zset", "abc", value);
                var range = conn.SortedSets.Range(3, "zset", 0, -1);

                Assert.AreEqual(value, conn.Wait(range).Single().Value);
            }
        }
    }
}
