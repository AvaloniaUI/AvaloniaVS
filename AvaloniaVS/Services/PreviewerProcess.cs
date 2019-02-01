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
        private ExceptionDetails _error;

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

        public ExceptionDetails Error
        {
            get => _error;
            private set
            {
                if (!Equals(_error, value))
                {
                    _error = value;
                    ErrorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsRunning => _process != null && !_process.HasExited;

        public event EventHandler ErrorChanged;
        public event EventHandler FrameReceived;
        
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
            _process.OutputDataReceived += ProcessOutputReceived;
            _process.ErrorDataReceived += ProcessErrorReceived;
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

            _log.Verbose("Finished PreviewerProcess.StartAsync()");
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
            _log.Verbose("Started PreviewerProcess.Dispose()");
            _log.Information("Closing previewer process");

            _listener?.Dispose();

            if (_connection != null)
            {
                _connection.OnMessage -= ConnectionMessageReceived;
                _connection.OnException -= ConnectionExceptionReceived;
                _connection.Dispose();
                _connection = null;
            }

            if (!_process.HasExited)
            {
                _process?.Kill();
            }

            _process = null;

            _log.Verbose("Finished PreviewerProcess.Dispose()");
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
            _connection.OnException += ConnectionExceptionReceived;
            _connection.OnMessage += ConnectionMessageReceived;

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

        private async Task SendAsync(object message)
        {
            _log.Debug("=> Sending {@Message}", message);
            await _connection.Send(message);
            _log.Debug("=> Sent {@Message}", message);
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

                    FrameReceived?.Invoke(this, EventArgs.Empty);
                }

                await SendAsync(new FrameReceivedMessage
                {
                    SequenceId = frame.SequenceId
                });
            }
            else if (message is UpdateXamlResultMessage update)
            {
                var error = update.Exception;

                if (error == null && !string.IsNullOrWhiteSpace(update.Error))
                {
                    error = new ExceptionDetails { Message = update.Error };
                }

                Error = error;
            }

            _log.Verbose("Finished PreviewerProcess.OnMessageAsync()");
        }

        private void ConnectionMessageReceived(IAvaloniaRemoteTransportConnection connection, object message)
        {
            OnMessageAsync(message).FireAndForget();
        }

        private void ConnectionExceptionReceived(IAvaloniaRemoteTransportConnection connection, Exception ex)
        {
            _log.Error(ex, "Connection error");
        }

        private void ProcessOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _log.Debug("<= {Data}", e.Data);
            }
        }

        private void ProcessErrorReceived(object sender, DataReceivedEventArgs e)
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

        private static bool Equals(ExceptionDetails a, ExceptionDetails b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            return a?.ExceptionType == b?.ExceptionType &&
                a?.Message == b?.Message &&
                a?.LineNumber == b?.LineNumber &&
                a?.LinePosition == b?.LinePosition;
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
