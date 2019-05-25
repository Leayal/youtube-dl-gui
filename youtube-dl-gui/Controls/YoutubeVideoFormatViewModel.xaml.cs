using System;
using System.Collections.Generic;
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
        public YoutubeVideoFormat VideoFormat { get; }

        public YoutubeVideoFormatViewModel(YoutubeVideoFormat format)
        {
            this.VideoFormat = format;
            this.DataContext = format;
            this.InitializeComponent();
        }
    }
}
