using Dapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
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
        private static ConcurrentDictionary<string, ThrottleCounterInCache> cache =new ConcurrentDictionary<string, ThrottleCounterInCache>();

        private string connString;
        private SemaphoreSlim locker = new SemaphoreSlim(1);

        public SqlServerRepository(string connString)
        {
            this.connString = connString;
        }

        public bool Any(string id)
        {
            using (var conn = new SqlConnection(connString))
            {
                string sql = $"SELECT * FROM ApiThrottleCounter WITH(NOLOCK) WHERE Id = N'{id}'";
                var result = conn.Query<ApiThrottleCounter>(sql);
                return result != null && result.Any();
            }
        }

        public void Clear()
        {
            // 이 repo는 자동 만료되기 때문에 여기서 아무것도 하지 않아도 된다.
        }

        public ThrottleCounter? FirstOrDefault(string id)
        {
            ThrottleCounterInDb entry = IncreaseVar(id, new TimeSpan(0, 0, 1)); // 1초당 Throttling

            if (entry.OldVal != null)
            {
                if (entry.OldCreatedTime < entry.CreatedTime)
                {
                    long maxTPS = max.GetOrAdd(id, entry.OldVal.Value);
                    if (maxTPS < entry.OldVal)
                    {
                        max.AddOrUpdate(id, entry.OldVal.Value, (k, v) => entry.OldVal.Value);
                    }
                }
            }

            return new ThrottleCounter()
            {
                Timestamp = entry.CreatedTime,
                TotalRequests = entry.Val - 1 // 다른 repo에서는 현재 요청건은 증가하지 않기 때문에 호화성 유지를 위해 -1 해준다.
            };
        }

        public void Remove(string id)
        {
            using (var conn = new SqlConnection(connString))
            {
                string sql = $"DELETE FROM ApiThrottleCounter WHERE Id = N'{id}'";
                conn.Execute(sql, null, commandType: System.Data.CommandType.Text);
            }
        }

        public void Save(string id, ThrottleCounter throttleCounter, TimeSpan expirationTime)
        {
            // 이 repo는 여기서 아무것도 하지 않아도 된다.
        }

        /// <summary>
        /// 기존 카운터에서 1증가된 값을 리턴한다.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="expirationTime"></param>
        /// <returns></returns>
        private ThrottleCounterInDb IncreaseVar(string id, TimeSpan expirationTime)
        {
            int rate = 10;  // 10번 후 1회만 db와 sync (RDB로 인한 성능 저하를 보완하기 위함)

            ThrottleCounterInCache inCache;
            ThrottleCounterInDb result;
            bool locked = false;

            // DB와 동기화 할 카운트 값(syncCount)을 구하고 이 값이 0보다 크면 DB와 동기화 한다.
            int syncCount = 0;
            try
            {
                locker.Wait();
                locked = true;

                DateTime now = DateTime.Now;
                // 메모리에 값이 있으면 +1
                if (cache.TryGetValue(id, out inCache))
                {
                    inCache.TempCount++;
                }
                else
                {
                    // 없으면 새로 메모리 카운터 생성
                    inCache = new ThrottleCounterInCache
                    {
                        ExpirationTime = expirationTime,
                        SynchronizedCount = 0,
                        TempCount = 1,
                        ExpirationDate = now.Add(expirationTime),
                        Timestamp = now
                    };
                }

                // 동기화할 카운트 조회
                syncCount = inCache.GetToSyncCount(rate, now);
                if (syncCount > 0)
                {
                    // db와 동기화하는 시간이 오래 걸릴 수 있으니 lock 구간을 해제하고
                    // 중복으로 동기화가 되지 않도록 Synchronizing 값을 true로 변경해 놓는다.
                    inCache.Synchronizing = true;
                    inCache.TempCount = 0;
                }
                cache[id] = inCache;    // 임시 카운트를 0으로 만든 값을 메모리에 다시 저장하고 lock 해제
            }
            finally
            {
                if (locked)
                {
                    locker.Release();
                    locked = false;
                }
            }

            // 동기화 대상 카운터가 0보다 클 때
            // db와 동기화를 실행할지 여부는 2가지 조건 중 하나라도 만족해야 한다.
            // 1. 임시 카운터가 동기화 배율(rate)에 도달한 경우
            // 2. 메모리에서 카운터의 시간이 만료되었다 판단될 경우 실제 db 시간 기준으로 만료되었는지 확인을 위해 동기화 필요
            if (syncCount > 0)
            {
                // 동기화 조건이 맞으면 DB와 동기화 실행
                result = SyncToDb(id, syncCount, (int)expirationTime.TotalSeconds);
                var syncCache = new ThrottleCounterInCache
                {
                    ExpirationTime = expirationTime,
                    SynchronizedCount = result.Val,
                    TempCount = 0,
                    ExpirationDate = result.CreatedTime.Add(expirationTime),
                    Timestamp = result.CreatedTime,
                    Synchronizing = false
                };

                // DB결과를 다시 메모리에 저장
                try
                {
                    locker.Wait();
                    locked = true;
                    if (!result.IsNewCounter())  
                    {
                        // db와 동기화 한 값이 새로 생성된 카운터이면 값 그대로 메모리에 저장하고
                        // db와 동기화 한 값이 새로 생성된 카운터가 아니라 기존에 있던 카운터라면
                        // db에는 TempCount가 저장되지 않기 때문에 기존 메모리에 있던 TempCount를 보관한다.
                        syncCache.TempCount = cache[id].TempCount;
                    }
                    cache[id] = syncCache;
                }
                finally
                {
                    if (locked)
                    {
                        locker.Release();
                        locked = false;
                    }
                }

                // db에서 리턴된 현재 누적값에 임시 카운터를 더한 값이 현재 카운터다.
                result.Val = result.Val + syncCache.TempCount;
            }
            else
            {
                // db와 동기화할 것이 없으면 메모리에서 조회된 값을 그대로 리턴한다.
                result = new ThrottleCounterInDb
                {
                    Val = inCache.TotalCount,
                    CreatedTime = inCache.Timestamp,
                    ExpiresAtTime = inCache.ExpirationDate,
                    OldCreatedTime = inCache.Timestamp,
                    OldExpiresAtTime = inCache.ExpirationDate,
                    OldVal = inCache.TotalCount - 1,
                };
            }

            return result;
        }

        private ThrottleCounterInDb SyncToDb(string id, int addVal, int expireSeconds)
        {
            using (var conn = new SqlConnection(connString))
            {
                string sql = $@"
DECLARE @now DATETIME2(7)
DECLARE @expiresAtTime DATETIME2(7)

SET @now = SYSDATETIME()
SET @expiresAtTime = DATEADD(second, {expireSeconds}, @now)

MERGE ApiThrottleCounter as t
USING (SELECT N'{id}' as Id) as s
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Val, AddVal, CreatedTime, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration)
    VALUES (s.Id, {addVal}, 0, @now, @expiresAtTime, NULL, NULL)
WHEN MATCHED THEN
    UPDATE SET
        t.Val = CASE WHEN @now > t.ExpiresAtTime THEN {addVal} ELSE t.Val + {addVal} END,
        t.ExpiresAtTime = CASE WHEN @now > t.ExpiresAtTime THEN @expiresAtTime ELSE t.ExpiresAtTime END,
        t.CreatedTime = CASE WHEN @now > t.ExpiresAtTime THEN @now ELSE t.CreatedTime END
OUTPUT INSERTED.Val as Val, INSERTED.CreatedTime, INSERTED.ExpiresAtTime, DELETED.Val as OldVal, DELETED.CreatedTime as OldCreatedTime, DELETED.ExpiresAtTime as OldExpiresAtTime;";
                
                var result = conn.Query<ThrottleCounterInDb>(sql);
                if (result?.Count() > 0)
                {
                    return result.First();
                }
                return null;
            }
        }
    }
}
