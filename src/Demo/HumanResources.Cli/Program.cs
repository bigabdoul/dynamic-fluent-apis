using System;

namespace HumanResources.Cli
{
    class Program
    {
        static void Main()
        {
            var p = new PersonWrapper()
                .FirstName("Abdoul")
                .LastName("Kaba")
                .BirthDate(new DateTime(1990, 7, 29))
                .Object;
            Console.Write($"Hello, {p.FirstName}! You say you are {p.Age} ");
            Console.WriteLine("and your last name is {p.LastName}, right?");
        }
    }
}
