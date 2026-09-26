namespace EmployeeRequests.Api.Domain;

/// <summary>Department → HubSpot owner. For the demo every department maps to the same owner.</summary>
public sealed class RoutingOptions
{
    public string DefaultOwnerId { get; set; } = "";
    public Dictionary<string, string> OwnerByDepartment { get; set; } = [];

    public string OwnerFor(string department) =>
        OwnerByDepartment.TryGetValue(department, out var owner) && owner != "" ? owner : DefaultOwnerId;
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
