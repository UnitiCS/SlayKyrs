namespace SLAU.Server.Configuration;

[Serializable]
public class ComputeNodeInfo
{
    public int Id { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public int MaxThreads { get; set; }
    public bool IsActive { get; set; }

    public ComputeNodeInfo()
    {
        MaxThreads = Environment.ProcessorCount;
        IsActive = true;
    }

    public ComputeNodeInfo(int id, string host, int port)
    {
        Id = id;
        Host = host;
        Port = port;
        MaxThreads = Environment.ProcessorCount;
        IsActive = true;
    }

    public override string ToString()
    {
        return $"Node {Id} ({Host}:{Port})";
    }
}