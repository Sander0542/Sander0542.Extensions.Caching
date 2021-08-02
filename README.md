# Sander0542.Extensions.Caching
![Last commit](https://img.shields.io/github/last-commit/Sander0542/Sander0542.Extensions.Caching?style=for-the-badge)
![License](https://img.shields.io/github/license/Sander0542/Sander0542.Extensions.Caching?style=for-the-badge)

This repository contains implementations of the `Microsoft.Extensions.Caching.Abstractions` package.

## Sander0542.Extensions.Caching.EntityFramework
![Current release](https://img.shields.io/nuget/v/Sander0542.Extensions.Caching.EntityFramework?style=for-the-badge)
![Downloads](https://img.shields.io/nuget/dt/Sander0542.Extensions.Caching.EntityFramework?style=for-the-badge)

This implementation of the `Microsoft.Extensions.Caching.Abstractions` can be used with Entity Framework Core.

### Example

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<YourDbContext>(builder => ...);
    
    services.AddDistributedEntityFrameworkCache<TestDbContext>(options => {
        options.SystemClock = new SystemClock();
        ...
    });
}
```

## Contributors
![https://github.com/Sander0542/Sander0542.Extensions.Caching/graphs/contributors](https://contrib.rocks/image?repo=Sander0542/Sander0542.Extensions.Caching)