using System.ComponentModel;

namespace Heron.MudCalendar.Extensions;

internal static class EnumExtensions
{
    internal static string ToDescriptionString(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field is null)
        {
            return value.ToString().ToLower();
        }

        return Attribute.GetCustomAttributes(field, typeof(DescriptionAttribute), false) is DescriptionAttribute[] { Length: > 0 } attributes
            ? attributes[0].Description
            : value.ToString().ToLower();
    }
    public static DateTime SetTime(this DateTime s, TimeSpan time) => new DateTime(s.Year, s.Month, s.Day, time.Hours, time.Minutes, time.Seconds);
    public static DateTime SetTime(this DateTime s, int hours, int minutes, int seconds = 0) => new DateTime(s.Year, s.Month, s.Day, hours, minutes, seconds);
}