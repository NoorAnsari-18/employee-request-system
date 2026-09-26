using EmployeeRequests.Api.Domain;
using Microsoft.Extensions.Configuration;

namespace EmployeeRequests.Tests;

public class RoutingTests
{
    // Mirrors Program.cs: routing.json first, then overrides (env vars on Render) on top.
    private static RoutingOptions Load(Dictionary<string, string?>? overrides = null) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "routing.json"))
            .AddInMemoryCollection(overrides ?? [])
            .Build()
            .GetSection("Routing")
            .Get<RoutingOptions>()!;

    [Theory]
    [InlineData(Departments.Hr, "HR Team")]
    [InlineData(Departments.It, "IT Support")]
    [InlineData(Departments.Payroll, "Payroll Team")]
    [InlineData(Departments.Operations, "Operations Team")]
    [InlineData(Departments.Other, "Triage Desk")]
    public void Every_department_has_a_responsible_team_with_a_mailbox(string department, string team)
    {
        var route = Load().TeamFor(department);

        Assert.Equal(team, route.Name);
        Assert.Contains("@", route.Email);
    }

    [Fact]
    public void Unknown_department_goes_to_the_triage_desk()
    {
        Assert.Equal("Triage Desk", Load().TeamFor("legal").Name);
    }

    [Fact]
    public void A_mailbox_can_be_replaced_without_code_changes()
    {
        // Same shape as the Render env var Routing__Teams__payroll__Email.
        var routing = Load(new() { ["Routing:Teams:payroll:Email"] = "new-payroll-lead@example.com" });

        Assert.Equal("new-payroll-lead@example.com", routing.TeamFor(Departments.Payroll).Email);
        Assert.Equal("Payroll Team", routing.TeamFor(Departments.Payroll).Name);
    }

    [Fact]
    public void Owner_falls_back_to_the_default_when_a_team_has_none()
    {
        var routing = Load(new() { ["Routing:Teams:hr:OwnerId"] = "" });

        Assert.Equal(routing.DefaultOwnerId, routing.OwnerFor(Departments.Hr));
    }
}
