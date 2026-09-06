using System;

namespace GoldenCornOrder.Models
{
    public static class TaiwanTimeHelper
    {
        // Taiwan Standard Time (TST) is UTC+8
        public static DateTime Now => DateTime.UtcNow.AddHours(8);
        public static DateTime Today => DateTime.UtcNow.AddHours(8).Date;
        public static string FormattedNow => Now.ToString("yyyy/MM/dd HH:mm:ss");
    }
}
