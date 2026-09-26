namespace EmployeeRequests.Api.Domain;

/// <summary>A responsible team: a role with a shared mailbox, not a person, so staff turnover doesn't change routing.</summary>
public sealed class TeamRoute
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    /// <summary>HubSpot owner for tickets of this team; empty → DefaultOwnerId.</summary>
    public string OwnerId { get; set; } = "";
}

/// <summary>
/// Department → responsible team, loaded from routing.json. Any value can be overridden without code,
/// e.g. Render env var <c>Routing__Teams__payroll__Email</c>.
/// </summary>
public sealed class RoutingOptions
{
    public string DefaultOwnerId { get; set; } = "";
    public string ManagerEmail { get; set; } = "";
    public Dictionary<string, TeamRoute> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The team for a department, falling back to the "other" (triage) team.</summary>
    public TeamRoute TeamFor(string department) =>
        Teams.GetValueOrDefault(department)
        ?? Teams.GetValueOrDefault(Departments.Other)
        ?? new TeamRoute { Name = "Triage Desk" };

    public string OwnerFor(string department) =>
        TeamFor(department).OwnerId is { Length: > 0 } owner ? owner : DefaultOwnerId;
}

/// <summary>Resolution SLA per priority. DemoMode swaps hours for minutes so escalation can be shown live.</summary>
public sealed class SlaOptions
{
    public bool DemoMode { get; set; }
    public Dictionary<Priority, double> Hours { get; set; } = [];
    public Dictionary<Priority, double> DemoMinutes { get; set; } = [];

    public DateTimeOffset DueAt(Priority priority, DateTimeOffset now) =>
        DemoMode
            ? now.AddMinutes(DemoMinutes.GetValueOrDefault(priority, 15))
            : now.AddHours(Hours.GetValueOrDefault(priority, 24));
}
