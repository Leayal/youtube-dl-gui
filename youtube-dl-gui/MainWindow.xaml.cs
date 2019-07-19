using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using System.Windows.Controls;
using youtube_dl_gui.Youtube;
using System.Text.RegularExpressions;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace youtube_dl_gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        const string ToolFilename = "youtube-dl.exe";

        private static readonly DependencyPropertyKey YoutubeDLPathProperty = DependencyProperty.RegisterReadOnly("YoutubeDLPath", typeof(string), typeof(MainWindow), new PropertyMetadata(null));
        public string YoutubeDLPath => (string)this.GetValue(YoutubeDLPathProperty.DependencyProperty);

        private static readonly DependencyPropertyKey IsYoutubeDownloadingProperty = DependencyProperty.RegisterReadOnly("IsYoutubeDownloading", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public bool IsYoutubeDownloading => (bool)this.GetValue(IsYoutubeDownloadingProperty.DependencyProperty);

        private static readonly DependencyPropertyKey IsYoutubeDownloadingIndeterminateProperty = DependencyProperty.RegisterReadOnly("IsYoutubeDownloadingIndeterminate", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public bool IsYoutubeDownloadingIndeterminate => (bool)this.GetValue(IsYoutubeDownloadingIndeterminateProperty.DependencyProperty);

        private static readonly DependencyPropertyKey YoutubeDownloadingTextProperty = DependencyProperty.RegisterReadOnly("YoutubeDownloadingText", typeof(string), typeof(MainWindow), new PropertyMetadata("Preparing"));
        public string YoutubeDownloadingText => (string)this.GetValue(YoutubeDownloadingTextProperty.DependencyProperty);

        private static readonly DependencyPropertyKey YoutubeDownloadingProgressProperty = DependencyProperty.RegisterReadOnly("YoutubeDownloadingProgress", typeof(double), typeof(MainWindow), new PropertyMetadata(0d));
        public double YoutubeDownloadingProgress => (double)this.GetValue(YoutubeDownloadingProgressProperty.DependencyProperty);

        private static readonly DependencyPropertyKey IsInStartUpProperty = DependencyProperty.RegisterReadOnly("IsInStartUp", typeof(bool), typeof(MainWindow), new PropertyMetadata(true, (obj, e) =>
        {
            obj.CoerceValue(IsToolBrowseAvailableProperty.DependencyProperty);
            obj.CoerceValue(IsCheckForUpdateAvailableProperty.DependencyProperty);
        }));
        public bool IsInStartUp => (bool)this.GetValue(IsInStartUpProperty.DependencyProperty);
        private static readonly DependencyPropertyKey IsToolBrowseAvailableProperty = DependencyProperty.RegisterReadOnly("IsToolBrowseAvailable", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, null, (obj, val) =>
        {
            return ((bool)val || (bool)obj.GetValue(IsInStartUpProperty.DependencyProperty));
        }));

        private static readonly DependencyPropertyKey IsCheckForUpdateAvailableProperty = DependencyProperty.RegisterReadOnly("IsCheckForUpdateAvailable", typeof(bool), typeof(MainWindow), new PropertyMetadata(true, null, (obj, val) =>
        {
            bool original = (bool)val;
            if (original)
            {
                bool mutated = (bool)obj.GetValue(IsInStartUpProperty.DependencyProperty);
                return !mutated;
            }
            else
            {
                return false;
            }
        }));
        public bool IsCheckForUpdateAvailable => (bool)this.GetValue(IsCheckForUpdateAvailableProperty.DependencyProperty);
        public bool IsToolBrowseAvailable => (bool)this.GetValue(IsToolBrowseAvailableProperty.DependencyProperty);

        private static readonly DependencyPropertyKey StarUpTextProperty = DependencyProperty.RegisterReadOnly("StarUpText", typeof(string), typeof(MainWindow), new PropertyMetadata($"Starting up\nSearching for '{ToolFilename}'"));
        public string StarUpText => (string)this.GetValue(StarUpTextProperty.DependencyProperty);

        private Task<string> where_youtubedl;
        private OpenFileDialog ofd;
        private SaveFileDialog sfd;
        private YoutubeDL youtubeTool;
        private Dictionary<string, YoutubeVideoInfo> cache_youtubeinfo;
        private RegistryKey registry;
        private VideoDownloadSession currentDownloadSession;
        private Regex regex_InvalidPathCharacters;
        private HttpClient httpClient;

        public MainWindow()
        {
            this.httpClient = null;
            this.registry = Registry.CurrentUser.CreateSubKey(Path.Combine("Software", "LamieYuI", "youtube-dl-gui"), true);
            this.where_youtubedl = Task.Run(async () =>
            {
                string browsePath = (string)this.registry.GetValue("ExePath", string.Empty);
                if (string.IsNullOrWhiteSpace(browsePath))
                {
                    string whereIsIt = await Helper.WhereAsync(ToolFilename);
                    this.registry.SetValue("ExePath", whereIsIt, RegistryValueKind.String);
                    return whereIsIt;
                }
                else
                {
                    if (File.Exists(browsePath))
                    {
                        return browsePath;
                    }
                    else
                    {
                        try
                        {
                            string whereIsIt = await Helper.WhereAsync(ToolFilename);
                            this.registry.SetValue("ExePath", whereIsIt, RegistryValueKind.String);
                            return whereIsIt;
                        }
                        catch (FileNotFoundException)
                        {
                            this.registry.DeleteValue("ExePath");
                            throw;
                        }
                    }
                }
            });

            this.regex_InvalidPathCharacters = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}–]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            this.sfd = new SaveFileDialog()
            {
                AddExtension = true,
                OverwritePrompt = true,
                RestoreDirectory = true
            };
            this.cache_youtubeinfo = new Dictionary<string, YoutubeVideoInfo>();
            this.youtubeTool = new YoutubeDL();
            this.InitializeComponent();

            this.CoerceValue(IsToolBrowseAvailableProperty.DependencyProperty);
            this.CoerceValue(IsCheckForUpdateAvailableProperty.DependencyProperty);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (this.httpClient != null)
                {
                    this.httpClient.CancelPendingRequests();
                    this.httpClient.Dispose();
                    this.httpClient = null;
                }
                this.registry.Dispose();
                if (currentDownloadSession != null)
                {
                    currentDownloadSession.CancelDownload();
                }
            }
            catch
            {

            }

            base.OnClosed(e);
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string youtubePath = await where_youtubedl;
                await this.StartUp(youtubePath);
            }
            catch (FileNotFoundException)
            {
                await this.StartUp(null);
            }
        }

        private async Task StartUp(string youtubePath)
        {
            if (string.IsNullOrEmpty(youtubePath))
            {
                this.SetValue(StarUpTextProperty, $"Cannot find '{ToolFilename}'.\nPlease browse for '{ToolFilename}' tool manually.");
                this.SetValue(IsInStartUpProperty, true);
                this.Title = "User Interface for youtube-dl";
            }
            else
            {
                var oldPath = this.youtubeTool.YoutubeDLPath;
                this.youtubeTool.YoutubeDLPath = youtubePath;
                try
                {
                    string version = await this.youtubeTool.GetCLIVersion();
                    this.SetValue(YoutubeDLPathProperty, youtubePath);
                    this.SetValue(IsInStartUpProperty, false);
                    this.Title = "User Interface for youtube-dl (v" + version + ")";
                    oldPath = null;
                }
                catch
                {
                    this.youtubeTool.YoutubeDLPath = oldPath;
                    this.SetValue(StarUpTextProperty, $"Cannot find '{ToolFilename}'.\nPlease browse for '{ToolFilename}' tool manually.");
                    this.SetValue(IsInStartUpProperty, true);
                    this.Title = "User Interface for youtube-dl";
                }
            }
            this.SetValue(IsToolBrowseAvailableProperty, true);
        }

        private async void ButtonBrowseTool_Click(object sender, RoutedEventArgs e)
        {
            string oldPath = this.YoutubeDLPath;
            if (ofd == null)
            {
                ofd = new OpenFileDialog()
                {
                    Title = "Browse for youtube-dl executable",
                    Filter = "Executable files (*.exe)|*.exe",
                    DefaultExt = "exe",
                    Multiselect = false,
                    AddExtension = false
                };
            }
            ofd.FileName = ToolFilename;
            if (File.Exists(oldPath))
            {
                ofd.InitialDirectory = Path.GetDirectoryName(oldPath);
            }
            if (ofd.ShowDialog(this) == true)
            {
                this.registry.SetValue("ExePath", ofd.FileName, RegistryValueKind.String);
                await this.StartUp(ofd.FileName);
            }
        }

        private void TextBoxYoutubeLink_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)
            {
                this.ButtonYoutubeLink_Click(sender, new RoutedEventArgs(Button.ClickEvent, sender));
            }
        }

        private async void ButtonYoutubeLink_Click(object sender, RoutedEventArgs e)
        {
            string youtubeurl = this.TextBoxYoutubeLink.Text;
            bool disablefixup = (this.disableFixup.IsChecked == true);
            if (currentDownloadSession != null || string.IsNullOrWhiteSpace(youtubeurl))
            {
                return;
            }

            youtubeurl = youtubeurl.Trim();

            this.SetValue(YoutubeDownloadingTextProperty, "Preparing");
            this.SetValue(IsYoutubeDownloadingIndeterminateProperty, true);
            this.SetValue(IsYoutubeDownloadingProperty, true);

            YoutubeVideoInfo something;
            if (!this.cache_youtubeinfo.TryGetValue(youtubeurl, out something))
            {
                try
                {
                    something = await this.youtubeTool.GetYoutubeVideoInformationAsync(youtubeurl);
                    this.cache_youtubeinfo.Add(youtubeurl, something);
                }
                catch (Exception ex)
                {
                    this.SetValue(IsYoutubeDownloadingProperty, false);
                    MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var formatStrings = new List<string>(something.Formats.Count + 1);
            Dictionary<string, YoutubeVideoFormat> filterData = new Dictionary<string, YoutubeVideoFormat>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                StringBuilder sb = new StringBuilder(160);
                List<YoutubeVideoFormat> audioOnly = new List<YoutubeVideoFormat>(),
                    videoOnly = new List<YoutubeVideoFormat>(),
                    video = new List<YoutubeVideoFormat>();

                for (int i = 0; i < something.Formats.Count; i++)
                {
                    var format = something.Formats[i];
                    bool hasAudio = !string.IsNullOrEmpty(format.AudioCodec),
                        hasVideo = !string.IsNullOrEmpty(format.VideoCodec);
                    if (hasAudio && hasVideo)
                    {
                        video.Add(format);
                    }
                    else
                    {
                        if (hasVideo)
                        {
                            videoOnly.Add(format);
                        }
                        else
                        {
                            audioOnly.Add(format);
                        }
                    }
                }
                audioOnly.Sort(YoutubeVideoFormatComparer.Revert);
                videoOnly.Sort(YoutubeVideoFormatComparer.Revert);
                video.Sort(YoutubeVideoFormatComparer.Revert);

                Action<YoutubeVideoFormat> handleData = (format) =>
                {
                    string formatName = YoutubeVideoFormatComparer.GetFriendlyFormatExtension(format);
                    sb.Append(formatName);
                    sb.Append(" ");
                    bool hasAudio = (format.AudioCodec != null),
                        hasVideo = (format.VideoCodec != null);
                    if (hasVideo && hasAudio)
                    {
                        var theHeight = format.VideoResolution.Height;
                        if (theHeight == 0)
                        {
                            sb.Append(format.FormatNote);
                        }
                        else
                        {
                            sb.Append(theHeight);
                            sb.Append("p");
                            if (format.FPS.HasValue && format.FPS.Value != 30)
                                sb.Append(format.FPS.Value);
                        }

                        sb.Append(" Video");
                    }
                    else
                    {
                        if (hasAudio)
                        {
                            // Audio only
                            sb.Append(format.AudioBirate);
                            sb.Append("k [Audio Only]");
                        }
                        else
                        {
                            // Video only
                            var theHeight = format.VideoResolution.Height;
                            if (theHeight == 0)
                            {
                                sb.Append(format.FormatNote);
                            }
                            else
                            {
                                sb.Append(theHeight);
                                sb.Append("p");
                                if (format.FPS.HasValue && format.FPS.Value != 30)
                                    sb.Append(format.FPS.Value);
                            }
                            sb.Append(" [Video Only]");
                        }
                    }

                    if (string.Equals(format.Protocol, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.Equals(format.Protocol, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                    {
                        if (formatName == "Vorbis")
                        {
                            sb.Append(" (*.ogg)|*.ogg");
                        }
                        else if (formatName == "Opus")
                        {
                            sb.Append(" (*.opus)|*.opus");
                        }
                        else if (format.FileExtension.Equals("webm", StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append(" (*.mkv)|*.mkv");
                        }
                        else
                        {
                            sb.Append(" (*.");
                            sb.Append(format.FileExtension);
                            sb.Append(")|*.");
                            sb.Append(format.FileExtension);
                        }
                    }
                    else
                    {
                        sb.Append(" (*ts).|*.ts");
                    }

                    string filterName = sb.ToString();
                    sb.Clear();
                    formatStrings.Add(filterName);
                    filterData[filterName] = format;
                };

                foreach (var format in video)
                    handleData(format);
                foreach (var format in videoOnly)
                    handleData(format);
                foreach (var format in audioOnly)
                    handleData(format);
            });

            sfd.Filter = string.Join("|", formatStrings);
            sfd.Title = "Download '" + something.Title + "'";
            sfd.FileName = this.regex_InvalidPathCharacters.Replace(something.Title + "-" + something.VideoID, "-").Normalize();

            if (sfd.ShowDialog(this) == true)
            {
                string formatString = formatStrings[sfd.FilterIndex - 1];
                if (filterData.TryGetValue(formatString, out var selectedFormat))
                {
                    this.Session_ProgressChanged(null, 0);
                    this.SetValue(IsYoutubeDownloadingIndeterminateProperty, false);
                    if (string.Equals(selectedFormat.Protocol, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.Equals(selectedFormat.Protocol, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SetValue(YoutubeDownloadingTextProperty, $"Downloading: {formatString}\nTo: {sfd.FileName}");
                    }
                    else
                    {
                        this.SetValue(YoutubeDownloadingTextProperty, $"Streaming: {formatString}\nTo: {sfd.FileName}");
                    }
                    currentDownloadSession = this.youtubeTool.PrepareVideoDownload(selectedFormat);
                    currentDownloadSession.DisableFixup = disablefixup;
                    currentDownloadSession.ProgressChanged += Session_ProgressChanged;
                    currentDownloadSession.DownloadCompleted += Session_DownloadCompleted;
                    try
                    {
                        currentDownloadSession.StartDownload(sfd.FileName);
                    }
                    catch (Exception ex)
                    {
                        currentDownloadSession.Dispose();
                        currentDownloadSession = null;
                        this.SetValue(IsYoutubeDownloadingProperty, false);
                        MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    this.SetValue(IsYoutubeDownloadingProperty, false);
                    this.cache_youtubeinfo.Remove(youtubeurl);
                    MessageBox.Show(this, "Unknown error occured. Please try again after a moment.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            else
            {
                this.SetValue(IsYoutubeDownloadingProperty, false);
            }
        }

        private void Session_DownloadCompleted(VideoDownloadSession sender, DownloadCompletedEventArgs e)
        {
            currentDownloadSession = null;
            sender.Dispose();
            if (!e.Cancelled)
            {
                if (e.Error != null)
                {
                    this.Dispatcher.BeginInvoke(new Session_ProgressChangedA(() =>
                    {
                        this.SetValue(IsYoutubeDownloadingProperty, false);
                        MessageBox.Show(this, e.Error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }
                else
                {
                    this.Dispatcher.BeginInvoke(new Session_ProgressChangedA(() =>
                    {
                        this.SetValue(IsYoutubeDownloadingProperty, false);
                        if (MessageBox.Show(this, $"Do you want to open output folder?", "Prompt", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            Process.Start("explorer.exe", $"/select,\"{e.DownloadDestination}\"");
                        }
                    }));
                }
            }
            else
            {
                this.Dispatcher.BeginInvoke(new Session_ProgressChangedA(() => this.SetValue(IsYoutubeDownloadingProperty, false)));
            }
        }

        private void Session_ProgressChanged(VideoDownloadSession sender, double e)
        {
            this.Dispatcher.BeginInvoke(new Session_ProgressChangedA(() =>
            {
                this.SetValue(YoutubeDownloadingProgressProperty, e);
            }));
        }

        private delegate void Session_ProgressChangedA();

        private void ProgressBarButton_Click(object sender, RoutedEventArgs e)
        {
            this.currentDownloadSession?.CancelDownload();
        }

        private async void ButtonCheckForToolUpdates_Click(object sender, RoutedEventArgs e)
        {
            this.SetValue(IsCheckForUpdateAvailableProperty, false);

            try
            {
                if (this.httpClient == null)
                {
                    this.httpClient = new HttpClient();
                    this.httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
                    this.httpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue()
                    {
                        Private = true,
                        MustRevalidate = true
                    };
                    this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:67.0) Gecko/20100101 Firefox/67.0");
                }

                string latestVersion = null;
                Uri latestVersionUri = null;

                var task_currentVersion = this.youtubeTool.GetCLIVersion();

                using (var repoResponse = await this.httpClient.GetAsync("https://api.github.com/repos/ytdl-org/youtube-dl/releases"))
                {
                    if (repoResponse.IsSuccessStatusCode)
                    {
                        using (Stream repoStream = await repoResponse.Content.ReadAsStreamAsync())
                        using (StreamReader sr = new StreamReader(repoStream))
                        using (JsonTextReader jtr = new JsonTextReader(sr))
                        {
                            var releases = await JArray.LoadAsync(jtr);
                            if (releases.Count != 0)
                            {
                                for (int i = 0; i < releases.Count; i++)
                                {
                                    if (releases[i] is JObject latestRelease)
                                    {
                                        if (string.Equals(latestRelease.Value<string>("target_commitish"), "master", StringComparison.OrdinalIgnoreCase))
                                        {
                                            bool? isprerelease = latestRelease.Value<bool?>("prerelease");
                                            if (isprerelease.HasValue && isprerelease.Value == false)
                                            {
                                                latestVersion = latestRelease.Value<string>("tag_name");
                                                if (!Uri.TryCreate("", UriKind.Absolute, out latestVersionUri))
                                                {
                                                    latestVersionUri = new Uri("https://github.com/ytdl-org/youtube-dl/releases/latest");
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new System.Net.WebException(repoResponse.ReasonPhrase);
                    }
                }

                if (string.IsNullOrWhiteSpace(latestVersion))
                {
                    throw new System.Net.WebException("Failed to obtain new version information.");
                }
                else
                {
                    string currentVersion = await task_currentVersion;
                    if (string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, "Your CLI tool is already at latest version.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        if (MessageBox.Show(this, $"Found new version: {latestVersion}\nYour current version is: {currentVersion}\n\nDo you want to open release page with your browser to download?", "Prompt", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            Process.Start(latestVersionUri.OriginalString);
                        }
                    }
                }
            }
            catch (System.Net.WebException webEx)
            {
                MessageBox.Show(this, webEx.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            this.SetValue(IsCheckForUpdateAvailableProperty, true);
        }
    }
}
