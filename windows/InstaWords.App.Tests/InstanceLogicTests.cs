using System;
using InstaWords.App.Services;
using Xunit;

namespace InstaWords.App.Tests
{
    /// <summary>Single instance: what a running copy publishes, and when a later launch takes its place.</summary>
    public class InstanceLogicTests
    {
        [Fact]
        public void InfoRoundTrips()
        {
            var text = InstanceLogic.FormatInfo(new Version(2, 3, 0, 0), 4321);
            Assert.Equal("InstaWords/1 2.3.0 4321", text);
            Assert.True(InstanceLogic.TryParseInfo(text + new string('\0', 200), out var v, out var pid)); // as read from the shared memory
            Assert.Equal(new Version(2, 3, 0), v);
            Assert.Equal(4321, pid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("\0\0\0")]
        [InlineData("InstaWords/2 2.3.0 4321")]   // a future format: not guessed at
        [InlineData("InstaWords/1 2.x 4321")]
        [InlineData("InstaWords/1 2.3.0")]
        [InlineData("InstaWords/1 2.3.0 -5")]
        [InlineData("InstaWords/1 2.3.0 0")]
        [InlineData("InstaWords/1 2.3.0 12 extra")]
        public void RejectsBadInfo(string text) => Assert.False(InstanceLogic.TryParseInfo(text, out _, out _));

        [Theory]
        [InlineData("2.2.0", "2.3.0", LaunchAction.TakeOver)]    // an older copy in the tray makes way
        [InlineData("2.2.0", "2.2.1", LaunchAction.TakeOver)]
        [InlineData("2.9.9", "10.0.0", LaunchAction.TakeOver)]   // numeric, not lexical
        [InlineData("2.3.0", "2.3.0", LaunchAction.ShowRunning)] // same version: show it (as before)
        [InlineData("2.3.0", "2.3.0.7", LaunchAction.ShowRunning)] // the 4th assembly part is ignored
        [InlineData("2.4.0", "2.3.0", LaunchAction.ShowRunning)] // an older launch never closes a newer copy
        public void OnlyANewerCopyTakesOver(string running, string mine, LaunchAction expected) =>
            Assert.Equal(expected, InstanceLogic.Decide(Version.Parse(running), Version.Parse(mine)));

        [Fact]
        public void UnknownVersionsCloseNothing()
        {
            Assert.Equal(LaunchAction.ShowRunning, InstanceLogic.Decide(null, new Version(9, 0, 0)));
            Assert.Equal(LaunchAction.ShowRunning, InstanceLogic.Decide(new Version(1, 0, 0), null));
        }
    }
}
