using System;

namespace GoldenCornOrder.Models
{
    public static class TaiwanTimeHelper
    {
        // Taiwan Standard Time (TST) is UTC+8
        public static DateTime Now => DateTime.UtcNow.AddHours(8);
        public static DateTime Today => DateTime.UtcNow.AddHours(8).Date;
        public static string FormattedNow => Now.ToString("yyyy/MM/dd HH:mm:ss");

        // Live Open Hours: Thursday, Friday, Saturday from 16:30 to 22:30
        public static bool IsLiveBusinessHours
        {
            get
            {
                var now = Now;
                var day = now.DayOfWeek;
                if (day == DayOfWeek.Thursday || day == DayOfWeek.Friday || day == DayOfWeek.Saturday)
                {
                    var time = now.TimeOfDay;
                    return time >= new TimeSpan(16, 30, 0) && time < new TimeSpan(22, 30, 0);
                }
                return false;
            }
        }
    }
}
