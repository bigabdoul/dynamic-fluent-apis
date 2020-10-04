using System;

namespace HumanResources
{
    public interface IPerson
    {
        string FirstName { get; set; }
        string LastName { get; set; }
        DateTime BirthDate { get; set; }
        double Age { get; }
    }
}
