﻿using Microsoft.Diagnostics.NETCore.Client;
using System;
using System.Collections.Generic;
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

    private static string TmpProfilerLibrary;

    /// <summary>
    /// Path of the profilers library in the shared temporary folder
    /// </summary>
    /// <returns></returns>
    public static string GetTmpProfilerLibrary()
    {
        if (TmpProfilerLibrary == null)
        {
            string profilerDll = GetLocalProfilerLibrary();
            string tmpProfilerDll = Path.Combine(PathUtils.DrDotnetBaseDirectory, ProfilerLibraryName);

            // Copy but don't overwrite. Instead, delete before, and copy after. This is required because in Linux if we do
            // a straight override while the library has already been loaded before (and not unloaded), it messed up the mappings 
            // and leads to a segfault
            File.Delete(tmpProfilerDll);
            File.Copy(profilerDll, tmpProfilerDll, false);

            TmpProfilerLibrary = tmpProfilerDll;
        }
        return TmpProfilerLibrary;
    }

    /// <summary>
    /// Path of the profilers library shipped localy with the program
    /// </summary>
    /// <returns></returns>
    public static string GetLocalProfilerLibrary()
    {
        string strExeFilePath = Assembly.GetExecutingAssembly().Location;
        string strWorkPath = Path.GetDirectoryName(strExeFilePath);
        string profilerDll = Path.Combine(strWorkPath, ProfilerLibraryName);
        return profilerDll;
    }

    public static Guid StartProfilingSession(ProfilerInfo profiler, int processId, ILogger logger)
    {
        string profilerDll = GetTmpProfilerLibrary();
        var sessionId = Guid.NewGuid();

        logger.LogInformation("Profiler library path: '{profilerDll}'", profilerDll);
        logger.LogInformation("Profiler version: '{version}'", VersionUtils.CurrentVersion);

        DiagnosticsClient client = new DiagnosticsClient(processId);

        SessionInfo sessionInfo = new SessionInfo();
        sessionInfo.Uuid = sessionId.ToString();
        sessionInfo.Parameters.AddRange(profiler.Parameters);
        byte[] sessionInfoSerialized = sessionInfo.ToByteArray();
        
        client.AttachProfiler(TimeSpan.FromSeconds(10), profiler.Guid, profilerDll, sessionInfoSerialized);

        logger.LogInformation("Attached profiler {ProfilerId} with session {sessionId} to process {processId}", profiler.Guid, sessionId, processId);

        return sessionId;
    }
}