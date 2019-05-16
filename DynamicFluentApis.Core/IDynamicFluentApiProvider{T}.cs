namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Specifies the contract for a generic type that implements a dynamic fluent API behaviour.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDynamicFluentApiProvider<T>
    {
        /// <summary>
        /// When implemented by a type, gets the underlying object the 
        /// <see cref="IDynamicFluentApiProvider{T}"/> is wrapped around.
        /// </summary>
        T Object { get; }
    }
}
