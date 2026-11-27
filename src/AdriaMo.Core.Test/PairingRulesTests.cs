using AdriaMo.Core.Accounts;

namespace AdriaMo.Core.Test;

public sealed class PairingRulesTests
{
    [Theory]
    [InlineData(null, "Required")]
    [InlineData("   ", "Required")]
    public void A_phone_needs_a_name(string? name, string code)
    {
        PairingRules.CheckName(name).Should().Be(new Constats.FieldProblem("name", code));
    }

    [Fact]
    public void A_name_is_checked_without_its_spaces_and_has_a_limit()
    {
        PairingRules.CheckName("  " + new string('é', PairingRules.NameMaxLength) + "  ").Should().BeNull();
        PairingRules.CheckName(new string('é', PairingRules.NameMaxLength + 1)).Should().Be(new Constats.FieldProblem("name", "TooLong"));
    }

    [Fact]
    public void A_code_lives_ten_minutes_and_not_one_tick_more()
    {
        var shown = new DateTimeOffset(2026, 11, 27, 14, 0, 0, TimeSpan.Zero);
        var expires = shown + PairingRules.CodeLifetime;

        PairingRules.IsAlive(expires, expires.AddTicks(-1)).Should().BeTrue();
        PairingRules.IsAlive(expires, expires).Should().BeFalse();
    }
}
