using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace WebCrawler
{
    using System.Net.Http;

    enum MessageType { Error, Warning, Default, Information };
    class Crawler
    {
        static private readonly string[] invalidTypes = new string[] 
        {
            "png",
            "jpg",
            "jpeg",
            "ico",
            "css",
            "exe",
            "zip",
            "svg",
            "bin",
            "pdf",
            "pptx",
            "xls",
            "docx",
            "json",
            "js",
            "tar",
            "xml"
        };

        private readonly ConcurrentQueue<string> urlsToVisit = new ConcurrentQueue<string>();
        private readonly HashSet<string> urlsVisited = new HashSet<string>();
        private readonly HashSet<string> allUrls = new HashSet<string>();
        private readonly List<Task> tasks = new List<Task>();
        private readonly SemaphoreSlim slowAccess = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim fastAccess = new SemaphoreSlim(1, 1);
        private readonly HttpClient httpClient = new HttpClient();
        private readonly SiteMap siteMap = new SiteMap();
        
        private int progressCounter;

        public readonly List<string> Keywords = new List<string>();
        public Func<double, bool> OnProgress { get; set; }
        public Func<string, MessageType, bool> OnMessage { get; set; }
        public bool IgnoreParameters { get; set; }
        public bool LookForFiles { get; set; }
        public int NumThreads { get; set; }
        public int QueriesPerThread { get; set; }

        public TimeSpan Timeout
        {
            get => httpClient.Timeout;
            set => httpClient.Timeout = value;
        }

        public int TotalUrls
        {
            get => allUrls.Count;
        }

        public int QueueCount
        {
            get => urlsToVisit.Count;
        }

        public Crawler()
        {
            IgnoreParameters = false;
            LookForFiles = true;

            NumThreads = 8;
            QueriesPerThread = 16;

            httpClient.MaxResponseContentBufferSize = 1024 * 1024 * 2; // 2 Megabytes
            httpClient.Timeout = new TimeSpan(0, 0, 5); // 5 seconds
        }

        ~Crawler()
        {
            httpClient.Dispose();
            slowAccess.Dispose();
            fastAccess.Dispose();
        }

        public void Run(CancellationToken token = default)
        {
            if (urlsToVisit.IsEmpty)
            {
                throw new EmptyQueueException("Queue was empty. Use Enqueue() to add url to visit");
            }

            progressCounter = 0;

            for (int i = 0; i < NumThreads; i++)
            {
                tasks.Add(Task.Run(() => Main(token)));
            }

            foreach (var task in tasks)
            {
                try
                {
                    task.Wait();
                }
                catch (Exception ex)
                {
                    OnMessage?.Invoke(ex.Message, MessageType.Error);
                }
            }

            tasks.Clear();
        }

        public void Enqueue(string url)
        {
            fastAccess.Wait();
            urlsToVisit.Enqueue(url);
            fastAccess.Release();
        }

        public string SiteMapToString()
        {
            return siteMap.ToString();
        }

        public void BuildSiteMap()
        {
            siteMap.OnProgress = OnProgress;
            siteMap.Build(allUrls);
        }

        private async Task Main(CancellationToken token)
        {
            for (int i = 0; i < QueriesPerThread; i++)
            {
                string url;
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (!urlsToVisit.TryDequeue(out url) && !token.IsCancellationRequested)
                {
                    await Task.Delay(1);

                    if (stopwatch.ElapsedMilliseconds > Timeout.TotalMilliseconds + 500)
                    {
                        throw new TimeoutException("Timed out, because the queue was empty.");
                    }
                }
                stopwatch.Stop();
                if (token.IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url, token);
                    if (response.IsSuccessStatusCode && response.Content.Headers.ContentType.MediaType == "text/html")
                    {
                        string body = await response.Content.ReadAsStringAsync();

                        await fastAccess.WaitAsync();
                        urlsVisited.Add(url);
                        fastAccess.Release();

                        await AddUrls(GetUrls(body, url));
                        OnMessage?.Invoke(url, MessageType.Default);
                    }
                }
                catch (Exception ex)
                {
                    OnMessage?.Invoke(ex.Message, MessageType.Error);
                }
                await fastAccess.WaitAsync();
                ++progressCounter;
                fastAccess.Release();
                OnProgress?.Invoke(progressCounter / (double)(NumThreads * QueriesPerThread) * 100.0);
            }
        }

        public async Task AddUrls(HashSet<string> urls)
        {
            await slowAccess.WaitAsync();
            foreach (string url in urls)
            {
                if (!urlsVisited.Contains(url) && IsValidFiletype(url))
                {
                    urlsToVisit.Enqueue(url);
                }

                if (!allUrls.Contains(url))
                {
                    allUrls.Add(url);
                }
            }
            slowAccess.Release();
        }

        public HashSet<string> GetUrls(string body, string currentUrl)
        {
            HashSet<string> urls = new HashSet<string>();

            int cursor;
            int begin, end;

            bool ConsumeBegin(string str)
            {
                cursor = body.IndexOf(str, cursor) + str.Length;
                begin = cursor;
                return cursor - str.Length != -1;
            }

            bool ConsumeEnd(char terminator)
            {
                cursor = body.IndexOf(terminator, cursor);
                end = cursor;
                return cursor != -1;
            }

            void AddToList(string url)
            {
                if (!urls.Contains(url) && ContainsAllKeywords(url))
                {
                    urls.Add(url);
                }
            }

            void Parse (string strBegin, char terminator)
            {
                cursor = 0;
                while (cursor != -1)
                {
                    if (!ConsumeBegin(strBegin))
                        break;

                    if (!ConsumeEnd(terminator))
                        break;

                    string url = body.Substring(begin, end - begin);

                    if (IgnoreParameters)
                        url = StripParameters(url);

                    if (url.StartsWith("http") && url.Length > 8)
                    {
                        AddToList(url);
                    }
                    else if (url.StartsWith("."))
                    {
                        AddToList(currentUrl + url.Substring(1));
                    }
                    else if (url.StartsWith("//"))
                    {
                        AddToList(GetTransferProtocol(Hostname(currentUrl)) + ":" + url);
                    }
                    else if (url.StartsWith("/"))
                    {
                        AddToList(Hostname(currentUrl) + url);
                    }
                }
            }

            Parse("href=\"", '"');
            if (LookForFiles)
            {
                Parse("src=\"", '"');
            }
                
            return urls;
        }

        private bool ContainsAllKeywords(string str)
        {
            foreach (string word in Keywords)
            {
                if (!str.Contains(word))
                    return false;
            }
            return true;
        }

        static public string StripParameters(string url)
        {
            int len = url.IndexOf('?');
            if (len == -1)
            {
                return url;
            }

            return url.Substring(0, len);
        }

        static public string Hostname(string url)
        {
            int len = url.IndexOf('/', 8); /* 8 == length https:// */
            if (len == -1)
            {
                return url;
            }

            return url.Substring(0, len);
        }

        static public string GetTransferProtocol(string hostname)
        {
            return hostname.StartsWith("https") ? "https" : "http";
        }

        static public bool IsValidFiletype(string url)
        {
            url = url.ToLower();
            foreach (string invalidType in invalidTypes)
            {
                if (url.EndsWith('.' + invalidType))
                {
                    return false;
                }
            }

            return true;
        }
    }

    class EmptyQueueException : Exception
    {
        public EmptyQueueException() { }
        public EmptyQueueException(string message) : base(message) { }
    }
}
