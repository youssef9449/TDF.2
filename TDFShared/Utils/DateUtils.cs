using System;

namespace TDFShared.Utils
{
    public static class DateUtils
    {
        public static int CalculateBusinessDays(DateTime start, DateTime end)
        {
            int businessDays = 0;
            for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
            }
            return businessDays;
        }
    }
}
