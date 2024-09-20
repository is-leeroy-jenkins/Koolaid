using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WebCrawler
{
    /// <inheritdoc />
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static Crawler crawler;
        static StackPanel keywordPanel;
        static StackPanel actionLogStackPanel;
        static CancellationTokenSource cancellationTokenSource;
        static bool IsRunning;

        public MainWindow()
        {
            InitializeComponent();
            MyProgressBar.Maximum = 100;
            Reset();
        }

        private void ActivationButton_Click(object sender, RoutedEventArgs e)
        {
            if (crawler.QueueCount == 0)
                crawler.Enqueue(UrlInput.Text);

            crawler.IgnoreParameters = (bool)IgnoreParamsCheckBox.IsChecked;
            crawler.LookForFiles = (bool)LookForFilesCheckBox.IsChecked;

            if (int.TryParse(NumQueriesPerThreadInput.Text, out int result))
            {
                crawler.QueriesPerThread = result;
            }
            else
            {
                AddMessage("Invalid value for queries per thread", MessageType.Error);
                return;
            }

            if (int.TryParse(NumThreadsInput.Text, out result))
            {
                crawler.NumThreads = result;
            }
            else
            {
                AddMessage("Invalid value for number of threads", MessageType.Error);
                return;
            }

            if (!IsRunning)
            {
                ActivationButton.Content = "Stop";
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = new CancellationTokenSource();
                }

                new Thread(new ThreadStart(() =>
                {
                    IsRunning = true;
                    crawler.Run(cancellationTokenSource.Token);
                    OnMessage($"Total url count: {crawler.TotalUrls}", MessageType.Information);
                    ActivationButton.Dispatcher.Invoke(() => ActivationButton.Content = "Continue");
                    OnProgress(0.0);
                    IsRunning = false;
                })).Start();
            }
            else
            {
                cancellationTokenSource.Cancel();
                IsRunning = false;
            }
        }

        private void SetProgress(double progress)
        {
            MyProgressBar.Value = progress;
        }

        private void AddMessage(string msg, MessageType messageType = MessageType.Default)
        {
            var el = new TextBlock();

            switch (messageType)
            {
                case MessageType.Default:
                    el.Background = Brushes.White;
                    break;
                case MessageType.Error:
                    el.Background = Brushes.OrangeRed;
                    break;
                case MessageType.Warning:
                    el.Background = Brushes.Yellow;
                    break;
                case MessageType.Information:
                    el.Background = Brushes.LightBlue;
                    break;
                default:
                    el.Background = Brushes.White;
                    break;
            }

            el.Text = msg;
            actionLogStackPanel.Children.Add(el);
            ActionLogScrollViewer.Content = actionLogStackPanel;
            ActionLogScrollViewer.ScrollToVerticalOffset(ActionLogScrollViewer.VerticalOffset + 20);
        }

        private bool OnProgress(double progress)
        {
            MyProgressBar.Dispatcher.Invoke(() => SetProgress(progress));
            return true;
        }

        private bool OnMessage(string message, MessageType messageType = MessageType.Default)
        {
            Dispatcher.Invoke(() => AddMessage(message, messageType));
            return true;
        }

        private void AddKeywordButton_Click(object sender, RoutedEventArgs e)
        {
            if (KeywordInput.Text == "")
                return;

            var btn = new Button();
            string text = KeywordInput.Text;

            void Remove(object sndr, RoutedEventArgs rea)
            { 
                keywordPanel.Children.Remove(btn);
                crawler.Keywords.Remove(text);
            }

            btn.Content = text;
            btn.Click += Remove;

            crawler.Keywords.Add(text);
            KeywordInput.Text = "";
            keywordPanel.Children.Add(btn);
            ScrollViewerKeywords.Content = keywordPanel;
            ScrollViewerKeywords.ScrollToVerticalOffset(ScrollViewerKeywords.VerticalOffset + 20);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning)
                return;

            SaveButton.IsEnabled = false;
            ActivationButton.IsEnabled = false;
            string path = PathInput.Text;

            new Thread(new ThreadStart(() =>
            {
                OnMessage("Building map of the urls...", MessageType.Information);
                Stopwatch stopwatch = Stopwatch.StartNew();
                crawler.BuildSiteMap();
                string str = crawler.SiteMapToString();
                stopwatch.Stop();
                OnMessage($"Done. Took {stopwatch.ElapsedMilliseconds} ms", MessageType.Information);
                try
                {
                    System.IO.File.WriteAllText(path, str);
                    long filesize = new System.IO.FileInfo(path).Length;
                    OnMessage($"File saved to {path}. Size: {filesize / 1000} kb", MessageType.Information);
                }
                catch (Exception ex)
                {
                    OnMessage(ex.Message, MessageType.Error);
                }
                OnProgress(0.0);
                SaveButton.Dispatcher.Invoke(() => SaveButton.IsEnabled = true);
                ActivationButton.Dispatcher.Invoke(() => ActivationButton.IsEnabled = true);
            })).Start();
        }

        private void Reset()
        {
            crawler = new Crawler();
            crawler.NumThreads = 8;
            crawler.QueriesPerThread = 8;
            crawler.OnProgress = OnProgress;
            crawler.OnMessage = OnMessage;

            cancellationTokenSource = new CancellationTokenSource();

            IgnoreParamsCheckBox.IsChecked = crawler.IgnoreParameters;
            LookForFilesCheckBox.IsChecked = crawler.LookForFiles;
            NumQueriesPerThreadInput.Text = crawler.QueriesPerThread.ToString();
            NumThreadsInput.Text = crawler.NumThreads.ToString();
            PathInput.Text = @"C:\Users\Public\Map.txt";
            ActivationButton.Content = "Start";

            IsRunning = false;

            actionLogStackPanel = new StackPanel();
            ActionLogScrollViewer.Content = actionLogStackPanel;

            keywordPanel = new StackPanel();
            ScrollViewerKeywords.Content = keywordPanel;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning)
            {
                AddMessage("Cant reset while running!", MessageType.Warning);
            }
            else
            {
                Reset();
                AddMessage("Crawler reset", MessageType.Information);
            }
        }
    }
}
