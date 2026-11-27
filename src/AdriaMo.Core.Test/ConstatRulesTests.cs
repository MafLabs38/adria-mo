using AdriaMo.Core.Constats;

namespace AdriaMo.Core.Test;

public sealed class ConstatRulesTests
{
    [Fact]
    public void A_title_a_known_category_and_a_short_location_are_accepted() =>
        ConstatRules.Check(" Porte du hall ", "portes", "Hall B").Should().BeEmpty();

    [Fact]
    public void A_location_is_optional() => ConstatRules.Check("Porte", "portes", null).Should().BeEmpty();

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void A_title_is_required(string? title) =>
        ConstatRules.Check(title, "portes", null).Should().ContainKey("title").WhoseValue.Should().Equal("Required");

    [Fact]
    public void A_title_of_120_characters_is_accepted_and_121_are_too_long()
    {
        ConstatRules.Check(new string('x', 120), "portes", null).Should().BeEmpty();
        ConstatRules.Check(new string('x', 121), "portes", null)["title"].Should().Equal("TooLong");
    }

    [Fact]
    public void A_location_of_121_characters_is_too_long_and_120_are_accepted()
    {
        ConstatRules.Check("Porte", "portes", new string('x', 120)).Should().BeEmpty();
        ConstatRules.Check("Porte", "portes", new string('x', 121))["location"].Should().Equal("TooLong");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Portes")]
    [InlineData("inconnue")]
    public void An_unknown_category_is_refused(string? category) =>
        ConstatRules.Check("Porte", category, null)["category"].Should().Equal("Unknown");

    [Fact]
    public void Every_problem_is_reported_at_once() =>
        ConstatRules.Check("", "x", new string('x', 121)).Keys.Should().BeEquivalentTo(["title", "category", "location"]);

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("Gond arraché", false, null)]
    [InlineData(null, false, "Empty")]
    [InlineData("  ", false, "Empty")]
    public void A_piece_holds_a_photo_or_a_comment(string? text, bool hasPhoto, string? problem) =>
        ConstatRules.CheckPiece(text, hasPhoto).Should().Be(problem is null ? null : new FieldProblem("piece", problem));

    [Fact]
    public void A_comment_of_2000_characters_is_accepted_and_2001_are_too_long()
    {
        ConstatRules.CheckPiece(new string('x', 2000), false).Should().BeNull();
        ConstatRules.CheckPiece(new string('x', 2001), true).Should().Be(new FieldProblem("text", "TooLong"));
    }

    [Theory]
    [InlineData(ConstatStatus.Open, "open")]
    [InlineData(ConstatStatus.ReportedToLandlord, "reported-to-landlord")]
    [InlineData(ConstatStatus.InterventionPlanned, "intervention-planned")]
    [InlineData(ConstatStatus.InProgress, "in-progress")]
    [InlineData(ConstatStatus.Resolved, "resolved")]
    [InlineData(ConstatStatus.Closed, "closed")]
    [InlineData(ConstatStatus.Reopened, "reopened")]
    public void The_key_of_each_status_is_a_contract_of_the_public_page(ConstatStatus status, string key) =>
        ConstatStatusKey.Of(status).Should().Be(key);

    [Fact]
    public void Every_status_has_a_pinned_key() => Enum.GetValues<ConstatStatus>().Should().HaveCount(7);
}
