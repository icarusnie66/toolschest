using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MuxiaToolbox
{
    public class ToolInfo
    {
        public string Key, Name, Group, Icon, Summary;
        public string[] Instructions;
        public ToolInfo(string key, string name, string group, string icon, string summary, params string[] instructions)
        { Key = key; Name = name; Group = group; Icon = icon; Summary = summary; Instructions = instructions; }
    }

    public static class Catalog
    {
        public static readonly string[] Groups = { "图片应用", "文件清理", "共享与提取", "照片整理" };
        public static readonly List<ToolInfo> Tools = new List<ToolInfo> {
            new ToolInfo("screenshot", "截图识字与搜索", "图片应用", "▣", "框选屏幕区域，识别、搜索或保存图片。", "选择是否隐藏客户端界面。", "拖动鼠标框选区域。", "右键选择复制识别、搜索图片、保存本地或退出截图。"),
            new ToolInfo("empty_folders", "清理空文件夹", "文件清理", "□", "扫描指定目录中的空文件夹并移入回收站。", "选择文件夹或磁盘。", "开始扫描并核对列表。", "勾选后移入回收站。"),
            new ToolInfo("duplicate_files", "查找重复文件", "文件清理", "▤", "按大小和内容哈希准确查找重复文件。", "选择扫描位置。", "等待哈希比对完成。", "每组保留一份后移入回收站。"),
            new ToolInfo("empty_files", "清理空文件", "文件清理", "▱", "查找大小为 0 字节的文件并移入回收站。", "选择扫描位置。", "核对空文件列表。", "勾选后移入回收站。"),
            new ToolInfo("ftp", "局域网 FTP", "共享与提取", "⌁", "把指定文件夹临时共享给同一局域网中的设备。", "选择共享文件夹。", "设置端口、账号与权限。", "启动后使用显示的地址访问。"),
            new ToolInfo("article", "文章提炼", "共享与提取", "≡", "从长文章中提取一句话总结、核心要点或完整摘要。", "粘贴文章或导入文本。", "选择提炼模式。", "复制或导出结果。"),
            new ToolInfo("video", "视频转脚本", "共享与提取", "▷", "提取视频语音并整理为逐字稿、精简文稿或字幕。", "选择本地视频。", "选择输出类型。", "开始转写并导出结果。"),
            new ToolInfo("media_sort", "媒体分类", "照片整理", "▧", "把照片、GIF 动图和视频整理到三个文件夹。", "选择源和目标文件夹。", "预览分类结果。", "选择复制或移动后整理。"),
            new ToolInfo("smart_photos", "照片智能整理", "照片整理", "◇", "按来源、内容和拍摄信息重命名并细分照片。", "选择照片目录。", "预览分类和新文件名。", "核对低置信度项目后整理。")
        };
        public static ToolInfo Find(string key) { return Tools.First(t => t.Key == key); }
    }

    public class AppSettings
    {
        public string Theme = "system";
        public string Language = "zh-CN";
        public float FontSize = 10F;
        public int Scale = 100;
        public bool HideCapture = true;
        public List<string> Pinned = new List<string> { "screenshot", "duplicate_files", "smart_photos" };
        static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuxiaToolbox"); } }
        static string FileName { get { return Path.Combine(Folder, "settings.ini"); } }
        public static AppSettings Load()
        {
            AppSettings value = new AppSettings();
            try {
                foreach (string line in File.ReadAllLines(FileName)) {
                    string[] pair = line.Split(new[] {'='}, 2); if (pair.Length != 2) continue;
                    if (pair[0] == "theme") value.Theme = pair[1];
                    if (pair[0] == "dark") value.Theme = pair[1] == "1" ? "dark" : "light";
                    if (pair[0] == "language") value.Language = pair[1];
                    if (pair[0] == "font") value.FontSize = float.Parse(pair[1]);
                    if (pair[0] == "scale") value.Scale = int.Parse(pair[1]);
                    if (pair[0] == "hide") value.HideCapture = pair[1] == "1";
                    if (pair[0] == "pinned") value.Pinned = pair[1].Split(',').Where(x => x.Length > 0).ToList();
                }
            } catch { }
            if (value.Pinned.Remove("screenshot_ocr") | value.Pinned.Remove("screenshot_search")) {
                if (!value.Pinned.Contains("screenshot")) value.Pinned.Insert(0, "screenshot");
            }
            return value;
        }
        public void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(FileName, new[] {
                "theme=" + Theme, "language=" + Language, "font=" + FontSize, "scale=" + Scale,
                "hide=" + (HideCapture ? "1" : "0"), "pinned=" + string.Join(",", Pinned.ToArray())
            });
        }
    }
}

