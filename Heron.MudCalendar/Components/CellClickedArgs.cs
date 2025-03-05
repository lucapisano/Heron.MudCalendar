using Microsoft.AspNetCore.Components.Web;

namespace Heron.MudCalendar;

public class CellClickedArgs
{
    public MouseEventArgs? MouseEventArgs { get; set; }
    public DateTime Date { get; set; }
    public string? ResourceId { get; set; }
}
