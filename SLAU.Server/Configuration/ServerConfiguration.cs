using System.Xml.Serialization;

namespace SLAU.Server.Configuration;

[Serializable]
public class ServerConfiguration
{
    public int Port { get; set; } = 5000;
    public string NodesConfigPath { get; set; } = "nodes.xml";
    public int MaxConnections { get; set; } = 100;
    public int ConnectionTimeout { get; set; } = 30000; // миллисекунды
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public List<ComputeNodeInfo> Nodes { get; set; } = new List<ComputeNodeInfo>();

    public static ServerConfiguration Load(string path)
    {
        try
        {
            using (var reader = new StreamReader(path))
            {
                var serializer = new XmlSerializer(typeof(ServerConfiguration));
                return (ServerConfiguration)serializer.Deserialize(reader);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            return new ServerConfiguration();
        }
    }

    public void Save(string path)
    {
        try
        {
            using (var writer = new StreamWriter(path))
            {
                var serializer = new XmlSerializer(typeof(ServerConfiguration));
                serializer.Serialize(writer, this);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }
}