using MidiTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    /// Logique d'interaction pour RecallButtons.xaml
    /// </summary>
    public partial class RecallButtons : Window
    {
        internal class RecallMemory
        {
            internal List<Guid> BoxGuids = new List<Guid>();
            internal List<int> BoxPresets = new List<int>();
            internal int CurrentPreset;
        }

        private List<RoutingBox> Boxes;
        private ProjectConfiguration Project;
        private int CurrentPreset = 0;
        private RecallMemory[] Memory = new RecallMemory[8];

        public RecallButtons(List<RoutingBox> boxes, ProjectConfiguration project)
        {
            Boxes = boxes;
            Project = project;
            InitializeComponent();
            GetRecallFromProject();
        }

        private void GetRecallFromProject()
        {
            foreach (var item in Project.RecallData)
            {
                Memory[item.ButtonIndex] = new RecallMemory { CurrentPreset = item.ButtonIndex, BoxGuids = item.BoxGuids, BoxPresets = item.BoxPresets };
            }
        }

        private async void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            Button btnPreset = (sender) as Button;

            await PresetChange(btnPreset);
        }

        private async Task PresetChange(Button btnPreset)
        {
            await btnPreset.Dispatcher.InvokeAsync(() =>
            {
                CurrentPreset = Convert.ToInt32(btnPreset.Tag) - 1;

                switch (btnPreset.Name)
                {
                    case "btnRecall1":
                        btnRecall1.Background = Brushes.IndianRed;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall2":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.IndianRed;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall3":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.IndianRed;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall4":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.IndianRed;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall5":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.IndianRed;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall6":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.IndianRed;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall7":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.IndianRed;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall8":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.IndianRed;
                        break;
                }
            });

            int iPreset = CurrentPreset;

            if (Memory[iPreset] != null)
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < Memory[iPreset].BoxGuids.Count; i++)
                {
                    int item = i;
                    tasks.Add(EventPool.AddTask(async () => await ProcessBox(Memory[iPreset].BoxGuids[item], Memory[iPreset].BoxPresets[item])));
                }

                await Task.WhenAll(tasks);
            }
        }

        private async Task ProcessBox(Guid boxguid, int preset)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == boxguid);
            if (box != null)
            {
                await box.ChangePreset(preset);
            }
        }

        public void SaveRecallsToProject()
        {
            Project.RecallData.Clear();

            for (int i = 1; i <= 8; i++)
            {
                if (Memory[i - 1] != null)
                {
                    Project.RecallData.Add(new ProjectConfiguration.RecallConfiguration(Memory[i - 1].BoxGuids, Memory[i - 1].BoxPresets, "RECALL " + i, Memory[i - 1].CurrentPreset));
                }
            }
        }

        internal async Task SetButton(bool bNote, int iInitialValue, int iValue)
        {
            if (bNote)
            {
                if (iValue == iInitialValue) { await PresetChange(btnRecall1); }
                else if (iValue == iInitialValue + 1) { await PresetChange(btnRecall2); }
                else if (iValue == iInitialValue + 2) { await PresetChange(btnRecall3); }
                else if (iValue == iInitialValue + 3) { await PresetChange(btnRecall4); }
                else if (iValue == iInitialValue + 4) { await PresetChange(btnRecall5); }
                else if (iValue == iInitialValue + 5) { await PresetChange(btnRecall6); }
                else if (iValue == iInitialValue + 6) { await PresetChange(btnRecall7); }
                else if (iValue == iInitialValue + 7) { await PresetChange(btnRecall8); }
            }
            else
            {
                if (iValue == 0) { await PresetChange(btnRecall1); }
                else if (iValue == 1) { await PresetChange(btnRecall2); }
                else if (iValue == 2) { await PresetChange(btnRecall3); }
                else if (iValue == 3) { await PresetChange(btnRecall4); }
                else if (iValue == 4) { await PresetChange(btnRecall5); }
                else if (iValue == 5) { await PresetChange(btnRecall6); }
                else if (iValue == 6) { await PresetChange(btnRecall7); }
                else if (iValue == 7) { await PresetChange(btnRecall8); }
            }
        }

        private void btnSaveUIState_Click(object sender, RoutedEventArgs e)
        {
            int iButton = Convert.ToInt32(((Button)sender).Tag) - 1;

            List<Guid> boxguids = new List<Guid>();
            List<int> boxpresets = new List<int>();
            foreach (var box in Boxes) 
            {
                boxguids.Add(box.BoxGuid);
                boxpresets.Add(box.CurrentPreset);
            }

            Memory[iButton] = new RecallMemory { CurrentPreset = iButton, BoxGuids = boxguids, BoxPresets = boxpresets };
        }
    }
}
