using ClanWarTracker.Infrastructure.Telegram;

namespace ClanWarTracker.Tests;

/// <summary>Ролик YouTube в канале - превью самого ролика (играет в Telegram), а не фото обложки.</summary>
public class ChannelVideoTests
{
    [Theory]
    [InlineData("https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://i.ytimg.com/vi/a-B_c1/hqdefault.jpg", "https://www.youtube.com/watch?v=a-B_c1")]
    [InlineData("https://img.youtube.com/vi/dQw4w9WgXcQ/0.jpg", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://supercell.com/images/news.jpg", null)]
    [InlineData(null, null)]
    public void ThumbnailToVideoLink(string? photo, string? expected)
    {
        Assert.Equal(expected, TelegramChannelPublisher.VideoUrl(photo));
    }
}
