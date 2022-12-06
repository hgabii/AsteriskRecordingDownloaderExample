// See https://aka.ms/new-console-template for more information
using RecorderTest;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Starting...");

        AriClient ariClient = new AriClient();
        IFileAccessor fileAccessor = new FileAccessor();

        RecordingDownloader recordingDownloader = new RecordingDownloader(ariClient, fileAccessor, 2);

        recordingDownloader.Start();

        Console.WriteLine("Started");
        CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken ct = cts.Token;

        for (int i = 0; i < 1; i++)
        {
            int tNumber = i;

            Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    Random rnd = new Random();
                    int timeMs = rnd.Next(50, 101);

                    Thread.Sleep(timeMs);

                    RecordingItem r1 = new RecordingItem
                    {
                        BridgeId = $"{tNumber}-{j}",
                        MediaDirectory = "md",
                        MediaFile = "mf",
                        Name = $"Name-{tNumber}-{j}"
                    };

                    recordingDownloader.Enqueue(r1);

                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }, ct);
        }

        Console.ReadKey();

        Console.WriteLine("Stopping...");
        cts.Cancel();

        List<RecordingItem> recItems = recordingDownloader.Stop();

        Console.WriteLine("Stopped");

        Console.WriteLine(string.Join(";", recItems.Select(r => r.Name)));
    }
}