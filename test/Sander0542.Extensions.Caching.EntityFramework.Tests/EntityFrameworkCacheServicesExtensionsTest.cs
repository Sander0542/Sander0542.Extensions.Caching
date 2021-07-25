using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sander0542.Extensions.Caching.EntityFramework.Extensions;
using Sander0542.Extensions.Caching.EntityFramework.Tests.Helpers;
using Xunit;

namespace Sander0542.Extensions.Caching.EntityFramework.Tests
{
    public class EntityFrameworkCacheServicesExtensionsTest
    {
        [Fact]
        public void AddDistributedSqlServerCache_AddsAsSingleRegistrationService()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            EntityFrameworkCachingServiceExtensions.AddEntityFrameworkCacheServices<TestDbContext>(services);

            // Assert
            var serviceDescriptor = Assert.Single(services);
            Assert.Equal(typeof(IDistributedCache), serviceDescriptor.ServiceType);
            Assert.Equal(typeof(EntityFrameworkCache<TestDbContext>), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }

        [Fact]
        public void AddDistributedSqlServerCache_ReplacesPreviouslyUserRegisteredServices()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddDbContext<TestDbContext>(builder => {
                builder.UseInMemoryDatabase("Cache");
            });
            services.AddScoped(typeof(IDistributedCache), sp => Mock.Of<IDistributedCache>());

            // Act
            services.AddDistributedEntityFrameworkCache<TestDbContext>(_ => {});

            // Assert
            var serviceProvider = services.BuildServiceProvider();

            var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));

            Assert.NotNull(distributedCache);
            Assert.Equal(ServiceLifetime.Scoped, distributedCache.Lifetime);
            Assert.IsType<EntityFrameworkCache<TestDbContext>>(serviceProvider.GetRequiredService<IDistributedCache>());
        }

        [Fact]
        public void AddDistributedSqlServerCache_allows_chaining()
        {
            var services = new ServiceCollection();

            Assert.Same(services, services.AddDistributedEntityFrameworkCache<TestDbContext>(_ => {}));
        }
    }
}
