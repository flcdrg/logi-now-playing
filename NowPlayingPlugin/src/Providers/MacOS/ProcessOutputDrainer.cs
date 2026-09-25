namespace Loupedeck.NowPlayingPlugin.Providers.MacOS
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    internal static class ProcessOutputDrainer
    {
        public static async Task DrainNonEmptyLinesAsync(TextReader reader, Action<String> onLine)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (onLine == null)
            {
                throw new ArgumentNullException(nameof(onLine));
            }

            String line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (!String.IsNullOrWhiteSpace(line))
                {
                    onLine(line.Trim());
                }
            }
        }
    }
}
