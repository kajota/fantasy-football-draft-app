using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class ResponsesStreamParserTests
{
    [Fact]
    public void Takes_output_text_delta_only()
    {
        var text = ResponsesStreamParser.ExtractVisibleDelta(
            """{"type":"response.output_text.delta","delta":"Recommendation: Josh Allen"}""");
        Assert.Equal("Recommendation: Josh Allen", text);
    }

    [Fact]
    public void Ignores_full_snapshot_done_events()
    {
        var text = ResponsesStreamParser.ExtractVisibleDelta(
            """{"type":"response.output_text.done","text":"Recommendation: Josh Allen\n\nThe long answer."}""");
        Assert.Null(text);
    }

    [Fact]
    public void Ignores_reasoning_deltas()
    {
        Assert.Null(ResponsesStreamParser.ExtractVisibleDelta(
            """{"type":"response.reasoning_summary_text.delta","delta":"The user is asking if they should draft Josh Allen."}"""));
        Assert.Null(ResponsesStreamParser.ExtractVisibleDelta(
            """{"type":"response.reasoning_text.delta","delta":"They're hesitant about drafting a QB this early."}"""));
    }

    [Fact]
    public void Ignores_untyped_full_output_text()
    {
        Assert.Null(ResponsesStreamParser.ExtractVisibleDelta(
            """{"output_text":"the whole answer so far"}"""));
    }

    [Fact]
    public void Accepts_chat_completion_content_deltas()
    {
        var text = ResponsesStreamParser.ExtractVisibleDelta(
            """{"choices":[{"delta":{"content":"Malik Nabers"}}]}""");
        Assert.Equal("Malik Nabers", text);
    }
}
