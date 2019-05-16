using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DynamicFluentApis.Core
{
    /// <summary>
    /// Provides common information about an assembly.
    /// </summary>
    public sealed class AssemblyInfoProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyInfoProvider"/> class.
        /// </summary>
        private AssemblyInfoProvider()
        {
        }

        /// <summary>
        /// Gets the title of an assembly.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the description of an assembly.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the configuration of an assembly.
        /// </summary>
        public string Configuration { get; private set; }

        /// <summary>
        /// Gets the product name of an assembly.
        /// </summary>
        public string Product { get; private set; }

        /// <summary>
        /// Gets the product version of an assembly.
        /// </summary>
        public string ProductVersion { get; private set; }

        /// <summary>
        /// Gets the company name of an assembly.
        /// </summary>
        public string Company { get; private set; }

        /// <summary>
        /// Gets the copyright statement of an assembly.
        /// </summary>
        public string Copyright { get; private set; }

        /// <summary>
        /// Gets the trade mark text of an assembly.
        /// </summary>
        public string Trademark { get; private set; }

        /// <summary>
        /// Gets the file version of an assembly.
        /// </summary>
        public string FileVersion { get; private set; }

        /// <summary>
        /// Gets the culture name of an assembly.
        /// </summary>
        public string Culture { get; private set; }

        /// <summary>
        /// Gest the neutral language name of an assembly's resources.
        /// </summary>
        public string NeutralResourcesLanguage { get; private set; }

        /// <summary>
        /// Gets the unique identifier of an assembly.
        /// </summary>
        public string Guid { get; private set; }

        /// <summary>
        /// Gets the COM (Component Object Model) visibility of an assembly.
        /// </summary>
        public bool ComVisible { get; private set; }

        /// <summary>
        /// Gets the assembly from which the information has been extracted.
        /// </summary>
        public Assembly Assembly { get; private set; }

        /// <summary>
        /// Returns the string representation of the current instance of the <see cref="AssemblyInfoProvider"/> class.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Product}, Version={ProductVersion ?? FileVersion ?? "0.0.0.0"}, Culture={Culture ?? "neutral"}";
        }

        /// <summary>
        /// Extracts information from the specified assembly.
        /// </summary>
        /// <param name="asm">The assembly from which to extract information.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="asm"/> is null (Nothing in Visual Basic).</exception>
        public static AssemblyInfoProvider GetAssemblyInfo(Assembly asm)
        {
            if (asm == null) throw new ArgumentNullException(nameof(asm));

            var parts = asm.FullName.Split(',');
            // fancy formatting just for better readability
            return new AssemblyInfoProvider
            {
                Assembly        = asm,
                Title           = asm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title                   ?? string.Empty,
                Description     = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description       ?? string.Empty,
                Configuration   = asm.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration   ?? string.Empty,
                Product         = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product               ?? parts[0],
                ProductVersion  = asm.GetCustomAttribute<AssemblyVersionAttribute>()?.Version               ?? parts[1].Split('=')[1],
                Company         = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company               ?? string.Empty,
                Copyright       = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright           ?? string.Empty,
                Trademark       = asm.GetCustomAttribute<AssemblyTrademarkAttribute>()?.Trademark           ?? string.Empty,
                FileVersion     = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version           ?? string.Empty,
                Culture         = asm.GetCustomAttribute<AssemblyCultureAttribute>()?.Culture               ?? string.Empty,
                Guid            = asm.GetCustomAttribute<GuidAttribute>()?.Value                            ?? string.Empty,
                ComVisible      = asm.GetCustomAttribute<ComVisibleAttribute>()?.Value                      ?? false,
            };
        }
    }
}
