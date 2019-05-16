using System;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Allows rewriting setter property names during the generation of a fluent API wrapper setter method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class FluentApiSetterMethodAttribute : FluentApiMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiSetterMethodAttribute"/> class.
        /// </summary>
        public FluentApiSetterMethodAttribute() : this(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiSetterMethodAttribute"/> class using the provided parameters.
        /// </summary>
        /// <param name="setterName"></param>
        public FluentApiSetterMethodAttribute(string setterName) : base(setterName, null, true)
        {
        }
    }
}
