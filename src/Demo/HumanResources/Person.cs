using System;

namespace HumanResources
{
    public class Person : IPerson
    {
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual DateTime BirthDate { get; set; }
        public double Age { get => DateTime.Today.Subtract(BirthDate).TotalDays / 365; }
    }
}
