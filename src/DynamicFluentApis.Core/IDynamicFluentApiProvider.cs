namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Specifies the contract for a type that implements a dynamic fluent API behaviour.
    /// </summary>
    public interface IDynamicFluentApiProvider
    {
        /// <summary>
        /// When implemented by a type, gets the underlying object the 
        /// <see cref="IDynamicFluentApiProvider"/> is wrapped around.
        /// </summary>
        object Object { get; }
    }
}
