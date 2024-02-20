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
    /// Logique d'interaction pour MidiLog.xaml
    /// </summary>
    public partial class MidiLog : Window
    {
        public MidiLog()
        {
            InitializeComponent();
        }

        internal void AddLog(string sLog)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
            {
                Paragraph paragraph = new Paragraph(new Run(sLog));
                paragraph.LineHeight = 5;
                rtbMidiLog.Document.Blocks.Add(paragraph);
                if (rtbMidiLog.Document.Blocks.Count > 100)
                {
                    rtbMidiLog.Document.Blocks.Clear();
                }
                rtbMidiLog.ScrollToEnd();
            });
            }
            else
            {
                Paragraph paragraph = new Paragraph(new Run(sLog));
                paragraph.LineHeight = 5;
                rtbMidiLog.Document.Blocks.Add(paragraph);
                if (rtbMidiLog.Document.Blocks.Count > 100)
                {
                    rtbMidiLog.Document.Blocks.Clear();
                }
                rtbMidiLog.ScrollToEnd();
            }
        }
    }
}
