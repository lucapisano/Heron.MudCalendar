using System.Globalization;
using Heron.MudCalendar.Extensions;
using Heron.MudCalendar.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Utilities;
using EnumExtensions = Heron.MudCalendar.Extensions.EnumExtensions;

namespace Heron.MudCalendar;

public partial class ResourceView : ComponentBase, IDisposable
{
    [CascadingParameter]
    public MudCalendar Calendar { get; set; } = new();
    [Inject] public ILogger<ResourceView> _logger { get; set; }

    private ElementReference _scrollDiv;
    private JsService? _jsService;
    private DotNetObjectReference<ResourceView>? _dotNetRef;
    Dictionary<ResourceItem, CalendarCell> Columns = new Dictionary<ResourceItem, CalendarCell>();
    private int MinutesInDay => GetMinutesInDay(); //= 24 * 60;
    private int PixelsInCell => Calendar.DayCellHeight;

    private int CellsInDay => MinutesInDay / (int)Calendar.DayTimeInterval;
    private int PixelsInDay => CellsInDay * PixelsInCell;

    protected virtual int DaysInView => 1;
    private int GetMinutesInDay()
    {
        if (Calendar.DayStartTime == Calendar.DayEndTime)
            return 24 * 60;
        return (int)Math.Ceiling((Calendar.DayEndTime - Calendar.DayStartTime).TotalMinutes);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (Columns == null || !Columns.Any())
            return;
        if (firstRender || scrollRequired)
        {
            await ScrollToCurrentTime();
            scrollRequired = false;
            
            // Register for pointer-based drop events (WebView2 fix)
            await RegisterPointerDropHandler();
        }
    }

    /// <summary>
    /// Registers a JavaScript event listener to handle pointer-based drag-drop events.
    /// This is needed for WebView2 where native HTML5 drag-drop doesn't work with mouse.
    /// </summary>
    private async Task RegisterPointerDropHandler()
    {
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // First, define the helper function to store the reference
            await JsRuntime.InvokeVoidAsync("eval", @"
                window.__setResourceViewRef = function(ref) { 
                    window.__resourceViewDotNetRef = ref;
                    console.log('ResourceView DotNetRef set:', !!ref);
                };
            ");
            
            // Then pass the DotNetObjectReference to JavaScript
            await JsRuntime.InvokeVoidAsync("__setResourceViewRef", _dotNetRef);
            
            _logger?.LogInformation("Pointer drop handler registered successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register pointer drop handler - this is normal for non-WebView2 environments");
        }
    }

    /// <summary>
    /// Called from JavaScript when a pointer-based drop occurs (WebView2 fix).
    /// </summary>
    [JSInvokable]
    public async Task OnPointerDropAsync(string itemId, string targetZoneId)
    {
        _logger?.LogInformation("OnPointerDropAsync: {ItemId} -> {TargetZoneId}", itemId, targetZoneId);
        
        var item = Calendar.Items.FirstOrDefault(x => x.Id == itemId);
        if (item == null)
        {
            _logger?.LogWarning("Item not found: {ItemId}", itemId);
            return;
        }

        // Create a MudItemDropInfo and call the existing ItemDropped handler
        var dropInfo = new MudItemDropInfo<CalendarItem>(item, targetZoneId, 0);
        await ItemDropped(dropInfo);
    }
    async Task ScrollToCurrentTime()
    {
        var time = DateTime.Now.TimeOfDay;
        if (DateTime.Now.Date != Calendar.CurrentDay.Date)
            time = Calendar.DayStartTime.ToTimeSpan();
        await ScrollToTime(time);
    }
    DateOnly _lastDay;
    bool scrollRequired = false;
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        BuildCols();
        if (_lastDay != DateOnly.FromDateTime(Calendar.CurrentDay.Date))
        {
            scrollRequired = true;
        }
        _lastDay = DateOnly.FromDateTime(Calendar.CurrentDay.Date);
    }
    /*
    protected override void OnParametersSet()
    {
        BuildCols();
    }*/
    void BuildCols()
    {
        Columns.Clear();
        if (Calendar.Resources == null)
            return;
        foreach (var resource in Calendar.Resources)
        {
            var cell = new CalendarCell { Date = Calendar.CurrentDay.Date };
            if (Calendar.CurrentDay.Date == DateTime.Today) cell.Today = true;

            var q = Calendar.Items.Where(i => i.ResourceId == resource.Id);
            q = q.Where(i =>
                (i.Start.Date == Calendar.CurrentDay) || //events for today
                (i.Start.Date <= Calendar.CurrentDay && i.End.HasValue && i.End.Value > Calendar.CurrentDay) //events that started before today and end today or after
              );
            var l = q.OrderBy(i => i.Start).ToList();
            //remove items that are not in the current day time range or that are not drawable due to hours out of range

            //remove all events that start before the day start time and ends before the day start time
            l.RemoveAll(x => x.Start.TimeOfDay < Calendar.DayStartTime.ToTimeSpan()
                || x.End.HasValue && x.End.Value.TimeOfDay <= Calendar.DayStartTime.ToTimeSpan());

            //l = l.Where(i => i.Start.TimeOfDay >= Calendar.DayStartTime.ToTimeSpan() && i.Start.TimeOfDay < Calendar.DayEndTime.ToTimeSpan()).ToList();

            //overwrite start time for all events that start before the day start time but end after the day start time
            foreach (var item in l.Where(x => x.Start.TimeOfDay < Calendar.DayStartTime.ToTimeSpan()
                && x.End.HasValue && x.End.Value.TimeOfDay > Calendar.DayStartTime.ToTimeSpan()))
            {
                item.Start = item.Start.SetTime(Calendar.DayStartTime.ToTimeSpan());
            }

            //overwrite end time for all events that end after the day end time
            foreach (var item in l.Where(x => x.End.HasValue && x.End.Value.TimeOfDay > Calendar.DayEndTime.ToTimeSpan()))
            {
                item.End = item.End.Value.SetTime(Calendar.DayEndTime.ToTimeSpan());
            }

            l.RemoveAll(x => x.End.HasValue && x.Start == x.End.Value);
            cell.Items = l;
            Columns.Add(resource, cell);
        }
    }
    /// <summary>
    /// Styles the header grid
    /// </summary>
    protected virtual string HeaderClass =>
        new CssBuilder("mud-cal-grid")
            .AddClass("mud-cal-grid-header")
            .AddClass("mud-cal-week-header", DaysInView == 7)
            .AddClass("mud-cal-work-week-header", DaysInView == 5)
            .AddClass("mud-cal-day-header", DaysInView == 1)
            .Build();

    /// <summary>
    /// Styles the main grid
    /// </summary>
    protected virtual string GridClass =>
        new CssBuilder("mud-cal-grid")
            .AddClass("mud-cal-week-grid", DaysInView == 7)
            .AddClass("mud-cal-work-week-grid", DaysInView == 5)
            .AddClass("mud-cal-day-grid", DaysInView == 1)
            .Build();

    /// <summary>
    /// Styles added to each day.
    /// </summary>
    /// <param name="calendarCell">The cell.</param>
    /// <param name="row">The current row in the table being rendered.</param>
    /// <returns></returns>
    protected virtual string DayStyle(CalendarCell calendarCell, int row)
    {
        return new StyleBuilder()
            .AddStyle("border-left",
                $"1px solid var(--mud-palette-{EnumExtensions.ToDescriptionString(Calendar.Color)})",
                calendarCell.Today && Calendar.HighlightToday)
            .AddStyle("border-right",
                $"1px solid var(--mud-palette-{EnumExtensions.ToDescriptionString(Calendar.Color)})",
                calendarCell.Today && Calendar.HighlightToday)
            .AddStyle("border-top",
                $"1px solid var(--mud-palette-{EnumExtensions.ToDescriptionString(Calendar.Color)})",
                row == 0 && calendarCell.Today && Calendar.HighlightToday)
            .AddStyle("border-bottom",
                $"1px solid var(--mud-palette-{EnumExtensions.ToDescriptionString(Calendar.Color)})",
                row + 1 == CellsInDay && calendarCell.Today && Calendar.HighlightToday)
            .Build();
    }

    /// <summary>
    /// Styles the position and height of the div containing an item.
    /// </summary>
    /// <param name="position">Position information for the div.</param>
    /// <returns></returns>
    protected virtual string EventStyle(ItemPosition position)
    {
        return new StyleBuilder()
            .AddStyle("position", "absolute")
            .AddStyle("top", $"{position.Top}px")
            .AddStyle("height", $"{position.Height}px")
            .AddStyle("left",
                (((position.Position / (double)position.Total) - (1.0 / position.Total)) * 100).ToInvariantString() +
                "%")
            .AddStyle("width", (100d / position.Total).ToInvariantString() + "%")
            .Build();
    }

    /// <summary>
    /// Styles for the cell where the time is displayed..
    /// </summary>
    /// <param name="row">The row being styled.</param>
    /// <returns></returns>
    protected virtual string TimeCellClassname(int row)
    {
        return new CssBuilder()
            .AddClass("mud-cal-week-cell", IsHourCell(row))
            .AddClass("mud-cal-time-cell", IsHourCell(row))
            .AddClass("mud-cal-week-not-today")
            .Build();
    }

    /// <summary>
    /// Styles for each cell in the view.
    /// </summary>
    /// <param name="cell">The cell being styled.</param>
    /// <param name="row">The row being styled.</param>
    /// <returns></returns>
    protected virtual string DayCellClassname(CalendarCell cell, int row)
    {
        return new CssBuilder()
            .AddClass("mud-cal-week-cell")
            .AddClass("mud-cal-week-cell-half", !IsHourCell(row))
            .AddClass("mud-cal-week-not-today", !cell.Today || !Calendar.HighlightToday)
            .Build();
    }

    /// <summary>
    /// Styles that set the height of each cell.
    /// </summary>
    /// <returns></returns>
    protected virtual string CellHeightStyle()
    {
        return new StyleBuilder()
            .AddStyle("height", $"{Calendar.DayCellHeight}px")
            .Build();
    }

    /// <summary>
    /// The style of the line showing the current time.
    /// </summary>
    /// <returns></returns>
    protected virtual string TimelineStyle()
    {
        return new StyleBuilder()
            .AddStyle("position", "absolute")
            .AddStyle("width", "100%")
            .AddStyle("border", "1px solid var(--mud-palette-gray-default)")
            .AddStyle("top", $"{TimelinePosition().ToInvariantString()}px")
            .Build();
    }

    /// <summary>
    /// Method invoked when the user clicks on the hyper link in the cell.
    /// </summary>
    /// <param name="cell">The cell that was clicked.</param>
    /// <param name="row">The row that was clicked.</param>
    /// <param name="resource">The resource the cell belongs to.</param>
    /// <returns></returns>
    protected virtual async Task OnCellLinkClicked(CalendarCell cell, int row, ResourceItem? resource = default, MouseEventArgs? mouseEventArgs = default)
    {
        var time = GetCellTime(row);
        var date = cell.Date.Add(time.ToTimeSpan());
        //var date = cell.Date.AddMinutes(row * (int)Calendar.DayTimeInterval);
        if (Calendar.CellClicked.HasDelegate)
            await Calendar.CellClicked.InvokeAsync(new CellClickedArgs { MouseEventArgs = mouseEventArgs, Date = date, ResourceId = resource?.Id });
    }

    /// <summary>
    /// Method invoked when the user clicks on the calendar item.
    /// </summary>
    /// <param name="item">The calendar item that was clicked.</param>
    /// <returns></returns>
    protected virtual Task OnItemClicked(CalendarItem item)
    {
        return Calendar.ItemClicked.InvokeAsync(item);
    }

    protected TimeOnly GetCellTime(int row)
    {
        var hour = row / (60.0 / (double)Calendar.DayTimeInterval);
        var timeSpan = TimeSpan.FromHours(hour);
        return Calendar.DayStartTime.Add(timeSpan);
    }
    /// <summary>
    /// Creates a string with the time to be displayed.
    /// </summary>
    /// <param name="row">The current row in the table.</param>
    /// <returns></returns>
    protected virtual string DrawTime(int row)
    {
        var time = GetCellTime(row);
        return Calendar.Use24HourClock ? time.ToString("HH:mm") : time.ToString("h tt");
    }

    /// <summary>
    /// Calculates the row of the timeline for the current time.
    /// </summary>
    /// <returns>The row of the timeline.</returns>
    protected int TimelineRow()
    {
        var minutes = DateTime.Now.Subtract(DateTime.Today).TotalMinutes;
        var row = (int)Math.Floor(minutes / (int)Calendar.DayTimeInterval);

        return row;
    }

    /// <summary>
    /// Adjusts the end time when resizing an item (changing its height).
    /// </summary>
    protected async Task ItemHeightChanged(CalendarItem item, int intervals)
    {
        var minutes = intervals * (int)Calendar.DayTimeInterval;
        var proposedEnd = item.Start.AddMinutes(minutes);

        var oldEnd = item.End;
        item.End = proposedEnd;

        if (Calendar.ItemChanging != null)
        {
            var allowed = false;
            try
            {
                allowed = await Calendar.ItemChanging(item);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ItemChanging (resize). Cancelling change.");
            }

            if (!allowed)
            {
                // revert and abort
                item.End = oldEnd;
                /*
                await InvokeAsync(async () => {
                    BuildCols()
                });
                */
                return;
            }
        }

        await Calendar.ItemChanged.InvokeAsync(item);
    }

    private double TimelinePosition()
    {
        var minutes = DateTime.Now.Subtract(DateTime.Today).TotalMinutes -
                      (TimelineRow() * (int)Calendar.DayTimeInterval);
        var position = (minutes / (int)Calendar.DayTimeInterval) * Calendar.DayCellHeight;

        return position;
    }
    double GetStartMinutes(ItemPosition position)
        => (position.Item.Start.Hour * 60 + position.Item.Start.Minute)
                - Calendar.DayStartTime.ToTimeSpan().TotalMinutes;
    double GetEndMinutes(ItemPosition position)
    {
        var end = (position.Item.End.GetValueOrDefault().Hour * 60 + position.Item.End.GetValueOrDefault().Minute)
                - Calendar.DayStartTime.ToTimeSpan().TotalMinutes;
        if (end < 0) end = 0;
        if (end > MinutesInDay) end = MinutesInDay;
        return end;
    }
    private int CalcTop(ItemPosition position)
    {
        double minutes = 0;
        if (DateOnly.FromDateTime(position.Item.Start.Date) == position.Date)
        {
            minutes = GetStartMinutes(position);
        }

        var percent = minutes / MinutesInDay;
        var top = PixelsInDay * percent;

        return (int)Math.Round(top);
    }

    private int CalcHeight(ItemPosition position)
    {
        double start = 0;
        if (DateOnly.FromDateTime(position.Item.Start.Date) == position.Date)
        {
            start = GetStartMinutes(position);
        }

        var end = start + 60;
        if (position.Item.End.HasValue)
        {
            end = MinutesInDay;
            if (DateOnly.FromDateTime(position.Item.End.Value.Date) == position.Date)
            {
                end = GetEndMinutes(position);
            }
        }

        if (end > MinutesInDay) end = MinutesInDay;
        var minutes = end - start;
        var percent = minutes / MinutesInDay;
        var height = PixelsInDay * percent;

        if (height < Calendar.DayItemMinHeight)
        {
            height = Calendar.DayItemMinHeight;
        }

        return (int)Math.Round(height);
    }

    private async Task ScrollToTime(TimeSpan? time = default)
    {
        if (_scrollDiv.Id == default)
            return;
        try
        {
            time = time ?? new TimeSpan(Calendar.DayStartTime.Hour, Calendar.DayStartTime.Minute, 0);
            var startMinutes = (time.Value.Hours * 60) + time.Value.Minutes - Calendar.DayStartTime.ToTimeSpan().TotalMinutes;
            var percent = (double)startMinutes / MinutesInDay;
            var scrollTo = PixelsInDay * percent;

            _jsService ??= new JsService(JsRuntime);
            await _jsService.Scroll(_scrollDiv, (int)scrollTo);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, $"unable to {nameof(ScrollToTime)}");
        }
    }
    /*
    private async Task ScrollToDay()
    {
        var startMinutes = (Calendar.DayStartTime.Hour * 60) + Calendar.DayStartTime.Minute;
        var percent = (double)startMinutes / MinutesInDay;
        var scrollTo = PixelsInDay * percent;

        _jsService ??= new JsService(JsRuntime);
        await _jsService.Scroll(_scrollDiv, (int)scrollTo);
    }
    */
    protected virtual RenderFragment<CalendarItem> CellTemplate => Calendar.CellTemplate;

    private IEnumerable<ItemPosition> CalcPositions(IEnumerable<CalendarItem> items, DateOnly date)
    {
        var positions = new List<ItemPosition>();
        var overlaps = new List<ItemPosition>();
        foreach (var item in items)
        {
            // Check that the end date is valid
            if (item.End.HasValue && item.End <= item.Start)
            {
                _logger?.LogWarning($"End date {item.End} of calendar item must be after start date {item.Start} for item {item.Id}");
                continue;
            }

            // Create new position object
            var position = new ItemPosition { Item = item, Position = 0, Total = overlaps.Count + 1, Date = date };
            position.Top = CalcTop(position);
            position.Height = CalcHeight(position);
            if (position.Bottom > PixelsInDay)
            {
                position.Height = PixelsInDay - position.Top;
            }

            // Remove overlaps that are not relevant
            overlaps.RemoveAll(o => o.Bottom <= position.Top);
            positions.Add(position);

            // Calculate the position
            for (var i = 1; i <= overlaps.Count; i++)
            {
                if (overlaps.Any(o => o.Position == i) == false)
                {
                    position.Position = i;
                }
            }

            if (position.Position == 0) position.Position = overlaps.Count + 1;

            overlaps.Add(position);
            var maxPosition = overlaps.Max(o => o.Position);
            foreach (var overlap in overlaps)
            {
                overlap.Total = maxPosition;
            }
        }

        // Calculate the total overlapping events
        foreach (var position in positions)
        {
            var filteredPos = positions.Where(p => p.Top < position.Bottom && p.Bottom > position.Top);
            int max = 0;
            if (filteredPos.Any())
            {
                max = filteredPos.Max(p => p.Position);
            }
            if (max > position.Total)
            {
                position.Total = max;

                // Need to update overlapping events
                var overlappingPositions = positions.Where(p => p.Top < position.Bottom && p.Bottom > position.Top);
                foreach (var overlappedPosition in overlappingPositions)
                {
                    if (overlappedPosition.Total < max)
                    {
                        overlappedPosition.Total = max;
                    }
                }
            }
        }

        return positions;
    }
    private async Task ItemDropped(MudItemDropInfo<CalendarItem> dropItem)
    {
        if (dropItem.Item == null) return;
        var item = dropItem.Item;

        var oldStart = item.Start;
        var oldEnd = item.End;
        var oldResourceId = item.ResourceId;

        var duration = item.End?.Subtract(item.Start) ?? TimeSpan.Zero;
        DateTime proposedStart = default;
        DateTime? proposedEnd = null;

        var id = dropItem.DropzoneIdentifier;

        // Pattern 1: time-slot zone => resourceId|yyyy-MM-dd|row
        if (id.Contains('|'))
        {
            var parts = id.Split('|');
            if (parts.Length == 3 &&
                DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                int.TryParse(parts[2], out var row))
            {
                // Update resource
                item.ResourceId = parts[0];
                var minutes = Calendar.DayStartTime.ToTimeSpan().TotalMinutes + ((double)row / CellsInDay) * MinutesInDay;
                proposedStart = day.Date.AddMinutes(minutes);
            }
        }
        else
        {
            // Legacy patterns:
            // a) date_row  (still supported if ever used)
            // b) drop onto another item's zone (zone id == existing item's Id)
            var parts = id.Split("_");
            if (parts.Length >= 2 && DateTime.TryParse(parts[0], out var date))
            {
                if (int.TryParse(parts[1], out var cell))
                {
                    var minutes = Calendar.DayStartTime.ToTimeSpan().TotalMinutes + ((double)cell / CellsInDay) * MinutesInDay;
                    proposedStart = date.AddMinutes(minutes);
                }
            }
            else
            {
                // Dropped onto another item
                var existingItem = Calendar.Items.FirstOrDefault(x => x.Id == parts[0]);
                if (existingItem != null)
                {
                    proposedStart = existingItem.Start;
                    item.ResourceId = existingItem.ResourceId;
                }
            }
        }

        if (proposedStart == default)
            return;

        proposedEnd = item.End.HasValue ? proposedStart.Add(duration) : (DateTime?)null;

        item.Start = proposedStart;
        if (proposedEnd.HasValue)
            item.End = proposedEnd;

        var allowed = true;
        if (Calendar.ItemChanging != null)
        {
            try
            {
                allowed = await Calendar.ItemChanging(item);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ItemChanging (drag). Cancelling change.");
                allowed = false;
            }
        }

        if (!allowed)
        {
            // revert all
            item.Start = oldStart;
            item.End = oldEnd;
            item.ResourceId = oldResourceId;
            return;
        }

        Calendar.Refresh();
        await Calendar.ItemChanged.InvokeAsync(item);
    }

    private bool IsHourCell(int row)
    {
        //return (int)Calendar.DayTimeInterval >= 60 || row % (60 / (int)Calendar.DayTimeInterval) == 0;
        return true;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _jsService?.Dispose();
        _dotNetRef?.Dispose();
    }
}