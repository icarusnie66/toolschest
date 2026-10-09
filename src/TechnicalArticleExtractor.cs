using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MuxiaToolbox
{
    public static class TechnicalArticleExtractor
    {
        class Paragraph { public int Number; public string Text; public int Score; }
        static readonly string[] Noise = { "相关阅读", "相关推荐", "关注我们", "扫码关注", "作者简介", "广告", "点击阅读原文", "点赞", "转发", "收藏" };
        static readonly string[] Technical = { "API", "SDK", "HTTP", "QPS", "延迟", "内存", "架构", "配置", "版本", "函数", "参数", "错误码", "数据库", "服务", "代码", "命令", "CVE", "RFC" };

        public static string Extract(string original)
        {
            if (string.IsNullOrWhiteSpace(original) || original.Trim().Length < 40) return "原文未提供可提炼的实质技术信息";
            List<string> codeBlocks = ExtractCodeBlocks(original);
            List<Paragraph> paragraphs = Clean(original);
            if (paragraphs.Count == 0) return "原文未提供可提炼的实质技术信息";
            bool technical = Technical.Any(k => original.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) || codeBlocks.Count > 0;
            string genre = DetectGenre(original);
            foreach (Paragraph p in paragraphs) p.Score = Score(p.Text, genre);
            List<Paragraph> ranked = paragraphs.OrderByDescending(p => p.Score).ThenBy(p => p.Number).ToList();
            if (original.Length < 1500) return ShortOutput(ranked, technical, codeBlocks);
            return LongOutput(paragraphs, ranked, technical, genre, codeBlocks, original.Length);
        }

        static List<Paragraph> Clean(string text)
        {
            text = Regex.Replace(text, @"```[\s\S]*?```", "\n【代码块已保留】\n");
            string[] raw = Regex.Split(text.Replace("\r", ""), @"\n\s*\n|\n");
            List<Paragraph> result = new List<Paragraph>(); int n = 0;
            foreach (string item in raw) {
                string value = Regex.Replace(item.Trim(), @"\s+", " ");
                if (value.Length == 0 || Noise.Any(x => value.StartsWith(x))) continue;
                if (Regex.IsMatch(value, @"^(大家好|你好|各位好|今天我想聊聊)[，,。 ]") && value.Length < 60) continue;
                n++; result.Add(new Paragraph { Number = n, Text = value });
            }
            return result;
        }

        static List<string> ExtractCodeBlocks(string text)
        {
            return Regex.Matches(text, @"```[\s\S]*?```").Cast<Match>().Select(m => m.Value.Trim()).ToList();
        }

        static string DetectGenre(string text)
        {
            if (Regex.IsMatch(text, @"(事故|故障|根因|影响面|恢复时间|复盘|postmortem)", RegexOptions.IgnoreCase)) return "故障复盘 / Postmortem";
            if (Regex.IsMatch(text, @"(changelog|release note|新增|弃用|移除|breaking change)", RegexOptions.IgnoreCase)) return "Changelog / Release Note";
            if (Regex.IsMatch(text, @"(RFC|架构设计|设计决策|替代方案|未决项)", RegexOptions.IgnoreCase)) return "架构设计 / RFC";
            if (Regex.IsMatch(text, @"(实验|论文|研究问题|数据集|相关工作|局限性)", RegexOptions.IgnoreCase)) return "论文 / 深度长文";
            if (Regex.IsMatch(text, @"(endpoint|端点|返回值|错误码|鉴权|API|SDK)", RegexOptions.IgnoreCase)) return "API / SDK 文档";
            return "技术博客 / 教程";
        }

        static int Score(string text, string genre)
        {
            int score = 0;
            score += Regex.Matches(text, @"\d+(\.\d+)?(%|ms|s|MB|GB|QPS|P\d+|倍)?", RegexOptions.IgnoreCase).Count * 5;
            score += Regex.Matches(text, @"(因为|因此|所以|导致|采用|取舍|相比|但|限制|不要|必须|错误|配置|参数|版本|结论|根因|影响)").Count * 4;
            score += Technical.Count(k => text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) * 2;
            if (text.Contains("【代码块已保留】")) score += 8;
            if (genre.StartsWith("故障") && Regex.IsMatch(text, "根因|影响|缓解|预防")) score += 8;
            if (genre.StartsWith("API") && Regex.IsMatch(text, "参数|返回|鉴权|错误码")) score += 8;
            return score;
        }

        static string ShortOutput(List<Paragraph> ranked, bool technical, List<string> codeBlocks)
        {
            StringBuilder b = new StringBuilder();
            if (!technical) b.AppendLine("内容非技术类，术语提取可能不准确").AppendLine();
            b.AppendLine("## 核心要点");
            foreach (Paragraph p in ranked.Take(Math.Min(5, Math.Max(3, ranked.Count)))) b.AppendLine(KeyLine(p));
            AppendCode(b, codeBlocks); return Limit(b.ToString(), Math.Max(300, ranked.Sum(p => p.Text.Length) / 4));
        }

        static string LongOutput(List<Paragraph> paragraphs, List<Paragraph> ranked, bool technical, string genre, List<string> codeBlocks, int originalLength)
        {
            StringBuilder b = new StringBuilder();
            if (!technical) b.AppendLine("内容非技术类，术语提取可能不准确").AppendLine();
            Paragraph core = ranked.First();
            b.AppendLine("一句话总结：" + Clip(Tagged(core.Text), 40)).AppendLine();
            b.AppendLine("核心要点：");
            int keyLimit = Math.Max(120, (int)(originalLength * .08)); int used = 0;
            foreach (Paragraph p in ranked.Take(7)) {
                string line = KeyLine(p);
                if (used + line.Length > keyLimit && used > 0) break; b.AppendLine(line); used += line.Length;
            }
            b.AppendLine().AppendLine("## 背景与目标");
            AppendSelected(b, paragraphs.Where(p => Regex.IsMatch(p.Text, "背景|目标|问题|场景|动机")).Take(2), paragraphs.Take(1));
            b.AppendLine().AppendLine("## 核心方案 / 技术要点");
            AppendSelected(b, ranked.Where(p => Regex.IsMatch(p.Text, "采用|方案|实现|配置|参数|步骤|机制|设计|API|命令")).Take(6), ranked.Take(4));
            b.AppendLine().AppendLine("## 关键数据 / 实验结果");
            List<Paragraph> data = paragraphs.Where(p => Regex.IsMatch(p.Text, @"\d+(\.\d+)?(%|ms|s|MB|GB|QPS|P\d+|倍)", RegexOptions.IgnoreCase)).Take(5).ToList();
            if (data.Count == 0) b.AppendLine("原文未提供量化数据"); else AppendSelected(b, data, data);
            List<Paragraph> limits = paragraphs.Where(p => Regex.IsMatch(p.Text, "限制|局限|不适用|缺陷|仅限|前提|风险|但")).Take(4).ToList();
            if (limits.Count > 0) { b.AppendLine().AppendLine("## 局限性与边界条件"); AppendSelected(b, limits, limits); }
            List<Paragraph> todo = paragraphs.Where(p => Regex.IsMatch(p.Text, "TODO|待办|后续|下一步|未决|计划", RegexOptions.IgnoreCase)).Take(4).ToList();
            if (todo.Count > 0) { b.AppendLine().AppendLine("## 待办 / 后续动作"); AppendSelected(b, todo, todo); }
            AppendCode(b, codeBlocks);
            List<string> refs = Regex.Matches(string.Join("\n", paragraphs.Select(p => p.Text).ToArray()), @"https?://\S+|\b(?:CVE-\d{4}-\d+|RFC\s?\d+)\b", RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Value.TrimEnd('。', '，', ',', ')')).Distinct().ToList();
            if (refs.Count > 0) { b.AppendLine().AppendLine("## 参考"); foreach (string r in refs) b.AppendLine("- " + r); }
            return Limit(b.ToString(), Math.Max(1, (int)(originalLength * .25)));
        }

        static void AppendSelected(StringBuilder b, IEnumerable<Paragraph> primary, IEnumerable<Paragraph> fallback)
        {
            List<Paragraph> rows = primary.ToList(); if (rows.Count == 0) rows = fallback.ToList();
            foreach (Paragraph p in rows) b.AppendLine(Tagged(p.Text) + "〔段落" + p.Number + "〕");
        }
        static string Tagged(string text)
        {
            if (Regex.IsMatch(text, "我认为|作者认为|看来|显然|最佳|应该|建议")) return "【观点】" + text;
            if (Regex.IsMatch(text, "可能|推测|或许|预计")) return "【推断】" + text;
            if (Regex.IsMatch(text, "矛盾|一方面.*另一方面")) return "【存疑】" + text;
            return text;
        }
        static string Anchor(string text)
        {
            Match m = Regex.Match(text, @"[A-Za-z][A-Za-z0-9_.+/#-]{1,24}|[\u4e00-\u9fff]{2,8}"); return m.Success ? m.Value : "要点";
        }
        static string Clip(string value, int max)
        { if (value.Length <= max) return value; return value.Substring(0, Math.Max(1,max-1)).TrimEnd('，','。',',',' ') + "…"; }
        static string KeyLine(Paragraph p)
        {
            string prefix = "- **" + Anchor(p.Text) + "** ";
            string suffix = "〔段落" + p.Number + "〕";
            return prefix + Clip(Tagged(p.Text), Math.Max(1, 80 - prefix.Length - suffix.Length)) + suffix;
        }
        static void AppendCode(StringBuilder b, List<string> blocks)
        { if (blocks.Count == 0) return; b.AppendLine().AppendLine("## 关键代码与命令"); foreach (string code in blocks) b.AppendLine(code).AppendLine(); }
        static string Limit(string value, int max)
        {
            value = value.Trim();
            if (value.Length <= max) return value;
            const string suffix = "\n【原文未明确】输出已按长度规则截断。";
            if (max <= suffix.Length) return value.Substring(0, max);
            return value.Substring(0, max - suffix.Length).TrimEnd() + suffix;
        }
    }
}
