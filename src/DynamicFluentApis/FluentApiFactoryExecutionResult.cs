using System;

namespace DynamicFluentApis
{
    /// <summary>
    /// Represents the result of the execution of a method defined in the <see cref="FluentApiFactory"/> class.
    /// </summary>
    public sealed class FluentApiFactoryExecutionResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FluentApiFactoryExecutionResult"/> class.
        /// </summary>
        private FluentApiFactoryExecutionResult()
        {
        }

        /// <summary>
        /// Determines whether the execution was successful.
        /// </summary>
        public bool Succeeded { get; private set; }

        /// <summary>
        /// Gets the assembly output file name in case of successful execution in the factory.
        /// </summary>
        public string AssemblyFileName { get; private set; }

        /// <summary>
        /// Gets the error that occured during the execution in the factory.
        /// </summary>
        public Exception Error { get; private set; }

        internal static FluentApiFactoryExecutionResult Create(Exception error, string fileName)
            => new FluentApiFactoryExecutionResult { Error = error, Succeeded = error == null, AssemblyFileName = fileName };
    }
}
