using MidiTools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
    /// Logique d'interaction pour Projects.xaml
    /// </summary>
    public partial class Projects : Window
    {
        internal Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData> Project;
        private SQLiteDatabaseManager Database;

        public Projects(SQLiteDatabaseManager db, List<string[]> sListProjects)
        {
            Database = db;

            InitializeComponent();
            InitPage(sListProjects);
        }

        private void InitPage(List<string[]> sListProjects)
        {
            tvProjects.Items.Clear();

            //Id, ProjectGuid, Name, DateProject, Author, Active
            IEnumerable<string> distinctValues = sListProjects.Select(arr => arr[1]).Distinct();

            foreach (var prj in distinctValues)
            {
                var versions = sListProjects.Where(p => p[1].Equals(prj)).ToList();

                TreeViewItem prjItem = new TreeViewItem();
                prjItem.Header = versions.Count > 0 ? versions[0][2] : prj;
                prjItem.Tag = prj;

                foreach (var ver in versions)
                {
                    TreeViewItem prjversion = new TreeViewItem();
                    prjversion.Header = string.Concat("Date : ", ver[3], " [", ver[4], "]");
                    prjversion.Foreground = ver[5].Equals("1") ? Brushes.Green : Brushes.Red;
                    prjversion.Tag =string.Concat(prj, "|", ver[0]);
                    prjItem.Items.Add(prjversion);
                }
                tvProjects.Items.Add(prjItem);
            }
        }

        private void btnDeleteProject_Click(object sender, RoutedEventArgs e)
        {
            if (tvProjects.SelectedItem != null)
            {
                var prj = (TreeViewItem)tvProjects.SelectedItem;
                string sProject = prj.Tag.ToString();
                if (sProject.Length > 0)
                {
                    bool bWholeProject = false;
                    if (sProject.IndexOf("|") > 0)
                    {
                        bWholeProject = false;
                    }
                    else
                    {
                        bWholeProject= true;
                    }

                    var confirm = MessageBox.Show(bWholeProject ? "Delete Whole Project ?" : "Delete Project Version ?", "Are you Sure", MessageBoxButton.YesNo);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        Database.DeleteProject(sProject, bWholeProject);
                        InitPage(Database.GetProjects());

                        MessageBox.Show("Project Deleted");
                    }
                }
            }
        }

        private void btnChooseProject_Click(object sender, RoutedEventArgs e)
        {
            var item = tvProjects.SelectedItem;

            if (item != null)
            {
                string sGuid = ((TreeViewItem)item).Tag.ToString();
                Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData> project = Database.GetProject(sGuid);
                if (project != null)
                {
                    Project = project;
                }
            }

            Close();
        }
    }
}
