using System;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Allows rewriting property names during the generation of a fluent API wrapper method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class FluentApiMethodAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiMethodAttribute"/> class.
        /// </summary>
        protected FluentApiMethodAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiMethodAttribute"/> class using the specified parameter.
        /// </summary>
        /// <param name="setterName">The name of the setter method.</param>
        public FluentApiMethodAttribute(string setterName) : this(setterName, null, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiMethodAttribute"/> class using the specified parameters.
        /// </summary>
        /// <param name="setterName">The name of the setter method.</param>
        /// <param name="getterName">The name of the getter method.</param>
        /// <param name="nogetter">false to generate a getter method; otherwise, true.</param>
        public FluentApiMethodAttribute(string setterName, string getterName, bool nogetter)
        {
            GetterName = getterName;
            SetterName = setterName;
            NoGetter = nogetter;
        }

        /// <summary>
        /// Gets or sets the name of the getter method to implement.
        /// </summary>
        public string GetterName { get; set; }

        /// <summary>
        /// Gets or sets the name of the setter method to implement.
        /// </summary>
        public string SetterName { get; set; }

        /// <summary>
        /// Determines whether a getter method should be defined.
        /// </summary>
        public bool NoGetter { get; set; }
    }
}
