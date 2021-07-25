using System;
using System.Linq;

namespace Sander0542.Extensions.Caching.EntityFramework.Tests.Helpers
{
    public static class AttributesExtensions
    {
        public static T GetAttributeFrom<T>(this object instance, string propertyName) where T : Attribute
        {
            var attrType = typeof(T);
            var property = instance.GetType().GetProperty(propertyName);
            return (T)property.GetCustomAttributes(attrType, false).First();
        }
    }
}
