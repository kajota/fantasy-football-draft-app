using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Data.Services;

namespace FantasyDraftAssistant.Data.Tests;

public class TeamPortraitStoreTests
{
    [Fact]
    public async Task Saves_and_finds_a_jpeg()
    {
        var root = Path.Combine(Path.GetTempPath(), "fda-portraits-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new TeamPortraitStore(new AppPaths(root));
            var id = TeamId.New();
            Assert.False(store.Exists(id));
            await store.SaveAsync(id, [0xFF, 0xD8, 0xFF, 0x00, 0x01]);
            Assert.True(store.Exists(id));
            Assert.EndsWith(".jpg", store.ExistingPath(id));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
