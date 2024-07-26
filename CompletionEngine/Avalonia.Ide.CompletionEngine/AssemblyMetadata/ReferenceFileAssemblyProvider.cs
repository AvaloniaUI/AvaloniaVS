using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public class ReferenceFileAssemblyProvider : IAssemblyProvider
    {
        private readonly string _path;
        private readonly string _xamlPrimaryAssemblyPath;

        /// <summary>
        /// Create a new instance of <see cref="ReferenceFileAssemblyProvider"/>—an implementation of <see cref="IAssemblyProvider"/>.
        /// </summary>
        /// <param name="path">
        /// <para>
        /// The full path of a plaint text file.<br/>
        /// Each line in the file should be the full path of an assembly (e.g. <c>C:\Users\Username\.nuget\packages\avalonia\11.0.4\ref\net6.0\Avalonia.Base.dll</c>)
        /// </para>
        /// <b>EXAMPLES</b><br/>
        /// - <c>C:\Repos\RepoRoot\src\MyApp\Debug\net8.0\Avalonia\references</c><br/>
        /// - <c>C:\Repos\RepoRoot\src\artifacts\obj\MyApp\debug\Avalonia\references</c><br/>
        /// - <c>C:\Repos\RepoRoot\src\artifacts\obj\MyApp\debug_net8.0\Avalonia\references</c><br/>
        /// See <see href="https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output#examples">Artifacts output layout &amp;gt; Examples</see> for more 'artifacts' path examples.
        /// </param>
        /// <param name="xamlPrimaryAssemblyPath">Promary XAML Assembly path</param>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is null or empty</exception>
        public ReferenceFileAssemblyProvider(string path, string xamlPrimaryAssemblyPath)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            _path = path;
            _xamlPrimaryAssemblyPath = xamlPrimaryAssemblyPath;
        }

        /// <summary>
        /// Reads the plain text file at <see cref="path"/> and returns the referenced assemblies' full paths.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="IOException">Failed to read the project's references file.</exception>
        public IEnumerable<string> GetAssemblies()
        {
            List<string> result = new List<string>(300);
            if (!string.IsNullOrEmpty(_xamlPrimaryAssemblyPath))
            {
                result.Add(_xamlPrimaryAssemblyPath);
            }
            try
            {
                result.AddRange(File.ReadAllLines(_path));
            }
            catch (Exception ex) when
                (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to read file '{_path}'.", ex);
            }
            return result;
        }
    }
}
