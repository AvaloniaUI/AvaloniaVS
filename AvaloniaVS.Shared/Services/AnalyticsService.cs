using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AvaloniaVS.Services;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace AvaloniaVS.Shared.Services
{
    class AnalyticsService
    {
        // Constants
        private const string DefaultFolderName = ".avalonia_ide";
        private const string RecordFilePrefix = "avalonia_ide";
        private const string DestinationUrl = "https://av-build-tel-api-v1.avaloniaui.net/api/ide/usage";           

        // Dependencies
        private readonly DTE _dte;
        private readonly IVsShell _shell;
        private readonly HttpClient _httpClient;
        private readonly Stopwatch _stopwatch = new();

        // State
        private readonly bool _enabled;
        private readonly Guid _uniqueIdentifier;
        private readonly string targetDir;

        public AnalyticsService(IAvaloniaVSSettings settings, DTE dte, IVsShell shell)
        {
            _enabled = settings.UsageTracking;
            _dte = dte;
            _shell = shell;
            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            targetDir = Path.Combine(appDataDir, DefaultFolderName);
            Directory.CreateDirectory(targetDir);

            _uniqueIdentifier = UniqueIdentifierProvider.GetOrCreateIdentifier();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(10000) };
        }

        public async Task TrackLaunchAsync()
        {
            if (!_enabled || _dte == null)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                       

            var payload = AvaloniaExtentionLoadedAnalyticsPayload.Initialise(
                _uniqueIdentifier,
                _dte.Edition,
                await GetVersionAsync());           

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    WriteAnalytics(payload);
                    await SweepAndSendAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to SweepAndSendAsync: {ex}");
                }
            });           
        }

        private async Task<string> GetVersionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            object versionObj = null;
            int hr = _shell.GetProperty((int)__VSSPROPID5.VSSPROPID_ReleaseVersion, out versionObj);

            if (ErrorHandler.Succeeded(hr) && versionObj is string fullVersion)
            {
                return fullVersion;
            }

            // fallback if shell isn’t available for some reason
            return _dte.Version;
        }

        private async Task SweepAndSendAsync()
        {
            while (true)
            {
                var payloads = new List<AvaloniaExtentionLoadedAnalyticsPayload>();

                foreach (var dataFile in Directory.EnumerateFiles(targetDir).ToList())
                {
                    if (Path.GetFileName(dataFile).StartsWith(RecordFilePrefix))
                    {
                        try
                        {
                            var data = File.ReadAllBytes(dataFile);

                            var payload = AvaloniaExtentionLoadedAnalyticsPayload.FromByteArray(data);

                            payloads.Add(payload);
                        }
                        catch (Exception)
                        {
                        }

                        if (payloads.Count == 50)
                        {
                            break;
                        }
                    }
                }

                if (payloads.Count > 0)
                {
                    bool sent = false;

                    try
                    {
                        sent = await SendAsync(payloads);
                    }
                    catch (Exception)
                    {
                    }

                    if (sent)
                    {
                        _stopwatch.Restart();

                        foreach (var payload in payloads)
                        {
                            var file = Path.Combine(targetDir, $"{RecordFilePrefix}_{payload.RecordId}");
                            File.Delete(file);
                        }
                    }
                }

                if (_stopwatch.Elapsed > TimeSpan.FromSeconds(30))
                {
                    return;
                }

                await Task.Delay(100);
            }
        }

        private async Task<bool> SendAsync(IList<AvaloniaExtentionLoadedAnalyticsPayload> payloads)
        {
            if (payloads.Count < 1)
            {
                return true;
            }

            var content = new ByteArrayContent(AvaloniaExtentionLoadedAnalyticsPayload.EncodeMany(payloads));

            try
            {
                var response = await _httpClient.PostAsync(DestinationUrl, content);

                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Failed to send payload: {ex}");
            }

            return false;
        }
             
        private void WriteAnalytics(AvaloniaExtentionLoadedAnalyticsPayload payload)
        {
            var dataPath = Path.Combine(targetDir, $"{RecordFilePrefix}_{payload.RecordId}");

            if (!File.Exists(dataPath))
            {
                var data = payload.Encode();

                File.WriteAllBytes(dataPath, data);
            }
        }    
    }

    public class AvaloniaExtentionLoadedAnalyticsPayload
    {
        public static readonly ushort PayloadVersion = 1;

        public Guid RecordId { get; private set; }

        public DateTimeOffset TimeStamp { get; private set; }

        public Guid Machine { get; private set; }

        public Ide Ide { get; private set; }

        public string Edition { get; private set; }

        public string Version { get; private set; }

        public static AvaloniaExtentionLoadedAnalyticsPayload Initialise(Guid machine, string vsEdition, string vsVersion)
        {
            var result = new AvaloniaExtentionLoadedAnalyticsPayload();

            result.RecordId = Guid.NewGuid();
            result.TimeStamp = DateTimeOffset.UtcNow;
            result.Machine = machine;
            result.Ide = Ide.Vs;
            result.Version = vsVersion;
            result.Edition = vsEdition;            
            return result;
        }

        public byte[] Encode()
        {
            using var m = new MemoryStream();
            using var writer = new BinaryWriter(m);
            writer.Write(PayloadVersion);
            writer.Write(RecordId.ToByteArray());
            writer.Write(TimeStamp.ToUnixTimeMilliseconds());
            writer.Write(Machine.ToByteArray());
            writer.Write((byte)Ide);
            writer.Write(Version ?? string.Empty);
            writer.Write(Edition ?? string.Empty);
            return m.ToArray();
        }

        public static byte[] EncodeMany(IList<AvaloniaExtentionLoadedAnalyticsPayload> payloads)
        {
            if (payloads.Count > 0)
            {
                if (payloads.Count > 50)
                {
                    throw new Exception("No more than 50 in a single packet.");
                }

                using var m = new MemoryStream();
                using var writer = new BinaryWriter(m);

                writer.Write(payloads.Count);

                foreach (var payload in payloads)
                {
                    writer.Write(payload.Encode());
                }

                return m.ToArray();
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public static AvaloniaExtentionLoadedAnalyticsPayload FromByteArray(byte[] data)
        {
            var result = new AvaloniaExtentionLoadedAnalyticsPayload();
            using var m = new MemoryStream(data);
            using var reader = new BinaryReader(m);

            result = FromBinaryReader(reader);

            return result;
        }

        public static AvaloniaExtentionLoadedAnalyticsPayload FromBinaryReader(BinaryReader reader)
        {
            var result = new AvaloniaExtentionLoadedAnalyticsPayload();
            var version = reader.ReadInt16();
            if (version == PayloadVersion)
            {
                result.RecordId = new Guid(reader.ReadBytes(16));
                result.TimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
                result.Machine = new Guid(reader.ReadBytes(16));
                result.Ide = (Ide)reader.ReadByte();
                result.Version = reader.ReadString();
                result.Edition = reader.ReadString();
            }

            return result;
        }        
    }

    public enum Ide
    {
        Unknown,
        Vs,
        Vs4Mac,
        Rider,
        Cli
    }

    /// <summary>
    /// Provides a reusable mechanism to retrieve or generate a persistent <see cref="Guid"/> 
    /// stored in the user’s Application Data folder.
    /// </summary>
    internal static class UniqueIdentifierProvider
    {
        public static Guid GetOrCreateIdentifier()
        {
            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetDir = Path.Combine(appDataDir, ".avalonia-build-tasks");
            Directory.CreateDirectory(targetDir);

            string idFilePath = Path.Combine(targetDir, "id");

            if (TryReadGuidFromFile(idFilePath, out Guid existingId))
            {
                return existingId;
            }

            Guid freshId = Guid.NewGuid();
            File.WriteAllBytes(idFilePath, freshId.ToByteArray());
            return freshId;
        }

        private static bool TryReadGuidFromFile(string path, out Guid result)
        {
            result = Guid.Empty;

            if (!File.Exists(path))
            {
                return false;
            }

            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch
            {
                // If reading fails for any reason, overwrite with a new GUID
                return false;
            }

            if (data.Length != 16)
            {
                // Corrupt or tampered file detected
                return false;
            }

            result = new Guid(data);
            return true;
        }
    }
}
