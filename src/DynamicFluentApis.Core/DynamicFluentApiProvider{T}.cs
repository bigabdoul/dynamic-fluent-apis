using System;
using System.Linq.Expressions;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Represents a strongly-typed fluent API provider wrapped around an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The underlying type this fluent API provider gives access to.</typeparam>
    /// <remarks>
    /// The idea is to create an instance of this class that dynamically creates a method for each public
    /// read-write property of the type <typeparamref name="T"/> (where T is Person for instance) like so:
    /// <para>
    /// <code>
    /// public class Person
    /// {
    ///     public string FirstName { get; set; }
    ///     public string LastName { get; set; }
    ///     public System.DateTime BirthDate { get; set; }
    ///     public virtual double Age { get => System.DateTime.Today.Subtract(BirthDate).TotalDays / 365; }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <code>
    /// Person p = new <see cref="DynamicFluentApiProvider"/>&lt;Person>().FirstName("Abdoul").LastName("Kaba").BirthDate(System.DateTime.Parse("1976-11-12")).Object;
    /// </code>
    /// </para>
    /// </remarks>
    public class DynamicFluentApiProvider<T> : DynamicFluentApiProvider, IDynamicFluentApiProvider<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicFluentApiProvider{T}"/> class.
        /// </summary>
        public DynamicFluentApiProvider() : this(Activator.CreateInstance<T>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicFluentApiProvider{T}"/> class using the specified object instance.
        /// </summary>
        /// <param name="instance">An object for which to provide fluent API support.</param>
        public DynamicFluentApiProvider(T instance) : base(instance)
        {
        }

        /// <summary>
        /// Gets the underlying strongly-typed object this <see cref="DynamicFluentApiProvider{T}"/> is wrapped around.
        /// </summary>
        /// <returns></returns>
        public virtual T Object => (T)((IDynamicFluentApiProvider)this).Object;

        /// <summary>
        /// Executes the specified action.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns></returns>
        public DynamicFluentApiProvider<T> A(Action<T> action)
        {
            action(Object);
            return this;
        }

        /// <summary>
        /// Gets the value of the specified property expression.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="property">A lambda expression that identifies the property value to return.</param>
        /// <returns></returns>
        public TProperty P<TProperty>(Expression<Func<T, TProperty>> property)
            => (TProperty)GetPropertyInfo(property).GetValue(Object);

        /// <summary>
        /// Sets the specified value to the given property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="property">A lambda expression that identifies the property value to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public DynamicFluentApiProvider<T> P<TProperty>(Expression<Func<T, TProperty>> property, TProperty value)
        {
            GetPropertyInfo(property).SetValue(Object, value);
            return this;
        }
    }
}
