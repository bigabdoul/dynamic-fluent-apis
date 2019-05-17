using DynamicFluentApis.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using static System.Text.RegularExpressions.Regex;

namespace DynamicFluentApis
{
    /// <summary>
    /// Provides functionalities that allow building a dynamic assembly with fluent API support
    /// for types from another assembly. This static class is not guaranteed to be thread-safe.
    /// </summary>
    public static class FluentApiFactory
    {
        #region private fields

        private const string get_ = "get_";
        private const string set_ = "set_";

        /// <summary>
        /// Regex to remove illegal characters from a type name. Strips digits from the beginning.
        /// </summary>
        private const string RE_VALID_TYPE_NAME = @"(^[0-9]*)|([^\w]*)";

        /// <summary>
        /// public class OriginalClass {}
        /// </summary>
        private const TypeAttributes PUBLIC_CLASS = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit;

        /// <summary>
        /// public interface IOriginalClass {}
        /// </summary>
        private const TypeAttributes PUBLIC_INTERFACE = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface;

        /// <summary>
        /// internal sealed class OriginalClassProxy : IOriginalClass {}
        /// </summary>
        private const TypeAttributes INTERNAL_SEALED_CLASS = TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit;

        private static readonly Assembly SysAsm = typeof(string).Assembly;
        private static readonly ConcurrentDictionary<string, Type> InternalDictionary = new ConcurrentDictionary<string, Type>();
        private static FluentApiFactoryOptions _options = new FluentApiFactoryOptions();

        private static volatile bool _factoryBusy;

        #endregion

        #region public

        #region properties

        /// <summary>
        /// Gets or sets the options for the <see cref="FluentApiFactory"/> class.
        /// </summary>
        public static FluentApiFactoryOptions Options
        {
            get => _options ?? (_options = new FluentApiFactoryOptions());
            set => _options = value;
        }

        #endregion

        #region events

        /// <summary>
        /// The event fired when an error a critical occurs.
        /// </summary>
        public static event Action<Exception> Error;

        /// <summary>
        /// The event fired when an attempt to delete an existing file with the same name as the output assembly file name fails.
        /// </summary>
        public static event Action<Exception, AssemblyBuilder, string> DeleteFileError;

        #endregion

        #region methods

        #region What is a FluentApiFactory worth without fluent API support?

        /// <summary>
        /// Enables configuration of the <see cref="FluentApiFactory"/> class using fluent API.
        /// </summary>
        /// <param name="overwrite">true to overwrite any previously-built existing assembly file; otherwise, false.</param>
        /// <returns></returns>
        public static FluentApiFactoryConfig Configure(bool overwrite = false) 
            => FluentApiFactoryConfig.Create().WithOptions(overwrite).WithConfig();

        #endregion

        /// <summary>
        /// Release all internal resources used by the <see cref="FluentApiFactory"/> class.
        /// </summary>
        public static void Reset()
        {
            CheckFactoryBusy();
            InternalDictionary.Clear();
        }

        /// <summary>
        /// Creates a dynamic fluent API assembly from the types contained in the specified type's assembly and return its file name.
        /// </summary>
        /// <param name="fromAssemblyToScan">The type's assembly to scan.</param>
        /// <param name="assemblyName">The name of the dynamic fluent API assembly to create.</param>
        /// <param name="parent">The base type from which created proxy types inherit.</param>
        /// <param name="overwriteExisting">true to overwrite the output assembly file if it exists; otherwise, false.</param>
        /// <returns>An initialized instance of the <see cref="FluentApiFactoryExecutionResult"/> class.</returns>
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Type fromAssemblyToScan, string assemblyName = null, Type parent = null, bool? overwriteExisting = null)
        {
            return AssemblyFrom(fromAssemblyToScan.Assembly, assemblyName, null, parent, overwriteExisting);
        }

        /// <summary>
        /// Creates a dynamic fluent API assembly from the specified types contained in the specified assembly and return its file name.
        /// </summary>
        /// <param name="assemblyToScan">
        /// The assembly to scan for types that will provide fluent API support. Only types marked 
        /// with the custom attribute <see cref="FluentApiTargetAttribute"/> will be considered.
        /// </param>
        /// <param name="assemblyName">The name of the dynamic fluent API assembly to create.</param>
        /// <param name="fileName">The physical file name under which the dynamic fluent API assembly will be saved.</param>
        /// <param name="parent">The base type from which created proxy types inherit.</param>
        /// <param name="overwriteExisting">true to overwrite the output assembly file if it exists; otherwise, false.</param>
        /// <returns>An initialized instance of the <see cref="FluentApiFactoryExecutionResult"/> class.</returns>
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Assembly assemblyToScan, string assemblyName = null, string fileName = null, Type parent = null, bool? overwriteExisting = null)
        {
            // get only types marked for fluent api support
            var assemblyTypes = assemblyToScan.GetTypes()
                .Where(t => t.GetCustomAttribute<FluentApiTargetAttribute>(true) != null)
                .ToArray();

            return AssemblyFrom(assemblyTypes, assemblyName, fileName, parent, overwriteExisting);
        }

        /// <summary>
        /// Creates a dynamic fluent API assembly from the specifed types.
        /// </summary>
        /// <param name="typesToScan">A one-dimensional array of types that will support fluent API pattern.</param>
        /// <param name="assemblyName">The name of the dynamic fluent API assembly to create.</param>
        /// <param name="fileName">The physical file name under which the dynamic fluent API assembly will be saved.</param>
        /// <param name="parent">The base type from which created proxy types inherit.</param>
        /// <param name="overwriteExisting">true to overwrite the output assembly file if it exists; otherwise, false.</param>
        /// <returns>An initialized instance of the <see cref="FluentApiFactoryExecutionResult"/> class.</returns>
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Type[] typesToScan, string assemblyName = null, string fileName = null, Type parent = null, bool? overwriteExisting = null)
        {
            CheckFactoryBusy();

            _factoryBusy = true;
            Exception error = null;

            try
            {
                var assemblyToScan = typesToScan.First().Assembly;

                if (string.IsNullOrEmpty(assemblyName))
                {
                    var asm = assemblyToScan.FullName;
                    fileName = GetAssemblyFileName(asm, out assemblyName);
                }
                else
                {
                    fileName = assemblyName + ".dll";
                }

                var typesCreated = 0;
                var info = AssemblyInfoProvider.GetAssemblyInfo(assemblyToScan);
                var asmBuilder = CreateAssemblyBuilder(assemblyName, info.GetCustomAttributeBuilders());
                var module = asmBuilder.CreateModuleBuilder(assemblyName, fileName);

                for (int i = 0; i < typesToScan.Length; i++)
                {
                    var original = typesToScan[i];
                    var originalProperties = original.GetFluentProperties();
                    var attr = original.GetCustomAttribute<FluentApiTargetAttribute>();

                    // check if the type features the 'FluentApiTargetAttribute'; when 'SetterMethodsOnly' 
                    // is set to true, all properties of the original proxy type won't have readable properties
                    // except one: the 'Object' property, which returns a reference to the extracted interface

                    var settersOnly = attr?.SetterMethodsOnly ?? false;
                    var staticInit = attr?.StaticNewInstanceMethod;

                    // Given the original type, extract its interface using all public read & write properties.
                    // We then create an internal sealed proxy of the original type that implements the interface.
                    // Finally, we create a fluent API wrapper for the original type; the wrapper has two public
                    // constructors: one that creates a new instance of the proxy, and the other one that
                    // accepts an instance of a type that implements the extracted interface.

                    var success =
                        module.CreateInterface(original, out var interfaceType, originalProperties) &&
                        module.CreateProxy(original, out var proxyType, new[] { interfaceType }, parent, INTERNAL_SEALED_CLASS, originalProperties) &&
                        module.CreateFluent(proxyType, out var fluentType, new[] { interfaceType }, parent, originalProperties, settersOnly, staticInit);

                    if (success) typesCreated++;
                }

                if (typesCreated > 0)
                {
                    asmBuilder.DefineVersionInfoResource(info.Product
                        , info.ProductVersion?.Replace('.', ':')
                        , info.Company
                        , info.Copyright
                        , info.Trademark
                    );

                    if (!TrySaveAssembly(asmBuilder, fileName, overwriteExisting))
                    {
                        fileName = null;
                        error = new IOException($"Could not save the file '{fileName}'.");
                    }
                }
                else
                {
                    fileName = null;
                    error = new InvalidProgramException("No type has been created.");
                }
            }
            catch (Exception ex)
            {
                fileName = null;
                error = ex;
                Error?.Invoke(ex);
            }
            finally
            {
                _factoryBusy = false;
            }

            return FluentApiFactoryExecutionResult.Create(error, fileName);
        }

        /// <summary>
        /// Constructs a default name when creating a dynamic fluent API assembly.
        /// </summary>
        /// <typeparam name="TTarget">The type for which to create an assembly name.</typeparam>
        /// <param name="assemblyName">Returns the assembly name.</param>
        /// <param name="suffix">
        /// A string to append to the name of the assembly. If null (Nothing in Visual Basic), 
        /// the namespace of the current <see cref="FluentApiFactory"/> type is used. 
        /// To avoid this behaviour, pass in an empty string, or your own suffix.
        /// </param>
        /// <param name="dontAppendHash">false to append the hash code of the assembly name to the returned file name; otherwise, true.</param>
        /// <returns>A string representing the physical file name of the assembly.</returns>
        public static string GetAssemblyFileName<TTarget>(out string assemblyName, string suffix = null, bool dontAppendHash = false)
            => GetAssemblyFileName(typeof(TTarget).FullName, out assemblyName, suffix, dontAppendHash);

        /// <summary>
        /// Constructs a default name when creating a dynamic fluent API assembly.
        /// </summary>
        /// <param name="typeName">The full name of type for which to create an assembly name.</param>
        /// <param name="assemblyName">Returns the assembly name.</param>
        /// <param name="suffix">
        /// A string to append to the name of the assembly. If null (Nothing in Visual Basic), 
        /// the namespace of the current <see cref="FluentApiFactory"/> type is used. 
        /// To avoid this behaviour, pass in an empty string, or your own suffix.
        /// </param>
        /// <param name="dontAppendHash">false to append the hash code of the assembly name to the returned file name; otherwise, true.</param>
        /// <returns>A string representing the physical file name of the assembly.</returns>
        public static string GetAssemblyFileName(string typeName, out string assemblyName, string suffix = null, bool dontAppendHash = false)
        {
            const string DOT = ".";

            if (suffix == null)
                suffix = $"{DOT}{typeof(FluentApiFactory).Namespace}";
            else if (!suffix.StartsWith(DOT))
                suffix += DOT;

            var hash = string.Empty;
            var name = typeName.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[0];
            assemblyName = $"{name}{suffix}";

            if (!dontAppendHash)
            {
                hash = DOT + assemblyName.GetHashCode().ToString("x");
            }

            return $"{assemblyName}{hash}{DOT}dll";
        }

        #endregion
        
        #endregion

        #region not public

        internal static AssemblyBuilder CreateAssemblyBuilder(string name, CustomAttributeBuilder[] assemblyAttributes = null)
        {
            var builder = System.Threading.Thread.GetDomain().DefineDynamicAssembly(
                new AssemblyName(name)
                , AssemblyBuilderAccess.RunAndSave
                , assemblyAttributes
            );
            return builder;
        }

        internal static ModuleBuilder CreateModuleBuilder(this AssemblyBuilder builder, string name, string fileName)
            => builder.DefineDynamicModule(name, fileName);

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

            result = null;

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
        ) {
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
        ) {
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
        ) {
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
        ) {
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

                    if (GetFluentApiMethodInfo(property, out FluentApiMethodAttribute methodAttr))
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

        internal static void ImplementProperty(this TypeBuilder targetBuilder, PropertyInfo property, ModuleBuilder module, bool isAbstract, Type intf = null, bool isInternal = false)
        {
            var propertyType = property.PropertyType;

            // should the property be ignored?
            if (property.GetCustomAttribute<FluentApiIgnoreAttribute>() != null)
                return;

            if (Options.DereferenceExternals)
            {
                if (module.CreateDereferencedTypes(propertyType, out var pt))
                    propertyType = pt;
                else
                    DereferenceExternalsFailed(property);
            }
            
            Type[] parameterTypes;
            MethodInfo propMethod = null;
            MethodBuilder method = null;
            FieldBuilder backingField = null;
            PropertyBuilder propertyBuilder = null;
            var camelCasePropertyName = property.Name.ToCamelCase();
            var propertyAttributes = PropertyAttributes.HasDefault;
            var attrs = (isInternal ? MethodAttributes.Assembly :  MethodAttributes.Public) | MethodAttributes.Virtual | MethodAttributes.HideBySig;

            if (isAbstract)
                attrs |= MethodAttributes.Abstract;
            else
                attrs |= MethodAttributes.SpecialName | MethodAttributes.NewSlot;

            if (property.CanRead)
            {
                propMethod = property.GetGetMethod();
                parameterTypes = propMethod.GetParameterTypes();

                propertyBuilder = targetBuilder.DefineProperty(property.Name, propertyAttributes, propertyType, null);
                method = targetBuilder.DefineMethod($"{get_}{property.Name}", attrs, CallingConventions.HasThis, propMethod.ReturnType, parameterTypes);

                if (!isAbstract)
                {
                    // the private _object field that holds a reference to the original type
                    backingField = targetBuilder.DefineField($"_{camelCasePropertyName}", propertyType, FieldAttributes.Private);
                    _implGet();
                }

                propertyBuilder.SetGetMethod(method);
            }

            if (property.CanWrite)
            {
                propMethod = property.GetSetMethod();
                parameterTypes = propMethod.GetParameterTypes();

                if (propertyBuilder == null)
                {
                    propertyBuilder = targetBuilder.DefineProperty(property.Name, propertyAttributes, propertyType, parameterTypes);
                }

                method = targetBuilder.DefineMethod($"{set_}{property.Name}", attrs, CallingConventions.HasThis, propMethod.ReturnType, parameterTypes);

                if (!isAbstract)
                {
                    if (backingField == null)
                        backingField = targetBuilder.DefineField($"_{camelCasePropertyName}", propertyType, FieldAttributes.Private);

                    _implSet();
                }

                propertyBuilder.SetSetMethod(method);
            }

            void _implGet()
            {
                method.SetImplementationFlags(MethodImplAttributes.IL);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, backingField);
                il.Emit(OpCodes.Ret);

                if (intf != null)
                {
                    var originalGetter = intf.GetMethod($"{get_}{property.Name}");
                    targetBuilder.DefineMethodOverride(method, originalGetter);
                }
            }

            void _implSet()
            {
                method.SetImplementationFlags(MethodImplAttributes.IL);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stfld, backingField);
                il.Emit(OpCodes.Ret);

                if (intf != null)
                {
                    var originalSetter = intf.GetMethod($"{set_}{property.Name}");
                    targetBuilder.DefineMethodOverride(method, originalSetter);
                }
            }
        }

        internal static TypeBuilder ImplementReadOnlyProperty(this TypeBuilder targetBuilder, string name, FieldInfo targetField)
        {
            var fieldType = targetField.FieldType;
            var propertyBuilder = targetBuilder.DefineProperty(name, PropertyAttributes.HasDefault, fieldType, null);
            var methodAttrs = MethodAttributes.Public | MethodAttributes.Virtual |
                MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot;
            var getter = targetBuilder.DefineMethod($"{get_}{name}", methodAttrs, fieldType, Type.EmptyTypes);

            var il = getter.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, targetField);
            il.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getter);
            return targetBuilder;
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
                    module.CreateProxy(sourceType, out var propTypeProxy, new[] { propTypeIntf }, attributes: INTERNAL_SEALED_CLASS)) {
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

            result = null;
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
        /// Create two constructors (default and one that takes a single parameter) for the original type.
        /// </summary>
        /// <param name="builder">The type builder.</param>
        /// <param name="originalsProxy">The type of the underlying object.</param>
        /// <param name="targetField">Returns the backing field info for the original type.</param>
        /// <param name="fieldType">The type of the backing field. If null (Nothing in Visual Basic), the original type is used.</param>
        /// <param name="parent">
        /// The base type of which the constructors being built should invoke the default constructor. If null, <see cref="object"/> is used;
        /// otherwise, this type must have a default (parameterless) constructor that can be invoked.
        /// </param>
        /// <returns></returns>
        internal static TypeBuilder CreateConstructors(this TypeBuilder builder, Type originalsProxy, out FieldInfo targetField, Type fieldType = null, Type parent = null)
        {
            if (fieldType == null) fieldType = originalsProxy;
            if (parent == null) parent = typeof(object);

            // for calling the base constructor; we suppose this class inherits directly from System.Object
            var objctor = parent.GetConstructor(Type.EmptyTypes);

            // the private _object field that holds a reference to the original or interface type
            var fieldBuilder = builder.DefineField($"_{Options.WrappedObjectPropertyName.ToCamelCase()}", fieldType, FieldAttributes.Private);

            // create the constructor builder
            var ctorBuilder = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var il = ctorBuilder.GetILGenerator();
            /* Sample IL to generate
            IL_0000:  ldarg.0
            IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
            IL_0006:  nop
            IL_0007:  nop
            IL_0008:  ldarg.0
            IL_0009:  newobj     instance void OriginalClassProxy::.ctor()
            IL_000e:  stfld      class FluentOriginalClass::_object
            IL_0013:  ret
            */
            il.Emit(OpCodes.Ldarg_0); // load 'this'
            il.Emit(OpCodes.Call, objctor); // call the base constructor
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, originalsProxy.GetConstructors()[0]); // create a new instance of the original type's proxy by invoking the default constructor
            il.Emit(OpCodes.Stfld, fieldBuilder); // store the new instance into the field '_object'
            il.Emit(OpCodes.Ret);

            // add a second constructor that takes in either the original or its interface type
            il = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { fieldType }).GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, objctor);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldarg_0); // load 'this'
            il.Emit(OpCodes.Ldarg_1); // load the first constructor argument
            il.Emit(OpCodes.Stfld, fieldBuilder); // store the argument into the '_object' field
            il.Emit(OpCodes.Ret);

            targetField = fieldBuilder;
            return builder;
        }

        internal static void ImplementFluentMethod(MethodBuilder methodBuilder, MethodInfo method, FieldInfo targetField, Type[] parameterTypes, bool isSetter = false)
        {
            var il = methodBuilder.GetILGenerator();

            if (isSetter)
            {
                /*
                IL_0000:  nop
                IL_0001:  ldarg.0
                IL_0002:  ldfld      class FluentOriginalClass::_object
                IL_0007:  ldarg.1
                IL_0008:  callvirt   instance void OriginalClassProxy::set_FirstName(string)
                IL_0009:  ldarg.0
                IL_0010:  ret
                */
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, targetField);
                il.Emit(OpCodes.Ldarg_1);
                il.EmitCall(OpCodes.Callvirt, method, parameterTypes);
                il.Emit(OpCodes.Ldarg_0); // load 'this' onto the the stack to return it
            }
            else
            {
                /*
                IL_0000:  ldarg.0
                IL_0001:  ldfld      class FluentOriginalClass::_object
                IL_0006:  callvirt   instance string OriginalClassProxy::get_FirstName()
                IL_000b:  ret    
                */
                il.Emit(OpCodes.Ldarg_0); // load 'this' onto stack
                il.Emit(OpCodes.Ldfld, targetField); // results in 'this._object'
                il.EmitCall(OpCodes.Callvirt, method, parameterTypes);
            }

            il.Emit(OpCodes.Ret);
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

        /// <summary>
        /// Returns an array of <see cref="CustomAttributeBuilder"/> elements 
        /// from the properties of the specified <see cref="AssemblyInfoProvider"/>.
        /// </summary>
        /// <param name="info">The assembly info provider to extract metadata from.</param>
        /// <param name="guid">
        /// The <see cref="Guid"/> string to set. If null (Nothing in Visual Basic), a new 
        /// <see cref="Guid"/> is generated; if empty, the value of the proerty 
        /// <see cref="AssemblyInfoProvider.Guid"/> is used; otherwise, the specified value is used.
        /// </param>
        /// <returns></returns>
        internal static CustomAttributeBuilder[] GetCustomAttributeBuilders(this AssemblyInfoProvider info, string guid = null)
        {
            var ctorTitle = typeof(AssemblyTitleAttribute).GetConstructor(new[] { typeof(string) });
            var ctorDescription = typeof(AssemblyDescriptionAttribute).GetConstructor(new[] { typeof(string) });
            var ctorConfiguration = typeof(AssemblyConfigurationAttribute).GetConstructor(new[] { typeof(string) });
            var ctorProduct = typeof(AssemblyProductAttribute).GetConstructor(new[] { typeof(string) });
            var ctorProductVersion = typeof(AssemblyVersionAttribute).GetConstructor(new[] { typeof(string) });
            var ctorCompany = typeof(AssemblyCompanyAttribute).GetConstructor(new[] { typeof(string) });
            var ctorCopyright = typeof(AssemblyCopyrightAttribute).GetConstructor(new[] { typeof(string) });
            var ctorTrademark = typeof(AssemblyTrademarkAttribute).GetConstructor(new[] { typeof(string) });
            var ctorFileVersion = typeof(AssemblyFileVersionAttribute).GetConstructor(new[] { typeof(string) });
            var ctorCulture = typeof(AssemblyCultureAttribute).GetConstructor(new[] { typeof(string) });
            var ctorGuid = typeof(GuidAttribute).GetConstructor(new[] { typeof(string) });
            var ctorComVisible = typeof(ComVisibleAttribute).GetConstructor(new[] { typeof(bool) });
            var ctorNeutralResourcesLanguage = typeof(NeutralResourcesLanguageAttribute).GetConstructor(new[] { typeof(string) });

            if (guid == null)
                guid = Guid.NewGuid().ToString();
            else if (Equals(guid, string.Empty))
                guid = info.Guid;

            return new[]
            {
                new CustomAttributeBuilder(ctorTitle, new[] { info.Title }),
                new CustomAttributeBuilder(ctorDescription, new[] { info.Description }),
                new CustomAttributeBuilder(ctorConfiguration, new[] { info.Configuration }),
                new CustomAttributeBuilder(ctorProduct, new[] { info.Product }),
                new CustomAttributeBuilder(ctorProductVersion, new[] { info.ProductVersion }),
                new CustomAttributeBuilder(ctorCompany, new[] { info.Company }),
                new CustomAttributeBuilder(ctorCopyright, new[] { info.Copyright }),
                new CustomAttributeBuilder(ctorTrademark, new[] { info.Trademark }),
                new CustomAttributeBuilder(ctorFileVersion, new[] { info.FileVersion }),
                new CustomAttributeBuilder(ctorCulture, new[] { info.Culture }),
                new CustomAttributeBuilder(ctorGuid, new[] { guid }),
                new CustomAttributeBuilder(ctorComVisible, new object[] { info.ComVisible }),
                new CustomAttributeBuilder(ctorNeutralResourcesLanguage, new[] { info.NeutralResourcesLanguage }),
            };
        }

        internal static bool IsWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);

        internal static string NotBlank(this string s) => s.NotBlank(string.Empty);

        internal static string NotBlank(this string s, string defValue)
            => string.IsNullOrWhiteSpace(s) ? defValue : s;

        /// <summary>
        /// Validates the string; optionally throws <see cref="ArgumentNullException"/> when the string is null or blank.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="thro">If <paramref name="s"/> is null or white space, true to throw an exception; otherwise, false to return an empty string.</param>
        /// <returns></returns>
        internal static string NotBlank(this string s, bool thro)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                if (thro) throw new ArgumentNullException("The string cannot be null or blank.", (Exception)null);
                return string.Empty;
            }
            else
            {
                return s;
            }
        }

        /// <summary>
        /// Make sure the string is a valid type name identifier by removing illegal characters.
        /// If the remaining string is blank, optionally throws <see cref="ArgumentNullException"/>.
        /// </summary>
        /// <param name="s">The string to validate.</param>
        /// <param name="thro">If <paramref name="s"/> is null or white space, true to throw an exception; otherwise, false to return an empty string.</param>
        /// <returns></returns>
        internal static string SanitizeName(this string s, bool thro = false) =>
            // use Regex to do replacements
            Replace(s.NotBlank(), RE_VALID_TYPE_NAME, string.Empty, RegexOptions.Compiled).NotBlank(thro);

        internal static T NotNull<T>(this T obj, string paramName = null) where T : class
        {
            if (obj == null)
            {
                if (paramName.IsWhiteSpace())
                {
                    throw new ArgumentNullException($"The argument of type {typeof(T).FullName} cannot be null.", (Exception)null);
                }
                else
                {
                    throw new ArgumentNullException(paramName, $"The argument of type {typeof(T).FullName} cannot be null.");
                }
            }
            return obj;
        }

        private static MethodBuilder CreateMethod(this TypeBuilder builder, MethodInfo method)
        {
            var parameterTypes = method.GetParameterTypes();
            var methBuilder = builder.DefineMethod(method.Name, method.Attributes, method.ReturnType, parameterTypes);
            return methBuilder;
        }

        private static Type[] GetParameterTypes(this MethodInfo methodInfo)
            => methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();

        private static string GetTypeName(this Type targetType, string prefix, string suffix = null)
            => $"{targetType.Namespace}.{prefix}{targetType.Name}{suffix}";

        /// <summary>
        /// Returns an array of <see cref="PropertyInfo"/> elements from the specified 
        /// type that isn't marked with the attribute <see cref="FluentApiIgnoreAttribute"/>.
        /// </summary>
        /// <param name="original">The type to filter. Only public read &amp; write properties are returned.</param>
        /// <returns></returns>
        private static PropertyInfo[] GetFluentProperties(this Type original)
        {
            return original.GetProperties()
                    .Where(p => p.CanRead && p.CanWrite)
                    .Where(p => p.GetCustomAttribute<FluentApiIgnoreAttribute>() == null)
                    .ToArray();
        }

        private static string ToCamelCase(this string s) => s[0].ToString().ToLower() + s.Substring(1);

        private static bool GetTypeFromDictionary(string fullTypeName, out Type result)
        {
            result = null;

            if (InternalDictionary.ContainsKey(fullTypeName))
            {
                while (!InternalDictionary.TryGetValue(fullTypeName, out result))
                {
                    Task.Delay(100);
                }
            }

            return result != null;
        }

        private static void AddTypeToDictionary(Type value) => AddTypeToDictionary(value.FullName, value);

        private static void AddTypeToDictionary(string key, Type value)
        {
            if (!InternalDictionary.ContainsKey(key))
            {
                while (!InternalDictionary.TryAdd(key, value))
                {
                    Task.Delay(100);
                }
            }
        }

        private static bool GetFluentApiMethodInfo(this PropertyInfo property, out FluentApiMethodAttribute methodAttr)
        {
            methodAttr = null;
            foreach (var attr in property.GetCustomAttributes(false))
            {
                if (attr is FluentApiSetterMethodAttribute setter)
                {
                    methodAttr = setter;
                    break;
                }
                else if (attr is FluentApiMethodAttribute setterGetter)
                {
                    methodAttr = setterGetter;
                    break;
                }
            }
            return methodAttr != null;
        }

        private static Type First(this Type[] types)
        {
            if (types != null && types.Length > 0)
            {
                return types[0];
            }
            return null;
        }

        private static void DereferenceExternalsFailed(PropertyInfo property)
        {
            if (Options.ThrowIfDereferenceExternalsFails)
            {
                throw new InvalidOperationException($"Cannot create a dereferenced type for the property {property.ReflectedType?.FullName}.{property.Name}.");
            }
        }

        private static void CheckFactoryBusy()
        {
            if (_factoryBusy)
                throw new InvalidOperationException("The fluent API factory is busy right now!");
        }

        private static bool TrySaveAssembly(AssemblyBuilder asmBuilder, string outputAssemblyFileName, bool? overwriteExisting = null)
        {
            var success = true;
            try
            {
                if (File.Exists(outputAssemblyFileName))
                {
                    if (!overwriteExisting.HasValue)
                        overwriteExisting = Options.OverwriteExisting;

                    if (overwriteExisting.Value)
                        File.Delete(outputAssemblyFileName);
                    else
                        throw new IOException($"The assembly '{outputAssemblyFileName}' already exists.");
                }
            }
            catch (Exception ex)
            {
                success = false;
                DeleteFileError?.Invoke(ex, asmBuilder, outputAssemblyFileName);
            }

            if (success)
            {
                //asmBuilder.Save(outputAssemblyFileName, PortableExecutableKinds.PE32Plus, ImageFileMachine.AMD64);
                asmBuilder.Save(outputAssemblyFileName);
            }

            return success;
        }
        
        #endregion
    }
}
