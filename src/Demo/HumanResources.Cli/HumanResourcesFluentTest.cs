using PowerConsole;
using static HumanResources.Cli.Program;

namespace HumanResources.Cli
{
    class HumanResourcesFluentTest
    {
        public static void Test()
        {
            var e = new MagicEmployee()
                .FirstName(PromptFirstName())
                .LastName(PromptLastName())
                .BirthDate(PromptBirthDate())
                .Target;

            SmartConsole.Default.WriteLine($"{e.FirstName} {e.LastName}\nDate of birth: {e.BirthDate:dd/MM/yyyy}");
        }
    }
}
