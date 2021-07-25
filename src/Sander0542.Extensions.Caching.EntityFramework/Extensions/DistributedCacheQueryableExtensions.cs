using System;
using System.Linq;
using Microsoft.Extensions.Internal;
using Sander0542.Extensions.Caching.EntityFramework.Models;

namespace Sander0542.Extensions.Caching.EntityFramework.Extensions
{
    public static class DistributedCacheQueryableExtensions
    {
        public static IQueryable<TModel> WhereId<TModel>(this IQueryable<TModel> source, string key) where TModel : DistributedCache
        {
            return source.Where(cache => cache.Id == key);
        }

        public static IQueryable<TModel> WhereActive<TModel>(this IQueryable<TModel> source, ISystemClock systemClock) where TModel : DistributedCache
        {
            return source.WhereActive(systemClock.UtcNow);
        }

        public static IQueryable<TModel> WhereActive<TModel>(this IQueryable<TModel> source, DateTimeOffset time) where TModel : DistributedCache
        {
            return source.Where(cache => cache.ExpiresAtTime > time);
        }

        public static IQueryable<TModel> WhereExpired<TModel>(this IQueryable<TModel> source, DateTimeOffset time) where TModel : DistributedCache
        {
            return source.Where(cache => cache.ExpiresAtTime <= time);
        }
    }
}
