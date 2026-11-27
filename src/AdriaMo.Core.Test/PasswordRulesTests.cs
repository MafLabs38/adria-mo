using AdriaMo.Core.Accounts;

namespace AdriaMo.Core.Test;

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData("la porte du hall")]
    [InlineData("Tr0ub4dor&3xy")]
    [InlineData("éléphant rose 7")]
    public void A_long_password_is_accepted_without_any_symbol_rule(string password) =>
        PasswordRules.Check(password, "eric").Should().BeEmpty();

    [Fact]
    public void Eleven_characters_are_too_short_and_twelve_are_enough()
    {
        PasswordRules.Check("bleu ciel ab", "eric").Should().BeEmpty();
        PasswordRules.Check("bleu ciel a", "eric").Should().Equal(PasswordProblem.TooShort);
    }

    [Fact]
    public void The_length_counts_characters_not_utf16_units()
    {
        // Twelve characters, of which four are outside the basic plane (two UTF-16 units each).
        PasswordRules.Check("🏠🏠🏠🏠 la porte", "eric").Should().BeEmpty();
        PasswordRules.Check("🏠🏠🏠🏠🏠🏠", "eric").Should().Contain(PasswordProblem.TooShort);
    }

    [Fact]
    public void An_accent_typed_as_two_characters_counts_as_one()
    {
        // "e" followed by a combining acute accent is "é" once normalized: eleven characters, not twelve.
        PasswordRules.Check("bleu cie\u0301l a", "eric").Should().Equal(PasswordProblem.TooShort);
        PasswordRules.Normalize("cie\u0301l").Should().Be("ci\u00e9l");
    }

    [Fact]
    public void One_hundred_twenty_eight_characters_are_accepted_and_one_more_is_too_long()
    {
        var longest = string.Concat(Enumerable.Repeat("abcdefgh", 16));
        PasswordRules.Check(longest, "eric").Should().BeEmpty();
        PasswordRules.Check(longest + "x", "eric").Should().Equal(PasswordProblem.TooLong);
    }

    [Fact]
    public void One_repeated_character_is_refused() =>
        PasswordRules.Check("aaaaaaaaaaaaaa", "eric").Should().Equal(PasswordProblem.OneRepeatedCharacter);

    [Fact]
    public void A_single_character_is_too_short_but_not_a_repetition() =>
        PasswordRules.Check("a", "eric").Should().Equal(PasswordProblem.TooShort);

    [Theory]
    [InlineData("eric-et-sa-porte")]
    [InlineData("ERIC2026porte!")]
    public void A_password_containing_the_user_name_is_refused_whatever_the_case(string password) =>
        PasswordRules.Check(password, "eric").Should().Equal(PasswordProblem.ContainsUserName);

    [Fact]
    public void An_empty_user_name_refuses_nothing() =>
        PasswordRules.Check("la porte du hall", "  ").Should().BeEmpty();

    [Theory]
    [InlineData("123456789012")]
    [InlineData("MotDePasse123")]
    [InlineData("azertyuiop123")]
    public void A_common_password_is_refused_whatever_the_case(string password) =>
        PasswordRules.Check(password, "eric").Should().Contain(PasswordProblem.TooCommon);

    [Fact]
    public void Several_problems_are_all_reported() =>
        PasswordRules.Check("eric", "eric").Should().Equal(PasswordProblem.TooShort, PasswordProblem.ContainsUserName);

    [Fact]
    public void Null_arguments_are_refused()
    {
        var noPassword = () => PasswordRules.Check(null!, "eric");
        var noUser = () => PasswordRules.Check("la porte du hall", null!);
        var noNormalize = () => PasswordRules.Normalize(null!);

        noPassword.Should().Throw<ArgumentNullException>();
        noUser.Should().Throw<ArgumentNullException>();
        noNormalize.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Both_roles_exist_super_admin_first() => Roles.All.Should().Equal("super-admin", "admin");
}
