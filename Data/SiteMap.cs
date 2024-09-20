using System;
using System.Collections.Generic;
using System.Text;

namespace WebCrawler
{
    class SiteMap
    {
        private readonly List<Folder> map = new List<Folder>();
        private int progressCounter;
        public Func<double, bool> OnProgress { get; set; }

        public void Build(HashSet<string> urls)
        {
            progressCounter = 0;
            foreach (string url in urls)
            {
                if (!url.StartsWith("http"))
                {
                    continue;
                }

                string hostname = Crawler.Hostname(url);

                bool isInserted = false;
                foreach (var folder in map)
                {
                    if (folder.Name == hostname)
                    {
                        if (hostname != url)
                        {
                            folder.Insert(url.Substring(hostname.Length + 1));
                        }

                        isInserted = true;
                        break;
                    }
                }

                if (!isInserted)
                {
                    var folder = new Folder(hostname);
                    if (hostname != url)
                    {
                        folder.Insert(url.Substring(hostname.Length + 1));
                    }
                    map.Add(folder);
                }
                if (++progressCounter % 100 == 0)
                    OnProgress?.Invoke(progressCounter / (double)(urls.Count) * 100.0);
            }
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();

            foreach (var folder in map)
            {
                str.Append(folder.BuildString());
            }

            return str.ToString();
        }
    }
}
