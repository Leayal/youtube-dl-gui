using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.IO;

namespace youtube_dl_gui.Youtube
{
    public class YoutubeDL
    {
        public string YoutubeDLPath { get; set; }

        public Task<string> GetCLIVersion()
        {
            string youtube_tool = this.YoutubeDLPath;
            if (string.IsNullOrWhiteSpace(youtube_tool))
                throw new FileNotFoundException();

            TaskCompletionSource<string> src = new TaskCompletionSource<string>();
            Process proc = new Process()
            {
                StartInfo = new ProcessStartInfo(youtube_tool, "--version")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };
            proc.Exited += (sender, e) =>
            {
                string data = proc.StandardOutput.ReadToEnd();
                if (string.IsNullOrWhiteSpace(data))
                {
                    src.SetException(new NullReferenceException());
                }
                else
                {
                    src.SetResult(data.Trim());
                }
                proc.Dispose();
            };
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                src.SetException(ex);
                proc.Dispose();
            }

            return src.Task;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="youtubelink"></param>
        /// <exception cref="FileNotFoundException">The CLI path is not specified.</exception>
        public Task<YoutubeVideoInfo> GetYoutubeVideoInformationAsync(string youtubelink)
        {
            string youtube_tool = this.YoutubeDLPath;
            if (string.IsNullOrWhiteSpace(youtube_tool))
                throw new FileNotFoundException();
            return Task.Run(() =>
            {
                using (Process proc = new Process()
                {
                    StartInfo = new ProcessStartInfo(youtube_tool, Helper.GetArg(new string[] {
                        "--no-warnings",
                        "--simulate",
                        "--no-call-home",
                        "--dump-json",
                        youtubelink
                    }))
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    }
                })
                {
                    JsonTextReader jtr = null;
                    JObject obj = null;

                    proc.Start();

                    try
                    {
                        int peekedChar = proc.StandardOutput.Peek();
                        if (peekedChar == -1)
                        {
                            string error = proc.StandardError.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                error = error.Trim();
                                throw new ApplicationException(error);
                            }
                            else
                            {
                                throw new InvalidProgramException();
                            }
                        }
                        else if (((char)peekedChar) == '{')
                        {
                            jtr = new JsonTextReader(proc.StandardOutput) { CloseInput = false };
                            obj = JObject.Load(jtr);
                        }
                        else
                        {
                            throw new InvalidProgramException();
                        }
                    }
                    finally
                    {
                        if (jtr != null)
                        {
                            jtr.Close();
                        }
                        proc.StandardOutput.Close();
                        proc.StandardError.Close();
                        if (!proc.HasExited)
                            proc.Kill();
                        proc.WaitForExit(5000);
                    }

                    return new YoutubeVideoInfo(obj);
                }
            });
        }

        /// <summary>
        /// Prepare a download session for the given video information and its format
        /// </summary>
        /// <param name="videoInfo">Video information</param>
        /// <param name="formatID">The format ID of the video</param>
        /// <exception cref="ArgumentNullException"><paramref name="formatID"/> is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">No format matched the <paramref name="formatID"/>.</exception>
        /// <returns></returns>
        public VideoDownloadSession PrepareVideoDownload(YoutubeVideoInfo videoInfo, string formatID)
        {
            if (string.IsNullOrWhiteSpace(formatID))
                throw new ArgumentNullException(nameof(formatID));

            if (videoInfo.Formats.Count == 0)
                throw new InvalidOperationException("The video has no available format to download.");

            return this.PrepareVideoDownload(videoInfo.Formats.First(x => string.Equals(x.FormatID, formatID)));
        }

        /// <summary>
        /// Prepare a download session for the given video format information
        /// </summary>
        /// <param name="format">The video format of <seealso cref="YoutubeVideoInfo"/></param>
        /// <returns></returns>
        public VideoDownloadSession PrepareVideoDownload(YoutubeVideoFormat format)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            return new VideoDownloadSession(this, format);
        }
    }

    public class VideoDownloadSession : IDisposable
    {
        const string Progress_DownloadText = "[download]";

        private YoutubeDL _tool;
        private int state;
        private YoutubeVideoFormat myformat;
        private CancellationTokenSource cancelSource;
        private Process proc;

        public VideoDownloadSession(YoutubeDL tool, YoutubeVideoFormat format)
        {
            this._tool = tool;
            this.state = 0;
            this.myformat = format;
            this.proc = new Process();
            this.cancelSource = new CancellationTokenSource();
        }

        public Action<VideoDownloadSession, double> ProgressChanged;
        public Action<VideoDownloadSession, DownloadCompletedEventArgs> DownloadCompleted;

        public void CancelDownload()
        {
            if (Interlocked.CompareExchange(ref this.state, 0, 1) == 1)
            {
                this.cancelSource.Cancel();
                if (!proc.HasExited)
                {
                    KillNicelyCmdProg.Experiments.StopProgramByAttachingToItsConsoleAndIssuingCtrlCEvent(proc);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outputFile">The destination to download video to.</param>
        /// <exception cref="InvalidOperationException">The download has already been started.</exception>
        /// <exception cref="System.IO.FileNotFoundException">The CLI path is not specified.</exception>
        public void StartDownload(string outputFile)
        {
            int currentState = Interlocked.CompareExchange(ref this.state, 1, 0);
            if (currentState == 0)
            {
                string youtube_tool = this._tool.YoutubeDLPath;
                if (string.IsNullOrWhiteSpace(youtube_tool))
                    throw new System.IO.FileNotFoundException();
                Task.Run(() =>
                {
                    Exception myex = null;
                    
                    string[] defaultArgs = new string[]
                    {
                        "--no-warnings",
                        "--newline",
                        "--no-part",
                        "--hls-prefer-native",
                        "--no-call-home",
                        "--format",
                        this.myformat.FormatID,
                        "--output",
                        outputFile,
                        myformat.VideoInfo.VideoHomepage.OriginalString
                    };

                    IEnumerable<string> myparams;
                    if (string.Equals(this.myformat.Protocol, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.Equals(this.myformat.Protocol, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                    {
                        myparams = defaultArgs;
                    }
                    else
                    {
                        var paramlist = new List<string>(defaultArgs.Length + 1);
                        paramlist.Add("--hls-use-mpegts");
                        myparams = paramlist;
                    }
                    proc.StartInfo = new ProcessStartInfo(youtube_tool, Helper.GetArg(myparams))
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    // Let user catch Exception
                    proc.Start();

                    try
                    {
                        string eachline = proc.StandardOutput.ReadLine();
                        int indexOfProgress_DownloadText, indexOfProgress_Percent;
                        double progressVal;

                        while (!string.IsNullOrEmpty(eachline))
                        {
                            indexOfProgress_DownloadText = eachline.IndexOf(Progress_DownloadText);
                            if (indexOfProgress_DownloadText == 0)
                            {
                                indexOfProgress_DownloadText += Progress_DownloadText.Length;
                                indexOfProgress_Percent = eachline.IndexOf('%', indexOfProgress_DownloadText);
                                if (indexOfProgress_Percent != -1)
                                {
                                    eachline = eachline.Substring(indexOfProgress_DownloadText, indexOfProgress_Percent - indexOfProgress_DownloadText);
                                    if (!string.IsNullOrWhiteSpace(eachline))
                                    {
                                        if (double.TryParse(eachline.Trim(), out progressVal))
                                        {
                                            this.ProgressChanged.BeginInvoke(this, progressVal, this.ProgressChanged.EndInvoke, null);
                                        }
                                    }
                                }
                            }
                            eachline = proc.StandardOutput.ReadLine();
                        }
                        Interlocked.Exchange(ref this.state, 2);
                    }
                    catch (Exception ex)
                    {
                        myex = ex;
                        Interlocked.Exchange(ref this.state, 0);
                    }
                    finally
                    {
                        if (!proc.HasExited)
                            proc.Kill();
                        proc.WaitForExit(5000);
                    }

                    this.DownloadCompleted.BeginInvoke(this, new DownloadCompletedEventArgs(outputFile, this.cancelSource.IsCancellationRequested, myex), this.DownloadCompleted.EndInvoke, null);
                }).ConfigureAwait(false);
            }
            else if (currentState == 1)
            {
                throw new InvalidOperationException("The download has already been started.");
            }
        }

        public void Dispose() => this.proc.Dispose();
    }

    public class DownloadCompletedEventArgs : EventArgs
    {
        public Exception Error { get; }
        public string DownloadDestination { get; }
        public bool Cancelled { get; }

        public DownloadCompletedEventArgs(string destinationFile) : this(destinationFile, false, null) { }

        public DownloadCompletedEventArgs(string destinationFile, Exception ex) : this(destinationFile, false, ex) { }

        public DownloadCompletedEventArgs(string destinationFile, bool cancelled) : this(destinationFile, cancelled, null) { }

        public DownloadCompletedEventArgs(string destinationFile, bool cancelled, Exception ex) : base()
        {
            this.Cancelled = cancelled;
            this.Error = ex;
            this.DownloadDestination = destinationFile;
        }
    }

    public class YoutubeVideoInfo
    {
        public string VideoID { get; }
        public string Title { get; }
        public string Description { get; }
        public int HighestFPS { get; }
        public string UploaderName { get; }
        public bool IsLiveStream { get; }
        public TimeSpan Duration { get; }
        public DateTimeOffset UploadDate { get; }
        public Uri VideoHomepage { get; }

        public YoutubeVideoInfo(JObject data)
        {
            this.UploaderName = data.Value<string>("uploader");
            this.Title = data.Value<string>("fulltitle");
            this.Description = data.Value<string>("description");
            this.VideoID = data.Value<string>("id");
            this.HighestFPS = data.Value<int>("fps");

            string tmp_str = data.Value<string>("webpage_url");
            if (string.IsNullOrWhiteSpace(tmp_str))
            {
                this.VideoHomepage = null;
            }
            else
            {
                this.VideoHomepage = new Uri(tmp_str);
            }

            bool? tmp_bool = data.Value<bool?>("is_live");

            this.IsLiveStream = (tmp_bool.HasValue && tmp_bool.Value);

            long seconds = data.Value<long>("duration");
            this.Duration = TimeSpan.FromSeconds(seconds);

            seconds = data.Value<long>("upload_date");
            this.UploadDate = DateTimeOffset.FromUnixTimeSeconds(seconds);

            if (data["thumbnails"] is JArray thumbnailArray)
            {
                var thumbnailList = new List<Uri>(thumbnailArray.Count);
                for (int i = 0; i < thumbnailArray.Count; i++)
                    thumbnailList.Add(new Uri(thumbnailArray[i].Value<string>("url")));

                this.Thumbnails = thumbnailList;
            }
            else
            {
                this.Thumbnails = new Uri[0];
            }

            if (data["formats"] is JArray formatArray)
            {
                var formatList = new List<YoutubeVideoFormat>(formatArray.Count);
                for (int i = 0; i < formatArray.Count; i++)
                    formatList.Add(new YoutubeVideoFormat(this, formatArray[i]));

                this.Formats = formatList;
            }
            else
            {
                this.Formats = new YoutubeVideoFormat[0];
            }
        }

        public IReadOnlyList<Uri> Thumbnails { get; }
        public IReadOnlyList<YoutubeVideoFormat> Formats { get; }
    }

    public class YoutubeVideoFormat
    {
        public string FormatName { get; }
        public string FileExtension { get; }
        public string FormatID { get; }
        public string FormatNote { get; }
        public int? AudioBirate { get; }
        public int? AudioSampleRate { get; }
        public double Bitrate { get; }

        public string VideoCodec { get; }
        public string AudioCodec { get; }

        public string Protocol { get; }

        public long? FileSize { get; }
        public int? FPS { get; }
        public System.Windows.Size VideoResolution { get; }

        public Uri DirectLink { get; }

        public YoutubeVideoInfo VideoInfo { get; }

        public YoutubeVideoFormat(YoutubeVideoInfo source, JToken data)
        {
            this.VideoInfo = source;
            this.FileExtension = data.Value<string>("ext");
            this.FormatID = data.Value<string>("format_id");
            this.FormatNote = data.Value<string>("format_note");
            this.FormatName = data.Value<string>("format");
            this.Protocol = data.Value<string>("protocol");

            string tmp_str = data.Value<string>("acodec");
            if (string.IsNullOrEmpty(tmp_str) || string.Equals(tmp_str, "none", StringComparison.OrdinalIgnoreCase))
            {
                this.AudioCodec = null;
            }
            else
            {
                this.AudioSampleRate = data.Value<int?>("asr");
                this.AudioCodec = tmp_str;
            }

            tmp_str = data.Value<string>("vcodec");
            if (string.IsNullOrEmpty(tmp_str) || string.Equals(tmp_str, "none", StringComparison.OrdinalIgnoreCase))
            {
                this.VideoResolution = new System.Windows.Size(0, 0);
                this.VideoCodec = null;
            }
            else
            {
                int? int_width = data.Value<int?>("width"),
                    int_height = data.Value<int?>("height");

                this.VideoResolution = new System.Windows.Size(int_width.HasValue ? int_width.Value : 0, int_height.HasValue ? int_height.Value : 0);

                this.VideoCodec = tmp_str;
            }
            this.FPS = data.Value<int?>("fps");
            this.Bitrate = data.Value<double>("tbr");
            this.AudioBirate = data.Value<int?>("abr");

            tmp_str = data.Value<string>("url");
            if (string.IsNullOrWhiteSpace(tmp_str))
            {
                this.DirectLink = null;
            }
            else
            {
                this.DirectLink = new Uri(tmp_str);
            }

            this.FileSize = data.Value<long?>("filesize");
        }
    }
}
