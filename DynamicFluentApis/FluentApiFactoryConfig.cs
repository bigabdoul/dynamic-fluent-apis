using DynamicFluentApis.Core;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace DynamicFluentApis
{
    /// <summary>
    /// Provides fluent API support for the static <see cref="FluentApiFactory"/> class.
    /// </summary>
    public sealed class FluentApiFactoryConfig : IDisposable
    {
        private bool _executed;
        private bool _disposed;
        private Type[] _assemblyTypes;
        private FluentApiFactoryOptions _options;
        private FluentApiFactoryExecutionResult _result;
        private Action<Exception> _onErrorHandler;
        private Action<Exception, AssemblyBuilder, string> _onDeleteHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiFactoryConfig"/> class.
        /// </summary>
        private FluentApiFactoryConfig()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="FluentApiFactoryConfig"/> class.
        /// </summary>
        /// <returns></returns>
        public static FluentApiFactoryConfig Create() => new FluentApiFactoryConfig();

        /// <summary>
        /// Add an event handler when a critical error occurs during the execution.
        /// </summary>
        /// <param name="handler">The delegate to invoke when an error occurs.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig OnError(Action<Exception> handler)
        {
            if (_onErrorHandler != null)
                throw new InvalidOperationException("An error handler already exists.");

            FluentApiFactory.Error += handler.NotNull(nameof(handler));
            _onErrorHandler = handler;
            return this;
        }

        /// <summary>
        /// Add an event handler when an attempt to delete an existing file with the same name as the output assembly file name fails.
        /// </summary>
        /// <param name="handler">The delegate to invoke when the event is fired.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig OnDeleteError(Action<Exception, AssemblyBuilder, string> handler)
        {
            if (_onDeleteHandler != null)
                throw new InvalidOperationException("A delete file error handler already exists.");

            FluentApiFactory.DeleteFileError += handler.NotNull(nameof(handler));
            _onDeleteHandler = handler;

            return this;
        }

        /// <summary>
        /// Uses an existing or creates and returns a new instance of the <see cref="FluentApiFactoryOptions"/> 
        /// class after setting the <see cref="FluentApiFactory.Options"/> property value.
        /// </summary>
        /// <param name="overwrite">true to overwrite any previously-built existing assembly file; otherwise, false.</param>
        /// <returns></returns>
        public FluentApiFactoryOptions WithOptions(bool overwrite = false)
        {
            if (_options == null)
                _options = new FluentApiFactoryOptions(this);

            return FluentApiFactory.Options = _options.SetOverwriteExisting(overwrite);
        }

        /// <summary>
        /// Returns an instance of the <see cref="FluentApiFactoryOptions"/> class 
        /// with an instruction to delete any previously-built existing assembly.
        /// </summary>
        /// <returns></returns>
        public FluentApiFactoryOptions WithOverwriteOptions() => WithOptions(overwrite: true);

        /// <summary>
        /// Set the <see cref="FluentApiFactory.Options"/> property value.
        /// </summary>
        /// <param name="options">The options to use.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig WithOptions(FluentApiFactoryOptions options)
        {
            _options = FluentApiFactory.Options = options.NotNull(nameof(options));
            return this;
        }

        /// <summary>
        /// Reset the <see cref="FluentApiFactory.Options"/> property value with
        /// a new instance of the <see cref="FluentApiFactoryOptions"/> class.
        /// </summary>
        /// <param name="overwrite">true to overwrite any previously-built existing assembly file; otherwise, false.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig WithDefaultOptions(bool overwrite = false)
        {
            _options = FluentApiFactory.Options = new FluentApiFactoryOptions(this).SetOverwriteExisting(overwrite);
            return this;
        }

        /// <summary>
        /// Set the types that will be processed during the <see cref="Execute"/> method.
        /// </summary>
        /// <param name="types">A one-dimensional array of types to process.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig Scan(params Type[] types)
        {
            _assemblyTypes = types.NotNull(nameof(types));
            return this;
        }

        /// <summary>
        /// Retrieve all types defined in the assembly of the specified type.
        /// </summary>
        /// <param name="type">The type's assembly to scan.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig ScanAssemblyFrom(Type type) => ScanAssembly(type.Assembly);

        /// <summary>
        /// Retrieve all types defined in the specified assembly.
        /// </summary>
        /// <param name="asm">The asssembly to scan.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig ScanAssembly(Assembly asm)
        {
            // get only types marked for fluent api support
            _assemblyTypes = asm.NotNull(nameof(asm)).GetTypes()
                .Where(t => t.GetCustomAttribute<FluentApiTargetAttribute>(true) != null)
                .ToArray();

            return this;
        }

        /// <summary>
        /// Executes the method <see cref="FluentApiFactory.AssemblyFrom(Type[], string, string, Type, bool?)"/>.
        /// Make sure that you have already called the method <see cref="ScanAssemblyFrom(Type)"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">You must first call the method <see cref="ScanAssemblyFrom(Type)"/>.</exception>
        public FluentApiFactoryConfig Execute()
        {
            try
            {
                if (_assemblyTypes == null)
                    ThrowScanRequired();

                _result = FluentApiFactory.AssemblyFrom(_assemblyTypes);
                _executed = true;

                if (!_result.Succeeded)
                    _onErrorHandler?.Invoke(_result.Error);
            }
            catch (Exception ex)
            {
                if (_onErrorHandler == null) throw;
                _onErrorHandler?.Invoke(ex);
            }

            return this;
        }

        /// <summary>
        /// Set the last execution result by invoking the specified action.
        /// </summary>
        /// <param name="action">The delegate to invoke.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig SetResult(Action<FluentApiFactoryExecutionResult> action)
        {
            action(_result);
            return this;
        }

        /// <summary>
        /// Reset the last execution configuration except for error handlers, and factory configuration options.
        /// The <see cref="FluentApiFactory"/>'s internal dictionary of previously created types, such as interfaces,
        /// proxies, and fluent API types, are also discarded.
        /// </summary>
        /// <returns></returns>
        public FluentApiFactoryConfig Reset()
        {
            FluentApiFactory.Reset();

            _result = null;
            _executed = false;
            _assemblyTypes = null;

            return this;
        }

        /// <summary>
        /// Dispose all resources used by the current <see cref="FluentApiFactoryConfig"/>, 
        /// including error handlers, and optionally the last execution result.
        /// </summary>
        /// <param name="everything">true to also discard the last execution result; otherwise, false.</param>
        /// <returns></returns>
        public FluentApiFactoryConfig Release(bool everything = false)
        {
            if (_onErrorHandler != null)
            {
                FluentApiFactory.Error -= _onErrorHandler;
                _onErrorHandler = null;
            }

            if (_onDeleteHandler != null)
            {
                FluentApiFactory.DeleteFileError -= _onDeleteHandler;
                _onDeleteHandler = null;
            }
            if (everything)
            {
                Reset();
            }
            else
            {
                FluentApiFactory.Reset();
            }
            return this;
        }

        /// <summary>
        /// Returns the result containing the dynamic assembly's file name, or the error that occured.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">You must first call the method <see cref="Execute"/>.</exception>
        public FluentApiFactoryExecutionResult Result() => _executed 
            ? _result 
            : throw new InvalidOperationException($"You must first call the method {MethodName(nameof(Execute))}.");

        /// <summary>
        /// Release all resources used by the current <see cref="FluentApiFactoryConfig"/> class.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            Release(true);
            _disposed = true;
        }

        private void ThrowScanRequired()
        {
            var m1 = MethodName(nameof(Scan), typeof(Type[]));
            var m2 = MethodName(nameof(ScanAssembly), typeof(Assembly));
            var m3 = MethodName(nameof(ScanAssemblyFrom), typeof(Type));
            var message = $"You must first scan for types using one of the methods {m1}, {m2}, or {m3}.";
            System.Diagnostics.Debug.WriteLine(message);
            throw new InvalidOperationException(message);
        }

        private static string MethodName(string name, params Type[] args)
        {
            var length = args?.Length ?? 0;
            var sb = new System.Text.StringBuilder($"{typeof(FluentApiFactoryConfig).FullName}.{name}");
            sb.Append('(');

            if (length > 0)
            {
                for (int i = 0; i < length; i++)
                {
                    sb.Append($"{args[i].Name}");
                    if (i + 1 < length)
                        sb.Append(", ");
                }
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}
