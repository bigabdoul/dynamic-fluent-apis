using System;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Marks a type as one that provides default values for another compatible type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FluentApiDefault : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiDefault"/> class.
        /// </summary>
        public FluentApiDefault()
        {
        }
    }
}
