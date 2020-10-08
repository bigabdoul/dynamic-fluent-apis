using DynamicFluentApis;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FluentAssemblyGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // parse this command line:
            /* fluent HumanResources.dll --types Person,Employee -f HumanResources.Fluent.dll 
             * --overwrite --prefix Magic --suffix Cloned --property-name Target
             */

            var app = new CommandLineApplication
            {
                Name = "fluent",
                Description = "Fluent API Assembly Generator"
            };

            app.HelpOption("-?|-h|--help");

            var asmArg = app.Argument("assembly", "The fully-qualified path to the source assembly to scan.");
            var scanTypesOption = app.Option("-t|--types <types>", "The types for which to generate fluent APIs.", CommandOptionType.SingleValue);
            var targetAsmFileOption = app.Option("-f|--file <file>", "The file name of the target output assembly to generate.", CommandOptionType.SingleValue);
            var overwriteOption = app.Option("-d|--overwrite", "Instructs to delete any previously-built existing assembly.", CommandOptionType.NoValue);
            var prefixOption = app.Option("-b|--prefix <prefix>", "The string to prepend to an output type.", CommandOptionType.SingleValue);
            var suffixOption = app.Option("-e|--suffix <suffix>", "The string to append to an output type.", CommandOptionType.SingleValue);
            var propertyNameOption = app.Option("-p|--property-name <property>", "The name of the property that returns a reference to the underlying type.", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                var console = PowerConsole.SmartConsole.Default;

                if (string.IsNullOrWhiteSpace(asmArg.Value))
                {
                    console.WriteError("You must specify the fully-qualified path to the assembly to scan.");
                    return 1;
                }

                try
                {
                    var asm = Assembly.LoadFile(asmArg.Value);
                    var scanTypes = scanTypesOption.HasValue() ? scanTypesOption.Value().Split(',') : null;
                    var typeList = new List<Type>();

                    if (scanTypes?.Length > 0)
                    {
                        typeList.AddRange(asm.GetTypes().Where(t => scanTypes.Contains(t.Name)));
                    }

                    using (var config = FluentApiFactory.Configure(overwriteOption.HasValue()))
                    {
                        config
                            .OnError(error => Console.WriteLine($"A critical error occured: {error}"))
                            .OnDeleteError((error, builder, file) =>
                                Console.WriteLine($"Could not delete the file '{file}'. Reason for failure: {error.Message}"))
                            .WithOptions()
                            .SetFluentTypeNamePrefix(prefixOption.HasValue() ? prefixOption.Value() : "Fluent")
                            .SetProxyClassNameSuffix(suffixOption.HasValue() ? suffixOption.Value() : "Proxy")
                            .SetWrappedObjectPropertyName(propertyNameOption.HasValue() ? propertyNameOption.Value() : "Object")
                            .WithConfig();

                        if (typeList.Count > 0)
                        {
                            config.Scan(typeList.ToArray());
                        }
                        else
                        {
                            config.ScanAssembly(asm, false);
                        }

                        var fileName = targetAsmFileOption.Value();
                        var dirName = string.IsNullOrWhiteSpace(fileName) ? Environment.CurrentDirectory : Path.GetDirectoryName(fileName);

                        // build the assembly and get the result
                        var result = config.Build(fileName).Result();

                        if (true == result?.Succeeded)
                        {
                            console.WriteLine($"The generated assembly is {dirName}{Path.DirectorySeparatorChar}{result.AssemblyFileName}!");
                        }
                        else
                        {
                            console.WriteError("Could not create the assembly! " + result?.Error?.Message);
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex);
                }

                return 2;
            });

            app.Execute(args);
        }
    }
}
