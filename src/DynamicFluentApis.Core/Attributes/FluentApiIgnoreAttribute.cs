using System;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Represents a custom attribute used to ignore a type when exploring an assemblies candidate types for fluent API support.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public class FluentApiIgnoreAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiIgnoreAttribute"/> class.
        /// </summary>
        public FluentApiIgnoreAttribute()
        {
        }
    }
}
