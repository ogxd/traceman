using Microsoft.Diagnostics.NETCore.Client;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Google.Protobuf;

namespace DrDotnet.Utils;

public static class ProfilingExtensions
{
    /// <summary>
    ///  Name of the profilers library. Different depending on the operating system.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public static string ProfilerLibraryName => Environment.OSVersion.Platform switch
    {
        PlatformID.Win32NT => "profilers.dll",
        // https://github.com/dotnet/runtime/issues/21660
        PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "libprofilers.dylib",
        PlatformID.Unix when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "libprofilers.so",
        _ => throw new NotImplementedException()
    };

    private static string? _tmpProfilerLibrary;

    /// <summary>
    /// Path of the profilers library in the shared temporary folder
    /// </summary>
    /// <returns></returns>
    public static string GetTmpProfilerLibrary()
    {
        if (_tmpProfilerLibrary == null)
        {
            string profilerDll = GetLocalProfilerLibrary();
            string tmpProfilerDll = Path.Combine(PathUtils.DrDotnetBaseDirectory, ProfilerLibraryName);

            // Copy but don't overwrite. Instead, delete before, and copy after. This is required because in Linux if we do
            // a straight override while the library has already been loaded before (and not unloaded), it messed up the mappings 
            // and leads to a segfault
            File.Delete(tmpProfilerDll);
            File.Copy(profilerDll, tmpProfilerDll, false);

            _tmpProfilerLibrary = tmpProfilerDll;
        }
        return _tmpProfilerLibrary;
    }

    /// <summary>
    /// Path of the profilers library shipped localy with the program
    /// </summary>
    /// <returns></returns>
    public static string GetLocalProfilerLibrary()
    {
        string strExeFilePath = Assembly.GetExecutingAssembly().Location;
        string? strWorkPath = Path.GetDirectoryName(strExeFilePath);
        string profilerDll = Path.Combine(strWorkPath!, ProfilerLibraryName);
        return profilerDll;
    }

    public static SessionInfo StartProfilingSession(ProfilerInfo profiler, ProcessInfo process, ILogger logger)
    {
        string profilerDll = GetTmpProfilerLibrary();

        logger.LogInformation("Profiler library path: '{profilerDll}'", profilerDll);
        logger.LogInformation("Profiler version: '{version}'", VersionUtils.CurrentVersion);

        DiagnosticsClient client = new DiagnosticsClient(process.Id);

        SessionInfo sessionInfo = new SessionInfo(profiler, process);
        byte[] sessionInfoSerialized = sessionInfo.ToByteArray();
        
        client.AttachProfiler(TimeSpan.FromSeconds(10), profiler.Guid, profilerDll, sessionInfoSerialized);

        logger.LogInformation("Attached profiler {ProfilerId} with session {sessionId} to process {processId}", profiler.Guid, sessionInfo.Guid, process.Id);

        return sessionInfo;
    }
}