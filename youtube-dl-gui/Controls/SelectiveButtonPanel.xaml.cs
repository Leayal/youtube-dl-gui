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
    public enum ContainerType
    {
        Audio = 0,
        Video = 1,
        All = 2
    }

    /// <summary>
    /// Interaction logic for SelectiveButtonPanel.xaml
    /// </summary>
    public partial class SelectiveButtonPanel : UserControl
    {
        private static readonly DependencyPropertyKey ContainerTypeProperty = DependencyProperty.RegisterReadOnly("ContainerType", typeof(ContainerType), typeof(SelectiveButtonPanel), new PropertyMetadata(ContainerType.All));

        public ContainerType ContainerType => (ContainerType)this.GetValue(ContainerTypeProperty.DependencyProperty);

        private Dictionary<YoutubeVideoFormat, YoutubeVideoFormatViewModel> itemList;

        public SelectiveButtonPanel()
        {
            this.itemList = new Dictionary<YoutubeVideoFormat, YoutubeVideoFormatViewModel>();
            this.InitializeComponent();
        }

        internal void Add(YoutubeVideoFormat item)
        {
            if (this.itemList.ContainsKey(item))
            {
                throw new ArgumentException("Item has already been added.", nameof(item));
            }
            // this.itemList.Add
            if (this.Content is WrapPanel wrapPanel)
            {
                YoutubeVideoFormatViewModel viewmodel = new YoutubeVideoFormatViewModel(item.FormatID);
                this.itemList.Add(item, viewmodel);
                wrapPanel.Children.Add(viewmodel);
            }
        }

        internal void Remove(YoutubeVideoFormat item)
        {

        }
    }
}
