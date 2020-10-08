using System;

namespace HumanResources
{
    /// <summary>
    /// Represents a person.
    /// </summary>
    public class Person
    {
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual DateTime BirthDate { get; set; }
        public int Age { get => BirthDate.GetFullYear(); }
    }
}
