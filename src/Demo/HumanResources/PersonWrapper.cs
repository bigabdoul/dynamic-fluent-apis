using System;

namespace HumanResources
{
    public class PersonWrapper
    {
        private readonly Person _person;

        public PersonWrapper()
        {
            _person = new Person();
        }

        public PersonWrapper(Person person)
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

        public Person Object { get => _person; }
    }
}
