using System.Globalization;
using MudBlazor;

namespace Heron.MudCalendar;

public class CalendarDateRange : DateRange
{
    public CalendarView View { get; }

    private readonly DateTime _currentDay;

    public CalendarDateRange(DateTime currentDay, CalendarView view, DayOfWeek? firstDayOfWeek = null)
        : base(GetStart(currentDay, view, firstDayOfWeek), GetEnd(currentDay, view, firstDayOfWeek))
    {
        _currentDay = currentDay;
        View = view;
    }

    private static DateTime GetStart(DateTime currentDay, CalendarView view, DayOfWeek? firstDayOfWeek)
    {
        switch (view)
        {
            case CalendarView.Day:
                return currentDay.Date;
            case CalendarView.Resource:
                return currentDay.Date;
            case CalendarView.Week:
            case CalendarView.WorkWeek:
                return GetFirstWeekDate(currentDay, firstDayOfWeek);
            case CalendarView.Month:
            default:
                return GetFirstMonthDate(currentDay);
        }
    }

    private static DateTime GetEnd(DateTime currentDay, CalendarView view, DayOfWeek? firstDayOfWeek)
    {
        switch (view)
        {
            case CalendarView.Day:
                return currentDay.Date;
            case CalendarView.Resource:
                return currentDay.Date;
            case CalendarView.Week:
                return GetLastWeekDate(currentDay, firstDayOfWeek);
            case CalendarView.WorkWeek:
                return GetLastWorkWeekDate(currentDay, firstDayOfWeek);
            case CalendarView.Month:
            default:
                return GetLastMonthDate(currentDay);
        }
    }
    
    public static DateTime GetFirstMonthDate(DateTime day)
    {
        // Get first day or the week for the first day of the month
        var date = new DateTime(day.Year, day.Month, 1);
        date = date.AddDays(GetDayOfWeek(date) * -1);
        return date;
    }

    public static DateTime GetLastMonthDate(DateTime day)
    {
        // Get the last day of the week for the last day of the month
        var date = day.AddMonths(1);
        date = new DateTime(date.Year, date.Month, 1).AddDays(-1);
        date = date.AddDays(6 - GetDayOfWeek(date));
        return date;
    }

    public static DateTime GetFirstWeekDate(DateTime day, DayOfWeek? firstDayOfWeek)
    {
        // Get first day of the week
        return day.AddDays(GetDayOfWeek(day, firstDayOfWeek) * -1);
    }

    public static DateTime GetLastWeekDate(DateTime day, DayOfWeek? firstDayOfWeek)
    {
        // Get last day of the week
        return day.AddDays(6 - GetDayOfWeek(day, firstDayOfWeek));
    }

    public static DateTime GetLastWorkWeekDate(DateTime day, DayOfWeek? firstDayOfWeek)
    {
        // Get last day of the work week
        return day.AddDays(4 - GetDayOfWeek(day, firstDayOfWeek));
    }

    public static int GetDayOfWeek(DateTime date, DayOfWeek? firstDayOfWeek = null)
    {
        // Get day as integer - first day of week = 0 .. last day = 6
        var firstDay = firstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var day = (int)date.DayOfWeek;
        day -= (int)firstDay;
        if (day < 0)
        {
            day = 7 + day;
        }

        return day;
    }
}