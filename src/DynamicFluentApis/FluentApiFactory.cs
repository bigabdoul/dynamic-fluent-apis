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
using System.Threading;

namespace DynamicFluentApis
{
    /// <summary>
    /// Provides functionalities that allow building a dynamic assembly with fluent API support
    /// for types from another assembly. This static class is not guaranteed to be thread-safe.
    /// </summary>
    public static class FluentApiFactory
    {
        #region fields

        const string get_ = "get_";
        const string set_ = "set_";

        /// <summary>
        /// Regex to remove illegal characters from a type name. Strips digits from the beginning.
        /// </summary>
        const string RE_VALID_TYPE_NAME = @"(^[0-9]*)|([^\w]*)";

        /// <summary>
        /// public class OriginalClass {}
        /// </summary>
        internal const TypeAttributes PUBLIC_CLASS = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit;

        /// <summary>
        /// internal sealed class OriginalClassProxy : IOriginalClass {}
        /// </summary>
        internal const TypeAttributes INTERNAL_SEALED_CLASS = TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.BeforeFieldInit;

        internal static readonly Assembly SysAsm = typeof(string).Assembly;
        static readonly ConcurrentDictionary<string, Type> InternalDictionary = new ConcurrentDictionary<string, Type>();
        static FluentApiFactoryOptions _options = new FluentApiFactoryOptions();

        static volatile bool _factoryBusy;

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
        /// Creates a dynamic fluent API assembly from the types contained 
        /// in the specified type's assembly and return its file name.
        /// </summary>
        /// <param name="fromAssemblyToScan">The type's assembly to scan.</param>
        /// <param name="assemblyName">
        /// The name of the dynamic fluent API assembly to create.
        /// </param>
        /// <param name="parent">The base type from which created proxy types inherit.</param>
        /// <param name="overwriteExisting">
        /// true to overwrite the output assembly file if it exists; otherwise, false.
        /// </param>
        /// <returns>
        /// An initialized instance of the <see cref="FluentApiFactoryExecutionResult"/> class.
        /// </returns>
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Type fromAssemblyToScan
            , string assemblyName = null, Type parent = null, bool? overwriteExisting = null)
        {
            return AssemblyFrom(fromAssemblyToScan.Assembly, assemblyName, null, parent
                , overwriteExisting);
        }

        /// <summary>
        /// Creates a dynamic fluent API assembly from the specified types 
        /// contained in the specified assembly and return its file name.
        /// </summary>
        /// <param name="assemblyToScan">
        /// The assembly to scan for types that will provide fluent API support. Only types marked 
        /// with the custom attribute <see cref="FluentApiTargetAttribute"/> will be considered.
        /// </param>
        /// <param name="assemblyName">
        /// The name of the dynamic fluent API assembly to create.
        /// </param>
        /// <param name="fileName">
        /// The physical file name under which the dynamic fluent API assembly will be saved.
        /// </param>
        /// <param name="parent">The base type from which created proxy types inherit.</param>
        /// <param name="overwriteExisting">
        /// true to overwrite the output assembly file if it exists; otherwise, false.
        /// </param>
        /// <returns>
        /// An initialized instance of the <see cref="FluentApiFactoryExecutionResult"/> class.
        /// </returns>
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Assembly assemblyToScan
            , string assemblyName = null, string fileName = null, Type parent = null
            , bool? overwriteExisting = null)
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
        public static FluentApiFactoryExecutionResult AssemblyFrom(this Type[] typesToScan
            , string assemblyName = null, string fileName = null, Type parent = null
            , bool? overwriteExisting = null)
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

                    // we should look for types that inherit other types and
                    // create interfaces that reflect that inheritance chain
                    var baseType = original.BaseType;

                    if (baseType != null)
                    {
                        // TODO: Create an interface for the base type.
                    }

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
            FieldBuilder backingField = null;
            PropertyBuilder propertyBuilder = null;
            var camelCasePropertyName = property.Name.ToCamelCase();
            var propertyAttributes = PropertyAttributes.HasDefault;
            var attrs = (isInternal ? MethodAttributes.Assembly :  MethodAttributes.Public) | MethodAttributes.Virtual | MethodAttributes.HideBySig;

            if (isAbstract)
                attrs |= MethodAttributes.Abstract;
            else
                attrs |= MethodAttributes.SpecialName | MethodAttributes.NewSlot;

            MethodInfo propMethod;
            MethodBuilder method;
            if (property.CanRead)
            {
                propMethod = property.GetGetMethod();
                parameterTypes = propMethod.GetParameterTypes();

                propertyBuilder = targetBuilder.DefineProperty(property.Name, propertyAttributes, propertyType, null);
                method = targetBuilder.DefineMethod($"{get_}{property.Name}", attrs, CallingConventions.HasThis, propMethod.ReturnType, parameterTypes);

                if (!isAbstract)
                {
                    // the _object field that holds a reference to the original type
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

            // the _object field that holds a reference to the original or interface type
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
            //var ctorProductVersion = typeof(AssemblyVersionAttribute).GetConstructor(new[] { typeof(string) });
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
                //new CustomAttributeBuilder(ctorProductVersion, new[] { info.ProductVersion }),
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
        /// <param name="thro">
        /// If <paramref name="s"/> is null or white space, true to throw 
        /// an exception; otherwise, false to return an empty string.
        /// </param>
        /// <returns></returns>
        internal static string SanitizeName(this string s, bool thro = false)=>
            // use Regex to do replacements
            Replace(s.NotBlank(), RE_VALID_TYPE_NAME, string.Empty
                , RegexOptions.Compiled).NotBlank(thro);

        internal static T NotNull<T>(this T obj, string paramName = null) where T : class
        {
            if (obj == null)
            {
                if (paramName.IsWhiteSpace())
                {
                    throw new ArgumentNullException(
                        $"The argument of type {typeof(T).FullName} cannot be null.",
                        (Exception)null
                    );
                }
                else
                {
                    throw new ArgumentNullException(paramName
                        , $"The argument of type {typeof(T).FullName} cannot be null."
                    );
                }
            }
            return obj;
        }

        internal static Type[] GetParameterTypes(this MethodInfo methodInfo)
            => methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();

        internal static string GetTypeName(this Type targetType, string prefix, string suffix = null)
            => $"{targetType.Namespace}.{prefix}{targetType.Name}{suffix}";

        /// <summary>
        /// Returns an array of <see cref="PropertyInfo"/> elements from the specified 
        /// type that isn't marked with the attribute <see cref="FluentApiIgnoreAttribute"/>.
        /// </summary>
        /// <param name="original">
        /// The type to filter. Only public read &amp; write properties are returned.
        /// </param>
        /// <returns></returns>
        internal static PropertyInfo[] GetFluentProperties(this Type original)
        {
            return original.GetProperties()
                    .Where(p => p.CanRead && p.CanWrite)
                    .Where(p => p.GetCustomAttribute<FluentApiIgnoreAttribute>() == null)
                    .ToArray();
        }

        internal static bool GetTypeFromDictionary(string fullTypeName, out Type result)
        {
            result = null;

            if (InternalDictionary.ContainsKey(fullTypeName))
            {
                while (!InternalDictionary.TryGetValue(fullTypeName, out result))
                {
                    Thread.Sleep(20);
                }
            }

            return result != null;
        }

        internal static void AddTypeToDictionary(Type value) =>
            AddTypeToDictionary(value.FullName, value);

        internal static void AddTypeToDictionary(string key, Type value)
        {
            if (!InternalDictionary.ContainsKey(key))
            {
                while (!InternalDictionary.TryAdd(key, value))
                {
                    Task.Delay(100);
                }
            }
        }

        internal static bool GetFluentApiMethodInfo(this PropertyInfo property
            , out FluentApiMethodAttribute methodAttr)
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

        internal static void DereferenceExternalsFailed(PropertyInfo property)
        {
            if (Options.ThrowIfDereferenceExternalsFails)
            {
                throw new InvalidOperationException(
                    $"Cannot create a dereferenced type for the property " +
                    $"{property.ReflectedType?.FullName}.{property.Name}."
                );
            }
        }

        private static MethodBuilder CreateMethod(this TypeBuilder builder, MethodInfo method)
        {
            var parameterTypes = method.GetParameterTypes();
            var methBuilder = builder.DefineMethod(method.Name
                , method.Attributes, method.ReturnType, parameterTypes
            );
            return methBuilder;
        }

        private static string ToCamelCase(this string s) =>
            s[0].ToString().ToLower() + s.Substring(1);

        private static Type First(this Type[] types)
        {
            if (types != null && types.Length > 0)
            {
                return types[0];
            }
            return null;
        }

        private static void CheckFactoryBusy()
        {
            if (_factoryBusy)
                throw new InvalidOperationException("The fluent API factory is busy right now!");
        }

        private static bool TrySaveAssembly(AssemblyBuilder asmBuilder
            , string outputAssemblyFileName, bool? overwriteExisting = null)
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
                        throw new IOException(
                            $"The assembly '{outputAssemblyFileName}' already exists."
                        );
                }
            }
            catch (Exception ex)
            {
                success = false;
                DeleteFileError?.Invoke(ex, asmBuilder, outputAssemblyFileName);
            }

            if (success)
            {
                //asmBuilder.Save(outputAssemblyFileName, PortableExecutableKinds.PE32Plus
                //    , ImageFileMachine.AMD64);
                asmBuilder.Save(outputAssemblyFileName);
            }

            return success;
        }
        
        #endregion
    }
}
