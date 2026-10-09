using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;

namespace MuxiaToolbox
{
    public static class EmbeddedOcrRuntime
    {
        const string ResourceName = "ToolChest.TesseractRuntime";
        const string RuntimeVersion = "5.4.0-embedded-v1";

        public static string EnsureAvailable()
        {
            string projectRuntime = Path.Combine(ProjectRoot(), "runtime", "tesseract");
            string projectExe = Path.Combine(projectRuntime, "tesseract.exe");
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream resource = assembly.GetManifestResourceStream(ResourceName);
            if (resource == null) return File.Exists(projectExe) ? projectExe : null;

            string overrideRoot = Environment.GetEnvironmentVariable("TOOL_CHEST_OCR_CACHE");
            string cacheBase = string.IsNullOrWhiteSpace(overrideRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolChest", "ocr")
                : overrideRoot;
            string target = Path.Combine(cacheBase, RuntimeVersion);
            string exe = Path.Combine(target, "tesseract.exe");
            string ready = Path.Combine(target, ".ready");
            if (File.Exists(exe) && File.Exists(ready)) { resource.Dispose(); return exe; }

            using (resource)
            using (Mutex mutex = new Mutex(false, "Local\\ToolChestEmbeddedOcr540")) {
                if (!mutex.WaitOne(TimeSpan.FromMinutes(2))) throw new IOException("等待内置 OCR 组件释放超时。");
                try {
                    if (File.Exists(exe) && File.Exists(ready)) return exe;
                    Directory.CreateDirectory(target);
                    string safeRoot = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
                    using (ZipArchive zip = new ZipArchive(resource, ZipArchiveMode.Read, false)) {
                        foreach (ZipArchiveEntry entry in zip.Entries) {
                            string destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
                            if (!destination.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("OCR 资源包含无效路径。");
                            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            entry.ExtractToFile(destination, true);
                        }
                    }
                    if (!File.Exists(exe)) throw new FileNotFoundException("内置 OCR 组件释放不完整。", exe);
                    File.WriteAllText(ready, RuntimeVersion);
                    return exe;
                } finally { mutex.ReleaseMutex(); }
            }
        }

        static string ProjectRoot()
        {
            string configured = Environment.GetEnvironmentVariable("TOOL_CHEST_ROOT");
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) return configured;
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        }
    }
}
