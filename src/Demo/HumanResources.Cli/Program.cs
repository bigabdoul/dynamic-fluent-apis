using System;
using PowerConsole;

namespace HumanResources.Cli
{
    class Program
    {
        private static SmartConsole MyConsole = SmartConsole.Default;

        static void Main()
        {
            var p = new PersonWrapper()
                .FirstName(PromptFirstName())
                .LastName(PromptLastName())
                .BirthDate(PromptBirthDate())
                .Object;

            MyConsole
                .WriteInfo($"Hello, {p.FirstName}! You say you are {p.Age:N0} " +
                $"and your last name is {p.LastName}, right?\n\n");

            if (!MyConsole.PromptNo("Generate HumanResources Assembly? (y/N) "))
            {
                HumanResourcesAssemblyGenerator.Build();
            }
        }

        internal static string PromptFirstName() => MyConsole.GetResponse("First name: ", "Firt name is required: ");
        internal static string PromptLastName() => MyConsole.GetResponse("Last name: ", "Last name is required: ");
        internal static DateTime PromptBirthDate() => MyConsole.GetResponse<DateTime>("Date of birth: ", "Your age cannot be less than 5: ", ValidateDate);
        private static bool ValidateDate(DateTime input) => input.GetFullYear() >= 5;
    }
}
