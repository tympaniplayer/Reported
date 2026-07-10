using Reported;

namespace Reported.Tests;

public sealed class GifUrlTests
{
    [Theory]
    // Tenor (existing behavior, preserved)
    [InlineData("https://tenor.com/view/tiger-woods-stare-we-can-do-it-gif-11974968")]
    [InlineData("https://www.tenor.com/view/some-gif-123")]
    [InlineData("https://media.tenor.com/abc123/tenor.gif")]
    // Giphy
    [InlineData("https://giphy.com/gifs/video-siz-platypus-ETV4MRojrqsve")]
    [InlineData("https://www.giphy.com/gifs/funny-thing-abc123")]
    [InlineData("https://media.giphy.com/media/ETV4MRojrqsve/giphy.gif")]
    [InlineData("https://media3.giphy.com/media/ETV4MRojrqsve/giphy.gif")]
    [InlineData("https://i.giphy.com/ETV4MRojrqsve.gif")]
    // Klipy
    [InlineData("https://klipy.com/gifs/fennec-tickle")]
    [InlineData("https://www.klipy.com/gifs/that-is-not-funny-austin-powers-1")]
    [InlineData("https://static2.klipy.com/ii/da290b156d64898341638f3c299e7478/2a/15/LuOGHATX.webp")]
    public void IsValid_KnownGifHosts_ReturnsTrue(string url)
    {
        Assert.True(GifUrl.IsValid(url));
    }

    [Theory]
    [InlineData("http://tenor.com/view/abc")]          // not https
    [InlineData("https://example.com/cool.gif")]        // unknown host
    [InlineData("https://giphy.com/")]                  // no gif path
    [InlineData("https://giphy.com/explore/link")]      // wrong path (not /gifs/)
    [InlineData("https://klipy.com/support/create-content")] // wrong path (not /gifs/)
    [InlineData("https://tenor.com.evil.com/view/x")]   // host spoofing
    [InlineData("https://media.giphy.com.evil.com/x")]  // host spoofing
    [InlineData("not a url")]
    [InlineData("")]
    public void IsValid_UnsupportedOrMalformed_ReturnsFalse(string url)
    {
        Assert.False(GifUrl.IsValid(url));
    }
}
