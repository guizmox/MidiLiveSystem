using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Data.Sqlite;


namespace MidiLiveSystem
{
    public class SQLiteDatabaseManager
    {
        private string file = "MidiLiveSystem.db";
        private string connectionString = $"Data Source=";

        public SQLiteDatabaseManager()
        {
            connectionString = string.Concat(connectionString, file);

            if (!File.Exists(file))
            {
                SQLitePCL.Batteries.Init();
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    CreateDatabase();
                }
            }
        }

        public void CreateDatabase()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Projects (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectGuid TEXT NOT NULL,
                    ProjectName TEXT NOT NULL,
                    Config TEXT NOT NULL,
                    Routing TEXT NOT NULL,
                    DateProject TEXT NOT NULL,
                    Author TEXT,
                    Active INT NOT NULL)";
                createTableCommand.ExecuteNonQuery();
            }
        }

        public void SaveProject(string guid, string projectname, string xmlconfig, string xmlrouting, string author)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Projects SET Active = 0 WHERE ProjectGuid = '" + guid + "';";
                updateCommand.ExecuteNonQuery();    

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Projects (ProjectGuid, ProjectName, Config, Routing, DateProject, Author, Active) VALUES (@projectid, @projectname, @config, @routing, @dateproject, @author, @active)";
                insertCommand.Parameters.AddWithValue("@projectid", guid);
                insertCommand.Parameters.AddWithValue("@projectname", projectname);
                insertCommand.Parameters.AddWithValue("@config", xmlconfig);
                insertCommand.Parameters.AddWithValue("@routing", xmlrouting);
                insertCommand.Parameters.AddWithValue("@dateproject", DateTime.Now.ToString());
                insertCommand.Parameters.AddWithValue("@author", author);
                insertCommand.Parameters.AddWithValue("@active", "1");
                insertCommand.ExecuteNonQuery();
            }
        }

        public List<string[]> GetProjects()
        {
            List<string[]> projects = new List<string[]>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, ProjectGuid, ProjectName, DateProject, Author, Active FROM Projects ORDER BY ProjectGuid ASC, Active DESC, DateProject DESC;";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        projects.Add(new string[6] { reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5) });
                    }
                }
            }

            return projects;
        }

        public void DeleteProject(string guid, bool bWholeproject)
        {
            string sQuery = "";
            if (bWholeproject)
            {
                sQuery = "DELETE FROM Projects WHERE ProjectGuid = '" + guid + "';";
            }
            else
            {
                sQuery = "DELETE FROM Projects WHERE ProjectGuid = '" + guid.Split('|')[0] + "' AND Id = " + guid.Split('|')[1] + ";";
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = sQuery;
                deleteCommand.ExecuteNonQuery();
            }
        }

        public Tuple<Guid, ProjectConfiguration, RoutingBoxes> ReadProject(string idDb)
        {
            string sId = "";
            string sProjectGuid = "";
            string sConfig = "";
            string sRouting = "";
            string sName = "";
            string sDate = "";
            string sAuthor = "";

            string sDbID = idDb.IndexOf('|') > 0 ? idDb.Split('|')[1] : idDb;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, ProjectGuid, ProjectName, Config, Routing, DateProject, Author, Active FROM Projects WHERE Id = '" + sDbID + "' AND Active = 1;";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sId = reader.GetString(0);
                        sProjectGuid = reader.GetString(1);
                        sName = reader.GetString(2);
                        sConfig = reader.GetString(3);
                        sRouting = reader.GetString(4);
                        sDate = reader.GetString(5);
                        sAuthor = reader.GetString(6);
                    }
                }
            }

            if (sId.Length > 0 && sConfig.Length > 0 && sRouting.Length > 0) 
            {
                ProjectConfiguration project;
                RoutingBoxes presets;

                XmlSerializer serializerConfig = new XmlSerializer(typeof(ProjectConfiguration));
                using (StringReader stream = new StringReader(sConfig))
                {
                    project = (ProjectConfiguration)serializerConfig.Deserialize(stream);
                }

                XmlSerializer serializerRouting = new XmlSerializer(typeof(RoutingBoxes));
                using (StringReader stream = new StringReader(sRouting))
                {
                    presets = (RoutingBoxes)serializerRouting.Deserialize(stream);
                }

                return new Tuple<Guid, ProjectConfiguration, RoutingBoxes>(Guid.Parse(sProjectGuid), project, presets);

            }

            return null;
        }

        public void CloseConnection()
        {
            // You don't need to explicitly close the connection in Microsoft.Data.Sqlite.
            // The connection is automatically closed when it goes out of scope.
        }
    }
}
