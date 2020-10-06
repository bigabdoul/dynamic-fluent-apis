using DynamicFluentApis.Core;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static System.Text.RegularExpressions.Regex;
using static DynamicFluentApis.FluentApiFactory;
using System.Collections.Generic;
using System.Collections;

namespace DynamicFluentApis
{
    internal static class ModuleBuilderExtensions
    {
        /// <summary>
        /// public interface IOriginalClass {}
        /// </summary>
        const TypeAttributes PUBLIC_INTERFACE = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface;

        internal static TypeBuilder CreateTypeBuilder(this ModuleBuilder builder, string targetTypeName, Type parent = null, TypeAttributes? attributes = null, Type[] interfaces = null)
            => builder.DefineType(targetTypeName
                , attributes ?? parent?.Attributes ?? PUBLIC_CLASS
                , parent
                , interfaces);

        internal static bool CreateInterface(this ModuleBuilder module, Type original, out Type result, PropertyInfo[] originalProperties = null)
        {
            var interfaceName = original.GetTypeName(Options.InterfaceNamePrefix);

            if (GetTypeFromDictionary(interfaceName, out result))
                return true;

            if (originalProperties == null)
                originalProperties = original.GetFluentProperties();

            if (originalProperties.Length > 0)
            {
                var targetBuilder = module.CreateTypeBuilder(interfaceName, attributes: PUBLIC_INTERFACE);

                for (int i = 0; i < originalProperties.Length; i++)
                {
                    targetBuilder.ImplementProperty(originalProperties[i], module, isAbstract: true);
                }

                result = targetBuilder.CreateType();
                AddTypeToDictionary(result);
            }

            return result != null;
        }

        internal static bool CreateProxy(this ModuleBuilder module
            , Type original
            , out Type result
            , Type[] interfaces = null
            , Type parent = null
            , TypeAttributes? attributes = null
            , PropertyInfo[] originalProperties = null
        )
        {
            return module.CreateType(original, out result, interfaces, parent, string.Empty, Options.ProxyClassNameSuffix, attributes, originalProperties);
        }

        internal static bool CreateType(this ModuleBuilder module
            , Type original
            , out Type result
            , Type[] interfaces = null
            , Type parent = null
            , string prefix = null
            , string suffix = null
            , TypeAttributes? attributes = null
            , PropertyInfo[] originalProperties = null
        )
        {
            var fullName = original.GetTypeName(prefix, suffix);

            if (GetTypeFromDictionary(fullName, out result))
                return true;

            result = null;

            if (originalProperties == null)
                originalProperties = original.GetFluentProperties();

            if (originalProperties.Length > 0)
            {
                var targetBuilder = module.CreateTypeBuilder(fullName
                    , parent
                    , attributes
                    , interfaces);

                // the first is considered the main interface
                var intface = interfaces.First();

                // if the original type is internal then we make all its properties internal,
                // provided that it doesn't implement any interface;
                // use the underlying system type because the original type hasn't been created yet
                bool isInternal = interfaces == null && targetBuilder.UnderlyingSystemType.IsNotPublic;

                for (int i = 0; i < originalProperties.Length; i++)
                {
                    targetBuilder.ImplementProperty(originalProperties[i], module, false, intface, isInternal);
                }

                result = targetBuilder.CreateType();
                AddTypeToDictionary(result);
            }

            return result != null;
        }

        internal static bool CreateFluent(this ModuleBuilder module
            , Type original
            , out Type result
            , Type[] interfaces = null
            , Type parent = null
            , PropertyInfo[] originalProperties = null
            , bool setterMethodsOnly = false
            , string staticInitializerMethod = null
        )
        {
            return module.CreateFluent(original
                , out result, Options.FluentTypeNamePrefix, interfaces, parent, originalProperties, setterMethodsOnly, staticInitializerMethod);
        }

        internal static bool CreateFluent(this ModuleBuilder module
            , Type original
            , out Type result
            , string prefix
            , Type[] interfaces = null
            , Type parent = null
            , PropertyInfo[] originalProperties = null
            , bool setterMethodsOnly = false
            , string staticInitializerMethod = null
        )
        {
            var typeName = original.GetTypeName(prefix);

            // make sure that the fluent type's name doesn't end with 'Proxy'
            var proxySuffix = Options.ProxyClassNameSuffix;

            if (typeName.EndsWith(proxySuffix))
                typeName = typeName.Substring(0, typeName.Length - proxySuffix.Length);

            // check if the type exists
            if (GetTypeFromDictionary(typeName, out result))
                // type has already been created
                return true;

            // try to create a new type and store it in the dictionary
            result = null;

            if (originalProperties == null)
                originalProperties = original.GetFluentProperties();

            if (originalProperties.Length > 0)
            {
                // create the builder for the original type and create the constructors on the fly
                var targetBuilder = module
                    .CreateTypeBuilder(typeName, parent)
                    //  the backing field type is preferrably an interface
                    .CreateConstructors(original, out var targetBackingField, fieldType: interfaces.First(), parent: parent);

                // the return type of the setter method
                var self = targetBuilder.UnderlyingSystemType;
                var dereference = Options.DereferenceExternals;

                for (int i = 0; i < originalProperties.Length; i++)
                {
                    var property = originalProperties[i];

                    // should the property be ignored?
                    if (property.GetCustomAttribute<FluentApiIgnoreAttribute>() != null)
                        continue;

                    var propertyType = property.PropertyType;

                    if (dereference)
                    {
                        if (module.CreateDereferencedTypes(propertyType, out var pt))
                            propertyType = pt;
                        else
                            DereferenceExternalsFailed(property);
                    }

                    var hasGetter = !setterMethodsOnly; // defaults to true
                    string getterMethodName = property.Name, setterMethodName = getterMethodName;

                    if (property.GetFluentApiMethodInfo(out FluentApiMethodAttribute methodAttr))
                    {
                        // override 'hasGetter'
                        hasGetter = !methodAttr.NoGetter;
                        getterMethodName = methodAttr.GetterName.NotBlank(property.Name);
                        setterMethodName = methodAttr.SetterName.NotBlank(property.Name);
                    }

                    MethodInfo baseMethod;
                    Type[] parameterTypes;
                    MethodBuilder methBuilder;

                    if (hasGetter)
                    {
                        baseMethod = property.GetGetMethod();
                        parameterTypes = baseMethod.GetParameterTypes();
                        methBuilder = targetBuilder.DefineMethod(getterMethodName, baseMethod.Attributes, propertyType, null);

                        ImplementFluentMethod(methBuilder, baseMethod, targetBackingField, parameterTypes);
                    }

                    baseMethod = property.GetSetMethod();
                    parameterTypes = baseMethod.GetParameterTypes();

                    methBuilder = targetBuilder.DefineMethod(setterMethodName, baseMethod.Attributes, self, new[] { propertyType });

                    ImplementFluentMethod(methBuilder, baseMethod, targetBackingField, parameterTypes, true);
                }

                // define read-only property to get a reference to the underlying original type
                result = targetBuilder
                    .ImplementReadOnlyProperty(Options.WrappedObjectPropertyName, targetBackingField)
                    .CreateType(); // create the type

                AddTypeToDictionary(result);
            }

            return result != null;
        }

        /// <summary>
        /// Decouples the source type from external dependencies.
        /// </summary>
        /// <param name="module">The module builder used to eventually create new types.</param>
        /// <param name="sourceType">The source type.</param>
        /// <param name="result">The cloned type to return.</param>
        /// <returns></returns>
        internal static bool CreateDereferencedTypes(this ModuleBuilder module, Type sourceType, out Type result)
        {
            result = null;

            if (sourceType.IsEnum)
            {
                if (module.CloneEnum(sourceType, out var en))
                {
                    result = en;
                }
            }
            else if (sourceType.IsValueType)
            {
                if (!Equals(SysAsm, sourceType.Assembly))
                {
                    if (module.CloneStruct(sourceType, out Type r))
                    {
                        result = r;
                    }
                }
                else
                {
                    result = sourceType;
                }
            }
            else if (!Equals(typeof(object), sourceType) && !Equals(typeof(string), sourceType))
            {
                bool proceed = true;
                bool isArray = sourceType.IsArray;

                if (isArray)
                {
                    // extract the type's name with Regex
                    var match = Match(sourceType.FullName, @"([\w\.]+)");
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value;
                        if (!GetTypeFromDictionary(name, out var arr))
                        {
                            sourceType = sourceType.Assembly.GetType(name);
                            proceed = sourceType != null;
                        }
                        else
                        {
                            sourceType = arr;
                        }
                    }
                    else
                    {
                        proceed = false;
                    }
                }
                else if (sourceType.IsGenericType)
                {
                    return module.MakeGenericType(sourceType, out result);
                }

                if (!proceed) return false;

                // create an interface and build a proxy that implements it
                if (module.CreateInterface(sourceType, out var propTypeIntf) &&
                    module.CreateProxy(sourceType, out _, new[] { propTypeIntf }, attributes: INTERNAL_SEALED_CLASS))
                {
                    // swap the source type with the newly created interface
                    if (isArray)
                    {
                        result = propTypeIntf.MakeArrayType();
                    }
                    else
                    {
                        result = propTypeIntf;
                    }
                }
            }
            else
            {
                result = sourceType;
            }

            return result != null;
        }

        /// <summary>
        /// Create and return a struct proxy based on the specified type.
        /// </summary>
        /// <param name="module">The module builder.</param>
        /// <param name="structType">The type used to proxy the struct.</param>
        /// <param name="result">Returns the created proxy struct.</param>
        /// <returns></returns>
        internal static bool CloneStruct(this ModuleBuilder module, Type structType, out Type result)
            => module.CreateProxy(structType, out result, parent: typeof(ValueType), attributes: structType.Attributes);

        /// <summary>
        /// Create and return an enum based on the specified type.
        /// </summary>
        /// <param name="module">The module builder.</param>
        /// <param name="enumType">The enumeration type to clone.</param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool CloneEnum(this ModuleBuilder module, Type enumType, out Type result)
        {
            if (GetTypeFromDictionary(enumType.FullName, out result))
            {
                return true;
            }

            var underlying = Enum.GetUnderlyingType(enumType);
            var eb = module.DefineEnum(enumType.FullName, enumType.Attributes, underlying);
            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType);

            for (int i = 0; i < names.Length; i++)
            {
                eb.DefineLiteral(names[i], Convert.ChangeType(values.GetValue(i), underlying));
            }

            result = eb.CreateType();
            AddTypeToDictionary(result);
            return result != null;
        }

        /// <summary>
        /// Try to extract the generic arguments from the generic type and create 
        /// dereferenced interfaces (and proxies) which we will remake generic 
        /// for the type to return.
        /// <para>
        /// For example, <see cref="IList{T}"/>
        /// becomes <see cref="IList"/>&lt;IT> where 
        /// &lt;T> is a type that implements the interface &lt;IT>.
        /// </para>
        /// </summary>
        /// <param name="module">The module builder used to create dereferenced interfaces.</param>
        /// <param name="sourceType">The generic type to transform.</param>
        /// <param name="targetType">Returns the new type whose generic arguments are interfaces.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="module"/> or <paramref name="sourceType"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="sourceType"/> is not generic.</exception>
        internal static bool MakeGenericType(this ModuleBuilder module, Type sourceType, out Type targetType)
        {
            if (!sourceType.IsGenericType) throw new InvalidOperationException($"{nameof(sourceType)} is not generic.");

            if (GetTypeFromDictionary(sourceType.FullName, out targetType))
                return true;

            targetType = sourceType;

            // extract the types from the generic property
            var gentypes = sourceType.GetGenericArguments();
            var genlist = new List<Type>();

            foreach (var t in gentypes)
            {
                // create dereferenced types
                if (module.CreateInterface(t, out var intf) &&
                    module.CreateProxy(t, out var proxy, new[] { intf })) // do we need the proxy later? if not, skip this method
                {
                    genlist.Add(intf);
                }
                else
                {
                    // cannot build a new generic type with the generated interfaces
                    return false;
                }
            }

            // construct a new generic type with the created interfaces
            var genTypeName = $"{sourceType.Namespace}.{sourceType.Name}";
            var genType = Type.GetType(genTypeName)?.MakeGenericType(genlist.ToArray());

            if (genType != null)
            {
                targetType = genType;
                // store this type's reference to avoid future overheads
                AddTypeToDictionary(sourceType.FullName, targetType);
            }

            return genType != null;
        }
    }
}
