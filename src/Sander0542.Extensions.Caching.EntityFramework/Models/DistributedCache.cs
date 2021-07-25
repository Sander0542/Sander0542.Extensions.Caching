using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sander0542.Extensions.Caching.EntityFramework.Models
{
    [Index(nameof(ExpiresAtTime))]
    public class DistributedCache
    {
        [Key]
        [Required]
        [MaxLength(499)]
        public string Id { get; set; }

        [Required]
        [MaxLength(8000)]
        public byte[] Value { get; set; }

        [Required]
        public DateTimeOffset ExpiresAtTime { get; set; }

        public int? SlidingExpirationInSeconds { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
