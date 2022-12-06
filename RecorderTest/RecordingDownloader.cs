using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecorderTest
{
    /// <summary>
    /// Background service to download recordings from Asterisk by guaranteeing that the
    /// number of recordings being downloaded at the same time never exceeds the specified 
    /// value.
    /// </summary>
    public class RecordingDownloader
    {
        private class DownloadableItem
        { 
            public DownloadableItem(RecordingItem recordingItem)
            {
                RecordingItem = recordingItem;
            }

            public RecordingItem RecordingItem { get; }
            public DateTime? TimeUntilRetryDownload { get; set; }
        }

        private static readonly int MaxTimeToRetryDownloadMins = 30;

        ///// <summary>
        ///// Logger instance to be used.
        ///// </summary>
        //private static readonly ILog logger = LogManager.GetLogger(nameof(RecordingDownloader));

        /// <summary>
        /// Collection of recordings ready to be stored.
        /// </summary>
        private readonly BlockingCollection<DownloadableItem> requestQueue = new BlockingCollection<DownloadableItem>();

        /// <summary>
        /// Collection of working threads.
        /// </summary>
        private readonly List<Thread> threads = new List<Thread>();

        /// <summary>
        /// ARI client used to communicate with Asterisk.
        /// </summary>
        private readonly AriClient ariClient;

        /// <summary>
        /// File system access wrapper class.
        /// </summary>
        private readonly IFileAccessor fileIOWrapper;

        private readonly object syncObject = new object();

        private bool isWorkerStopped = true;

        /// <summary>
        /// It creates a new FileStoreHandler instance.
        /// </summary>
        /// <param name="ariClient">The ARI client to download recordings from Asterisk through</param>
        /// <param name="fileIOWrapper">File system access object</param>
        /// <param name="numberOfWorkingThreads">Maximum number of recordings can be downloaded from Asterisk at the same time</param>
        /// <exception cref="ArgumentException"></exception>
        public RecordingDownloader(AriClient ariClient, IFileAccessor fileIOWrapper, int numberOfWorkingThreads)
        {
            string logStamp = nameof(RecordingDownloader);

            this.ariClient = ariClient ?? throw new ArgumentException(nameof(ariClient));
            this.fileIOWrapper = fileIOWrapper ?? throw new ArgumentException(nameof(fileIOWrapper));

            if (numberOfWorkingThreads < 1)
            {
                throw new ArgumentException("Number of working threads should be 1 or more", nameof(numberOfWorkingThreads));
            }

            for (int i = 0; i < numberOfWorkingThreads; i++)
            {
                var name = "Worker-" + i.ToString();

                var thread = new Thread(Worker)
                {
                    Name = name,
                    IsBackground = false
                };

                threads.Add(thread);

                Console.WriteLine($"{logStamp} - Thread {name} created");
            }

            Console.WriteLine($"{logStamp} - New instance created. WorkingThreadsCount: {numberOfWorkingThreads}");
        }

        /// <summary>
        /// It adds the specified recording item to the internal queue to download its
        /// recording from Asterisk.
        /// </summary>
        /// <param name="recording"></param>
        public void Enqueue(RecordingItem recording)
        {
            string logStamp = nameof(Enqueue);

            if (!requestQueue.IsAddingCompleted)
            {
                requestQueue.Add(new DownloadableItem(recording)); // TODO: Handle `InvalidOperationException` exception !!

                Console.WriteLine($"{logStamp} - Recording enqueued for download - CountSnapshot: {requestQueue.Count}");
            }
            else
            {
                Console.WriteLine($"{logStamp} - Item discarded! Queue already completed");
            }
        }

        /// <summary>
        /// The method is used to stop blocking worker threads and complete
        /// them when the request queue is empty.
        /// </summary>
        /// <returns>Recoverable recording items</returns>
        public List<RecordingItem> Stop()
        {
            string logStamp = nameof(Stop);

            requestQueue.CompleteAdding();

            lock (this.syncObject)
            {
                if (!this.isWorkerStopped)
                { 
                    this.isWorkerStopped = true;
                }
                else
                {
                    Console.WriteLine($"Already stopped");
                    return new List<RecordingItem>();
                }
            }

            threads.ForEach(t => t.Join());

            //Console.WriteLine($"{logStamp} - Handler stopped - recoverableRecoringsCount: {recoverableRecordings.Count}");
            return requestQueue.Select(r => r.RecordingItem).ToList();
        }

        /// <summary>
        /// The method is used to start worker threads.
        /// </summary>
        public void Start()
        {
            string logStamp = nameof(Start);

            try
            {
                lock (this.syncObject)
                {
                    if (this.isWorkerStopped)
                    {
                        this.isWorkerStopped = false;
                    }
                    else
                    {
                        Console.WriteLine($"Already started");
                        return;
                    }
                }

                Console.WriteLine($"{logStamp} - Starting handler");

                threads.ForEach(t =>
                {
                    if (t.ThreadState == ThreadState.Unstarted)
                    {
                        t.Start();
                    }
                    else
                    {
                        Console.WriteLine($"{logStamp} - Thread {t.Name} already started");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{logStamp} - Handler threads start failed", ex);
            }
        }

        /// <summary>
        /// Method executed by each working thread. The GetConsumingEnumerable method blocks
        /// thread when the queue is empty. Otherwise, thread will pick up items from the queue.
        /// In long term each working thread will pick up same number of request items from the
        /// collection.
        /// </summary>
        private void Worker()
        {
            string logStamp = nameof(Worker);

            Console.WriteLine($"{logStamp} - Worker started");

            foreach (DownloadableItem downloadableItem in requestQueue.GetConsumingEnumerable(CancellationToken.None))
            {
                DownloadFile(downloadableItem.RecordingItem, out bool retryDownloadLater, deleteRemote: true);

                if (retryDownloadLater)
                {
                    Console.WriteLine($"Re-queue the recording - Name: {downloadableItem.RecordingItem.Name}");

                    if (downloadableItem.TimeUntilRetryDownload == null)
                    {
                        DateTime timeUntilRetryDownload = DateTime.Now.AddMinutes(MaxTimeToRetryDownloadMins);
                        Console.WriteLine($"First download attempt was unsuccessful. Retry download later until: {timeUntilRetryDownload}");
                        downloadableItem.TimeUntilRetryDownload = timeUntilRetryDownload;
                    }
                    else if (DateTime.Now < downloadableItem.TimeUntilRetryDownload.Value)
                    {
                        Console.WriteLine($"Retry download later until: {downloadableItem.TimeUntilRetryDownload}");
                        requestQueue.TryAdd(downloadableItem);
                        // TODO: Check if add was successful and log...
                    }
                    else
                    {
                        Console.WriteLine($"Download retry timeout expired. Failed to download the recording - Name: {downloadableItem.RecordingItem.Name}");
                    }
                }

                lock (this.syncObject)
                { 
                    if (this.isWorkerStopped)
                    {
                        Console.WriteLine($"Worker stopped. Finish the work on this thread");
                        break;
                    }
                }
            }

            Console.WriteLine($"{logStamp} - Worker completed");
        }

        /// <summary>
        /// Downloads file from Asterisk server and stores on local file system. Finnaly
        /// deletes file from the Asterisk if requested.
        /// </summary>
        /// <param name="recording">Recording object to be downloaded</param>
        /// <returns></returns>
        private void DownloadFile(RecordingItem recording, out bool retryDownloadLater, bool deleteRemote = true)
        {
            string logStamp = nameof(DownloadFile);

            Console.WriteLine($"{logStamp} - Download file - Name: {recording.Name}");
            byte[] buffer;

            try
            {
                buffer = ariClient.Recordings.GetStoredFile(recording.Name);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"{logStamp} - Recording not found on the Asterisk server: {ex.Message}");
                    retryDownloadLater = false;
                    return;
                }
                else
                {
                    Console.WriteLine($"{logStamp} - Could not download recording. It can be connection failure or other issue - Exception: {ex?.InnerException?.Message}");
                    retryDownloadLater = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{logStamp} - Failed to download recording", ex);
                retryDownloadLater = true; // ???? Need to test.
                return;
            }

            try
            {
                using (var file = fileIOWrapper.Create(recording.FilePath))
                {
                    file.Write(buffer, 0, buffer.Length);
                    file.Flush();

                    Console.WriteLine($"{logStamp} - File stored - Size: {file.Length / 1024:0.###} KB, Path: {recording.FilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{logStamp} - Failed to create file - Path: {recording.FilePath}");
                retryDownloadLater = true;
                return;
            }

            if (deleteRemote)
            {
                try
                {
                    ariClient.Recordings.DeleteStored(recording.Name);
                    Console.WriteLine($"{logStamp} - Remote recording deleted - Name: {recording.Name}");
                    retryDownloadLater = false;
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"{logStamp} - Recording not found on the Asterisk server: {ex.Message}");
                        retryDownloadLater = false;
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"{logStamp} - Could not delete recording. It can be connection failure or other issue - Exception: {ex?.InnerException?.Message}");
                        retryDownloadLater = false; // ??? Need to test.
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{logStamp} - Failed to delete recording", ex);
                    retryDownloadLater = true; // ???? Need to test.
                    return;
                }
            }
            else
            {
                retryDownloadLater = false;
            }
        }
    }
}
