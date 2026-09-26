# Seeds demo tickets through the real API (same path as the portal form), then moves some along the lifecycle
# so the agent board has something in every column.
#
#   .\scripts\seed.ps1                                   # local API on http://localhost:5293
#   .\scripts\seed.ps1 -BaseUrl https://<app>.onrender.com

param([string]$BaseUrl = "http://localhost:5293")

$ErrorActionPreference = "Stop"

$requests = @(
    @{ name = "Priya Sharma";  email = "priya.sharma@example.com";  urgency = "Medium"; move = "Open"
       subject = "Annual leave 14-18 October"; description = "I would like to apply for five days of vacation leave from 14 to 18 October." },
    @{ name = "Rahul Mehta";   email = "rahul.mehta@example.com";   urgency = "Medium"; move = "Active"
       subject = "VPN keeps disconnecting"; description = "My laptop drops the VPN every few minutes since the update. I cannot access the shared drive." },
    @{ name = "Aisha Khan";    email = "aisha.khan@example.com";    urgency = "Low";    move = "Open"
       subject = "Salary not received for September"; description = "My salary is not received yet and the payslip is missing from the portal." },
    @{ name = "Daniel Joseph"; email = "daniel.joseph@example.com"; urgency = "Low";    move = "Finalized"
       subject = "Desk and parking for new joiner"; description = "Please book a desk on floor 3 and a parking spot for our new joiner starting Monday."
       note = "Desk 3-14 and parking bay B7 reserved from Monday." },
    @{ name = "Meera Iyer";    email = "meera.iyer@example.com";    urgency = "Medium"; move = "Finalized"
       subject = "Password reset for HR system"; description = "I forgot my password and I am locked out of the HR system login."
       note = "Password reset link sent; account unlocked." },
    @{ name = "Arjun Nair";    email = "arjun.nair@example.com";    urgency = "Medium"; move = "Active"
       subject = "Travel reimbursement for client visit"; description = "Need reimbursement for my client visit to Pune. Receipts attached, payment still pending." },
    @{ name = "Sara Thomas";   email = "sara.thomas@example.com";   urgency = "High";   move = "Open"
       subject = "Maternity leave paperwork"; description = "Please share the maternity leave policy and the forms I need to submit." },
    @{ name = "Vikram Rao";    email = "vikram.rao@example.com";    urgency = "Low";    move = "Open"
       subject = "Team offsite question"; description = "Who should I talk to about organising the team offsite lunch?" }
)

foreach ($r in $requests) {
    $body = @{ name = $r.name; email = $r.email; subject = $r.subject; description = $r.description; urgency = $r.urgency } | ConvertTo-Json
    $created = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/requests" -ContentType "application/json" -Body $body
    Write-Host ("{0,-24} {1,-11} {2,-7} {3}" -f $created.requestId, $created.department, $created.priority, $r.subject)

    if ($r.move -in @("Active", "Finalized")) {
        Invoke-RestMethod -Method Patch -Uri "$BaseUrl/api/tickets/$($created.ticketId)/status" -ContentType "application/json" `
            -Body (@{ status = "Active" } | ConvertTo-Json) | Out-Null
    }
    if ($r.move -eq "Finalized") {
        Invoke-RestMethod -Method Patch -Uri "$BaseUrl/api/tickets/$($created.ticketId)/status" -ContentType "application/json" `
            -Body (@{ status = "Finalized"; resolutionNote = $r.note } | ConvertTo-Json) | Out-Null
    }
}

Write-Host "`nSeeded $($requests.Count) requests. Open $BaseUrl/board"
