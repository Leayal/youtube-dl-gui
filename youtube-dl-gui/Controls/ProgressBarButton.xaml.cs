using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace youtube_dl_gui.Controls
{
    /// <summary>
    /// Interaction logic for Sample.xaml
    /// </summary>
    public partial class ProgressBarButton : Border
    {

        public event RoutedEventHandler Click
        {
            add => this.AddHandler(Button.ClickEvent, value);
            remove => this.RemoveHandler(Button.ClickEvent, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ProgressBarButton), new PropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)this.GetValue(TextProperty);
            set => this.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(ProgressBarButton), new PropertyMetadata(TextAlignment.Center));

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)this.GetValue(TextAlignmentProperty);
            set => this.SetValue(TextAlignmentProperty, value);
        }

        public ProgressBarButton() : base()
        {
            this.goforItLMB = false;
            this.InitializeComponent();
        }

        private bool goforItLMB;
        private void ButtonCancelDownload_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.goforItLMB = true;
            }
            else if (this.goforItLMB)
            {
                this.goforItLMB = false;
                this.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.Register("ProgressValue", typeof(double), typeof(ProgressBarButton), new PropertyMetadata(0d));

        public double ProgressValue
        {
            get => (double)this.GetValue(ProgressValueProperty);
            set => this.SetValue(ProgressValueProperty, value);
        }

        public static readonly DependencyProperty ProgressMaximumProperty = DependencyProperty.Register("ProgressMaximum", typeof(double), typeof(ProgressBarButton), new PropertyMetadata(100d));

        public double ProgressMaximum
        {
            get => (double)this.GetValue(ProgressMaximumProperty);
            set => this.SetValue(ProgressMaximumProperty, value);
        }
    }
}
