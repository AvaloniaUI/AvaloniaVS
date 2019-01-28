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
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Services
{
    public class PreviewerProcess : IDisposable
    {
        private readonly string _executablePath;
        private Process _process;
        private IAvaloniaRemoteTransportConnection _connection;
        private IDisposable _listener;
        private string _xaml;

        public PreviewerProcess(string executablePath)
        {
            _executablePath = executablePath;
        }

        public bool IsRunning => _process != null && !_process.HasExited;

        public event EventHandler<FrameReceivedEventArgs> FrameReceived;
        public event EventHandler<ResizedEventArgs> Resized;

        public void Start(string xaml)
        {
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

            _xaml = xaml;
            _listener = new BsonTcpTransport().Listen(
                IPAddress.Loopback,
                port,
                t => ConnectionInitializedAsync(t).FireAndForget());

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

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = args,
                    CreateNoWindow = true,
                    FileName = "dotnet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            _process.OutputDataReceived += OutputReceived;
            _process.ErrorDataReceived += ErrorReceived;
            _process.Exited += ProcessExited;

            Debug.WriteLine($"Starting process for '{_executablePath}'.");
            Debug.WriteLine($" - dotnet {args}.");

            _process.Start();

            if (!_process.HasExited)
            {
                Debug.WriteLine($"Process Id: {_process.Id}.");
            }
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

        private async Task ConnectionInitializedAsync(IAvaloniaRemoteTransportConnection connection)
        {
            Debug.WriteLine("Connection initialized.");

            _connection = connection;
            _connection.OnException += OnException;
            _connection.OnMessage += OnMessage;

            Debug.WriteLine("Sending UpdateXamlMessage.");

            await connection.Send(new UpdateXamlMessage
            {
                Xaml = _xaml,
                AssemblyPath = _executablePath,
            });

            Debug.WriteLine("Sending ClientSupportedPixelFormatsMessage.");

            await connection.Send(new ClientSupportedPixelFormatsMessage
            {
                Formats = new[]
                {
                    Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888,
                    Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888,
                }
            });

            Debug.WriteLine("Sending ClientRenderInfoMessage.");

            await connection.Send(new ClientRenderInfoMessage
            {
                DpiX = 96,
                DpiY = 96,
            });

            Debug.WriteLine("Sending UpdateXamlMessage.");

            await connection.Send(new UpdateXamlMessage
            {
                Xaml = _xaml,
            });
        }

        private void OnMessage(IAvaloniaRemoteTransportConnection connection, object message)
        {
            OnMessageAsync(message).FireAndForget();
        }

        private async Task OnMessageAsync(object message)
        {
            Debug.WriteLine($"Message received: '{message.GetType()?.Name}'.");

            if (message is FrameMessage frame)
            {
                Debug.WriteLine($"Frame received {frame.Width}x{frame.Height}.");

                if (FrameReceived != null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var bitmap = BitmapSource.Create(
                        frame.Width,
                        frame.Height,
                        96,
                        96,
                        ToWpf(frame.Format),
                        null,
                        frame.Data,
                        frame.Stride);
                    FrameReceived(this, new FrameReceivedEventArgs(bitmap));
                }

                await _connection.Send(new FrameReceivedMessage
                {
                    SequenceId = frame.SequenceId
                });
            }
            else if (message is RequestViewportResizeMessage resize)
            {
                Debug.WriteLine($"Resized to {resize.Width}x{resize.Height}.");

                if (Resized != null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    Resized(this, new ResizedEventArgs(new Size(resize.Width, resize.Height)));
                }
            }
        }

        private void OnException(IAvaloniaRemoteTransportConnection connection, Exception ex)
        {
        }

        private void OutputReceived(object sender, DataReceivedEventArgs e)
        {
        }

        private void ErrorReceived(object sender, DataReceivedEventArgs e)
        {
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            Debug.WriteLine($"Process {_process.Id} exited.");
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

        private static Process StartProcess(
            string commandName,
            string args,
            Action<object, DataReceivedEventArgs> outputReceivedCallback,
            Action<object, DataReceivedEventArgs> errorReceivedCallback = null,
            string workingDirectory = "",
            params string[] extraPaths)
        {
            var shellProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                }
            };

            shellProc.StartInfo.FileName = commandName;
            shellProc.StartInfo.Arguments = args;
            shellProc.StartInfo.CreateNoWindow = true;

            shellProc.OutputDataReceived += (s, a) => outputReceivedCallback(s, a);

            if (errorReceivedCallback != null)
            {
                shellProc.ErrorDataReceived += (s, a) => errorReceivedCallback(s, a);
            }

            shellProc.EnableRaisingEvents = true;
            shellProc.Start();
            shellProc.BeginOutputReadLine();
            shellProc.BeginErrorReadLine();

            return shellProc;
        }
    }
}
