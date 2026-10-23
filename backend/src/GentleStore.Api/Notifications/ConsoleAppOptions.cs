namespace GentleStore.Api.Notifications;

public class ConsoleAppOptions
{
    public const string SectionName = "Console";

    /// <summary>Public origin of the staff console, used for "open this order" links in alerts.</summary>
    public string Url { get; set; } = "http://localhost:5173";
}
