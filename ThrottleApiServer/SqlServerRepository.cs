using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebApiThrottle;

namespace ThrottleApiServer
{
    public class SqlServerRepository : IThrottleRepository
    {
        private static ConcurrentDictionary<string, long> max = new ConcurrentDictionary<string, long>();
        private static ConcurrentDictionary<string, string> cache =new ConcurrentDictionary<string, string>();

        private string connString;
        private string clientName = "sql_repo";
        private static object lockersObj = new object();
        private static Dictionary<string, SemaphoreSlim> lockers = new Dictionary<string, SemaphoreSlim>();

        public SqlServerRepository(string connString)
        {
            this.connString = connString;
        }

        public bool Any(string id)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public ThrottleCounter? FirstOrDefault(string id)
        {
            throw new NotImplementedException();
        }

        public void Remove(string id)
        {
            throw new NotImplementedException();
        }

        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            throw new NotImplementedException();
        }
    }
}
