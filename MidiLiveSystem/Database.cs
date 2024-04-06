using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.Data.Sqlite;
using MidiTools;
using MessagePack;
using MessagePack.Resolvers;
using System.Collections;
using NAudio.SoundFont;
using System.Threading.Tasks;

namespace MidiLiveSystem
{
    public class SQLiteDatabaseManager
    {
        public static string Database = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MidiLiveSystem", "MidiLiveSystem.db");
        private string connectionString = $"Data Source=";

        public SQLiteDatabaseManager()
        {
            connectionString = string.Concat(connectionString, Database);

            if (!File.Exists(Database))
            {
                var path = Path.GetDirectoryName(Database);
                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch { throw new Exception("Unable to create Database path : " + path); }
                }
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
                    Config BLOB NOT NULL,
                    Routing BLOB NOT NULL,
                    Recording BLOB,
                    Sequences BLOB,
                    DateProject TEXT NOT NULL,
                    Author TEXT,
                    Active INT NOT NULL)";
                createTableCommand.ExecuteNonQuery();

                createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Instruments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceName TEXT NOT NULL,
                    InstrumentData TEXT NOT NULL)";
                createTableCommand.ExecuteNonQuery();
            }
        }

        public async Task SaveInstruments(List<InstrumentData> instruments)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM Instruments;";
                await deleteCommand.ExecuteNonQueryAsync();
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                foreach (InstrumentData instr in instruments)
                {
                    string sData = "";
                    string sDevice = instr.Device;
                    XmlSerializer serializerConfig = new XmlSerializer(typeof(InstrumentData));
                    using (StringWriter textWriter = new StringWriter())
                    {
                        serializerConfig.Serialize(textWriter, instr);
                        sData = textWriter.ToString();
                    }

                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = "INSERT INTO Instruments (DeviceName, InstrumentData) VALUES (@devicename, @instrumentdata)";
                    insertCommand.Parameters.AddWithValue("@devicename", sDevice);
                    insertCommand.Parameters.AddWithValue("@instrumentdata", sData);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
        }

        public List<InstrumentData> LoadInstruments()
        {
            List<InstrumentData> instruments = new List<InstrumentData>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, DeviceName, InstrumentData FROM Instruments;";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        XmlSerializer serializerConfig = new XmlSerializer(typeof(InstrumentData));
                        using (StringReader stream = new StringReader(reader.GetString(2)))
                        {
                            instruments.Add((InstrumentData)serializerConfig.Deserialize(stream));
                        }
                    }
                }
            }

            return instruments;
        }

        public void SaveProject(List<RoutingBox> Boxes, ProjectConfiguration Project, MidiSequence RecordedSequence, SequencerData SeqData)
        {
            Project.IsDefaultConfig = false; //pour signifier que la config a été chargée, même si on est pas allé dans la menu de configuration du projet (et éviter de générer un nouvel ID à cause de ce flag)

            string sId = Project.ProjectId.ToString();
            string sProjectConfig = "";
            string sRoutingConfig = "";
            string sProjectName = Project.ProjectName;
            string sRecording = "";
            string sSeqData = "";

            Project.BoxNames = new List<string[]>();
            foreach (var box in Boxes)
            {
                Project.BoxNames.Add(new string[] { box.BoxName, box.BoxGuid.ToString(), box.GridPosition.ToString() });
            }

            XmlSerializer serializerConfig = new XmlSerializer(typeof(ProjectConfiguration));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerConfig.Serialize(textWriter, Project);
                sProjectConfig = textWriter.ToString();
            }


            List<BoxPreset> allpresets = new List<BoxPreset>();
            //sauvegarde des box
            foreach (RoutingBox box in Boxes)
            {
                allpresets.AddRange(box.GetRoutingBoxMemory());
            }

            XmlSerializer serializerRouting = new XmlSerializer(typeof(RoutingBoxes));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerRouting.Serialize(textWriter, new RoutingBoxes() { AllPresets = allpresets.ToArray() });
                sRoutingConfig = textWriter.ToString();
            }

            XmlSerializer serializerRecording = new XmlSerializer(typeof(MidiSequence));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerRecording.Serialize(textWriter, RecordedSequence);
                sRecording = textWriter.ToString();
            }

            XmlSerializer serializerSeqData = new XmlSerializer(typeof(SequencerData));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerSeqData.Serialize(textWriter, SeqData);
                sSeqData = textWriter.ToString();
            }

            List<string> sVersionsToDelete = GetOldProjectVersion(sId);

            int iExists = GetProjectName(sProjectName.Replace("'", "''"));
            if (iExists > 0)
            {
                sProjectName = string.Concat(sProjectName, " - ", (iExists + 1).ToString("00"), " (" + DateTime.Now.ToString("yyyy/MM/dd"), ")");
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                if (sVersionsToDelete.Count > 0)
                {
                    string sDelete = sVersionsToDelete.Count > 1 ? string.Concat(sVersionsToDelete, ",") : sVersionsToDelete[0];
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Projects WHERE ProjectGuid = '" + sId + "' AND Id IN (" + sDelete + ");";
                    deleteCommand.ExecuteNonQuery();
                }

                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Projects SET Active = 0 WHERE ProjectGuid = '" + sId + "';";
                updateCommand.ExecuteNonQuery();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Projects (ProjectGuid, ProjectName, Config, Routing, Recording, Sequences, DateProject, Author, Active) VALUES (@projectid, @projectname, @config, @routing, @recording, @sequences, @dateproject, @author, @active)";
                insertCommand.Parameters.AddWithValue("@projectid", sId);
                insertCommand.Parameters.AddWithValue("@projectname", sProjectName);
                insertCommand.Parameters.AddWithValue("@config", sProjectConfig);
                insertCommand.Parameters.AddWithValue("@routing", sRoutingConfig);
                insertCommand.Parameters.AddWithValue("@recording", sRecording);
                insertCommand.Parameters.AddWithValue("@sequences", sSeqData);
                insertCommand.Parameters.AddWithValue("@dateproject", DateTime.Now.ToString());
                insertCommand.Parameters.AddWithValue("@author", Environment.UserName);
                insertCommand.Parameters.AddWithValue("@active", "1");
                insertCommand.ExecuteNonQuery();
            }
        }

        public async Task SaveProjectV2(List<RoutingBox> Boxes, ProjectConfiguration Project, MidiSequence RecordedSequence, SequencerData SeqData)
        {
            Project.IsDefaultConfig = false; //pour signifier que la config a été chargée, même si on est pas allé dans la menu de configuration du projet (et éviter de générer un nouvel ID à cause de ce flag)
            string sId = Project.ProjectId.ToString();
            string sProjectName = Project.ProjectName;

            Project.BoxNames = new List<string[]>();
            foreach (var box in Boxes)
            {
                Project.BoxNames.Add(new string[] { box.BoxName, box.BoxGuid.ToString(), box.GridPosition.ToString() });
            }

            List<BoxPreset> allpresets = new List<BoxPreset>();
            //sauvegarde des box
            foreach (RoutingBox box in Boxes)
            {
                allpresets.AddRange(box.GetRoutingBoxMemory());
            }

            //MessagePackSerializer.DefaultOptions = MessagePackSerializer.DefaultOptions
            //.WithResolver(MessagePack.Resolvers.StandardResolver.Instance)
            //.WithCustomFormatter(new GZipMessagePackFormatter<Project>());

            byte[] binaryProject = MessagePackSerializer.Serialize(Project);
            byte[] binaryRouting = MessagePackSerializer.Serialize(new RoutingBoxes() { AllPresets = allpresets.ToArray() });
            byte[] binaryRecording = MessagePackSerializer.Serialize(RecordedSequence);
            byte[] binarySequencer = MessagePackSerializer.Serialize(SeqData);

            List<string> sVersionsToDelete = GetOldProjectVersion(sId);

            int iExists = GetProjectName(sProjectName.Replace("'", "''"));
            if (iExists > 0)
            {
                sProjectName = string.Concat(sProjectName, " - ", (iExists + 1).ToString("00"), " (" + DateTime.Now.ToString("yyyy/MM/dd"), ")");
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                if (sVersionsToDelete.Count > 0)
                {
                    string sDelete = sVersionsToDelete.Count > 1 ? string.Concat(sVersionsToDelete, ",") : sVersionsToDelete[0];
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM Projects WHERE ProjectGuid = '" + sId + "' AND Id IN (" + sDelete + ");";
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Projects SET Active = 0 WHERE ProjectGuid = '" + sId + "';";
                await updateCommand.ExecuteNonQueryAsync();

                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO Projects (ProjectGuid, ProjectName, Config, Routing, Recording, Sequences, DateProject, Author, Active) VALUES (@projectid, @projectname, @config, @routing, @recording, @sequences, @dateproject, @author, @active)";
                insertCommand.Parameters.AddWithValue("@projectid", sId);
                insertCommand.Parameters.AddWithValue("@projectname", sProjectName);

                SqliteParameter paramconfig = new SqliteParameter("@config", System.Data.DbType.Binary);
                paramconfig.Value = binaryProject;
                insertCommand.Parameters.Add(paramconfig);

                SqliteParameter paramrouting = new SqliteParameter("@routing", System.Data.DbType.Binary);
                paramrouting.Value = binaryRouting;
                insertCommand.Parameters.Add(paramrouting);

                SqliteParameter paramrecording = new SqliteParameter("@recording", System.Data.DbType.Binary);
                paramrecording.Value = binaryRecording;
                insertCommand.Parameters.Add(paramrecording);

                SqliteParameter paramsequences = new SqliteParameter("@sequences", System.Data.DbType.Binary);
                paramsequences.Value = binarySequencer;
                insertCommand.Parameters.Add(paramsequences);

                insertCommand.Parameters.AddWithValue("@dateproject", DateTime.Now.ToString());
                insertCommand.Parameters.AddWithValue("@author", Environment.UserName);
                insertCommand.Parameters.AddWithValue("@active", "1");
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private int GetProjectName(string sName)
        {
            int iQte = 0;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT COUNT(*) FROM Projects WHERE ProjectName LIKE '" + sName + "%';";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        iQte = reader.GetInt32(0);
                    }
                }
            }

            return iQte;
        }

        private List<string> GetOldProjectVersion(string sProjectGuid)
        {
            List<string> sVersions = new List<string>();

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id FROM Projects WHERE ProjectGuid = '" + sProjectGuid + "' ORDER BY DateProject DESC;";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sVersions.Add(reader.GetString(0));
                    }
                }
            }

            if (sVersions.Count > 2) //historique des versions à supprimer
            {
                sVersions = sVersions.GetRange(3, sVersions.Count - 3);
            }
            else { sVersions.Clear(); }


            return sVersions;
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

        public Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData> GetProject(string idDb)
        {
            string sId = "";
            string sProjectGuid = "";
            string sConfig = "";
            string sRouting = "";
            string sName = "";
            string sDate = "";
            string sAuthor = "";
            string sRecording = "";
            string sSeqData = "";

            string sDbID = idDb.IndexOf('|') > 0 ? idDb.Split('|')[1] : idDb;

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, ProjectGuid, ProjectName, Config, Routing, Recording, Sequences, DateProject, Author, Active FROM Projects WHERE Id = '" + sDbID + "' AND Active = 1;";

                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sId = reader.GetString(0);
                        sProjectGuid = reader.GetString(1);
                        sName = reader.GetString(2);
                        sConfig = reader.GetString(3);
                        sRouting = reader.GetString(4);
                        sRecording = reader.IsDBNull(5) ? null : reader.GetString(5);
                        sSeqData = reader.IsDBNull(6) ? null : reader.GetString(6);
                        sDate = reader.GetString(7);
                        sAuthor = reader.GetString(8);
                    }
                }
            }

            if (sId.Length > 0 && sConfig.Length > 0 && sRouting.Length > 0)
            {
                ProjectConfiguration project;
                RoutingBoxes presets;
                MidiSequence sequence = null;
                SequencerData seqs = null;

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

                if (!string.IsNullOrEmpty(sRecording))
                {
                    XmlSerializer serializerRecording = new XmlSerializer(typeof(MidiSequence));
                    using (StringReader stream = new StringReader(sRecording))
                    {
                        sequence = (MidiSequence)serializerRecording.Deserialize(stream);
                    }
                }

                if (!string.IsNullOrEmpty(sSeqData))
                {
                    XmlSerializer serializerSeqData = new XmlSerializer(typeof(SequencerData));
                    using (StringReader stream = new StringReader(sSeqData))
                    {
                        seqs = (SequencerData)serializerSeqData.Deserialize(stream);
                    }
                }

                return new Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData>(Guid.Parse(sProjectGuid), project, presets, sequence, seqs);
            }

            return null;
        }

        public async Task<Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData>> GetProjectV2(string idDb)
        {
            string sId = "";
            string sProjectGuid = "";
            string sName = "";
            string sDate = "";
            string sAuthor = "";
            Stream bConfig = null;
            Stream bRouting = null;
            Stream bRecording = null;
            Stream bSequencer = null;
            string sConfig = "";
            string sRouting = "";
            string sRecording = "";
            string sSequencer = "";

            string sDbID = idDb.IndexOf('|') > 0 ? idDb.Split('|')[1] : idDb;

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id, ProjectGuid, ProjectName, Config, Routing, Recording, Sequences, DateProject, Author, Active FROM Projects WHERE Id = '" + sDbID + "' AND Active = 1;";

                using (var reader = await selectCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        sId = reader.GetString(0);
                        sProjectGuid = reader.GetString(1);
                        sName = reader.GetString(2);
                        bConfig = reader.GetStream(3);
                        sConfig = reader.GetString(3);
                        bRouting = reader.GetStream(4);
                        sRouting = reader.GetString(4);
                        bRecording = reader.IsDBNull(5) ? null : reader.GetStream(5);
                        sRecording = reader.IsDBNull(5) ? null : reader.GetString(5);
                        bSequencer = reader.IsDBNull(6) ? null : reader.GetStream(6);
                        sSequencer = reader.IsDBNull(6) ? null : reader.GetString(6);
                        sDate = reader.GetString(7);
                        sAuthor = reader.GetString(8);
                    }
                }
            }

            ProjectConfiguration project;
            RoutingBoxes routing;
            MidiSequence recording = null;
            SequencerData sequencer = null;

            if (sId.Length > 0 && bConfig != null && bRouting != null)
            {
                try
                {
                    project = MessagePackSerializer.Deserialize<ProjectConfiguration>(StreamToByteArray(bConfig));
                }
                catch
                {
                    XmlSerializer serializerConfig = new XmlSerializer(typeof(ProjectConfiguration));
                    using (StringReader stream = new StringReader(sConfig))
                    {
                        project = (ProjectConfiguration)serializerConfig.Deserialize(stream);
                    }
                }

                try
                {
                    routing = MessagePackSerializer.Deserialize<RoutingBoxes>(StreamToByteArray(bRouting));
                }
                catch
                {
                    XmlSerializer serializerRouting = new XmlSerializer(typeof(RoutingBoxes));
                    using (StringReader stream = new StringReader(sRouting))
                    {
                        routing = (RoutingBoxes)serializerRouting.Deserialize(stream);
                    }
                }

                try
                {
                    recording = bRecording.Length > 0 ? MessagePackSerializer.Deserialize<MidiSequence>(StreamToByteArray(bRecording)) : null;
                }
                catch
                {
                    if (!string.IsNullOrEmpty(sRecording))
                    {
                        XmlSerializer serializerRecording = new XmlSerializer(typeof(MidiSequence));
                        using (StringReader stream = new StringReader(sRecording))
                        {
                            recording = (MidiSequence)serializerRecording.Deserialize(stream);
                        }
                    }
                }

                try
                {
                    sequencer = bSequencer.Length > 0 ? MessagePackSerializer.Deserialize<SequencerData>(StreamToByteArray(bSequencer)) : null;
                }
                catch
                {
                    if (!string.IsNullOrEmpty(sSequencer))
                    {
                        XmlSerializer serializerSeqData = new XmlSerializer(typeof(SequencerData));
                        using (StringReader stream = new StringReader(sSequencer))
                        {
                            sequencer = (SequencerData)serializerSeqData.Deserialize(stream);
                        }
                    }
                }

                return new Tuple<Guid, ProjectConfiguration, RoutingBoxes, MidiSequence, SequencerData>(Guid.Parse(sProjectGuid), project, routing, recording, sequencer);
            }

            return null;
        }

        public void CloseConnection()
        {
            // You don't need to explicitly close the connection in Microsoft.Data.Sqlite.
            // The connection is automatically closed when it goes out of scope.
        }

        public byte[] StreamToByteArray(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream); // Copie le contenu du Stream dans MemoryStream
                return memoryStream.ToArray(); // Retourne le contenu de MemoryStream sous forme de tableau de bytes
            }
        }
    }
}
