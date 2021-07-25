using Microsoft.EntityFrameworkCore;
using Sander0542.Extensions.Caching.EntityFramework.Models;

namespace Sander0542.Extensions.Caching.EntityFramework.Tests.Helpers
{
    public class TestDbContext : DbContext, IEntityFrameworkCachingDbContext
    {
        public TestDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<DistributedCache> DistributedCaches { get; set; }
    }
}
