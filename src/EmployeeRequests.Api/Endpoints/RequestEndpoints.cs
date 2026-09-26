using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using EmployeeRequests.Api.Domain;
using EmployeeRequests.Api.HubSpot;
using Microsoft.Extensions.Options;

namespace EmployeeRequests.Api.Endpoints;

public sealed record SubmitRequest(string? Name, string? Email, string? Subject, string? Description, string? Urgency, string? SourceChannel);

public sealed record StatusChange(string? Status, string? ResolutionNote);

public sealed class SecurityOptions
{
    /// <summary>Shared secret for machine callers (Zapier). Required for /api/escalations/run and for setting source_channel.</summary>
    public string ApiKey { get; set; } = "";
}

public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapPost("/requests", Submit);
        api.MapGet("/requests/{requestId}", Track);
        api.MapGet("/tickets", List);
        api.MapPatch("/tickets/{id}/status", ChangeStatus);
        api.MapPost("/escalations/run", RunEscalations);
    }

    // Intake: every channel (portal form, Zapier email adapter, …) lands here.
    private static async Task<IResult> Submit(
        SubmitRequest req, HttpContext http, Classifier classifier, HubSpotClient hubspot,
        IOptions<RoutingOptions> routing, IOptions<SlaOptions> sla, IOptions<SecurityOptions> security, TimeProvider clock,
        CancellationToken ct)
    {
        var errors = Validate(req);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var subject = req.Subject!.Trim();
        var description = req.Description!.Trim();
        var email = req.Email!.Trim().ToLowerInvariant();
        var name = req.Name!.Trim();

        // Only trusted callers (with the API key) may say the request came from another channel.
        var channel = HasValidApiKey(http, security.Value) && req.SourceChannel?.ToLowerInvariant() is { } c && SourceChannels.All.Contains(c)
            ? c
            : SourceChannels.Portal;

        var classification = classifier.Classify(subject, description);
        var priority = classifier.ResolvePriority(PriorityExtensions.ParsePriority(req.Urgency) ?? Priority.Medium, subject, description);
        var slaDue = sla.Value.DueAt(priority, clock.GetUtcNow());

        var contactId = await hubspot.UpsertContactAsync(name, email, ct);
        var ticketId = await hubspot.CreateTicketAsync(new()
        {
            ["subject"] = subject,
            ["content"] = description,
            ["hs_pipeline_stage"] = hubspot.StageId(Stage.Open),
            ["hs_ticket_priority"] = priority.ToHubSpot(),
            ["hubspot_owner_id"] = routing.Value.OwnerFor(classification.Department),
            ["department"] = classification.Department,
            ["employee_name"] = name,
            ["employee_email"] = email,
            ["classified_by"] = classification.ClassifiedBy,
            ["classification_confidence"] = classification.Confidence.ToString(CultureInfo.InvariantCulture),
            ["sla_due_at"] = slaDue.ToUnixTimeMilliseconds().ToString(),
            ["escalated"] = "false",
            ["source_channel"] = channel,
        }, contactId, ct);

        // The ID embeds HubSpot's own ticket ID, so it is unique without a counter.
        var requestId = $"REQ-{Departments.Code(classification.Department)}-{ticketId}";
        await hubspot.UpdateTicketAsync(ticketId, new() { ["request_id"] = requestId }, ct);

        return Results.Created($"/api/requests/{requestId}", new
        {
            requestId,
            ticketId,
            department = classification.Department,
            priority = priority.ToString(),
            slaDueAt = slaDue,
            classifiedBy = classification.ClassifiedBy,
            confidence = classification.Confidence,
            sourceChannel = channel,
        });
    }

    // Public status lookup. Shows no personal data.
    private static async Task<IResult> Track(string requestId, HubSpotClient hubspot, CancellationToken ct)
    {
        var ticketId = requestId.Split('-').LastOrDefault();
        if (ticketId is null || !ticketId.All(char.IsAsciiDigit))
            return Results.NotFound();

        var t = await hubspot.GetTicketAsync(ticketId, ct);
        if (t is null || !string.Equals(t.RequestId, requestId, StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();

        return Results.Ok(new
        {
            t.RequestId, t.Subject, t.Department, Priority = t.Priority.ToString(), Status = t.Stage.ToString(),
            t.SlaDueAt, t.Escalated, t.ResolutionNote, t.CreatedAt,
        });
    }

    // Agent board data. Employee email is left out because the demo board is public.
    private static async Task<IResult> List(HubSpotClient hubspot, CancellationToken ct)
    {
        var tickets = await hubspot.ListTicketsAsync(ct);
        return Results.Ok(tickets.Select(BoardView));
    }

    private static async Task<IResult> ChangeStatus(string id, StatusChange change, HubSpotClient hubspot, CancellationToken ct)
    {
        if (!Enum.TryParse<Stage>(change.Status, ignoreCase: true, out var target))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status must be Active or Finalized."] });

        var ticket = await hubspot.GetTicketAsync(id, ct);
        if (ticket is null)
            return Results.NotFound();

        if (!Lifecycle.CanMove(ticket.Stage, target))
            return Results.Conflict(new { error = $"A ticket cannot move from {ticket.Stage} to {target}. Allowed: Open → Active → Finalized." });

        var note = change.ResolutionNote?.Trim();
        if (target == Stage.Finalized && string.IsNullOrEmpty(note))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["resolutionNote"] = ["A resolution note is required to finalize a ticket."] });

        var props = new Dictionary<string, string> { ["hs_pipeline_stage"] = hubspot.StageId(target) };
        if (target == Stage.Finalized)
            props["resolution_note"] = note!;

        var updated = await hubspot.UpdateTicketAsync(id, props, ct);
        return Results.Ok(BoardView(updated));
    }

    // Called by the Zapier schedule. Escalates overdue tickets and returns them so Zapier can alert the manager.
    private static async Task<IResult> RunEscalations(
        HttpContext http, HubSpotClient hubspot, IOptions<SecurityOptions> security, TimeProvider clock, CancellationToken ct)
    {
        if (!HasValidApiKey(http, security.Value))
            return Results.Unauthorized();

        var overdue = await hubspot.FindOverdueAsync(clock.GetUtcNow(), ct);
        foreach (var t in overdue)
            await hubspot.UpdateTicketAsync(t.Id, new() { ["hs_ticket_priority"] = Priority.Urgent.ToHubSpot(), ["escalated"] = "true" }, ct);

        return Results.Ok(new
        {
            count = overdue.Count,
            summary = string.Join("\n", overdue.Select(t => $"{t.RequestId} [{t.Department}] {t.Subject} (due {t.SlaDueAt:u})")),
            escalated = overdue.Select(t => new { t.Id, t.RequestId, t.Subject, t.Department, t.SlaDueAt, t.OwnerId }),
        });
    }

    private static object BoardView(Ticket t) => new
    {
        t.Id, t.RequestId, t.Subject, t.Description, t.Department, Priority = t.Priority.ToString(), Status = t.Stage.ToString(),
        t.EmployeeName, t.SlaDueAt, t.Escalated, t.ClassifiedBy, t.Confidence, t.SourceChannel, t.ResolutionNote, t.CreatedAt,
    };

    private static Dictionary<string, string[]> Validate(SubmitRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(string field, string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) errors[field] = [$"{field} is required."];
            else if (value.Length > max) errors[field] = [$"{field} must be at most {max} characters."];
        }

        Require("name", r.Name, 100);
        Require("email", r.Email, 254);
        Require("subject", r.Subject, 200);
        Require("description", r.Description, 5000);

        if (!errors.ContainsKey("email") && !MailAddress.TryCreate(r.Email!.Trim(), out _))
            errors["email"] = ["email is not a valid address."];
        if (r.Urgency is not null && PriorityExtensions.ParsePriority(r.Urgency) is not (Priority.Low or Priority.Medium or Priority.High))
            errors["urgency"] = ["urgency must be Low, Medium or High."];
        return errors;
    }

    private static bool HasValidApiKey(HttpContext http, SecurityOptions security)
    {
        if (security.ApiKey.Length == 0 || !http.Request.Headers.TryGetValue("X-Api-Key", out var provided))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided.ToString()), Encoding.UTF8.GetBytes(security.ApiKey));
    }
}
