using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour DetachedBox.xaml
    /// </summary>
    public partial class DetachedBox : Window
    {
        public RoutingBox RoutingBox { get; set; }

        public DetachedBox(RoutingBox box)
        {
            RoutingBox = box;
            InitializeComponent();
            EncapsulateRoutingBox();
        }

        internal void EncapsulateRoutingBox()
        {
            this.Title = string.Concat(RoutingBox.BoxName, " [", RoutingBox.BoxGuid.ToString(), "]");
            Border border = new Border();
            border.BorderBrush = Brushes.Gray;
            border.BorderThickness = new Thickness(1);

            Grid.SetRow(border, 0);
            Grid.SetColumn(border, 0);
            gdRoutingBox.Children.Add(border);

            Frame frame = new Frame
            {
                Name = string.Concat("frmBox", 0, "x", 0),
                Tag = ""
            };

            gdRoutingBox.Children.Add(frame);
            Grid.SetRow(frame, 0);
            Grid.SetColumn(frame, 0);

            frame.Tag = RoutingBox.BoxGuid.ToString();
            frame.Navigate(RoutingBox);
            //gdRoutingBox.Children.Add(rtb);
        }

        private void Window_Closed(object sender, EventArgs e)
        {

        }
    }
}
