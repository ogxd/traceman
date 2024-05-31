#pragma warning disable CA1416

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DrDotnet.Utils;

public class FileContentWatcher : IDisposable
{
    private readonly FileSystemWatcher _fsWatcher;
    private readonly SafeFileHandle _fileHandle;
    
    private long _position;

    private readonly HashSet<Action<string>> _contentWritten = new();
    public event Action<string> ContentWritten
    {
        add
        {
            long fileSize = RandomAccess.GetLength(_fileHandle);
            string newContent = ReadContent(_position, fileSize);
            if (!string.IsNullOrEmpty(newContent))
            {
                // If there is content, we should notify the subscriber with current content
                value(newContent);
            }
            _contentWritten.Add(value);
        }
        remove => _contentWritten.Remove(value);
    }

    public FileContentWatcher(string path)
    {
        _fileHandle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        // Create a new FileSystemWatcher that watches exclusively for changes in this file
        _fsWatcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        
        _fsWatcher.NotifyFilter = NotifyFilters.LastAccess
                                | NotifyFilters.CreationTime
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Size;

        _fsWatcher.Changed += WatcherOnChanged;

        _fsWatcher.Filter = "*.log";
        _fsWatcher.IncludeSubdirectories = false;
        _fsWatcher.EnableRaisingEvents = true;
    }

    private string ReadContent(long from, long to)
    {
        long length = to - from;
        
        if (length <= 0)
        {
            return string.Empty;
        }
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)length);
        RandomAccess.Read(_fileHandle, buffer, _position);
        
        string content = Encoding.UTF8.GetString(buffer.AsSpan().Slice(0, (int)length));
        
        ArrayPool<byte>.Shared.Return(buffer);

        return content;
    }
    
    private void WatcherOnChanged(object sender, FileSystemEventArgs _)
    {
        long fileSize = RandomAccess.GetLength(_fileHandle);
        _position = fileSize;
        
        if (_contentWritten.Count == 0)
        {
            return;
        }
        
        string newContent = ReadContent(_position, fileSize);
        
        foreach (var action in _contentWritten)
        {
            try 
            {
                action(newContent);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public void Dispose()
    {
        _fsWatcher.Dispose();
    }
}