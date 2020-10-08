using System;

namespace HumanResources
{
    /// <summary>
    /// Provides extension methods for primitive types.
    /// </summary>
    public static class PrimitiveExtensions
    {
        /// <summary>
        /// Calculates the full year difference between <see cref="DateTime.Today"/>
        /// and the specified <paramref name="date"/>.
        /// </summary>
        /// <param name="date">The reference date.</param>
        /// <returns>
        /// An integer that represents the difference between 
        /// <see cref="DateTime.Year"/> and <paramref name="date"/>.
        /// </returns>
        public static int GetFullYear(this DateTime date)
        {
            var today = DateTime.Today;
            var thisYear = today.Year;
            var dateYear = date.Year;

            if (dateYear == thisYear) return 0;

            var futureDate = dateYear > thisYear;

            if (futureDate)
                // swap values
                (thisYear, dateYear) = (dateYear, thisYear);
            
            var diff = thisYear - dateYear;

            // check if the specified month and day have not been reached
            if (!futureDate)
            {
                if ((today.Month < date.Month) || (today.Month == date.Month) && (today.Day < date.Day))
                    diff--;
            }
            else if ((today.Month > date.Month) || (today.Month == date.Month) && (today.Day > date.Day))
                diff--;
            
            return futureDate ? -diff : diff;
        }
    }
}
