using MidiTools;
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
    /// Logique d'interaction pour EditValue.xaml
    /// </summary>
    public partial class EditValue : Window
    {
        private readonly SequenceStep Step;
        private readonly List<TextBox> tbNotes = new();
        private readonly List<TextBox> tbVelos = new();

        public EditValue(SequenceStep step, string sText)
        {
            InitializeComponent();
            Step = step;
            InitPage(step, sText);
        }

        private void InitPage(SequenceStep step, string sText)
        {
            lbPrompt.Content = sText;

            for (int i = 0; i < step.NotesAndVelocity.Count; i++)
            {
                RowDefinition rowDefinition = new()
                {
                    Height = new GridLength((1 / step.NotesAndVelocity.Count), GridUnitType.Star)
                };
                gdControls.RowDefinitions.Add(rowDefinition);

                //label note
                Label lblNote = new() { Content = "Note (" + Tools.MidiNoteNumberToNoteName(step.NotesAndVelocity[i][0]) + ") : ", Foreground = Brushes.White };
                Grid.SetColumn(lblNote, 0);
                Grid.SetRow(lblNote, i);
                gdControls.Children.Add(lblNote);
                //texbox note
                TextBox tbNote = new() { HorizontalAlignment = HorizontalAlignment.Left, Name = "tbNote_" + i, MaxLength=3, Text = step.NotesAndVelocity[i][0].ToString() };
                Grid.SetColumn(tbNote, 1);
                Grid.SetRow(tbNote, i);
                gdControls.Children.Add(tbNote);
                tbNotes.Add(tbNote);

                //label velocity
                Label lblVelo = new() { Content = "Velocity : ", Foreground=Brushes.White };
                Grid.SetColumn(lblVelo, 2);
                Grid.SetRow(lblVelo, i);
                gdControls.Children.Add(lblVelo);

                //textbox velocity
                TextBox tbVelo = new() { HorizontalAlignment = HorizontalAlignment.Left, Name = "tbVelo_" + i, MaxLength=3, Text = step.NotesAndVelocity[i][1].ToString() };
                Grid.SetColumn(tbVelo, 3);
                Grid.SetRow(tbVelo, i);
                gdControls.Children.Add(tbVelo);
                tbVelos.Add(tbVelo);
            }
        }

        internal SequenceStep GetStep()
        {
            List<int[]> notesAndVelo = new();

            for (int i = 0; i < tbNotes.Count; i++)
            {
                int iNote;
                int.TryParse(tbNotes[i].Text, out iNote);
                int iVelo;
                int.TryParse(tbVelos[i].Text, out iVelo);

                if (iNote >= 0 && iNote <= 127)
                { }
                else { iNote = Step.NotesAndVelocity[i][0]; }
                if (iVelo >= 0 && iVelo <= 127)
                { }
                else { iVelo = Step.NotesAndVelocity[i][1]; }

                notesAndVelo.Add(new int[2] { iNote, iVelo  });
            }

            SequenceStep newStep = new(Step.Step, Step.GatePercent, Step.StepCount, notesAndVelo);

            return newStep;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
