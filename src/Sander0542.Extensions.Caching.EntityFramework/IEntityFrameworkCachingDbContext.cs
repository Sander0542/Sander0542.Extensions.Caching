using Microsoft.EntityFrameworkCore;
using Sander0542.Extensions.Caching.EntityFramework.Models;

namespace Sander0542.Extensions.Caching.EntityFramework
{
    public interface IEntityFrameworkCachingDbContext
    {
        public DbSet<DistributedCache> DistributedCaches { get; set; }
    }
}
