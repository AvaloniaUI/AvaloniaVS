using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;
using Microsoft.VisualStudio.Shell;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Services
{
    public class PreviewerProcess : IDisposable, ILogEventEnricher
    {
        private readonly string _executablePath;
        private readonly ILogger _log;
        private Process _process;
        private IAvaloniaRemoteTransportConnection _connection;
        private IDisposable _listener;
        private WriteableBitmap _bitmap;

        public PreviewerProcess(string executablePath)
        {
            _executablePath = executablePath;
            _log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Destructure.ToMaximumStringLength(32)
                .Enrich.With(this)
                .WriteTo.Logger(Log.Logger)
                .CreateLogger();
        }

        public BitmapSource Bitmap => _bitmap;
        public string Error { get; set; }
        public bool IsRunning => _process != null && !_process.HasExited;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;

        public async Task StartAsync(string xaml)
        {
            _log.Verbose("Started PreviewerProcess.StartAsync()");

            if (_listener != null)
            {
                throw new InvalidOperationException("Previewer process already started.");
            }

            if (!File.Exists(_executablePath))
            {
                throw new FileNotFoundException(
                    "Could not find executable '{_executablePath}'. " + 
                    "Please build your project to enable previewing and intellisense.");
            }

            var port = FreeTcpPort();
            var tcs = new TaskCompletionSource<object>();

            _listener = new BsonTcpTransport().Listen(
                IPAddress.Loopback,
                port,
#pragma warning disable VSTHRD101
                async t =>
                {
                    try
                    {
                        await ConnectionInitializedAsync(t, xaml);
                    } catch (Exception ex)
                    {
                        _log.Error(ex, "Error initializing connection");
                    }
                    finally
                    {
                        tcs.SetResult(null);
                    }
                });
#pragma warning restore VSTHRD101

            var executableDir = Path.GetDirectoryName(_executablePath);
            var extensionDir = Path.GetDirectoryName(GetType().Assembly.Location);
            var targetName = Path.GetFileNameWithoutExtension(_executablePath);
            var runtimeConfigPath = Path.Combine(executableDir, targetName + ".runtimeconfig.json");
            var depsPath = Path.Combine(executableDir, targetName + ".deps.json");
            var hostAppPath = Path.Combine(extensionDir, "Avalonia.Designer.HostApp.dll");

            EnsureExists(runtimeConfigPath);
            EnsureExists(depsPath);
            EnsureExists(depsPath);

            var args = $@"exec --runtimeconfig ""{runtimeConfigPath}"" --depsfile ""{depsPath}"" ""{hostAppPath}"" --transport tcp-bson://127.0.0.1:{port}/ ""{_executablePath}""";

            var processInfo = new ProcessStartInfo
            {
                Arguments = args,
                CreateNoWindow = true,
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            _log.Information("Starting previewer process for '{ExecutablePath}'", _executablePath);
            _log.Debug("> dotnet.exe {Args}", args);

            _process = Process.Start(processInfo);
            _process.OutputDataReceived += OutputReceived;
            _process.ErrorDataReceived += ErrorReceived;
            _process.Exited += ProcessExited;
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();

            if (!_process.HasExited)
            {
                _log.Information("Started previewer process for '{ExecutablePath}'", _executablePath);
                await tcs.Task;
            }
            else
            {
                throw new ApplicationException($"The previewer process exited unexpectedly with code {_process.ExitCode}.");
            }

            _log.Verbose("Started PreviewerProcess.StartAsync()");
        }

        public async Task UpdateXamlAsync(string xaml)
        {
            if (_process == null)
            {
                throw new InvalidOperationException("Process not started.");
            }

            if (_connection == null)
            {
                throw new InvalidOperationException("Process not finished initializing.");
            }

            await SendAsync(new UpdateXamlMessage
            {
                Xaml = xaml,
            });
        }

        public void Dispose()
        {
            _listener?.Dispose();

            if (_connection != null)
            {
                _connection.OnMessage -= OnMessage;
                _connection.OnException -= OnException;
                _connection.Dispose();
                _connection = null;
            }

            if (!_process.HasExited)
            {
                _process?.Kill();
            }

            _process = null;
        }

        void ILogEventEnricher.Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_process?.HasExited != true)
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Pid", _process?.Id ?? 0));
            }
        }

        private async Task ConnectionInitializedAsync(IAvaloniaRemoteTransportConnection connection, string xaml)
        {
            _log.Verbose("Started PreviewerProcess.ConnectionInitializedAsync()");
            _log.Information("Connection initialized");

            _connection = connection;
            _connection.OnException += OnException;
            _connection.OnMessage += OnMessage;

            await SendAsync(new UpdateXamlMessage
            {
                Xaml = xaml,
                AssemblyPath = _executablePath,
            });

            await SendAsync(new ClientSupportedPixelFormatsMessage
            {
                Formats = new[]
                {
                    Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888,
                    Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888,
                }
            });

            await SendAsync(new ClientRenderInfoMessage
            {
                DpiX = 96,
                DpiY = 96,
            });

            await SendAsync(new UpdateXamlMessage
            {
                Xaml = xaml,
            });

            _log.Verbose("Finished PreviewerProcess.ConnectionInitializedAsync()");
        }

        private Task SendAsync(object message)
        {
            _log.Debug("=> {@Message}", message);
            return _connection.Send(message);
        }

        private void OnMessage(IAvaloniaRemoteTransportConnection connection, object message)
        {
            OnMessageAsync(message).FireAndForget();
        }

        private async Task OnMessageAsync(object message)
        {
            _log.Verbose("Started PreviewerProcess.OnMessageAsync()");
            _log.Debug("<= {@Message}", message);

            if (message is FrameMessage frame)
            {
                if (Error == null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_bitmap == null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
                    {
                        _bitmap = new WriteableBitmap(
                            frame.Width,
                            frame.Height,
                            96,
                            96,
                            ToWpf(frame.Format),
                            null);
                    }

                    _bitmap.WritePixels(
                        new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight),
                        frame.Data,
                        frame.Stride,
                        0);

                    FrameReceived?.Invoke(this, new FrameReceivedEventArgs(_bitmap));
                }

                await SendAsync(new FrameReceivedMessage
                {
                    SequenceId = frame.SequenceId
                });
            }
            else if (message is UpdateXamlResultMessage update)
            {
                Error = update.Error;
            }

            _log.Verbose("Finished PreviewerProcess.OnMessageAsync()");
        }

        private void OnException(IAvaloniaRemoteTransportConnection connection, Exception ex)
        {
            _log.Error(ex, "Connection error");
        }

        private void OutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _log.Debug("<= {Data}", e.Data);
            }
        }

        private void ErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _log.Error("<= {Data}", e.Data);
            }
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            _log.Information("Process exited");
        }

        private static void EnsureExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not find '{path}'.");
            }
        }

        private static int FreeTcpPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private System.Windows.Media.PixelFormat ToWpf(Avalonia.Remote.Protocol.Viewport.PixelFormat format)
        {
            switch (format)
            {
                case Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888:
                    return PixelFormats.Bgra32;
                case Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgb565:
                    return PixelFormats.Bgr565;
                case Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888:
                    return PixelFormats.Pbgra32;
                default:
                    throw new NotSupportedException("Unsupported pixel format.");
            }
        }
    }
}
