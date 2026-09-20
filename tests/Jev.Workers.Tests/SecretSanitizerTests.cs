using Jev.Workers.Process;

namespace Jev.Workers.Tests;

public sealed class SecretSanitizerTests
{
    [Theory]
    [InlineData("OPENAI_API_KEY=sk-secretsecretsecret", "sk-secretsecretsecret")]
    [InlineData("Authorization: Bearer super-secret-token", "super-secret-token")]
    [InlineData("use xai-abcdefghijklmnopqrstuvwxyz", "xai-abcdefghijklmnopqrstuvwxyz")]
    public void Redacts_tokens_and_never_echoes_the_secret(string input, string secret)
    {
        var redacted = SecretSanitizer.Redact(input);
        Assert.Contains(SecretSanitizer.Redacted, redacted);
        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_ordinary_prompts_alone()
    {
        const string prompt = "Scaffold a hello console app in /tmp/workspace";
        Assert.Equal(prompt, SecretSanitizer.Redact(prompt));
    }
}
