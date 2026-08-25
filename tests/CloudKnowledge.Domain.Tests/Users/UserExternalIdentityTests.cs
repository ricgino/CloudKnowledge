using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Domain.Tests.Users;

public sealed class UserExternalIdentityTests
{
    [Fact]
    public void LinkExternalIdentity_WhenNotLinked_ShouldLinkIdentity()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-external-subject");

        Assert.Equal(
            "https://issuer.example.com/",
            user.ExternalIssuer);

        Assert.Equal(
            "alice-external-subject",
            user.ExternalSubject);
    }

    [Fact]
    public void LinkExternalIdentity_WhenSameIdentityIsLinkedAgain_ShouldBeIdempotent()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-external-subject");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-external-subject");

        Assert.Equal(
            "https://issuer.example.com/",
            user.ExternalIssuer);

        Assert.Equal(
            "alice-external-subject",
            user.ExternalSubject);
    }

    [Fact]
    public void LinkExternalIdentity_WhenDifferentIdentityAlreadyExists_ShouldThrow()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        user.LinkExternalIdentity(
            "https://issuer.example.com/",
            "alice-external-subject");

        Assert.Throws<InvalidOperationException>(
            () =>
                user.LinkExternalIdentity(
                    "https://another-issuer.example.com/",
                    "another-subject"));
    }

    [Fact]
    public void LinkExternalIdentity_WhenIssuerOrSubjectIsEmpty_ShouldThrow()
    {
        var user =
            UserAccount.Create(
                "alice@example.com",
                "Alice");

        Assert.Throws<ArgumentException>(
            () =>
                user.LinkExternalIdentity(
                    "",
                    "subject"));

        Assert.Throws<ArgumentException>(
            () =>
                user.LinkExternalIdentity(
                    "https://issuer.example.com/",
                    ""));
    }
}