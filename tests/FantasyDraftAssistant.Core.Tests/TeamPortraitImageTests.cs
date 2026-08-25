using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Core.Tests;

public class TeamPortraitImageTests
{
    [Fact]
    public void Identifies_jpeg_png_and_webp()
    {
        Assert.Equal(".jpg", TeamPortraitImage.Identify([0xFF, 0xD8, 0xFF, 0x00]));
        Assert.Equal(".png", TeamPortraitImage.Identify([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal(".webp", TeamPortraitImage.Identify(
        [
            0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50
        ]));
        Assert.Null(TeamPortraitImage.Identify([0x47, 0x49, 0x46, 0x38]));
        Assert.Equal(".jpg", TeamPortraitImage.ExtensionOrJpeg([0x00, 0x01]));
    }

    [Fact]
    public void Rejects_empty_huge_and_unknown()
    {
        Assert.Equal("That file is empty.", TeamPortraitImage.RejectReason([]));
        Assert.Equal("That image is too large (max 8 MB).", TeamPortraitImage.RejectReason(new byte[11], maxBytes: 10));
        Assert.Equal("Use a JPEG, PNG, or WebP image.", TeamPortraitImage.RejectReason([0x47, 0x49, 0x46, 0x38, 0x39, 0x61]));
        Assert.Null(TeamPortraitImage.RejectReason([0xFF, 0xD8, 0xFF, 0xE0]));
    }
}
