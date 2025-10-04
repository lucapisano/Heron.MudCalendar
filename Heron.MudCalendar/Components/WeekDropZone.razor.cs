using Heron.MudCalendar.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace Heron.MudCalendar;

public partial class WeekDropZone : IDisposable
{
    [CascadingParameter]
    public MudCalendar Calendar { get; set; } = new();
    
    private string _id = Guid.NewGuid().ToString();

    private JsService? _jsService;
    
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter]
    public ItemPosition? Position { get; set; }
    
    [Parameter]
    public string? Style { get; set; }
    string GetIdentifier()
    {
        return Position?.Item.Id;
        //string.Concat(Position.Date.Date.ToString("d"), "_", Position.Item.Id);
    }

    private string Classname =>
        new CssBuilder("mud-cal-drop-item")
            .AddClass($"mud-cal-{_id}")
            .Build();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        
        if (firstRender)
        {
            _jsService ??= new JsService(JsRuntime);
            try
            {
                await _jsService.AddDragHandler($"mud-cal-{_id}", Position?.Width ?? 1);
            }
            catch (TaskCanceledException) { }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _jsService?.Dispose();
    }
}