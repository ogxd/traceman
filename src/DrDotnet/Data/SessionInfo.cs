using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrDotnet.Utils;
using Google.Protobuf;

public partial class SessionInfo
{
    public const string SESSION_FILE_NAME = "session.json";

    public SessionInfo(ProfilerInfo profiler, ProcessInfo process)
    {
        Uuid = Guid.NewGuid().ToString();
        Profiler = profiler;
        Process = process;
        Timestamp = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
    }
    
    public Guid Guid => new(Uuid);

    public DateTime TimestampDate => DateTime.Parse(Timestamp, CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns all reports (session.json is not included, because it's not a report per say)
    /// </summary>
    /// <returns>Reports generated during the profiling session</returns>
    public IEnumerable<FileInfo> EnumerateReports()
    {
        return new FileInfo(Path).Directory!.EnumerateFiles()
            .Where(x => x.Name != SESSION_FILE_NAME)
            .OrderBy(x => x.Name);
    }

    public string Path => GetPath(Guid);

    public static SessionInfo FromPath(string sessionFilePath)
    {
        if (!File.Exists(sessionFilePath))
            throw new FileNotFoundException($"There is no session file at path '{sessionFilePath}'");

        var jsonString = File.ReadAllText(sessionFilePath);
        
        // Unfortunately, System.Text.Json is unable to deserialize special fields like RepeatedFields
        // var options = new JsonSerializerOptions
        // {
        //     PropertyNameCaseInsensitive = true
        // };
        //var session = JsonSerializer.Deserialize<SessionInfo>(jsonString, options);
        
        var message = (IMessage)Activator.CreateInstance(typeof(SessionInfo))!;
        var session = (SessionInfo)JsonParser.Default.Parse(jsonString, message?.Descriptor);
        
        return session!;
    }
    
    public static string GetPath(Guid sessionId)
    {
        return System.IO.Path.Combine(PathUtils.DrDotnetBaseDirectory, sessionId.ToString(), SESSION_FILE_NAME);
    }

    public bool IsCompleted => File.Exists(Path);
    
    public async Task AwaitUntilCompletion()
    {
        while (!IsCompleted)
        {
            // Wait until the session manifest has been written
            await Task.Delay(500);
        }
    }
}