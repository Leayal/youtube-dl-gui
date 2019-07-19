using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using youtube_dl_gui.Youtube;

namespace youtube_dl_gui.Controls
{
    /// <summary>
    /// Interaction logic for YoutubeVideoFormatViewModel.xaml
    /// </summary>
    public partial class YoutubeVideoFormatViewModel : UserControl
    {
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(YoutubeVideoFormatViewModel), new PropertyMetadata(false));
        public bool IsSelected
        {
            get => (bool)this.GetValue(IsSelectedProperty);
            set => this.SetValue(IsSelectedProperty, value);
        }

        public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent("Selected", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(YoutubeVideoFormatViewModel)); //CheckBox.UncheckedEvent;
        public IList<YoutubeVideoFormat> VideoFormats { get; }

        public string Codec { get; }

        private ListCollectionView qualityList;

        public YoutubeVideoFormatViewModel(string codecString)
        {
            this.qualityList = new ListCollectionView(new ObservableCollection<YoutubeVideoFormatDataContext>());
            this.qualityList.CustomSort = QualityPriorityComparer.Default;
            this.Codec = codecString;

            this.InitializeComponent();

            this.qualitySelection.ItemsSource = this.qualityList;
            // this.VideoFormats = this.qualityList;
        }

        public void AddFormat(YoutubeVideoFormat format)
        {
            this.qualityList.Contains(format);
        }

        static string GetFriendlyVideoFormatExtension(string formatString)
        {
            if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
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
            else if (formatString.StartsWith("av01", StringComparison.OrdinalIgnoreCase))
            {
                return "AV1";
            }
            else
            {
                return formatString.FirstLetterToUpper();
            }
        }

        static string GetFriendlyAudioFormatExtension(string formatString)
        {
            if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return "Opus";
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return "Vorbis";
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return "M4A";
            }
            else
            {
                return formatString.FirstLetterToUpper();
            }
        }

        public event RoutedEventHandler Selected
        {
            add => this.AddHandler(SelectedEvent, value);
            remove => this.RemoveHandler(SelectedEvent, value);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
            if (e.ChangedButton == MouseButton.Left)
            {
                this.OnSelected();
            }
        }

        protected void OnSelected()
        {
            this.RaiseEvent(new RoutedEventArgs(SelectedEvent));
        }

        class YoutubeVideoFormatDataContext
        {
            public YoutubeVideoFormat Format { get; }

            public YoutubeVideoFormatDataContext(YoutubeVideoFormat format)
            {
                this.Format = format;
            }

            public override bool Equals(object obj)
            {
                if (obj is YoutubeVideoFormatDataContext dataContext)
                    return this.Format.Equals(dataContext.Format);
                else
                    return false;
            }

            public override int GetHashCode()
            {
                return this.Format.GetHashCode();
            }

            public override string ToString()
            {
                return base.ToString();
            }
        }

        class QualityPriorityComparer : IComparer, IComparer<YoutubeVideoFormatDataContext>
        {
            public static readonly QualityPriorityComparer Default = new QualityPriorityComparer();

            private QualityPriorityComparer() { }
            public int Compare(object x, object y)
            {
                if (x is YoutubeVideoFormatDataContext xGo && y is YoutubeVideoFormatDataContext yGo)
                {
                    return this.Compare(xGo, yGo);
                }
                else
                {
                    return 0;
                }
            }

            public int Compare(YoutubeVideoFormatDataContext x, YoutubeVideoFormatDataContext y) => YoutubeVideoFormatComparer.Revert.Compare(x.Format, y.Format);
        }
    }
}
