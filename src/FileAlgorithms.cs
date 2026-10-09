using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MuxiaToolbox
{
    public static class FileAlgorithms
    {
        public static List<List<string>> FindDuplicates(string root)
        {
            Dictionary<long,List<string>> sizes = new Dictionary<long,List<string>>();
            foreach (string file in EnumerateFiles(root)) {
                try {
                    long size = new FileInfo(file).Length; if (size <= 0) continue;
                    if (!sizes.ContainsKey(size)) sizes[size] = new List<string>(); sizes[size].Add(file);
                } catch { }
            }
            Dictionary<string,List<string>> hashes = new Dictionary<string,List<string>>();
            foreach (KeyValuePair<long,List<string>> group in sizes.Where(x => x.Value.Count > 1)) {
                foreach (string file in group.Value) {
                    try {
                        string digest; using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(file)) digest = BitConverter.ToString(sha.ComputeHash(stream));
                        string key = group.Key + ":" + digest; if (!hashes.ContainsKey(key)) hashes[key] = new List<string>(); hashes[key].Add(file);
                    } catch { }
                }
            }
            return hashes.Values.Where(x => x.Count > 1).ToList();
        }

        public static IEnumerable<string> EnumerateFiles(string root)
        {
            Stack<string> pending = new Stack<string>(); HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); pending.Push(root);
            while (pending.Count > 0) {
                string folder = pending.Pop(), full; try { full = Path.GetFullPath(folder); } catch { continue; } if (!visited.Add(full)) continue;
                string[] files = new string[0], folders = new string[0]; try { files = Directory.GetFiles(folder); folders = Directory.GetDirectories(folder); } catch { }
                foreach (string file in files) yield return file;
                foreach (string child in folders) { try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue; } catch { } pending.Push(child); }
            }
        }
    }
}
