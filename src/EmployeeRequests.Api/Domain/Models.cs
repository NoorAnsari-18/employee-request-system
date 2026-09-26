namespace EmployeeRequests.Api.Domain;

public enum Stage { Open, Active, Finalized }

public enum Priority { Low, Medium, High, Urgent }

/// <summary>Department values as stored in the HubSpot "department" dropdown.</summary>
public static class Departments
{
    public const string Hr = "hr";
    public const string It = "it";
    public const string Payroll = "payroll";
    public const string Operations = "operations";
    public const string Other = "other";

    /// <summary>Short code used in the public request ID, e.g. REQ-IT-123.</summary>
    public static string Code(string department) => department switch
    {
        Hr => "HR",
        It => "IT",
        Payroll => "PAY",
        Operations => "OPS",
        _ => "OTH",
    };
}

/// <summary>Values of the HubSpot "source_channel" dropdown.</summary>
public static class SourceChannels
{
    public static readonly string[] All = ["portal", "email", "whatsapp", "intercom"];
    public const string Portal = "portal";
}

public static class Lifecycle
{
    /// <summary>Tickets may only move forward one step: Open → Active → Finalized.</summary>
    public static bool CanMove(Stage from, Stage to) =>
        (from, to) is (Stage.Open, Stage.Active) or (Stage.Active, Stage.Finalized);
}

public static class PriorityExtensions
{
    public static string ToHubSpot(this Priority p) => p.ToString().ToUpperInvariant();

    public static Priority? ParsePriority(string? value) =>
        Enum.TryParse<Priority>(value, ignoreCase: true, out var p) ? p : null;
}
