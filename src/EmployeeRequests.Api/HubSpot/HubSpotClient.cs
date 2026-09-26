using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmployeeRequests.Api.Domain;
using Microsoft.Extensions.Options;

namespace EmployeeRequests.Api.HubSpot;

public sealed class HubSpotOptions
{
    public string Token { get; set; } = "";
    public string PipelineId { get; set; } = "";
    public Dictionary<Stage, string> StageIds { get; set; } = [];
}

public sealed class HubSpotException(HttpStatusCode status, string body)
    : Exception($"HubSpot API returned {(int)status}: {body}")
{
    public HttpStatusCode Status { get; } = status;
}

/// <summary>A ticket as the portal sees it, mapped from HubSpot properties.</summary>
public sealed record Ticket(
    string Id,
    string? RequestId,
    string Subject,
    string? Description,
    string Department,
    Priority Priority,
    Stage Stage,
    string? EmployeeName,
    string? EmployeeEmail,
    DateTimeOffset? SlaDueAt,
    bool Escalated,
    string? ClassifiedBy,
    double? Confidence,
    string? SourceChannel,
    string? ResolutionNote,
    string? OwnerId,
    DateTimeOffset CreatedAt);

/// <summary>Thin typed wrapper over the HubSpot CRM v3 REST API. The only component that writes to HubSpot.</summary>
public sealed class HubSpotClient(HttpClient http, IOptions<HubSpotOptions> options)
{
    // HubSpot-defined association type: ticket → contact.
    private const int TicketToContactAssociationType = 16;

    private static readonly string[] TicketProperties =
    [
        "subject", "content", "hs_pipeline", "hs_pipeline_stage", "hs_ticket_priority", "hubspot_owner_id",
        "request_id", "department", "employee_name", "employee_email", "classified_by",
        "classification_confidence", "sla_due_at", "escalated", "resolution_note", "source_channel", "createdate",
    ];

    private readonly HubSpotOptions _opt = options.Value;

    public async Task<string> UpsertContactAsync(string name, string email, CancellationToken ct = default)
    {
        var (first, last) = SplitName(name);
        var body = new
        {
            inputs = new[]
            {
                new
                {
                    idProperty = "email",
                    id = email,
                    properties = new Dictionary<string, string> { ["email"] = email, ["firstname"] = first, ["lastname"] = last },
                },
            },
        };
        var json = await SendAsync(HttpMethod.Post, "crm/v3/objects/contacts/batch/upsert", body, ct);
        return json!["results"]![0]!["id"]!.GetValue<string>();
    }

    public async Task<string> CreateTicketAsync(Dictionary<string, string> properties, string? contactId, CancellationToken ct = default)
    {
        properties["hs_pipeline"] = _opt.PipelineId;
        object body = contactId is null
            ? new { properties }
            : new
            {
                properties,
                associations = new[]
                {
                    new
                    {
                        to = new { id = contactId },
                        types = new[] { new { associationCategory = "HUBSPOT_DEFINED", associationTypeId = TicketToContactAssociationType } },
                    },
                },
            };
        var json = await SendAsync(HttpMethod.Post, "crm/v3/objects/tickets", body, ct);
        return json!["id"]!.GetValue<string>();
    }

    /// <summary>Updates properties and returns the full ticket (PATCH only echoes the changed properties).</summary>
    public async Task<Ticket> UpdateTicketAsync(string id, Dictionary<string, string> properties, CancellationToken ct = default)
    {
        await SendAsync(HttpMethod.Patch, $"crm/v3/objects/tickets/{Uri.EscapeDataString(id)}", new { properties }, ct);
        return await GetTicketAsync(id, ct) ?? throw new HubSpotException(System.Net.HttpStatusCode.NotFound, $"Ticket {id} vanished after update");
    }

    /// <summary>Direct read by ID. Unlike search, this has no indexing delay, so it works right after creation.</summary>
    public async Task<Ticket?> GetTicketAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var json = await SendAsync(HttpMethod.Get, $"crm/v3/objects/tickets/{Uri.EscapeDataString(id)}?properties={string.Join(',', TicketProperties)}", null, ct);
            var ticket = Map(json!);
            return json!["properties"]?["hs_pipeline"]?.GetValue<string>() == _opt.PipelineId ? ticket : null;
        }
        catch (HubSpotException e) when (e.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    /// <summary>Latest tickets in the Employee Requests pipeline, newest first (board view).</summary>
    public Task<IReadOnlyList<Ticket>> ListTicketsAsync(CancellationToken ct = default) =>
        SearchAsync([Filter("hs_pipeline", "EQ", _opt.PipelineId)], ct);

    /// <summary>Tickets past their SLA that are not Finalized and not yet escalated.</summary>
    public Task<IReadOnlyList<Ticket>> FindOverdueAsync(DateTimeOffset now, CancellationToken ct = default) =>
        SearchAsync(
        [
            Filter("hs_pipeline", "EQ", _opt.PipelineId),
            Filter("hs_pipeline_stage", "NEQ", _opt.StageIds[Stage.Finalized]),
            Filter("sla_due_at", "LT", now.ToUnixTimeMilliseconds().ToString()),
            Filter("escalated", "NEQ", "true"),
        ], ct);

    public string StageId(Stage stage) => _opt.StageIds[stage];

    private async Task<IReadOnlyList<Ticket>> SearchAsync(object[] filters, CancellationToken ct)
    {
        var body = new
        {
            filterGroups = new[] { new { filters } },
            properties = TicketProperties,
            sorts = new[] { new { propertyName = "createdate", direction = "DESCENDING" } },
            limit = 100,
        };
        var json = await SendAsync(HttpMethod.Post, "crm/v3/objects/tickets/search", body, ct);
        return json!["results"]!.AsArray().Select(r => Map(r!)).ToList();
    }

    private static object Filter(string propertyName, string op, string value) =>
        new { propertyName, @operator = op, value };

    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.Token);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HubSpotException(response.StatusCode, text);
        return text.Length == 0 ? null : JsonNode.Parse(text);
    }

    private Ticket Map(JsonNode node)
    {
        var p = node["properties"]!;
        string? S(string name) => p[name]?.GetValue<string>() is { Length: > 0 } v ? v : null;

        var stageId = S("hs_pipeline_stage");
        var stage = _opt.StageIds.FirstOrDefault(s => s.Value == stageId).Key; // unknown → Open

        return new Ticket(
            Id: node["id"]!.GetValue<string>(),
            RequestId: S("request_id"),
            Subject: S("subject") ?? "",
            Description: S("content"),
            Department: S("department") ?? Departments.Other,
            Priority: PriorityExtensions.ParsePriority(S("hs_ticket_priority")) ?? Priority.Medium,
            Stage: stage,
            EmployeeName: S("employee_name"),
            EmployeeEmail: S("employee_email"),
            SlaDueAt: ParseDate(S("sla_due_at")),
            Escalated: S("escalated") == "true",
            ClassifiedBy: S("classified_by"),
            Confidence: double.TryParse(S("classification_confidence"), System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : null,
            SourceChannel: S("source_channel"),
            ResolutionNote: S("resolution_note"),
            OwnerId: S("hubspot_owner_id"),
            CreatedAt: ParseDate(S("createdate")) ?? DateTimeOffset.MinValue);
    }

    // HubSpot returns datetimes as ISO strings, but accepts/returns epoch milliseconds in some paths.
    private static DateTimeOffset? ParseDate(string? v) =>
        v is null ? null
        : long.TryParse(v, out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
        : DateTimeOffset.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d
        : null;

    private static (string First, string Last) SplitName(string name)
    {
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
    }
}
