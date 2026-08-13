namespace MultiStreamViewer.Models;

public class StreamInfo
{
    public int SlotIndex { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool IsConnected { get; set; }
}