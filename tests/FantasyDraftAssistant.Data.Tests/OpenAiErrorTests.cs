using FantasyDraftAssistant.Providers.AI;

namespace FantasyDraftAssistant.Data.Tests;

public class OpenAiErrorTests
{
    [Fact]
    public void Billing_not_active_is_explained()
    {
        var message = OpenAiProviderAdapter.FormatOpenAiError(429, """
            {
              "error": {
                "message": "Your account is not active, please check your billing details on our website.",
                "type": "billing_not_active",
                "code": "billing_not_active"
              }
            }
            """);

        Assert.Contains("not billed", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ChatGPT Plus", message, StringComparison.Ordinal);
        Assert.DoesNotContain("billing_not_active", message);
    }

    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5-mini")]
    [InlineData("gpt-5.6-terra")]
    [InlineData("o3-mini")]
    public void Newer_openai_models_use_max_completion_tokens(string model)
    {
        Assert.True(OpenAiProviderAdapter.RequiresMaxCompletionTokens(model));
        var json = System.Text.Json.JsonSerializer.Serialize(
            OpenAiProviderAdapter.ChatCompletionBody(model, 16, new[] { new { role = "user", content = "ping" } }));
        Assert.Contains("max_completion_tokens", json);
        Assert.DoesNotContain("\"max_tokens\"", json);
    }

    [Fact]
    public void Gpt_4_1_keeps_max_tokens()
    {
        Assert.False(OpenAiProviderAdapter.RequiresMaxCompletionTokens("gpt-4.1"));
        var json = System.Text.Json.JsonSerializer.Serialize(
            OpenAiProviderAdapter.ChatCompletionBody("gpt-4.1", 16, new[] { new { role = "user", content = "ping" } }));
        Assert.Contains("\"max_tokens\"", json);
        Assert.DoesNotContain("max_completion_tokens", json);
        Assert.DoesNotContain("reasoning_effort", json);
    }

    [Fact]
    public void Gpt5_sends_low_reasoning_effort_in_fast_mode()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            OpenAiProviderAdapter.ChatCompletionBody("gpt-5", 4000, new[] { new { role = "user", content = "ping" } }, fastMode: true));
        Assert.Contains("\"reasoning_effort\":\"low\"", json);
        Assert.Contains("max_completion_tokens", json);
    }

    [Fact]
    public void OpenAi_delta_reads_message_content_and_finish_reason()
    {
        Assert.Equal("Take Jeanty", OpenAiProviderAdapter.ExtractOpenAiDelta("""
            {"choices":[{"message":{"content":"Take Jeanty"},"finish_reason":null}]}
            """));
        Assert.Equal("length", OpenAiProviderAdapter.ExtractOpenAiFinishReason("""
            {"choices":[{"delta":{},"finish_reason":"length"}]}
            """));
    }
}
