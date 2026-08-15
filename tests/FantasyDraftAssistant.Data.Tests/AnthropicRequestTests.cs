using FantasyDraftAssistant.Providers.AI;

namespace FantasyDraftAssistant.Data.Tests;

public class AnthropicRequestTests
{
    [Fact]
    public void Opus_5_sends_low_effort_and_a_roomy_max_tokens()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            AnthropicProviderAdapter.MessagesBody("claude-opus-5", fastMode: true, "ping"));
        Assert.Contains("\"effort\":\"low\"", json);
        Assert.Contains("\"max_tokens\":4000", json);
    }

    [Fact]
    public void Haiku_does_not_send_effort()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            AnthropicProviderAdapter.MessagesBody("claude-haiku-4-5", fastMode: true, "ping"));
        Assert.DoesNotContain("output_config", json);
        Assert.Contains("\"max_tokens\":800", json);
    }

    [Fact]
    public void Stop_reason_and_text_delta_are_parsed()
    {
        Assert.Equal("max_tokens", AnthropicProviderAdapter.ExtractAnthropicStopReason("""
            {"type":"message_delta","delta":{"stop_reason":"max_tokens"}}
            """));
        Assert.Equal("Take Jeanty", AnthropicProviderAdapter.ExtractAnthropicDelta("""
            {"type":"content_block_delta","delta":{"type":"text_delta","text":"Take Jeanty"}}
            """));
        Assert.Null(AnthropicProviderAdapter.ExtractAnthropicDelta("""
            {"type":"content_block_delta","delta":{"type":"thinking_delta","thinking":"hmm"}}
            """));
    }
}
