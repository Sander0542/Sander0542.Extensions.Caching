using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Sander0542.Extensions.Caching.EntityFramework.Operations;
using Sander0542.Extensions.Caching.EntityFramework.Options;

namespace Sander0542.Extensions.Caching.EntityFramework
{
    public class EntityFrameworkCache<TContext> : IDistributedCache where TContext : DbContext, IEntityFrameworkCachingDbContext
    {
        private static readonly TimeSpan MinimumExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);

        private readonly TContext _dbContext;
        private readonly IDatabaseOperations _dbOperations;
        private readonly ISystemClock _systemClock;

        private readonly TimeSpan _expiredItemsDeletionInterval;
        private DateTimeOffset _lastExpirationScan;
        private readonly Action _deleteExpiredCachedItemsDelegate;
        private readonly TimeSpan _defaultSlidingExpiration;
        private readonly object _mutex = new object();

        public EntityFrameworkCache(TContext dbContext, IOptions<EntityFrameworkCacheOptions> options)
        {
            _dbContext = dbContext;

            var cacheOptions = options.Value;

            if (cacheOptions.ExpiredItemsDeletionInterval.HasValue && cacheOptions.ExpiredItemsDeletionInterval.Value < MinimumExpiredItemsDeletionInterval)
            {
                throw new ArgumentException($"{nameof(cacheOptions.ExpiredItemsDeletionInterval)} cannot be less than the minimum value of {MinimumExpiredItemsDeletionInterval.TotalMinutes} minutes.");
            }

            if (cacheOptions.DefaultSlidingExpiration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheOptions.DefaultSlidingExpiration), cacheOptions.DefaultSlidingExpiration, "The sliding expiration value must be positive.");
            }

            _systemClock = cacheOptions.SystemClock ?? new SystemClock();
            _expiredItemsDeletionInterval = cacheOptions.ExpiredItemsDeletionInterval ?? DefaultExpiredItemsDeletionInterval;
            _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
            _defaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration;

            _dbOperations = new DatabaseOperations<TContext>(dbContext, _systemClock);
        }

        /// <inheritdoc />
        public byte[] Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var value = _dbOperations.GetCacheItem(key);

            ScanForExpiredItemsIfRequired();

            return value;
        }

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            var value = await _dbOperations.GetCacheItemAsync(key, token).ConfigureAwait(false);

            ScanForExpiredItemsIfRequired();

            return value;
        }

        /// <inheritdoc />
        public void Refresh(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _dbOperations.RefreshCacheItem(key);

            ScanForExpiredItemsIfRequired();
        }

        /// <inheritdoc />
        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            await _dbOperations.RefreshCacheItemAsync(key, token).ConfigureAwait(false);

            ScanForExpiredItemsIfRequired();
        }

        /// <inheritdoc />
        public void Remove(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _dbOperations.DeleteCacheItem(key);

            ScanForExpiredItemsIfRequired();
        }

        /// <inheritdoc />
        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            await _dbOperations.DeleteCacheItemAsync(key, token).ConfigureAwait(false);

            ScanForExpiredItemsIfRequired();
        }

        /// <inheritdoc />
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            GetOptions(ref options);

            _dbOperations.SetCacheItem(key, value, options);

            ScanForExpiredItemsIfRequired();
        }

        /// <inheritdoc />
        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            token.ThrowIfCancellationRequested();

            GetOptions(ref options);

            await _dbOperations.SetCacheItemAsync(key, value, options, token).ConfigureAwait(false);

            ScanForExpiredItemsIfRequired();
        }

        private void ScanForExpiredItemsIfRequired()
        {
            lock (_mutex)
            {
                var utcNow = _systemClock.UtcNow;
                if (utcNow - _lastExpirationScan > _expiredItemsDeletionInterval)
                {
                    _lastExpirationScan = utcNow;
                    Task.Run(_deleteExpiredCachedItemsDelegate);
                }
            }
        }

        private void DeleteExpiredCacheItems()
        {
            _dbOperations.DeleteExpiredCacheItems();
        }

        private void GetOptions(ref DistributedCacheEntryOptions options)
        {
            if (!options.AbsoluteExpiration.HasValue && !options.AbsoluteExpirationRelativeToNow.HasValue && !options.SlidingExpiration.HasValue)
            {
                options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = _defaultSlidingExpiration
                };
            }
        }

        internal TContext GetContext()
        {
            return _dbContext;
        }
    }
}
