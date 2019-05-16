using System;

namespace DynamicFluentApis
{
    class FluentApi<T>
    {
        //private static FluentApi<T> _instance;

        static FluentApi()
        {
            if (!typeof(T).IsInterface)
            {
                throw new NotSupportedException($"{typeof(T).FullName} is not an interface.");
            }
        }

        public static T Create()
        {
            if (DynamicFluentApiFactory.CreateProxy(null, typeof(T), out var result, typeof(T)))
            {
                T obj = (T)Activator.CreateInstance(result);
                return obj;
            }
            throw new InvalidOperationException("Cannot create an instance of the specified type.");
        }
    }
}
