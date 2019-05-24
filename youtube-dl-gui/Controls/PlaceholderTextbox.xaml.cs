using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace youtube_dl_gui.Controls
{
    /// <summary>
    /// Interaction logic for PlaceholderTextbox.xaml
    /// </summary>
    public partial class PlaceholderTextbox : TextBox
    {
        public static readonly DependencyProperty PlaceholderColorProperty = DependencyProperty.Register("PlaceholderColor", typeof(Brush), typeof(PlaceholderTextbox), new PropertyMetadata((new Func<Brush>(() => { var result = new SolidColorBrush(Color.FromRgb(230, 230, 230)); result.Freeze(); return result; }))()));

        public string PlaceholderColor
        {
            get => (string)this.GetValue(PlaceholderColorProperty);
            set => this.SetValue(PlaceholderColorProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(PlaceholderTextbox), new PropertyMetadata(string.Empty));

        public string Placeholder
        {
            get => (string)this.GetValue(PlaceholderProperty);
            set => this.SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextAlignmentProperty = DependencyProperty.Register("PlaceholderTextAlignment", typeof(TextAlignment), typeof(PlaceholderTextbox), new PropertyMetadata(TextAlignment.Center));

        public TextAlignment PlaceholderTextAlignment
        {
            get => (TextAlignment)this.GetValue(PlaceholderTextAlignmentProperty);
            set => this.SetValue(PlaceholderTextAlignmentProperty, value);
        }

        public PlaceholderTextbox() : base()
        {
            this.InitializeComponent();
        }
    }
}
