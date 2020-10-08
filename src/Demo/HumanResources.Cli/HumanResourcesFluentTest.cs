using static HumanResources.Cli.Program;

namespace HumanResources.Cli
{
    class HumanResourcesFluentTest
    {
        public static void Test()
        {
            var person = new FluentPerson()
                .FirstName(PromptFirstName())
                .LastName(PromptLastName())
                .BirthDate(PromptBirthDate())
                .Object;

            var employee = new FluentEmployee()
                .FirstName(PromptFirstName())
                .LastName(PromptLastName())
                .BirthDate(PromptBirthDate())
                .Object;
        }
    }
}
