namespace Loupedeck.NowPlayingPlugin.Actions
{
    using System;

    internal static class NowPlayingActionInvalidator
    {
        public static void InvalidateAllParameters(Action invalidateAllParameters)
        {
            if (invalidateAllParameters == null)
            {
                throw new ArgumentNullException(nameof(invalidateAllParameters));
            }

            invalidateAllParameters();
        }
    }
}
