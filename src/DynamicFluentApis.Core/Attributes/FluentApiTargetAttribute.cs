using System;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Provides fluent API support to the class it decorates.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class FluentApiTargetAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiTargetAttribute"/> class.
        /// </summary>
        public FluentApiTargetAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiTargetAttribute"/> class using the provided parameter.
        /// </summary>
        /// <param name="settersOnly">
        /// Determines whether only setter methods should be defined on the fluent API wrapper class.
        /// </param>
        public FluentApiTargetAttribute(bool settersOnly)
        {
            SetterMethodsOnly = settersOnly;
        }

        /// <summary>
        /// Determines whether only setter methods should be defined on the fluent API wrapper class.
        /// </summary>
        public bool SetterMethodsOnly { get; set; }

        /// <summary>
        /// Gets or sets the name of the static method that initializes a new instance 
        /// of the proxy class, to be defined on the fluent API wrapper class.
        /// </summary>
        public string StaticNewInstanceMethod { get; set; }
    }
}
