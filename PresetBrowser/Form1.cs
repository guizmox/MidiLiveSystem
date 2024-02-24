using MidiTools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows.Forms;

namespace PresetBrowser
{
    public partial class Form1 : Form
    {
        private readonly int[] blackKeyIndices = { 1, 3, 6, 8, 10 };
        private System.Timers.Timer Clock;

        MidiRouting Routing = new MidiRouting();
        InstrumentData Instrument = null;
        NoteGenerator Note = new NoteGenerator(1, 1, 1, 1);
        private Guid RoutingGuid;

        public Form1()
        {

            InitializeComponent();
            CreateKeyboard();

            foreach (var device in MidiRouting.InputDevices)
            {
                cbMidiIN.Items.Add(device.Name);
            }


            foreach (var device in MidiRouting.OutputDevices)
            {
                cbMidiOUT.Items.Add(device.Name);
            }

            MidiRouting.NewLog += Event_MidiLog;

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Event_UIClock;
            Clock.Interval = 1000;
            Clock.Start();

        }

        private void Event_UIClock(object sender, ElapsedEventArgs e)
        {
            if (Routing != null)
            {
                MethodInvoker methodInvokerDelegate = delegate ()
                {
                    lbMidiInfo.Text = Routing.CyclesInfo; 
                };

                //This will be true if Current thread is not UI thread.
                if (this.InvokeRequired)
                    this.Invoke(methodInvokerDelegate);
                else
                    methodInvokerDelegate();
            }
        }

        private void Event_MidiLog(string sDevice, bool bIn, string e)
        {
            MethodInvoker methodInvokerDelegate = delegate ()
            {
                if (rtbMonitor.Lines.Length > 100)
                {
                    rtbMonitor.Clear();
                }
                rtbMonitor.AppendText(e + Environment.NewLine);
                rtbMonitor.ScrollToCaret();
            };
            //This will be true if Current thread is not UI thread.
            if (this.InvokeRequired)
                this.Invoke(methodInvokerDelegate);
            else
                methodInvokerDelegate();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Clock.Stop();

            if (Routing != null)
            {
                try
                {
                    Routing.DeleteAllRouting();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void Keyboard_KeyUp(object sender, EventArgs e)
        {
            PictureBox key = sender as PictureBox;
            string noteName = key.Tag.ToString();
            Note.SetNote(noteName);
            Note.SetVelocity(tbVelocity.Text);

            MethodInvoker methodInvokerDelegate2 = delegate ()
            {
                key.BackColor = IsBlackKey(Convert.ToInt32(noteName)) ? Color.Black : Color.White;
            };
            if (this.InvokeRequired)
                this.Invoke(methodInvokerDelegate2);
            else
                methodInvokerDelegate2();

            if (cbMidiOUT.SelectedItem != null)
            {
                PlayNote(false, cbMidiOUT.SelectedItem.ToString());
            }
        }

        private void Keyboard_KeyDown(object sender, EventArgs e)
        {
            PictureBox key = sender as PictureBox;
            string noteName = key.Tag.ToString();
            Note.SetNote(noteName);
            Note.SetVelocity(tbVelocity.Text);

            MethodInvoker methodInvokerDelegate = delegate ()
            {
                key.BackColor = Color.Red;
            };
            if (this.InvokeRequired)
                this.Invoke(methodInvokerDelegate);
            else
                methodInvokerDelegate();

            if (cbMidiOUT.SelectedItem != null)
            {
                PlayNote(true, cbMidiOUT.SelectedItem.ToString());
            }
        }

        private void tbChannelMidiIN_TextChanged(object sender, EventArgs e)
        {
            ChangeRouting();
        }

        private void tbChannelMidiOUT_TextChanged(object sender, EventArgs e)
        {
            ChangeRouting();
        }

        private void btnOctaveMoins_Click(object sender, EventArgs e)
        {
            Note.ChangeOctave(-1);
        }

        private void btnOctavePlus_Click(object sender, EventArgs e)
        {
            Note.ChangeOctave(1);
        }

        private void tbVelocity_TextChanged(object sender, EventArgs e)
        {
            Note.SetVelocity(tbVelocity.Text);
        }

        private void cbMidiIN_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbMidiIN.SelectedItem != null)
            {
                ChangeRouting();
            }
        }

        private void cbMidiOUT_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbMidiOUT.SelectedItem != null)
            {
                ChangeRouting();
            }
        }

        private void tVPresets_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string idx = e.Node.Name;
            ChangePreset(idx);
        }

        private void tVPresets_KeyUp(object sender, KeyEventArgs e)
        {
            TreeView tv = (TreeView)sender;
            if (tv != null)
            {
                if (e.KeyCode == Keys.Down)
                {
                    string idx = tv.SelectedNode.Name;
                    ChangePreset(idx);
                }
                else if (e.KeyCode == Keys.Up)
                {
                    string idx = tv.SelectedNode.Name;
                    ChangePreset(idx);
                }
            }
        }

        private void ckSortByBank_CheckedChanged(object sender, EventArgs e)
        {
            if (Instrument != null)
            {
                if (ckSortByBank.Checked)
                { Instrument = Instrument.Sort(true); }
                else
                { Instrument = Instrument.Sort(false); }

                PopulateHierarchyTree();
            }
        }

        private void BtnOpenCubaseFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "(*.txt)|*.txt";
            openFileDialog1.FileName = "";
            openFileDialog1.Title = "Choose a Cubase Instrument File";
            openFileDialog1.ShowDialog();
            string sFile = openFileDialog1.FileName;

            if (File.Exists(sFile))
            {
                Instrument = new InstrumentData(sFile);
                if (Instrument.Device == "")
                {
                    MessageBox.Show("Not a valid Cubase Instrument File.");
                }
                else
                {
                    if (ckSortByBank.Checked)
                    { Instrument = Instrument.Sort(true); }
                    PopulateHierarchyTree();
                }
            }
        }

        private void ChangePreset(string idx)
        {
            if (idx.Length > 0)
            {
                MidiPreset mp = Instrument.GetPreset(idx);
                lbProgram.Text = mp.Tag;

                int iChannel = 0;
                if (int.TryParse(tbChannelMidiOUT.Text, out iChannel))
                {
                    if (cbMidiOUT.SelectedItem != null)
                    {
                        mp.Channel = iChannel;
                        SendMIDIProgramChange(mp, cbMidiOUT.SelectedItem.ToString());
                    }
                    
                }
                else
                {
                    MessageBox.Show("Wrong MIDI OUT channel (" + tbChannelMidiOUT.Text + ") - Expecting value in 1-16 range");
                }
            }
        }

        private void PopulateHierarchyTree()
        {
            tVPresets.Nodes.Clear();

            int iNodesL1 = 0;
            int iNodesL2 = 0;
            int iNodesL3 = 0;
            int iNodesL4 = 0;

            foreach (var g in Instrument.Categories)
            {
                TreeNode tn = new TreeNode(g.Category);

                if (g.Presets.Count > 0)
                {
                    foreach (var p in g.Presets)
                    {
                        tn.Nodes.Add(p.Id, p.PresetName);
                    }
                }
                switch (g.Level)
                {
                    case 1:
                        tVPresets.Nodes.Add(tn);
                        break;
                    case 2:
                        tVPresets.Nodes[iNodesL1 - 1].Nodes.Add(tn);
                        break;
                    case 3:
                        tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes.Add(tn);
                        break;
                    case 4:
                        tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes[iNodesL3 - 1].Nodes.Add(tn);
                        break;
                    case 5:
                        tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes[iNodesL3 - 1].Nodes[iNodesL4 - 1].Nodes.Add(tn);
                        break;
                }
                iNodesL1 = tVPresets.Nodes.Count;
                iNodesL2 = tVPresets.Nodes.Count > 0 ? tVPresets.Nodes[iNodesL1 - 1].Nodes.Count : 0;
                iNodesL3 = tVPresets.Nodes.Count > 0 && tVPresets.Nodes[iNodesL1 - 1].Nodes.Count > 0 ? tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes.Count : 0;
                iNodesL4 = tVPresets.Nodes.Count > 0 && tVPresets.Nodes[iNodesL1 - 1].Nodes.Count > 0 && tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes.Count > 0 ? tVPresets.Nodes[iNodesL1 - 1].Nodes[iNodesL2 - 1].Nodes[iNodesL3 - 1].Nodes.Count : 0;
            }
        }

        private void ChangeRouting()
        {
            int iChIN = 0;
            int iChOUT = 0;
            if (int.TryParse(tbChannelMidiIN.Text.Trim(), out iChIN) && int.TryParse(tbChannelMidiOUT.Text.Trim(), out iChOUT))
            {
                try
                {
                    if (RoutingGuid != null)
                    {
                        Routing.DeleteRouting(RoutingGuid);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while removing Routing Data : " + ex.Message);
                }

                try
                {
                    RoutingGuid = Routing.AddRouting(cbMidiIN.SelectedItem == null ? "" : cbMidiIN.SelectedItem.ToString(), cbMidiOUT.SelectedItem == null ? "" : cbMidiOUT.SelectedItem.ToString(), iChIN, iChOUT, new MidiOptions());

                    if (RoutingGuid == Guid.Empty)
                    {
                        MessageBox.Show("Wrong Routing ! Expecting 1-16 values.");
                    }
                    else
                    {
                        Routing.InitRouting(RoutingGuid, new MidiOptions());
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while changing Routing Configuration : " + ex.Message);
                }

            }
        }

        private void PlayNote(bool bOn, string sDevice)
        {
            try
            {
                if (bOn)
                {
                    Routing.SendNote(RoutingGuid, Note, true, sDevice);
                }
                else
                {
                    Routing.SendNote(RoutingGuid, Note, false, sDevice);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to communicate with MIDI OUT device : " + ex.Message);
            }
        }

        private void SendMIDIProgramChange(MidiPreset mp, string sDevice)
        {
            try
            {
                Routing.SendProgramChange(RoutingGuid, mp);
            }
            catch (Exception ex) { MessageBox.Show("Unable to open MIDI port : " + ex.Message); }
        }

        private void CreateKeyboard()
        {
            int numberOfKeys = 3 * 12; // 3 octaves
            int whiteKeyHeigth = 100;
            int blackKeyHeigth = 60;
            int keyWidth = 30;
            int startingY = 10;
            int xPosition = 10;

            List<PictureBox> pbKeyx = new List<PictureBox>();

            for (int i = 0; i < numberOfKeys; i++)
            {
                PictureBox key = new PictureBox();
                key.Top = startingY;

                if (IsBlackKey(i % 12))
                {
                    key.Width = Convert.ToInt32(keyWidth * 0.6);
                    key.BorderStyle = BorderStyle.FixedSingle;
                    key.Height = blackKeyHeigth;
                    key.Left = Convert.ToInt32(xPosition - 10);
                    key.BackColor = Color.Black;
                }
                else
                {
                    key.Width = keyWidth;
                    key.BorderStyle = BorderStyle.FixedSingle;
                    key.Height = whiteKeyHeigth;
                    key.Left = xPosition;
                    key.BackColor = Color.White;

                    xPosition += keyWidth;
                }

                // Assigning note name to the PictureBox's Tag property
                key.Tag = GetNoteName(i);

                // Event handler for mouse click
                key.MouseDown += Keyboard_KeyDown;
                key.MouseUp += Keyboard_KeyUp;

                pbKeyx.Add(key);
            }

            foreach (var note in pbKeyx.Where(p => IsBlackKey(Convert.ToInt32(p.Tag) % 12)))
            {
                pnlKeys.Controls.Add(note);
            }

            foreach (var note in pbKeyx.Where(p => !IsBlackKey(Convert.ToInt32(p.Tag) % 12)))
            {
                pnlKeys.Controls.Add(note);
            }
        }

        private bool IsBlackKey(int keyIndex)
        {
            return Array.IndexOf(blackKeyIndices, keyIndex % 12) != -1;
        }

        private string GetNoteName(int keyIndex)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octaveSize = 12;
            int octave = keyIndex / octaveSize;
            int noteIndex = keyIndex % octaveSize;

            return (keyIndex).ToString();
            //return noteNames[noteIndex] + octave;
        }

    }


}

