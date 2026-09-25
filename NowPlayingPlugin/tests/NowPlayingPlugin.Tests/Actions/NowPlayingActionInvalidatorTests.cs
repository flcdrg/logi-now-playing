namespace Loupedeck.NowPlayingPlugin.Tests.Actions
{
    using System;

    using Loupedeck.NowPlayingPlugin.Actions;

    using Xunit;

    public class NowPlayingActionInvalidatorTests
    {
        [Fact]
        public void InvalidateAllParameters_InvokesAllParameterNotificationExactlyOnce()
        {
            var invocationCount = 0;

            NowPlayingActionInvalidator.InvalidateAllParameters(() => invocationCount++);

            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public void InvalidateAllParameters_WithNullCallback_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => NowPlayingActionInvalidator.InvalidateAllParameters(null));
        }
    }
}
