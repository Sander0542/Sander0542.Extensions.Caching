using System;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;

namespace Sander0542.Extensions.Caching.EntityFramework.Options
{
    public class EntityFrameworkCacheOptions : IOptions<EntityFrameworkCacheOptions>
    {
        /// <summary>
        /// An abstraction to represent the clock of a machine in order to enable unit testing.
        /// </summary>
        public ISystemClock SystemClock { get; set; }

        /// <summary>
        /// The periodic interval to scan and delete expired items in the cache. Default is 30 minutes.
        /// </summary>
        public TimeSpan? ExpiredItemsDeletionInterval { get; set; }

        /// <summary>
        /// The default sliding expiration set for a cache entry if neither Absolute or SlidingExpiration has been set explicitly.
        /// By default, its 20 minutes.
        /// </summary>
        public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

        EntityFrameworkCacheOptions IOptions<EntityFrameworkCacheOptions>.Value => this;
    }
}
