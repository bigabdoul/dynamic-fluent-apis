using System;

namespace DynamicFluentApis
{
    /// <summary>
    /// Represents an object that determines the way the static <see cref="FluentApiFactory"/> class is operating.
    /// </summary>
    public sealed class FluentApiFactoryOptions
    {
        #region fields

        private string _interfaceNamePrefix = "I";
        private string _proxyClassNameSuffix = "Proxy";
        private string _fluentTypeNamePrefix = "Fluent";
        private string _wrapperObjectPropertyName = "Object";
        private readonly FluentApiFactoryConfig _fluentApiWrapper;

        #endregion

        #region default constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiFactoryOptions"/> class.
        /// </summary>
        public FluentApiFactoryOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiFactoryOptions"/> class.
        /// </summary>
        /// <param name="wrapper">The wrapper to return when switching back. This is for providing fluent API support.</param>
        internal FluentApiFactoryOptions(FluentApiFactoryConfig wrapper)
        {
            _fluentApiWrapper = wrapper;
        }

        #endregion

        #region public properties

        /// <summary>
        /// When creating an interface, gets or sets the string to prepend to its type name.
        /// </summary>
        public string InterfaceNamePrefix
        {
            get => _interfaceNamePrefix;
            set => _interfaceNamePrefix = value.SanitizeName();
        }

        /// <summary>
        /// When creating a proxy of an original type, gets or sets the string to append to the type's name. Default is 'Proxy'.
        /// </summary>
        public string ProxyClassNameSuffix
        {
            get => _proxyClassNameSuffix;
            set => _proxyClassNameSuffix = value.SanitizeName();
        }

        /// <summary>
        /// When creating a fluent wrapper type, gets or sets the string to prepend to the type's name. Default is 'Fluent'.
        /// </summary>
        public string FluentTypeNamePrefix
        {
            get => _fluentTypeNamePrefix;
            set => _fluentTypeNamePrefix = value.SanitizeName();
        }

        /// <summary>
        /// In a fluent wrapper type, gets or sets the name of the property that returns a reference to the underlying type.
        /// </summary>
        public string WrappedObjectPropertyName
        {
            get => _wrapperObjectPropertyName;
            set => _wrapperObjectPropertyName = value.SanitizeName(true);
        }

        /// <summary>
        /// Determines whether to create completely independent types within the
        /// dynamic assembly, therefore removing the need of external references.
        /// The default value is true.
        /// </summary>
        public bool DereferenceExternals { get; set; } = true;

        /// <summary>
        /// Determines whether to throw an exception when dereferencing an external
        /// type fails. The default value is true.
        /// </summary>
        public bool ThrowIfDereferenceExternalsFails { get; set; } = true;

        /// <summary>
        /// Determines whether to overwrite an existing assembly with the same file name.
        /// </summary>
        public bool OverwriteExisting { get; set; }

        #endregion

        #region Fluent API Support

        /// <summary>
        /// Creates a new instance of the <see cref="FluentApiFactoryOptions"/> class.
        /// </summary>
        /// <returns></returns>
        public static FluentApiFactoryOptions Create() => new FluentApiFactoryOptions();

        /// <summary>
        /// When creating an interface, sets the string to prepend to its type name.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetInterfaceNamePrefix(string value)
        {
            InterfaceNamePrefix = value;
            return this;
        }

        /// <summary>
        /// When creating a proxy of an original type, sets the string to append to the type's name.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetProxyClassNameSuffix(string value)
        {
            ProxyClassNameSuffix = value;
            return this;
        }

        /// <summary>
        /// When creating a fluent wrapper type, sets the string to prepend to the type's name.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetFluentTypeNamePrefix(string value)
        {
            FluentTypeNamePrefix = value;
            return this;
        }

        /// <summary>
        /// In a fluent wrapper type, sets the name of the property that returns a reference to the underlying type.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetWrappedObjectPropertyName(string value)
        {
            WrappedObjectPropertyName = value;
            return this;
        }

        /// <summary>
        /// Set whether to create completely independent types within the
        /// dynamic assembly, therefore removing the need of external references.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetDereferenceExternals(bool value)
        {
            DereferenceExternals = value;
            return this;
        }

        /// <summary>
        /// Set whether to throw an exception when dereferencing an external type fails
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetThrowIfDereferenceExternalsFails(bool value)
        {
            ThrowIfDereferenceExternalsFails = value;
            return this;
        }

        /// <summary>
        /// Set whether to overwrite an existing assembly with the same file name.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions SetOverwriteExisting(bool value)
        {
            OverwriteExisting = value;
            return this;
        }

        /// <summary>
        /// Switches back to the <see cref="FluentApiFactoryConfig"/> instance that 
        /// initialized the current <see cref="FluentApiFactoryOptions"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public FluentApiFactoryConfig WithConfig()
        {
            if (_fluentApiWrapper == null)
                new InvalidOperationException();

            return _fluentApiWrapper;
        } 

        #endregion
    }
}
