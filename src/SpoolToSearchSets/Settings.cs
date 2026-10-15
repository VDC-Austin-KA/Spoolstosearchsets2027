using System;
using System.IO;
using System.Xml.Serialization;

namespace SpoolToSearchSets
{
    /// <summary>Last-used dialog values, saved to %AppData%\SpoolToSearchSets\settings.xml.</summary>
    public class Settings
    {
        public string Category = "";
        public string Property = "";
        public string FolderName = "";
        public string Delimiter = "-";
        public int PrimaryIndex;
        public int SecondaryIndex;

        public string MultiFolderCategory = "";
        public string MultiFolderProperty = "";
        public string MultiSetCategory = "";
        public string MultiSetProperty = "";
        public string MultiFolderName = "";

        public bool UseSelection;
        public bool DeleteExisting;
        public int SelectedTab;

        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpoolToSearchSets", "settings.xml");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    using (var stream = File.OpenRead(FilePath))
                        return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
            }
            catch (Exception) { /* corrupt or unreadable: fall back to defaults */ }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (var stream = File.Create(FilePath))
                    new XmlSerializer(typeof(Settings)).Serialize(stream, this);
            }
            catch (Exception) { /* settings are a convenience; never block the run */ }
        }
    }
}
