using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Sander0542.Extensions.Caching.EntityFramework.Extensions;
using Sander0542.Extensions.Caching.EntityFramework.Models;

namespace Sander0542.Extensions.Caching.EntityFramework.Operations
{
    public class DatabaseOperations<TContext> : IDatabaseOperations where TContext : DbContext, IEntityFrameworkCachingDbContext
    {
        private readonly TContext _dbContext;
        private readonly ISystemClock _systemClock;

        public DatabaseOperations(TContext dbContext, ISystemClock systemClock)
        {
            _dbContext = dbContext;
            _systemClock = systemClock;
        }

        public byte[] GetCacheItem(string key)
        {
            var utcNow = _systemClock.UtcNow;
            
            RefreshCacheItem(key);

            return _dbContext.DistributedCaches
                .WhereId(key)
                .WhereActive(utcNow)
                .Select(cache => cache.Value)
                .AsNoTracking()
                .FirstOrDefault();
        }

        public async Task<byte[]> GetCacheItemAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            var utcNow = _systemClock.UtcNow;

            await RefreshCacheItemAsync(key, token);

            return await _dbContext.DistributedCaches
                .WhereId(key)
                .WhereActive(utcNow)
                .Select(cache => cache.Value)
                .AsNoTracking()
                .FirstOrDefaultAsync(token);
        }

        public void RefreshCacheItem(string key)
        {
            var utcNow = _systemClock.UtcNow;

            var cache = _dbContext.DistributedCaches
                .WhereId(key)
                .WhereActive(utcNow)
                .Where(cache1 => cache1.SlidingExpirationInSeconds != null)
                .FirstOrDefault(cache1 => cache1.AbsoluteExpiration == null || cache1.AbsoluteExpiration != cache1.ExpiresAtTime);

            if (cache == null) return;

            RefreshCache(cache, utcNow);

            _dbContext.SaveChanges();
            _dbContext.Entry(cache).State = EntityState.Detached;
        }

        public async Task RefreshCacheItemAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            var utcNow = _systemClock.UtcNow;

            var cache = await _dbContext.DistributedCaches
                .WhereId(key)
                .WhereActive(utcNow)
                .Where(cache1 => cache1.SlidingExpirationInSeconds != null)
                .FirstOrDefaultAsync(cache1 => cache1.AbsoluteExpiration == null || cache1.AbsoluteExpiration != cache1.ExpiresAtTime, token);

            if (cache == null) return;

            RefreshCache(cache, utcNow);

            await _dbContext.SaveChangesAsync(token);
            _dbContext.Entry(cache).State = EntityState.Detached;
        }

        public void DeleteCacheItem(string key)
        {
            RemoveCache(key);
            _dbContext.SaveChanges();
        }

        public async Task DeleteCacheItemAsync(string key, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            RemoveCache(key);
            await _dbContext.SaveChangesAsync(token);
        }

        public void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var cache = SetCache(key, value, options, _systemClock.UtcNow);

            if (_dbContext.DistributedCaches.Any(cache1 => cache1.Id == cache.Id))
            {
                _dbContext.DistributedCaches.Attach(cache);
                _dbContext.Entry(cache).State = EntityState.Modified;
            }
            else
            {
                _dbContext.DistributedCaches.Add(cache);
            }
            _dbContext.SaveChanges();
            _dbContext.Entry(cache).State = EntityState.Detached;
        }

        public async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            var cache = SetCache(key, value, options, _systemClock.UtcNow);

            if (await _dbContext.DistributedCaches.AnyAsync(cache1 => cache1.Id == cache.Id, token))
            {
                _dbContext.DistributedCaches.Attach(cache);
                _dbContext.Entry(cache).State = EntityState.Modified;
            }
            else
            {
                await _dbContext.DistributedCaches.AddAsync(cache, token);
            }
            await _dbContext.SaveChangesAsync(token);
            _dbContext.Entry(cache).State = EntityState.Detached;
        }

        public void DeleteExpiredCacheItems()
        {
            var utcNow = _systemClock.UtcNow;

            _dbContext.RemoveRange(_dbContext.DistributedCaches.WhereExpired(utcNow).ToList());
            _dbContext.SaveChanges();
        }

        private void RefreshCache(DistributedCache cache, DateTimeOffset utcNow)
        {
            if (!cache.AbsoluteExpiration.HasValue)
            {
                cache.ExpiresAtTime = utcNow.AddSeconds(cache.SlidingExpirationInSeconds.Value);
                return;
            }

            var absoluteExpiration = cache.AbsoluteExpiration.Value;
            var dateDiff = (absoluteExpiration - utcNow).TotalSeconds;
            var slidingExpirationInSeconds = cache.SlidingExpirationInSeconds.GetValueOrDefault(0);

            cache.ExpiresAtTime = dateDiff <= slidingExpirationInSeconds ? absoluteExpiration : utcNow.AddSeconds(slidingExpirationInSeconds);
        }

        private void RemoveCache(string key)
        {
            var tmpCache = new DistributedCache
            {
                Id = key
            };

            _dbContext.DistributedCaches.Attach(tmpCache);
            _dbContext.DistributedCaches.Remove(tmpCache);
        }

        private DistributedCache SetCache(string key, byte[] value, DistributedCacheEntryOptions options, DateTimeOffset utcNow)
        {
            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            return new DistributedCache
            {
                Id = key,
                Value = value,
                ExpiresAtTime = options.SlidingExpiration.HasValue ? utcNow.Add(options.SlidingExpiration.Value) : absoluteExpiration.Value,
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpirationInSeconds = (int?)options.SlidingExpiration?.TotalSeconds
            };
        }

        protected DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;

            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = options.AbsoluteExpiration.Value;
            }

            return absoluteExpiration;
        }

        protected void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
            }
        }
    }
}
