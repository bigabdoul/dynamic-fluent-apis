# dynamic-fluent-apis

DynamicFluentApis is a .NET project written in C# which provides functionalities that allow you to build dynamic assemblies with fluent API support, for types located in other assemblies. Simply put, never, ever write that kind of code again:

```C#
using System;

namespace Demo
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

Instead, write this:

```C#
using System;
using DynamicFluentApis;
using DynamicFluentApis.Core;

namespace Demo
{
    public class Program
    {
        public static void Main()
        {
            var result = FluentApiFactory.Configure(true).Scan(typeof(Person)).Execute().Release().Result();
            if (result.Succeeded)
            {
                Console.WriteLine($"The generated assembly is {Environment.CurrentDirectory}\\{result.FileName}!");
                Console.WriteLine("What's next? Grab that file and a reference to it in your project. You'll be able to use your fluent wrapper as shown in the previous demo.");
            }
            else
            {
                Console.WriteLine("Could not create the assembly!");
            }
        }

        [FluentApiTarget]
        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime BirthDate { get; set; }
            public double Age { get => DateTime.Today.Subtract(BirthDate).TotalDays / 365; }
        }
    }
}
```

You get the idea? Perfect!
