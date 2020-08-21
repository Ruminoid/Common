// From:
// https://github.com/gregyjames/OctaneDownloader
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ruminoid.Common.Utilities;
using SafeObjectPool;

namespace Ruminoid.Common.Net
{
    public class Downloader : IProgressable
    {
        #region Constructor

        public Downloader()
        {
            UnlockDownloadLimits();
            Progress = new Progress();
            _tasksDone = 0;
        }

        public Downloader(Progress progress)
        {
            UnlockDownloadLimits();
            Progress = progress;
            _tasksDone = 0;
        }

        #endregion

        #region Variables

        private static int _tasksDone;
        private static long _responseLength;

        private readonly ObjectPool<HttpClient> _wcPool = new ObjectPool<HttpClient>(10, () =>
        {
            // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
            HttpClient client = new HttpClient(
                    //Use our custom Retry handler, with a max retry value of 10
                    new RetryHandler(new HttpClientHandler(), 10))
                {MaxResponseContentBufferSize = (int) _responseLength};

            //client.MaxResponseContentBufferSize = partSize;
            client.DefaultRequestHeaders.ConnectionClose = false;
            client.Timeout = Timeout.InfiniteTimeSpan;

            return client;
        });

        #endregion

        #region Progress

        public Progress Progress { get; }

        #endregion

        #region Helper Methods

        private static void UnlockDownloadLimits()
        {
            ServicePointManager.UseNagleAlgorithm = true;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        private static void SetMaxThreads()
        {
            ThreadPool.GetMaxThreads(out int maxWorkerThreads,
                out int maxConcurrentActiveRequests);

            ThreadPool.SetMaxThreads(
                maxWorkerThreads, maxConcurrentActiveRequests);
        }

        private EventfulConcurrentQueue<FileChunk> GetTaskList(double parts)
        {
            var asyncTasks = new EventfulConcurrentQueue<FileChunk>();

            //Delegate for Dequeue
            asyncTasks.ItemDequeued += delegate
            {
                //Tasks done holds the count of the tasks done
                //Parts *2 because there are Parts number of Enqueue AND Dequeue operations
                Progress.Percentage = _tasksDone / (parts * 2);
            };

            //Delegate for Enqueue
            asyncTasks.ItemEnqueued += delegate
            {
                Progress.Percentage = _tasksDone / (parts * 2);
            };

            return asyncTasks;
        }

        private void CombineMultipleFilesIntoSingleFile(List<FileChunk> files, string outputFilePath)
        {
            Progress.Detail = $"正在合并{files.Count}分段……";
            using FileStream outputStream = File.Create(outputFilePath);
            foreach (FileChunk inputFilePath in files)
            {
                using (FileStream inputStream = File.OpenRead(inputFilePath.TempFileName))
                {
                    // Buffer size can be passed as the second argument.
                    outputStream.Position = inputFilePath.Start;
                    inputStream.CopyTo(outputStream);
                }

                Progress.Detail = $"分段{inputFilePath.Start}到{inputFilePath.End}已经合并。";
                File.Delete(inputFilePath.TempFileName);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Tuple<Task<HttpResponseMessage>, FileChunk> GetStreamTask(FileChunk piece, long responseLength, Uri uri,
            EventfulConcurrentQueue<FileChunk> asyncTasks)
        {
            using var wcObj = _wcPool.Get();

            Progress.Detail = "正在下载……";

            //Open a http request with the range
            HttpRequestMessage request = new HttpRequestMessage {RequestUri = uri};
            request.Headers.ConnectionClose = false;
            request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

            //Send the request
            var downloadTask = wcObj.Value.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                CancellationToken.None);

            //Use interlocked to increment Tasks done by one
            Interlocked.Add(ref _tasksDone, 1);
            asyncTasks.Enqueue(piece);

            return new Tuple<Task<HttpResponseMessage>, FileChunk>(downloadTask, piece);
        }

        private List<FileChunk> GetChunkList(long partSize, long responseLength)
        {
            //Variable to hold the old loop end
            int previous = 0;
            var pieces = new List<FileChunk>();

            //Loop to add all the events to the queue
            for (int i = (int) partSize; i <= responseLength; i += (int) partSize)
            {
                Progress.Detail = "写入缓存……";

                if (i + partSize < responseLength)
                {
                    //Start and end values for the chunk
                    int start = previous;
                    int currentEnd = i;

                    pieces.Add(new FileChunk(start, currentEnd, true));

                    //Set the start of the next loop to be the current end
                    previous = currentEnd;
                }
                else
                {
                    //Start and end values for the chunk
                    int start = previous;
                    int currentEnd = i;

                    pieces.Add(new FileChunk(start, (int) responseLength, true));

                    //Set the start of the next loop to be the current end
                    previous = currentEnd;
                }
            }

            return pieces;
        }

        #endregion

        #region Functions

        /// <summary>
        ///     Download a resource as a byte array in memory
        /// </summary>
        /// <param name="url">The URL of the resource to download.</param>
        /// <param name="parts">Number of parts to download file as</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<byte[]> DownloadByteArray(string url, double parts)
        {
            _responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            long partSize = (long) Math.Floor(_responseLength / parts);
            var pieces = new List<FileChunk>();

            ThreadPool.GetMaxThreads(out int maxWorkerThreads,
                out int maxConcurrentActiveRequests);

            bool changeSucceeded = ThreadPool.SetMaxThreads(
                maxWorkerThreads, maxConcurrentActiveRequests);

            //Console.WriteLine(responseLength + " TOTAL SIZE");
            //Console.WriteLine(partSize + " PART SIZE" + "\n");

            Progress.Detail = $"文件总大小：{_responseLength}字节";

            try
            {
                using MemoryStream ms = new MemoryStream();
                ms.SetLength(_responseLength);

                //Using custom concurrent queue to implement Enqueue and Dequeue Events
                var asyncTasks = new EventfulConcurrentQueue<FileChunk>();

                //Delegate for Dequeue
                asyncTasks.ItemDequeued += delegate
                {
                    //Tasks done holds the count of the tasks done
                    //Parts *2 because there are Parts number of Enqueue AND Dequeue operations
                    Progress.Percentage = _tasksDone / (parts * 2);
                };

                //Delegate for Enqueue
                asyncTasks.ItemEnqueued += delegate
                {
                    Progress.Percentage = _tasksDone / (parts * 2);
                };

                // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                HttpClient client = new HttpClient(
                        //Use our custom Retry handler, with a max retry value of 10
                        new RetryHandler(new HttpClientHandler(), 10))
                    {MaxResponseContentBufferSize = 1000000000};

                client.DefaultRequestHeaders.ConnectionClose = false;
                client.Timeout = Timeout.InfiniteTimeSpan;

                //Variable to hold the old loop end
                int previous = 0;

                //Loop to add all the events to the queue
                for (int i = (int) partSize; i <= _responseLength; i += (int) partSize)
                {
                    Progress.Detail = "写入缓存……";

                    if (i + partSize < _responseLength)
                    {
                        //Start and end values for the chunk
                        int start = previous;
                        int currentEnd = i;

                        pieces.Add(new FileChunk(start, currentEnd));

                        //Set the start of the next loop to be the current end
                        previous = currentEnd;
                    }
                    else
                    {
                        //Start and end values for the chunk
                        int start = previous;
                        int currentEnd = i;

                        pieces.Add(new FileChunk(start, (int) _responseLength));

                        //Set the start of the next loop to be the current end
                        previous = currentEnd;
                    }
                }

                var getFileChunk = new TransformManyBlock<IEnumerable<FileChunk>, FileChunk>(chunk => chunk,
                    new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = int.MaxValue, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount // Parallelize on all cores
                    });

                var getStream = new TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>>(
                    piece =>
                    {
                        Progress.Detail = "正在下载……";

                        //Open a http request with the range
                        HttpRequestMessage request = new HttpRequestMessage {RequestUri = new Uri(url)};
                        request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

                        //Send the request
                        var downloadTask = client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                        //Use interlocked to increment Tasks done by one
                        Interlocked.Add(ref _tasksDone, 1);
                        asyncTasks.Enqueue(piece);

                        return new Tuple<Task<HttpResponseMessage>, FileChunk>(downloadTask, piece);
                    }, new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = (int) parts, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount // Parallelize on all cores
                    }
                );

                var writeStream = new ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>>(async tuple =>
                {
                    var buffer = new byte[tuple.Item2.End - tuple.Item2.Start];
                    using (Stream stream = await tuple.Item1.Result.Content.ReadAsStreamAsync())
                    {
                        await stream.ReadAsync(buffer, 0, buffer.Length);
                    }

                    lock (ms)
                    {
                        ms.Position = tuple.Item2.Start;
                        ms.Write(buffer, 0, buffer.Length);
                    }

                    FileChunk s = new FileChunk();
                    asyncTasks.TryDequeue(out s);
                    Interlocked.Add(ref _tasksDone, 1);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = (int) parts, // Cap the item count
                    MaxDegreeOfParallelism = Environment.ProcessorCount // Parallelize on all cores
                });

                DataflowLinkOptions linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                getFileChunk.LinkTo(getStream, linkOptions);
                getStream.LinkTo(writeStream, linkOptions);

                getFileChunk.Post(pieces);
                getFileChunk.Complete();

                await writeStream.Completion.ContinueWith(task =>
                {
                    if (asyncTasks.Count != 0) return;
                    ms.Flush();
                    ms.Close();
                    //onComplete?.Invoke(ms.ToArray());
                });

                Progress.TriggerComplete(this, EventArgs.Empty);

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Progress.Detail = "发生错误：" + ex.Message;
            }

            Progress.TriggerComplete(this, EventArgs.Empty);

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="url">The URL of the resource to download.</param>
        /// <param name="parts">Number of parts to download file as</param>
        /// <param name="outFile">Outfile name, will be auto generated if null.</param>
        /// <param name="onUpdate">Progress OnComplete function</param>
        /// <returns></returns>
        public async Task DownloadFile(string url, double parts, string outFile = null)
        {
            #region Variables

            EventfulConcurrentQueue<FileChunk> asyncTasks;
            TransformManyBlock<IEnumerable<FileChunk>, FileChunk> getFileChunk;
            TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>> getStream;
            ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>> writeStream;
            //Get response length 
            _responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            //Calculate Part size
            long partSize = (long) Math.Round(_responseLength / parts);
            //Get the content ranges to download
            var pieces = GetChunkList(partSize, _responseLength);
            //URL To uri
            Uri uri = new Uri(url);
            //Outfile name for later null check
            string filename = "";
            filename = outFile ?? Path.GetFileName(uri.LocalPath);

            #endregion

            //Console.WriteLine(_responseLength + " TOTAL SIZE");
            //Console.WriteLine(partSize + " PART SIZE" + "\n");

            Progress.Detail = $"文件总大小：{_responseLength}字节";

            //Set max threads to those supported by system
            SetMaxThreads();

            try
            {
                //Using custom concurrent queue to implement Enqueue and Dequeue Events
                asyncTasks = GetTaskList(parts);

                Progress.Detail = "缓存完成";

                //Transform many to get from List<Filechunk> => Filechunk essentially iterating
                getFileChunk =
                    new TransformManyBlock<IEnumerable<FileChunk>, FileChunk>(chunk => chunk,
                        new ExecutionDataflowBlockOptions());

                //Gets the request stream from the filechunk 
                getStream = new TransformBlock<FileChunk, Tuple<Task<HttpResponseMessage>, FileChunk>>(piece =>
                    {
                        var newTask = GetStreamTask(piece, _responseLength, uri, asyncTasks);
                        return newTask;
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = Environment.ProcessorCount, // Cap the item count
                        MaxDegreeOfParallelism = Environment.ProcessorCount // Parallelize on all cores
                    }
                );

                //Writes the request stream to a tempfile
                writeStream = new ActionBlock<Tuple<Task<HttpResponseMessage>, FileChunk>>(async task =>
                {
                    using (Stream streamToRead = await task.Item1.Result.Content.ReadAsStreamAsync())
                    {
                        using (FileStream fileToWriteTo = File.Open(task.Item2.TempFileName, FileMode.OpenOrCreate,
                            FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            fileToWriteTo.Position = 0;
                            await streamToRead.CopyToAsync(fileToWriteTo, (int) partSize, CancellationToken.None);
                        }

                        FileChunk s = new FileChunk();
                        Interlocked.Add(ref _tasksDone, 1);
                        asyncTasks.TryDequeue(out s);
                    }

                    GC.Collect(0, GCCollectionMode.Forced);
                }, new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount, // Cap the item count
                    MaxDegreeOfParallelism = Environment.ProcessorCount // Parallelize on all cores
                });

                //Propage errors and completion
                DataflowLinkOptions linkOptions = new DataflowLinkOptions {PropagateCompletion = true};

                //Build the data flow pipeline
                getFileChunk.LinkTo(getStream, linkOptions);
                getStream.LinkTo(writeStream, linkOptions);

                //Post the file pieces
                getFileChunk.Post(pieces);
                getFileChunk.Complete();

                //Write all the streams
                await writeStream.Completion.ContinueWith(task =>
                {
                    //If all the tasks are done, Join the temp files
                    if (asyncTasks.Count == 0)
                    {
                        CombineMultipleFilesIntoSingleFile(pieces, filename);
                    }
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
            }
            catch (Exception ex)
            {
                Progress.Detail = "发生错误：" + ex.Message;

                //Delete the temp files if there's an error
                foreach (FileChunk piece in pieces)
                {
                    try
                    {
                        File.Delete(piece.TempFileName);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                }
            }

            Progress.TriggerComplete(this, EventArgs.Empty);
        }

        #endregion
    }

    internal sealed class EventfulConcurrentQueue<T> : ConcurrentQueue<T>
    {
        public ConcurrentQueue<T> Queue;

        public EventfulConcurrentQueue()
        {
            Queue = new ConcurrentQueue<T>();
        }

        public new int Count => Queue.Count;

        public new void Enqueue(T item)
        {
            Queue.Enqueue(item);
            OnItemEnqueued();
        }


        public new bool TryDequeue(out T result)
        {
            bool success = Queue.TryDequeue(out result);

            if (success)
            {
                OnItemDequeued(result);
            }

            return success;
        }

        public event EventHandler ItemEnqueued;
        public event EventHandler<ItemDequeuedEventArgs<T>> ItemDequeued;

        private void OnItemEnqueued()
        {
            ItemEnqueued?.Invoke(this, EventArgs.Empty);
        }

        private void OnItemDequeued(T item)
        {
            ItemDequeued?.Invoke(this, new ItemDequeuedEventArgs<T> {Item = item});
        }
    }

    internal sealed class ItemDequeuedEventArgs<T> : EventArgs
    {
        public T Item { get; set; }
    }

    internal class FileChunk
    {
        public FileChunk()
        {
        }

        public FileChunk(int startByte, int endByte)
        {
            TempFileName = Guid.NewGuid().ToString();
            Start = startByte;
            End = endByte;
        }

        public FileChunk(int startByte, int endByte, bool createFile)
        {
            TempFileName = Guid.NewGuid().ToString();
            File.Create(TempFileName);
            Start = startByte;
            End = endByte;
        }

        public int Start { get; set; }
        public int End { get; set; }
        public string TempFileName { get; }
        public int Id { get; set; }
    }

    internal class HttpClientPool : IDisposable
    {
        private readonly ConcurrentBag<HttpClient> _objects;

        public HttpClientPool(int number)
        {
            _objects = new ConcurrentBag<HttpClient>();
            Parallel.For(0, number, i => { _objects.Add(GenerateClient()); });
        }

        public void Dispose()
        {
            foreach (HttpClient client in _objects)
            {
                client.Dispose();
            }
        }

        private HttpClient GenerateClient()
        {
            HttpClient client = new HttpClient(
                    //Use our custom Retry handler, with a max retry value of 10
                    new RetryHandler(new HttpClientHandler(), 10))
                {MaxResponseContentBufferSize = 1000000000};

            client.DefaultRequestHeaders.ConnectionClose = false;
            client.Timeout = Timeout.InfiniteTimeSpan;

            return client;
        }

        public HttpClient Get()
        {
            return _objects.TryTake(out HttpClient item) ? item : GenerateClient();
        }

        public void Return(HttpClient item)
        {
            _objects.Add(item);
        }
    }

    internal class RetryHandler : DelegatingHandler
    {
        // Strongly consider limiting the number of retries - "retry forever" is
        // probably not the most user friendly way you could respond to "the
        // network cable got pulled out."
        private readonly int _maxRetries = 3;

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
        {
            _maxRetries = maxRetries;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (int i = 0; i < _maxRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
            }

            return response;
        }
    }
}
