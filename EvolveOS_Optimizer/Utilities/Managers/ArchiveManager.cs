using System;
using System.IO;
using System.IO.Compression;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    internal static class ArchiveManager
    {
        internal static void Unarchive(string path, byte[] resource)
        {
            string? folderDir = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(folderDir))
            {
                folderDir = AppContext.BaseDirectory;
            }

            if (!Directory.Exists(folderDir))
            {
                Directory.CreateDirectory(folderDir);
            }

            using MemoryStream fileOut = new MemoryStream(resource);
            using GZipStream gzipStream = new GZipStream(fileOut, CompressionMode.Decompress);
            using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

            gzipStream.CopyTo(fileStream);
        }

        public static byte[] GetResourceBytes(string resourceName)
        {
            // The name is usually "ProjectNamespace.FolderName.FileName"
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream($"EvolveOS_Optimizer.Resources.{resourceName}"))
            {
                if (stream == null) return Array.Empty<byte>();
                byte[] ba = new byte[stream.Length];
                stream.Read(ba, 0, ba.Length);
                return ba;
            }
        }
    }
}