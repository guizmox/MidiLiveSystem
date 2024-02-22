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
    /// Logique d'interaction pour Keyboard.xaml
    /// </summary>
    public partial class Keyboard : Window
    {
        public delegate void KeyboardInput(string sKey);
        public static event KeyboardInput KeyPressed;

        private bool IsMaj = false;

        public Keyboard()
        {
            InitializeComponent();
        }

        private void KeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            var button = ((Button)sender).Tag.ToString();      
            
            if (!IsMaj)
            { button = button.ToLower(); }
            else
            { button = button.ToUpper(); }

            KeyPressed?.Invoke(button);
        }

        private void btnMaj_Click(object sender, RoutedEventArgs e)
        {
            var button = ((Button)sender);

            if (button.Background == Brushes.DarkGray) 
            { 
                button.Background = Brushes.IndianRed; 
                IsMaj = true; 
                button.Content = "MAJ"; 
            }
            else
            {
                button.Background = Brushes.DarkGray;
                IsMaj = false; 
                button.Content = "MIN"; 
            }
        }
    }
}
