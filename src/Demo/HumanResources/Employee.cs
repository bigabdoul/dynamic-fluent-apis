namespace HumanResources
{
    /// <summary>
    /// Represents an employee.
    /// </summary>
    public class Employee : Person
    {
        public int EmployeeId { get; set; }
        public string JobTitle { get; set; }
        public string Department { get; set; }
        public decimal Salary { get; set; }
        public string SocialSecurityNumber { get; set; }
    }
}
