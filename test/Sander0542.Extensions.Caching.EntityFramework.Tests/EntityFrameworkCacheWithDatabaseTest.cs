using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Sander0542.Extensions.Caching.EntityFramework.Extensions;
using Sander0542.Extensions.Caching.EntityFramework.Models;
using Sander0542.Extensions.Caching.EntityFramework.Options;
using Sander0542.Extensions.Caching.EntityFramework.Tests.Helpers;
using Xunit;

namespace Sander0542.Extensions.Caching.EntityFramework.Tests
{
    public class EntityFrameworkCacheWithDatabaseTest
    {
        private const int DefaultValueColumnWidth = 8000;
        private const int CacheItemIdColumnWidth = 499;

        private int _dbName = 1;

        [Fact]
        public async Task ReturnsNullValue_ForNonExistingCacheItem()
        {
            // Arrange
            var cache = GetSqlServerCache();

            // Act
            var value = await cache.GetAsync("NonExisting");

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpirationSetInThePast_Throws()
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cache = GetSqlServerCache(GetCacheOptions(testClock));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => {
                return cache.SetAsync(
                    key,
                    expectedValue,
                    new DistributedCacheEntryOptions().SetAbsoluteExpiration(testClock.UtcNow.AddHours(-1)));
            });
            Assert.Equal("The absolute expiration value must be in the future.", exception.Message);
        }

        [Fact]
        public async Task SetCacheItem_SucceedsFor_KeyEqualToMaximumSize()
        {
            // Arrange
            // Create a key with the maximum allowed key length. Here a key of length 898 bytes is created.
            var key = new string('a', CacheItemIdColumnWidth);
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cache = GetSqlServerCache(GetCacheOptions(testClock));

            // Act
            await cache.SetAsync(
                key, expectedValue,
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

            // Assert
            var cacheItem = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Equal(expectedValue, cacheItem.Value);

            // Act
            await cache.RemoveAsync(key);

            // Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Null(cacheItemInfo);
        }

        [Fact]
        public async Task SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
        {
            // Arrange
            var key = Guid.NewGuid().ToString();
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cacheOptions = GetCacheOptions(testClock);
            var cache = GetSqlServerCache(cacheOptions);
            var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);

            // Act
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = null,
                AbsoluteExpirationRelativeToNow = null,
                SlidingExpiration = null
            });

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                cacheOptions.DefaultSlidingExpiration,
                null,
                expectedExpirationTime);

            var cacheItem = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Equal(expectedValue, cacheItem.Value);

            // Act
            await cache.RemoveAsync(key);

            // Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Null(cacheItemInfo);
        }

        [Fact]
        public async Task UpdatedDefaultSlidingExpiration_SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
        {
            // Arrange
            var key = Guid.NewGuid().ToString();
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cacheOptions = GetCacheOptions(testClock);
            cacheOptions.DefaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration.Add(TimeSpan.FromMinutes(10));
            var cache = GetSqlServerCache(cacheOptions);
            var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);

            // Act
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = null,
                AbsoluteExpirationRelativeToNow = null,
                SlidingExpiration = null
            });

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                cacheOptions.DefaultSlidingExpiration,
                null,
                expectedExpirationTime);

            var cacheItem = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Equal(expectedValue, cacheItem.Value);

            // Act
            await cache.RemoveAsync(key);

            // Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Null(cacheItemInfo);
        }

        [Fact]
        public async Task SetCacheItem_FailsFor_KeyGreaterThanMaximumSize()
        {
            // Arrange
            // Create a key which is greater than the maximum length.
            var key = new string('b', CacheItemIdColumnWidth + 1);
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cache = GetSqlServerCache(GetCacheOptions(testClock));

            // Act
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

            // Assert
            var cacheItem = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Null(cacheItem);
        }

        // Arrange
        [Theory]
        [InlineData(10, 11)]
        [InlineData(10, 30)]
        public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem(int slidingExpirationWindow, int accessItemAt)
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            await cache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(slidingExpirationWindow)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(accessItemAt));

            // Act
            var value = await cache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Theory]
        [InlineData(5, 15)]
        [InlineData(10, 20)]
        public async Task SetWithSlidingExpiration_ExtendsExpirationTime(int accessItemAt, int expected)
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpirationWindow = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var expectedExpirationTime = testClock.UtcNow.AddSeconds(expected);
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

            testClock.Add(TimeSpan.FromSeconds(accessItemAt));
            // Act
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                slidingExpirationWindow,
                null,
                expectedExpirationTime);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(50)]
        public async Task SetWithSlidingExpirationAndAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem(int accessItemAt)
        {
            // Arrange
            var testClock = new TestClock();
            var utcNow = testClock.UtcNow;
            var slidingExpiration = TimeSpan.FromSeconds(5);
            var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(
                key,
                expectedValue,
                // Set both sliding and absolute expiration
                new DistributedCacheEntryOptions()
                    .SetSlidingExpiration(slidingExpiration)
                    .SetAbsoluteExpiration(absoluteExpiration));

            // Act
            utcNow = testClock.Add(TimeSpan.FromSeconds(accessItemAt)).UtcNow;
            var value = await cache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpirationRelativeToNow_ReturnsNullValue_ForExpiredCacheItem()
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            await cache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(10)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));

            // Act
            var value = await cache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task SetWithAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem()
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            await cache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(testClock.UtcNow.Add(TimeSpan.FromSeconds(30))));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));

            // Act
            var value = await cache.GetAsync(key);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpirationRelativeToNow()
        {
            // Arrange
            var testClock = new TestClock();
            var absoluteExpirationRelativeToUtcNow = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var expectedAbsoluteExpiration = testClock.UtcNow.Add(absoluteExpirationRelativeToUtcNow);

            // Act
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(absoluteExpirationRelativeToUtcNow));

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                null,
                expectedAbsoluteExpiration,
                expectedAbsoluteExpiration);
        }

        [Fact]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var expectedAbsoluteExpiration = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            // Act
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(expectedAbsoluteExpiration));

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                null,
                expectedAbsoluteExpiration,
                expectedAbsoluteExpiration);
        }

        [Fact]
        public async Task SetCacheItem_UpdatesAbsoluteExpirationTime()
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

            // Act & Assert
            // Creates a new item
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                null,
                absoluteExpiration,
                absoluteExpiration);

            // Updates an existing item with new absolute expiration time
            absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                null,
                absoluteExpiration,
                absoluteExpiration);
        }

        [Fact]
        public async Task SetCacheItem_WithValueLargerThan_DefaultColumnWidth()
        {
            // Arrange
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = new byte[DefaultValueColumnWidth + 100];
            var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

            // Act
            // Creates a new item
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));

            // Assert
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                null,
                absoluteExpiration,
                absoluteExpiration);
        }

        [Fact]
        public async Task ExtendsExpirationTime_ForSlidingExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // The operations Set and Refresh here extend the sliding expiration 2 times.
            var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

            // Act
            testClock.Add(TimeSpan.FromSeconds(5));
            await cache.RefreshAsync(key);

            // Assert
            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, TimeSpan.FromSeconds(cacheItemInfo.SlidingExpirationInSeconds.GetValueOrDefault(0)));
            Assert.Null(cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact]
        public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
        {
            // Arrange
            var testClock = new TestClock();
            var utcNow = testClock.UtcNow;
            var slidingExpiration = TimeSpan.FromSeconds(5);
            var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(
                key,
                expectedValue,
                // Set both sliding and absolute expiration
                new DistributedCacheEntryOptions()
                    .SetSlidingExpiration(slidingExpiration)
                    .SetAbsoluteExpiration(absoluteExpiration));

            // Act && Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(utcNow.AddSeconds(5), cacheItemInfo.ExpiresAtTime);

            // Accessing item at time...
            utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                slidingExpiration,
                absoluteExpiration,
                utcNow.AddSeconds(5));

            // Accessing item at time...
            utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                slidingExpiration,
                absoluteExpiration,
                utcNow.AddSeconds(5));

            // Accessing item at time...
            utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            // The expiration extension must not exceed the absolute expiration
            await AssertGetCacheItemFromDatabaseAsync(
                cache,
                key,
                expectedValue,
                slidingExpiration,
                absoluteExpiration,
                absoluteExpiration);
        }

        [Fact]
        public async Task DoestNotExtendsExpirationTime_ForAbsoluteExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var expectedExpiresAtTime = testClock.UtcNow.Add(absoluteExpirationRelativeToNow);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpirationRelativeToNow));
            testClock.Add(TimeSpan.FromSeconds(25));

            // Act
            var value = await cache.GetAsync(key);

            // Assert
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);

            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact]
        public async Task RefreshItem_ExtendsExpirationTime_ForSlidingExpiration()
        {
            // Arrange
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // The operations Set and Refresh here extend the sliding expiration 2 times.
            var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

            // Act
            testClock.Add(TimeSpan.FromSeconds(5));
            await cache.RefreshAsync(key);

            // Assert
            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, TimeSpan.FromSeconds(cacheItemInfo.SlidingExpirationInSeconds.Value));
            Assert.Null(cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact]
        public async Task GetCacheItem_IsCaseSensitive()
        {
            // Arrange
            var key = Guid.NewGuid().ToString().ToLower(CultureInfo.InvariantCulture);// lower case
            var cache = GetSqlServerCache();
            await cache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

            // Act
            var value = await cache.GetAsync(key.ToUpper(CultureInfo.InvariantCulture));// key made upper case

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task GetCacheItem_DoesNotTrimTrailingSpaces()
        {
            // Arrange
            var key = string.Format(CultureInfo.InvariantCulture, "  {0}  ", Guid.NewGuid());// with trailing spaces
            var cache = GetSqlServerCache();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(
                key,
                expectedValue,
                new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

            // Act
            var value = await cache.GetAsync(key);

            // Assert
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);
        }

        [Fact]
        public async Task DeletesCacheItem_OnExplicitlyCalled()
        {
            // Arrange
            var key = Guid.NewGuid().ToString();
            var cache = GetSqlServerCache();
            await cache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("Hello, World!"),
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

            // Act
            await cache.RemoveAsync(key);

            // Assert
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.Null(cacheItemInfo);
        }

        private IDistributedCache GetSqlServerCache(EntityFrameworkCacheOptions options = null)
        {
            if (options == null)
            {
                options = GetCacheOptions();
            }

            var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase((_dbName++).ToString()).Options);
            context.Database.EnsureCreated();

            return new EntityFrameworkCache<TestDbContext>(context, options);
        }

        private EntityFrameworkCacheOptions GetCacheOptions(ISystemClock testClock = null)
        {
            return new()
            {
                SystemClock = testClock ?? new TestClock(),
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2)
            };
        }

        private async Task AssertGetCacheItemFromDatabaseAsync(
            IDistributedCache cache,
            string key,
            byte[] expectedValue,
            TimeSpan? slidingExpiration,
            DateTimeOffset? absoluteExpiration,
            DateTimeOffset expectedExpirationTime)
        {
            var value = await cache.GetAsync(key);
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(cache, key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds.HasValue ? TimeSpan.FromSeconds(cacheItemInfo.SlidingExpirationInSeconds.Value) : null);
            Assert.Equal(absoluteExpiration, cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpirationTime, cacheItemInfo.ExpiresAtTime);
        }

        private async Task<DistributedCache> GetCacheItemFromDatabaseAsync(IDistributedCache cache, string key)
        {
            return await ((EntityFrameworkCache<TestDbContext>)cache).GetContext().DistributedCaches.WhereId(key).AsNoTracking().FirstOrDefaultAsync();
        }
    }
}
