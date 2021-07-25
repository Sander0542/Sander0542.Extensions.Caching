using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Sander0542.Extensions.Caching.EntityFramework.Options;

namespace Sander0542.Extensions.Caching.EntityFramework.Extensions
{
    public static class EntityFrameworkCachingServiceExtensions
    {
        public static IServiceCollection AddDistributedEntityFrameworkCache<TContext>(this IServiceCollection services, Action<EntityFrameworkCacheOptions> setupAction) where TContext : DbContext, IEntityFrameworkCachingDbContext
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            AddEntityFrameworkCacheServices<TContext>(services);
            services.Configure(setupAction);

            return services;
        }

        internal static void AddEntityFrameworkCacheServices<TContext>(IServiceCollection services) where TContext : DbContext, IEntityFrameworkCachingDbContext
        {
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, EntityFrameworkCache<TContext>>());
        }
    }

}
