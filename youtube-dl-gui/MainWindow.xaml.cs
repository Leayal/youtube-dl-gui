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

namespace youtube_dl_gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        const string ToolFilename = "youtube-dl.exe";

        private static readonly DependencyPropertyKey YoutubeDLPathProperty = DependencyProperty.RegisterReadOnly("YoutubeDLPath", typeof(string), typeof(MainWindow), new PropertyMetadata(null, (obj, e) =>
        {
            if (obj is MainWindow mymainwindow)
            {
                mymainwindow.youtubeTool.YoutubeDLPath = (string)e.NewValue;
            }
        }));
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
        }));
        public bool IsInStartUp => (bool)this.GetValue(IsInStartUpProperty.DependencyProperty);
        private static readonly DependencyPropertyKey IsToolBrowseAvailableProperty = DependencyProperty.RegisterReadOnly("IsToolBrowseAvailable", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, null, (obj, val) =>
        {
            return ((bool)val || (bool)obj.GetValue(IsInStartUpProperty.DependencyProperty));
        }));
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

        public MainWindow()
        {
            this.registry = Registry.CurrentUser.CreateSubKey(Path.Combine("Software", "LamieYuI", "youtube-dl-gui"), true);
            string browsePath = (string)this.registry.GetValue("ExePath", string.Empty);
            if (string.IsNullOrWhiteSpace(browsePath))
            {
                this.where_youtubedl = Helper.WhereAsync(ToolFilename);
            }
            else
            {
                this.where_youtubedl = Task.Run(() =>
                {
                    if (File.Exists(browsePath))
                    {
                        return browsePath;
                    }
                    else
                    {
                        this.registry.DeleteValue("ExePath");
                        return null;
                    }
                });
            }

            this.regex_InvalidPathCharacters = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            this.cache_youtubeinfo = new Dictionary<string, YoutubeVideoInfo>();
            this.youtubeTool = new YoutubeDL();
            this.InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            this.registry.Dispose();
            base.OnClosed(e);
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string youtubePath = await where_youtubedl;
                this.StartUp(youtubePath);
            }
            catch (FileNotFoundException)
            {
                this.StartUp(null);
            }
        }

        private void StartUp(string youtubePath)
        {
            if (string.IsNullOrEmpty(youtubePath))
            {
                this.SetValue(StarUpTextProperty, $"Cannot find '{ToolFilename}'.\nPlease browse for '{ToolFilename}' tool manually.");
                this.SetValue(IsInStartUpProperty, true);
            }
            else
            {
                this.SetValue(YoutubeDLPathProperty, youtubePath);
                this.SetValue(IsInStartUpProperty, false);
            }
            this.SetValue(IsToolBrowseAvailableProperty, true);
        }

        private void ButtonBrowseTool_Click(object sender, RoutedEventArgs e)
        {
            string oldPath = this.YoutubeDLPath;
            if (ofd == null)
            {
                ofd = new OpenFileDialog()
                {
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
                this.StartUp(ofd.FileName);
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
            // https://www.youtube.com/watch?v=O6NlT9PXqwE
            string youtubeurl = this.TextBoxYoutubeLink.Text;
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
                something = await this.youtubeTool.GetYoutubeVideoInformationAsync(youtubeurl);
                this.cache_youtubeinfo.Add(youtubeurl, something);
            }

            if (sfd == null)
            {
                sfd = new SaveFileDialog()
                {
                    AddExtension = true,
                    OverwritePrompt = true,
                    RestoreDirectory = true
                };
            }

            var formats = new List<YoutubeVideoFormat>(something.Formats.Count);
            var formatStrings = new List<string>(something.Formats.Count);
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
                    string formatName = GetFriendlyFormatExtension(format);
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
            sfd.FileName = this.regex_InvalidPathCharacters.Replace(something.Title + "-" + something.VideoID, "-");

            if (sfd.ShowDialog(this) == true)
            {
                string formatString = formatStrings[sfd.FilterIndex - 1];
                if (filterData.TryGetValue(formatString, out var selectedFormat))
                {
                    this.Session_ProgressChanged(null, 0);
                    this.SetValue(IsYoutubeDownloadingIndeterminateProperty, false);
                    this.SetValue(YoutubeDownloadingTextProperty, $"Downloading: {formatString}\nTo: {sfd.FileName}");
                    currentDownloadSession = this.youtubeTool.PrepareVideoDownload(selectedFormat);
                    currentDownloadSession.ProgressChanged += Session_ProgressChanged;
                    currentDownloadSession.DownloadCompleted += Session_DownloadCompleted;
                    currentDownloadSession.StartDownload(sfd.FileName);
                }
                else
                {
                    this.SetValue(IsYoutubeDownloadingProperty, false);
                    MessageBox.Show(this, "Unknown error occured.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            else
            {
                this.SetValue(IsYoutubeDownloadingProperty, false);
            }
        }

        class YoutubeVideoFormatComparer : IComparer<YoutubeVideoFormat>
        {
            class RevertYoutubeVideoFormatComparer : YoutubeVideoFormatComparer
            {
                internal RevertYoutubeVideoFormatComparer() : base() { }

                public override int Compare(YoutubeVideoFormat left, YoutubeVideoFormat right) => -base.Compare(left, right);
            }
            public static readonly YoutubeVideoFormatComparer Default = new YoutubeVideoFormatComparer();
            public static readonly YoutubeVideoFormatComparer Revert = new RevertYoutubeVideoFormatComparer();

            private YoutubeVideoFormatComparer() { }

            public virtual int Compare(YoutubeVideoFormat left, YoutubeVideoFormat right)
            {
                bool left_hasVideo = (left.VideoCodec != null),
                    right_hasVideo = (right.VideoCodec != null);

                if (GetFriendlyFormatExtension(left) == GetFriendlyFormatExtension(right))
                {
                    if (left_hasVideo && right_hasVideo)
                    {
                        double left_height = left.VideoResolution.Height,
                            right_height = right.VideoResolution.Height;

                        if (left_height == right_height)
                        {
                            int leftFPS = left.FPS.HasValue ? left.FPS.Value : 30,
                                rightFPS = right.FPS.HasValue ? right.FPS.Value : 30;
                            if (leftFPS == rightFPS)
                            {
                                int left_priority = GetFormatPriority(left),
                                    right_priority = GetFormatPriority(right);
                                if (left_priority == right_priority)
                                {
                                    return 0;
                                }
                                else if (left_priority > right_priority)
                                {
                                    return 1;
                                }
                                else
                                {
                                    return -1;
                                }
                            }
                            else if (leftFPS > rightFPS)
                            {
                                return 1;
                            }
                            else
                            {
                                return -1;
                            }
                        }
                        else if (left_height > right_height)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else if (!left_hasVideo && !right_hasVideo)
                    {
                        int left_audiobitrate = left.AudioBirate.Value,
                            right_audiobitrate = right.AudioBirate.Value;

                        if (left_audiobitrate == right_audiobitrate)
                        {
                            int left_priority = GetFormatPriority(left),
                              right_priority = GetFormatPriority(right);
                            if (left_priority == right_priority)
                            {
                                return 0;
                            }
                            else if (left_priority > right_priority)
                            {
                                return 1;
                            }
                            else
                            {
                                return -1;
                            }
                        }
                        else if (left_audiobitrate > right_audiobitrate)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        if (left_hasVideo)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                }
                else
                {
                    int left_priority = GetFormatPriority(left),
                        right_priority = GetFormatPriority(right);
                    if (left_priority == right_priority)
                    {
                        return 0;
                    }
                    else if (left_priority > right_priority)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
        }

        static int GetFormatPriority(YoutubeVideoFormat format)
        {
            string formatString = format.VideoCodec ?? format.AudioCodec;
            if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return -3;
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return -5;
            }
            else if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            else if (formatString.Equals("vp8.0", StringComparison.OrdinalIgnoreCase) || formatString.Equals("vp8", StringComparison.OrdinalIgnoreCase))
            {
                return -2;
            }
            else if (formatString.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return -4;
            }
            else
            {
                return 0;
            }
        }

        static string GetFriendlyFormatExtension(YoutubeVideoFormat format)
        {
            string formatString = format.VideoCodec ?? format.AudioCodec;
            if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return "Opus";
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return "Vorbis";
            }
            else if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            {
                return "VP9";
            }
            else if (formatString.Equals("vp8.0", StringComparison.OrdinalIgnoreCase) || formatString.Equals("vp8", StringComparison.OrdinalIgnoreCase))
            {
                return "VP8";
            }
            else if (formatString.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
            {
                return "H264";
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return "M4A";
            }
            else
            {
                return FirstLetterToUpper(formatString);
            }
        }

        public static string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }

        private void Session_DownloadCompleted(object sender, DownloadCompletedEventArgs e)
        {
            currentDownloadSession = null;
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

        private void Session_ProgressChanged(object sender, double e)
        {
            this.Dispatcher.BeginInvoke(new Session_ProgressChangedA(() =>
            {
                this.SetValue(YoutubeDownloadingProgressProperty, e);
            }));
        }

        private delegate void Session_ProgressChangedA();

        bool buttonCancelDownload_ClickLeft;
        private void ButtonCancelDownload_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.buttonCancelDownload_ClickLeft = true;
            }
            else if (this.buttonCancelDownload_ClickLeft)
            {
                this.buttonCancelDownload_ClickLeft = false;
                this.currentDownloadSession?.CancelDownload();
            }
        }
    }
}
