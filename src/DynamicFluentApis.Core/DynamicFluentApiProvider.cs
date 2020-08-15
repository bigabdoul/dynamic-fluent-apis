using System;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicFluentApis.Core
{

    /// <summary>
    /// Represents a fluent API builder wrapped around an object.
    /// </summary>
    public class DynamicFluentApiProvider : DynamicObject, IDynamicFluentApiProvider
    {
        /// <summary>
        /// The underlying instance object.
        /// </summary>
        protected object _object;

        private PropertyInfo[] _objProps;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicFluentApiProvider"/> class.
        /// </summary>
        protected DynamicFluentApiProvider()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicFluentApiProvider"/> class.
        /// </summary>
        /// <param name="obj">The object for which to provide fluent API support.</param>
        public DynamicFluentApiProvider(object obj)
        {
            _object = obj ?? throw new ArgumentNullException(nameof(obj));
        }

        /// <summary>
        /// Provides the implementation for operations that invoke a member. Classes derived
        /// from the <see cref="DynamicObject"/> class can override this method to specify
        /// dynamic behavior for operations such as calling a method.
        /// </summary>
        /// <param name="binder">
        /// Provides information about the dynamic operation. The binder.Name property provides
        /// the name of the member on which the dynamic operation is performed. For example,
        /// for the statement sampleObject.SampleMethod(100), where sampleObject is an instance
        /// of the class derived from the System.Dynamic.DynamicObject class, binder.Name
        /// returns "SampleMethod". The binder.IgnoreCase property specifies whether the
        /// member name is case-sensitive.
        /// </param>
        /// <param name="args">
        /// The arguments that are passed to the object member during the invoke operation.
        /// For example, for the statement sampleObject.SampleMethod(100), where sampleObject
        /// is derived from the System.Dynamic.DynamicObject class, args[0] is equal to 100.
        /// </param>
        /// <param name="result">The result of the member invocation.</param>
        /// <returns></returns>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = this;
            try
            {
                if (GetProperty(binder.Name, out var prop))
                {
                    prop.SetValue(_object, args[0]);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Provides the implementation for operations that get member values. Classes derived
        /// from the System.Dynamic.DynamicObject class can override this method to specify
        /// dynamic behavior for operations such as getting a value for a property.
        /// </summary>
        /// <param name="binder">
        /// Provides information about the dynamic operation. The binder.Name property provides
        /// the name of the member on which the dynamic operation is performed. For example,
        /// for the statement sampleObject.SampleMethod(100), where sampleObject is an instance
        /// of the class derived from the System.Dynamic.DynamicObject class, binder.Name
        /// returns "SampleMethod". The binder.IgnoreCase property specifies whether the
        /// member name is case-sensitive.
        /// </param>
        /// <param name="result">
        /// The result of the get operation. For example, if the method is called for a property,
        /// you can assign the property value to result.
        /// </param>
        /// <returns></returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (GetProperty(binder.Name, out var prop))
            {
                result = prop.GetValue(_object);
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// Returns the underlying object.
        /// </summary>
        /// <returns></returns>
        object IDynamicFluentApiProvider.Object => _object;

        /// <summary>
        /// Casts the underlying object to the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual T GetObject<T>() => (T)_object;

        /// <summary>
        /// Extracts the <see cref="PropertyInfo"/> from the given property (lambda) expression.
        /// </summary>
        /// <typeparam name="TSource">The type of the source object.</typeparam>
        /// <typeparam name="TProperty">The type of the property defined in <typeparamref name="TSource"/>.</typeparam>
        /// <param name="property">The property from which to extract the <see cref="PropertyInfo"/>.</param>
        /// <returns></returns>
        protected internal static PropertyInfo GetPropertyInfo<TSource, TProperty>(Expression<Func<TSource, TProperty>> property)
        {
            if (!(property.Body is MemberExpression member))
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a method, not a property.",
                    property.ToString()));

            var propInfo = member.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a field, not a property.",
                    property.ToString()));

            var type = typeof(TSource);
            if (!propInfo.ReflectedType.IsAssignableFrom(type))
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a property that is not from type {1}.",
                    property.ToString(),
                    type));

            return propInfo;
        }

        private bool GetProperty(string name, out PropertyInfo result)
        {
            result = null;
            if (_objProps == null)
            {
                _objProps = _object?.GetType().GetProperties();
            }
            result = _objProps?.FirstOrDefault(p => p.Name == name);
            return result != null;
        }
    }
}
