using DynamicFluentApis;
using PowerConsole;
using System;

namespace HumanResources.Cli
{
    internal class HumanResourcesAssemblyGenerator
    {
        public static void Build()
        {
            var console = SmartConsole.Default;
            try
            {
                // generate a dynamic assembly for the single Person class (very unlikely)
                // minimalistic approach:
                var result = FluentApiFactory.Configure(overwrite: true)
                    .Scan(typeof(Person), typeof(Employee))
                    .Build()
                    .ReleaseResources()
                    .Result();

                WriteAssemblyLocation(result);
                
                /*
                // or if you want to scan the whole HumanResources assembly
                // you have to be explicit; the ScanAssembly(Assembly) and
                // ScanAssemblyFrom(Type) methods retrieve only types marked
                // with the custom attribute FluentApiTargetAttribute
                var types = typeof(Person).Assembly.GetTypes();

                result = FluentApiFactory.Configure(overwrite: true).Scan(types).Build().ReleaseResources().Result();

                WriteAssemblyLocation(result);

                // you can even generate multiple dynamic assemblies
                // with this full-blown approach:
                using (var config = FluentApiFactory.Configure(true))
                {
                    FluentApiFactoryExecutionResult result1 = null;
                    result = config
                        .OnError(error => console.WriteLine($"A critical error occured: {error}"))
                        .OnDeleteError((error, builder, file) =>
                            console.WriteLine($"Could not delete the file '{file}'. Reason for failure: {error.Message}")
                        )
                        .WithOverwriteOptions()
                        // these 'Set...' methods modify the default prefix and suffix values
                        // public interface IPerson {...}
                        // internal sealed class PersonCloned : IPerson {...}
                        .SetProxyClassNameSuffix("Cloned")
                        .SetFluentTypeNamePrefix("Magic")
                        // public class MagicPerson { ... public virtual IPerson Target { get; } }
                        .SetWrappedObjectPropertyName("Target")
                        .WithConfig()
                        .ScanAssemblyFrom(typeof(Person))
                        .Build()
                        .Result();

                    WriteAssemblyLocation(result);
                    WriteAssemblyLocation(result1);
                }

                if (result.Succeeded)
                {
                    console.WriteLines("What's next? Grab that file and a reference to it in your project.",
                        "You'll be able to use your fluent wrapper as shown in the next demo.",
                        "The assemblies' names are similar to: HumanResources.DynamicFluentApis.abcdef.dll ",
                        "and HumanResources.Web.Mvc.DynamicFluentApis.fedcba.dll",
                        "Where 'abcdef' and 'fedcba' are (for instance) the hash codes generated ",
                        "for the dynamic fluent API assemblies.");
                }
                */
            }
            catch (Exception ex)
            {
                console.WriteError($"An unexpected error occured: {ex.Message}");
            }
            
            void WriteAssemblyLocation(FluentApiFactoryExecutionResult result)
            {
                if (true == result?.Succeeded)
                {
                    console.WriteLine($"The generated assembly is {Environment.CurrentDirectory}\\{result.AssemblyFileName}!");
                }
                else
                {
                    console.WriteError("Could not create the assembly! " + result?.Error?.Message);
                }
            }

        }
    }
}
