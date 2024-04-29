using MaterialDesignColors.ColorManipulation;
using MidiTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour PresetBrowser.xaml
    /// </summary>
    public partial class PresetBrowser : Window
    {
        private bool ItemFound = false;
        private string CurrentPreset = "";
        public delegate void BrowserPresetChanged(MidiPreset mp);
        public event BrowserPresetChanged OnPresetChanged;

        private readonly InstrumentData InstrumentPresets;

        public PresetBrowser(InstrumentData instr, MidiPreset preset = null)
        {
            InitializeComponent();

            InstrumentPresets = instr;

            if (instr != null)
            {
                PopulateHierarchyTree(instr);
            }

            lblCaption.Content = "Preset Browser";

            if (preset != null && preset.PresetName.Length > 0)
            {
                tbLsb.Text = preset.Lsb.ToString();
                tbMsb.Text = preset.Msb.ToString();
                tbPrg.Text = preset.Prg.ToString();
                tbName.Text = preset.PresetName;

                SearchPreset(tvPresets.Items, preset);

            }
        }

        private void SearchPreset(ItemCollection items, MidiPreset preset)
        {
            if (items.Count > 0)
            {
                foreach (TreeViewItem item in items)
                {
                    item.IsExpanded = true;
                    SearchPreset(item.Items, preset);

                    if (ItemFound) 
                    { 
                        break; 
                    }
                    else
                    {
                        item.IsExpanded = false;
                        if (item.ToolTip != null && item.ToolTip.ToString().Equals(preset.Id))
                        {
                            ItemFound = true;
                            item.IsSelected = true;
                        }
                    }
                }
            }
        }

        private void tvPresets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                TreeViewItem tvi = (TreeViewItem)e.NewValue;
                if (tvi != null)
                {
                    if (tvi.Tag.ToString().IndexOf("-") > -1) //c'est une catégorie
                    {
                        string idx = tvi.Tag.ToString();
                        ChangePreset(idx);
                    }
                }
            }
            catch
            {

            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void tbPrg_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (InstrumentPresets != null)
            {
                if (tbPrg != null && tbMsb != null & tbLsb != null)
                {
                    if (tbPrg.IsFocused || tbMsb.IsFocused || tbLsb.IsFocused)
                    {
                        FilterTreeViewByPrg(tbPrg.Text.Trim(), tbMsb.Text.Trim(), tbLsb.Text.Trim());
                    }
                }
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTreeViewByString(tbFilterTextBox.Text);
        }

        private void PopulateHierarchyTree(InstrumentData instr)
        {
            tvPresets.Items.Clear();

            string sL1 = "";
            string sL2 = "";
            string sL3 = "";
            string sL4 = "";

            TreeViewItem catFav = new()
            {
                Header = "- FAVOURITES -",
                Tag = "0",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.IndianRed
            };

            foreach (var g in instr.Categories)
            {
                foreach (var p in g.Presets)
                {
                    if (p.IsFavourite)
                    {
                        TreeViewItem presetItem = new()
                        {
                            Header = p.PresetName,
                            Tag = p.Id,
                            FontWeight = FontWeights.Normal,
                            Foreground = Brushes.Black,
                        };
                        catFav.Items.Add(presetItem);
                    }
                }
            }
            tvPresets.Items.Add(catFav);

            foreach (var g in instr.Categories)
            {

                TreeViewItem categoryItem = new()
                {
                    Header = g.Category,
                    Tag = g.IndexInFile.ToString(),
                    FontWeight = FontWeights.Normal,
                    Foreground = Brushes.DarkBlue
                };

                //if (g.Category.Equals("Orchestral Woodwinds"))
                //{

                //}

                if (g.Presets.Count > 0)
                {
                    foreach (var p in g.Presets)
                    {
                        TreeViewItem presetItem = new()
                        {
                            Header = p.PresetName,
                            Tag = p.Id,
                            FontWeight = FontWeights.Normal,
                            Foreground = Brushes.Black,
                            ToolTip = string.Concat(p.Prg, "-", p.Msb, "-", p.Lsb)
                        };
                        categoryItem.Items.Add(presetItem);
                    }
                }

                // Déterminez le niveau de la catégorie et ajoutez-la au bon parent
                switch (g.Level)
                {
                    case 1:
                        sL1 = g.IndexInFile.ToString();
                        sL2 = "";
                        sL3 = "";
                        sL4 = "";
                        tvPresets.Items.Add(categoryItem);
                        break;
                    case 2:
                        sL2 = g.IndexInFile.ToString();
                        sL3 = "";
                        sL4 = "";
                        FindParentNode(tvPresets, sL1).Items.Add(categoryItem);
                        break;
                    case 3:
                        sL3 = g.IndexInFile.ToString();
                        sL4 = "";
                        FindParentNode(tvPresets, sL2).Items.Add(categoryItem);
                        break;
                    case 4:
                        sL4 = g.IndexInFile.ToString();
                        FindParentNode(tvPresets, sL3).Items.Add(categoryItem);
                        break;
                    case 5:
                        FindParentNode(tvPresets, sL4).Items.Add(categoryItem);
                        break;
                        // Ajoutez d'autres cas selon votre hiérarchie
                }
            }
        }

        private static TreeViewItem FindParentNode(ItemsControl parent, string categoryIndexInFile)
        {
            foreach (var item in parent.Items)
            {
                TreeViewItem treeItem = item as TreeViewItem;
                if (treeItem.Tag.ToString() == categoryIndexInFile)
                {
                    return treeItem;
                }
                else
                {
                    var result = FindParentNode(treeItem, categoryIndexInFile);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        private void ChangePreset(string idx)
        {
            CurrentPreset = idx;

            if (idx.Length > 0)
            {
                MidiPreset mp = InstrumentPresets.GetPreset(idx);
                lblPresetInfo.Content = Path.GetFileName(InstrumentPresets.CubaseFile);
                tbMsb.Text = mp.Msb.ToString();
                tbLsb.Text = mp.Lsb.ToString();
                tbPrg.Text = mp.Prg.ToString();
                tbName.Text = mp.PresetName.ToString();
                tbSysex.Text = mp.SysEx;
                ckSetFavorite.IsChecked = mp.IsFavourite;
                OnPresetChanged?.Invoke(mp);
            }
        }

        private void FilterTreeViewByPrg(string sPrg, string sMsb, string sLsb)
        {
            var filter = new InstrumentData
            {
                CubaseFile = InstrumentPresets.CubaseFile,
                Device = InstrumentPresets.Device
            };
            foreach (PresetHierarchy category in InstrumentPresets.Categories)
            {
                PresetHierarchy cat = new()
                {
                    IndexInFile = category.IndexInFile,
                    Category = category.Category,
                    Level = 1,
                    Raw = category.Raw,
                    Presets = new List<MidiPreset>()
                };

                foreach (var preset in category.Presets)
                {
                    if ((preset.Prg.ToString().Equals(sPrg, StringComparison.InvariantCultureIgnoreCase) || sPrg.Length == 0) &&
                        (preset.Msb.ToString().Equals(sMsb, StringComparison.InvariantCultureIgnoreCase) || sMsb.Length == 0) &&
                        (preset.Lsb.ToString().Equals(sLsb, StringComparison.InvariantCultureIgnoreCase) || sLsb.Length == 0))
                    {
                        MidiPreset p = new()
                        {
                            PresetName = preset.PresetName,
                            Prg = preset.Prg,
                            Lsb = preset.Lsb,
                            Msb = preset.Msb,
                            InstrumentGroup = preset.InstrumentGroup,
                            Channel = preset.Channel
                        };

                        cat.Presets.Add(p);
                    }
                }

                if (cat.Presets.Count > 0)
                {
                    filter.Categories.Add(cat);
                }
            }
            PopulateHierarchyTree(filter);
        }

        private void FilterTreeViewByString(string filterText)
        {
            if (InstrumentPresets != null)
            {
                if (string.IsNullOrWhiteSpace(filterText))
                {
                    PopulateHierarchyTree(InstrumentPresets);
                }
                else
                {
                    var filter = new InstrumentData
                    {
                        CubaseFile = InstrumentPresets.CubaseFile,
                        Device = InstrumentPresets.Device
                    };
                    foreach (PresetHierarchy category in InstrumentPresets.Categories)
                    {
                        PresetHierarchy cat = new()
                        {
                            IndexInFile = category.IndexInFile,
                            Category = category.Category,
                            Level = 1,
                            Raw = category.Raw,
                            Presets = new List<MidiPreset>()
                        };

                        foreach (var preset in category.Presets)
                        {
                            if (preset.PresetName.Contains(filterText.Trim(), StringComparison.InvariantCultureIgnoreCase))
                            {
                                MidiPreset p = new()
                                {
                                    PresetName = preset.PresetName,
                                    Prg = preset.Prg,
                                    Lsb = preset.Lsb,
                                    Msb = preset.Msb,
                                    InstrumentGroup = preset.InstrumentGroup,
                                    Channel = preset.Channel
                                };

                                cat.Presets.Add(p);
                            }
                        }

                        if (cat.Presets.Count > 0)
                        {
                            filter.Categories.Add(cat);
                        }
                    }
                    PopulateHierarchyTree(filter);
                }
            }
        }

        internal void GetPreset()
        {
            MidiPreset mp = new()
            {
                PresetName = tbName.Text.Trim()
            };

            int iErrors = 0;

            mp.Msb = 0;
            if (!int.TryParse(tbMsb.Text.Trim(), out mp.Msb))
            {
                iErrors++;
            }

            mp.Lsb = 0;
            if (!int.TryParse(tbLsb.Text.Trim(), out mp.Lsb))
            {
                iErrors++;
            }

            mp.Prg = 0;
            if (!int.TryParse(tbPrg.Text.Trim(), out mp.Prg))
            {
                iErrors++;
            }

            mp.SysEx = tbSysex.Text.Trim();

            if (iErrors == 3)
            {
                mp.PresetName = "[ERROR]";
                MessageBox.Show("Unable to parse MSB/LSB/PRG values. Default value will be used.");
            }

            OnPresetChanged?.Invoke(mp);
        }

        private void ckSetFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (InstrumentPresets != null)
            {
                if (tvPresets.SelectedValue.ToString().IndexOf("-") > -1) //c'est une catégorie
                {
                    if (!((CheckBox)sender).IsChecked.Value)
                    {
                        string idx = tvPresets.SelectedValue.ToString();
                        MidiPreset p = InstrumentPresets.GetPreset(idx);
                        p.IsFavourite = false;
                    }
                    else
                    {
                        string idx = tvPresets.SelectedValue.ToString();
                        MidiPreset p = InstrumentPresets.GetPreset(idx);
                        p.IsFavourite = true;
                    }
                    PopulateHierarchyTree(InstrumentPresets);
                }
            }
        }

        private void cbSysExInit_Click(object sender, RoutedEventArgs e)
        {
            if (tbPrg.Text.Length > 0 && tbMsb.Text.Length > 0 && tbLsb.Text.Length > 0 && CurrentPreset.Length > 0)
            {
                SysExInput sys = new SysExInput();
                sys.ShowDialog();
                if (sys.InvalidData)
                {
                    MessageBox.Show("Cancelled.");
                }
                else
                {
                    TextRange textRange = new(sys.rtbSysEx.Document.ContentStart, sys.rtbSysEx.Document.ContentEnd);
                    string sSysex = textRange.Text.Replace("-", "").Trim();
                    tbSysex.Text = sSysex;
                    InstrumentPresets.GetPreset(CurrentPreset).SysEx = sSysex;
                }
            }
            else
            {
                MessageBox.Show("You must choose a Preset first (or manually set MSB/LSB/PRG data)");
            }
        }
    }
}
