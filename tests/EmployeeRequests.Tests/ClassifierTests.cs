using EmployeeRequests.Api.Domain;

namespace EmployeeRequests.Tests;

public class ClassifierTests
{
    // Uses the real rules.json shipped with the API, so these tests guard the production keyword table.
    private static readonly Classifier Sut = new(ClassificationRules.Load(Path.Combine(AppContext.BaseDirectory, "rules.json")));

    [Theory]
    [InlineData("Annual leave next month", "I would like to apply for 5 days of vacation from 10 Oct.", Departments.Hr)]
    [InlineData("Maternity leave paperwork", "Please share the maternity policy and forms.", Departments.Hr)]
    [InlineData("VPN not connecting", "My laptop can't connect to the VPN since this morning.", Departments.It)]
    [InlineData("Password reset", "I forgot my password for the HR system.", Departments.It)]
    [InlineData("Payslip missing", "My September payslip is not in the portal and the salary looks lower.", Departments.Payroll)]
    [InlineData("Travel reimbursement", "Need reimbursement for the client visit, receipts attached. Payment pending.", Departments.Payroll)]
    [InlineData("my machine is consuming more CPU", "The computer is very slow since yesterday.", Departments.It)]
    [InlineData("Form-16 to proof my employability", "Need my Form-16 for the last financial year.", Departments.Payroll)]
    [InlineData("Desk booking and parking", "Please book a desk on floor 3 and a parking spot for Friday.", Departments.Operations)]
    public void Classifies_clear_requests_by_rules(string subject, string description, string expected)
    {
        var result = Sut.Classify(subject, description);

        Assert.Equal(expected, result.Department);
        Assert.Equal("rules", result.ClassifiedBy);
        Assert.True(result.Confidence >= 0.6);
    }

    [Fact]
    public void Unknown_text_falls_back_to_other_with_zero_confidence()
    {
        var result = Sut.Classify("Question", "Who should I talk to about the team lunch?");

        Assert.Equal(Departments.Other, result.Department);
        Assert.Equal("fallback", result.ClassifiedBy);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void Mixed_signals_fall_back_instead_of_guessing()
    {
        // One HR point and one IT point: 50% confidence is below the 0.6 threshold.
        var result = Sut.Classify("Onboarding", "New joiner needs a laptop");

        Assert.Equal(Departments.Other, result.Department);
        Assert.Equal("fallback", result.ClassifiedBy);
        Assert.Equal(0.5, result.Confidence);
    }

    [Fact]
    public void A_single_clear_keyword_is_enough()
    {
        // One unambiguous signal routes the request; only conflicting signals go to triage.
        var result = Sut.Classify("Internet is not Working at my Cubicle", "Hey internet is not working at my cubicle, can someone check?");

        Assert.Equal(Departments.It, result.Department);
        Assert.Equal("rules", result.ClassifiedBy);
    }

    // Real submissions that were misrouted to Triage before the rules were tuned.
    [Theory]
    [InlineData("I need access to Certain WebSite on my Machine", "Please give me acess to certain website on my machine", Departments.It)]
    [InlineData("Leave as not Feeling Well", "I would not be able to join in the office today as I am not feeling well today", Departments.Hr)]
    [InlineData("Salery not received", "My salery for this month has not come", Departments.Payroll)]
    public void Real_world_phrasing_and_common_typos_are_classified(string subject, string description, string expected)
    {
        var result = Sut.Classify(subject, description);

        Assert.Equal(expected, result.Department);
        Assert.Equal("rules", result.ClassifiedBy);
    }

    [Fact]
    public void Short_keywords_match_whole_words_only()
    {
        // "ac" must not match inside "access" or "account".
        var result = Sut.Classify("Account access", "I need access to my account");

        Assert.Equal(0, result.Scores[Departments.Operations]);
    }

    [Theory]
    [InlineData(Priority.Low, "Salary not received", "It is the 5th and my salary is not received.", Priority.High)]
    [InlineData(Priority.Medium, "Login", "I can’t login to anything, urgent please", Priority.High)] // curly apostrophe
    [InlineData(Priority.Low, "Salary not credited", "My salary is not credited this month.", Priority.High)]
    [InlineData(Priority.Low, "Leave", "Planning leave in December.", Priority.Low)]
    [InlineData(Priority.High, "Leave", "Planning leave in December.", Priority.High)] // never lowered
    public void Priority_is_raised_by_boosters_but_never_lowered(Priority requested, string subject, string description, Priority expected)
    {
        Assert.Equal(expected, Sut.ResolvePriority(requested, subject, description));
    }
}

public class LifecycleTests
{
    [Theory]
    [InlineData(Stage.Open, Stage.Active, true)]
    [InlineData(Stage.Active, Stage.Finalized, true)]
    [InlineData(Stage.Open, Stage.Finalized, false)] // no skipping
    [InlineData(Stage.Finalized, Stage.Active, false)] // no reopening
    [InlineData(Stage.Active, Stage.Open, false)]
    [InlineData(Stage.Open, Stage.Open, false)]
    public void Only_forward_single_steps_are_allowed(Stage from, Stage to, bool allowed)
    {
        Assert.Equal(allowed, Lifecycle.CanMove(from, to));
    }
}

public class SlaTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    private static SlaOptions Options(bool demo) => new()
    {
        DemoMode = demo,
        Hours = new() { [Priority.Low] = 72, [Priority.Medium] = 24, [Priority.High] = 4 },
        DemoMinutes = new() { [Priority.Low] = 30, [Priority.Medium] = 15, [Priority.High] = 5 },
    };

    [Fact]
    public void Production_sla_uses_hours()
    {
        Assert.Equal(Now.AddHours(4), Options(demo: false).DueAt(Priority.High, Now));
        Assert.Equal(Now.AddHours(72), Options(demo: false).DueAt(Priority.Low, Now));
    }

    [Fact]
    public void Demo_mode_uses_minutes()
    {
        Assert.Equal(Now.AddMinutes(5), Options(demo: true).DueAt(Priority.High, Now));
    }
}
