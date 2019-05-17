# dynamic-fluent-apis

DynamicFluentApis is a .NET (Framework 4.6.1) project written in C# which provides functionalities that allow you to build dynamic assemblies with fluent API support, for types located in other assemblies. Simply put, never, ever write that kind of code again:

```C#
using System;

namespace Demo1.LegacyWayOfProvidingFluentApiSupport
{
    public class Program
    {
        public static void Main()
        {
            var p = new PersonWrapper().FirstName("Abdoul").LastName("Kaba").BirthDate(new DateTime(1990,7,29)).Object;
            Console.WriteLine($"Hello, {p.FirstName}! You say you are {p.Age} and your last name is {p.LastName}, right?");
        }

        public interface IPerson
        {
            string FirstName { get; set; }
            string LastName { get; set; }
            DateTime BirthDate { get; set; }
            double Age { get; }
        }

        public class Person : IPerson
        {
            public virtual string FirstName { get; set; }
            public virtual string LastName { get; set; }
            public virtual DateTime BirthDate { get; set; }
            public double Age { get => DateTime.Today.Subtract(BirthDate).TotalDays / 365; }
        }

        public class PersonWrapper
        {
            IPerson _person;

            public PersonWrapper()
            {
                _person = new Person();
            }

            public PersonWrapper(IPerson person)
            {
                _person = person;
            }

            public string FirstName() => _person.FirstName;

            public PersonWrapper FirstName(string value)
            {
                _person.FirstName = value;
                return this;
            }

            public string LastName() => _person.LastName;

            public PersonWrapper LastName(string value)
            {
                _person.LastName = value;
                return this;
            }

            public DateTime BirthDate() => _person.BirthDate;

            public PersonWrapper BirthDate(DateTime value)
            {
                _person.BirthDate = value;
                return this;
            }

            public IPerson Object { get => _person; }
        }
    }
}
```

What's the problem with the code above? Well, technically not much! But, for every single type you want to be fluent API capable, you need to manually write a wrapper class like `PersonWrapper` just to be able to chain method calls. This can be tedious when you have an assembly with dozens or even hundreds of interesting types you want to support.

That's where `DynamicFluentApis` steps in and generates all that boiler-plate code for you. So, instead of writing code like the one above, you just let `DynamicFluentApis` generate another dynamic assembly for you that supports fluent API on whatever types you want. You then just grab that assembly and add it as a reference to the latest hot project you're working on.

## Steps to generate a dynamic assembly with fluent API support

First things first.

1. From your favourite command line or terminal, change the working directory to one of your liking and then pull this repo with git (or download a zipped version): `> git clone https://github.com/bigabdoul/dynamic-fluent-apis.git`
2. Reference both projects, namely `DynamicFluentApis` and `DynamicFluentApis.Core`, within a new console or a unit test project (using .NET Framework 4.6.1).
3. Add a reference to the console project from the library/libraries or project(s) you want to provide fluent API support with. Let's call these referenced assemblies `HumanResources` and `HumanResources.Web.Mvc` for instance.
4. Pretending that `HumanResources` contains the `Person` class, and `HumanResources.Web.Mvc` contains the `BootstrapModalModel` class, write the following code to get started:

```C#
using System;
using static System.Console;
using DynamicFluentApis;
using DynamicFluentApis.Core;
using HumanResources;
using HumanResources.Web.Mvc;

namespace Demo2.GeneratingDynamicAssemblies
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                // generate a dynamic assembly for the single Person class (very unlikely)
                // minimalistic approach:
                var result = FluentApiFactory.Configure().Scan(typeof(Person)).Execute().Release().Result();

                WriteAssemblyLocation(result);

                // or if you want to scan the whole HumanResources assembly
                // you have to be explicit; the ScanAssembly(Assembly) and
                // ScanAssemblyFrom(Type) methods retrieve only types marked
                // with the custom attribute FluentApiTargetAttribute
                var types = typeof(Person).Assembly.GetTypes();

                result = FluentApiFactory.Configure(overwrite: true).Scan(types).Execute.Release().Result();

                WriteAssemblyLocation(result);

                // you can even generate multiple dynamic assemblies
                // with this full-blown approach:
                using (var config = FluentApiFactory.Configure(true))
                {
                    FluentApiFactoryExecutionResult result1 = null;
                    result = config
                        .OnError(error => WriteLine($"A critical error occured: {error}"))
                        .OnDeleteError((error, builder, file) => WriteLine($"Could not delete the file '{file}'. Reason for failure: {error.Message}"))
                        .WithOverwriteOptions()
                        // these methods modify the default prefix and suffix values
                        .SetProxyClassNameSuffix("Cloned")      // internal sealed class PersonCloned : IPerson {...} (public interface IPerson {...} is dynamically created)
                        .SetFluentTypeNamePrefix("Magic")       // public class MagicPerson {...}
                        .SetWrappedObjectPropertyName("Target") // public class MagicPerson { ... public virtual IPerson Target { get; } }
                        .WithConfig()
                        .ScanAssemblyFrom(typeof(Person))
                        .Execute()
                        .SetResult(r => result1 = r)
                        .Reset()
                        .WithDefaultOptions(overwrite: true)            // default options use 'Proxy' suffix, and 'Fluent' prefix
                        .ScanAssemblyFrom(typeof(BootstrapModalModel))  // public class FluentBootstrapModalModel {...}
                        .Execute()
                        .Result();

                    WriteAssemblyLocation(result);
                    WriteAssemblyLocation(result1);
                }

                if (result.Succeeded)
                {
                    WriteLine("What's next? Grab that file and a reference to it in your project.");
                    WriteLine("You'll be able to use your fluent wrapper as shown in the next demo.");
                    WriteLine("The assembly's name is similar to: Demo2.DynamicFluentApis.abcdef.dll");
                    WriteLine("Where 'abcdef' is the hash code generated for the assembly.");
                }
            }
            catch(Exception ex)
            {
                WriteLine($"An unexpected error occured: {ex.Message}");
            }

            void WriteAssemblyLocation(FluentApiFactoryExecutionResult result)
            {
                if (true == result?.Succeeded)
                {
                    WriteLine($"The generated assembly is {Environment.CurrentDirectory}\\{result.FileName}!");
                }
                else
                {
                    WriteLine("Could not create the assembly!");
                }
            }
        }
    }
}
```

## What's going on

You get the idea? Perfect! Now, before we proceed to see how we can use the assemblies generated in the previous demo, let's examine what's going on here.

## Using the dynamic assemblies

```C#
using System;
using DynamicFluentApis.Core;
using HumanResources.DynamicFluentApis;
using HumanResources.Web.Mvc.DynamicFluentApis;
using static System.Console;

namespace Demo3.ShowcasingFluentApiSupport
{
    public class Program
    {
        public static void Main()
        {
            // fluent API support demo coming soon...
        }
    }
}
```
