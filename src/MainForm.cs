using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuxiaToolbox
{
    public class ToolCardButton : Button
    {
        public ToolCardButton() { SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true); }
    }

    public class MainForm : Form
    {
        public AppSettings Settings;
        public Color Bg, Surface, Panel, TextColor, Muted, Border, Accent, AccentSoft;
        Panel content;
        ToolInfo selected;
        Panel helpPanel;
        Panel toolArea;

        public MainForm()
        {
            Settings = AppSettings.Load();
            Text = "工具宝匣"; StartPosition = FormStartPosition.CenterScreen;
            Width = 1280; Height = 800; MinimumSize = new Size(1000, 650);
            Font = new Font("Microsoft YaHei UI", Settings.FontSize, FontStyle.Regular);
            ApplyPalette(); ShowHome();
        }

        public void ApplyPalette()
        {
            bool dark = Settings.Theme == "dark" || (Settings.Theme == "system" && SystemUsesDarkTheme());
            if (dark) {
                Bg = Color.FromArgb(22,26,30); Surface = Color.FromArgb(32,37,42); Panel = Color.FromArgb(37,43,49);
                TextColor = Color.FromArgb(237,242,245); Muted = Color.FromArgb(169,179,188); Border = Color.FromArgb(57,66,74);
                Accent = Color.FromArgb(40,183,214); AccentSoft = Color.FromArgb(21,63,73);
            } else {
                Bg = Color.FromArgb(244,246,248); Surface = Color.White; Panel = Color.FromArgb(248,250,251);
                TextColor = Color.FromArgb(24,33,43); Muted = Color.FromArgb(101,113,127); Border = Color.FromArgb(220,227,232);
                Accent = Color.FromArgb(7,153,189); AccentSoft = Color.FromArgb(231,247,251);
            }
            BackColor = Bg; ForeColor = TextColor; Font = new Font("Microsoft YaHei UI", Settings.FontSize);
        }

        bool SystemUsesDarkTheme()
        {
            try {
                object value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return Convert.ToInt32(value) == 0;
            } catch { return false; }
        }

        public string T(string simplified, string traditional, string english)
        {
            if (Settings.Language == "en") return english;
            if (Settings.Language == "zh-TW") return traditional;
            return simplified;
        }

        public string L(string value)
        {
            if (Settings.Language == "zh-CN") return value;
            Dictionary<string,string> map = Settings.Language == "en" ? English : Traditional;
            return map.ContainsKey(value) ? map[value] : value;
        }

        static readonly Dictionary<string,string> English = new Dictionary<string,string> {
            {"图片应用","Image tools"},{"文件清理","File cleanup"},{"共享与提取","Share & extract"},{"照片整理","Photo organizer"},
            {"截图识字与搜索","Screenshot OCR & search"},{"清理空文件夹","Empty folders"},{"查找重复文件","Duplicate files"},{"清理空文件","Empty files"},{"局域网 FTP","LAN FTP"},{"文章提炼","Article extractor"},{"视频转脚本","Video transcript"},{"媒体分类","Media sorter"},{"照片智能整理","Smart photos"},
            {"开始扫描","Scan"},{"浏览","Browse"},{"等待扫描","Ready"},{"路径","Path"},{"修改时间","Modified"},{"将勾选项移入回收站","Move checked items to Recycle Bin"},
            {"开始截图  Ctrl+Shift+A","Capture  Ctrl+Shift+A"},{"截图时隐藏客户端","Hide client window while capturing"},{"保存到本地","Save"},{"搜索图片","Image search"},
            {"启动服务","Start server"},{"停止服务","Stop server"},{"匿名访问","Anonymous"},{"允许上传","Allow uploads"},{"共享文件夹","Shared folder"},
            {"导入文档","Import"},{"按规则提炼","Extract"},{"复制结果","Copy"},{"自动判定体裁与复杂度","Automatic genre and complexity detection"},
            {"选择视频","Choose video"},{"开始转写","Transcribe"},{"导出结果","Export"},{"预览分类","Preview"},{"开始整理","Organize"}
        };
        static readonly Dictionary<string,string> Traditional = new Dictionary<string,string> {
            {"图片应用","圖片應用"},{"文件清理","檔案清理"},{"共享与提取","共享與擷取"},{"照片整理","照片整理"},
            {"截图识字与搜索","截圖識字與搜尋"},{"清理空文件夹","清理空資料夾"},{"查找重复文件","尋找重複檔案"},{"清理空文件","清理空檔案"},{"局域网 FTP","區域網路 FTP"},{"文章提炼","文章提煉"},{"视频转脚本","影片轉腳本"},{"媒体分类","媒體分類"},{"照片智能整理","照片智慧整理"},
            {"开始扫描","開始掃描"},{"浏览","瀏覽"},{"等待扫描","等待掃描"},{"路径","路徑"},{"修改时间","修改時間"},{"将勾选项移入回收站","將勾選項移入資源回收筒"},
            {"开始截图  Ctrl+Shift+A","開始截圖  Ctrl+Shift+A"},{"截图时隐藏客户端","截圖時隱藏用戶端"},{"保存到本地","儲存到本機"},{"搜索图片","搜尋圖片"},
            {"启动服务","啟動服務"},{"停止服务","停止服務"},{"匿名访问","匿名存取"},{"允许上传","允許上傳"},{"共享文件夹","共享資料夾"},
            {"导入文档","匯入文件"},{"按规则提炼","依規則提煉"},{"复制结果","複製結果"},{"自动判定体裁与复杂度","自動判定體裁與複雜度"},
            {"选择视频","選擇影片"},{"开始转写","開始轉寫"},{"导出结果","匯出結果"},{"预览分类","預覽分類"},{"开始整理","開始整理"}
        };

        void TranslateTree(Control root)
        {
            foreach (Control child in root.Controls) { child.Text = L(child.Text); TranslateTree(child); }
        }

        public Label Label(string text, float size, bool bold, Color? color)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = color ?? TextColor,
                Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(4) };
        }

        public Button Button(string text, EventHandler click, bool accent)
        {
            Button value = new Button { Text = text, AutoSize = true, Height = 38, FlatStyle = FlatStyle.Flat,
                BackColor = accent ? Accent : Surface, ForeColor = accent ? Color.White : TextColor, Padding = new Padding(10,4,10,4), Margin = new Padding(5) };
            value.FlatAppearance.BorderColor = accent ? Accent : Border; value.Click += click; return value;
        }

        void ResetContent()
        {
            Controls.Clear();
            content = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            Controls.Add(content);
        }

        void AttachPage(Panel header, Control body)
        {
            TableLayoutPanel frame = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Dock = DockStyle.Fill; body.Dock = DockStyle.Fill;
            frame.Controls.Add(header, 0, 0); frame.Controls.Add(body, 0, 1);
            content.Controls.Add(frame);
        }

        public Panel Header(string page, bool home)
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Surface, Padding = new Padding(18,10,18,10) };
            int x = 18;
            if (!home) { Button back = Button("← " + T("返回", "返回", "Back"), delegate { ShowHome(); }, false); back.Location = new Point(x, 10); header.Controls.Add(back); x += 92; }
            string caption = home ? "▣  " + T("工具宝匣", "工具寶匣", "Tool Chest") : page;
            Label title = Label(caption, Settings.FontSize + 2, true, TextColor);
            title.Location = new Point(x, 20); header.Controls.Add(title); return header;
        }

        public void ShowHome()
        {
            ResetContent(); selected = Catalog.Tools[0];
            Panel header = Header("", true);
            Button settings = Button("⚙ " + T("设置", "設定", "Settings"), delegate { ShowSettings(); }, false);
            settings.AutoSize = false; settings.Size = new Size(92, 40); settings.Location = new Point(header.ClientSize.Width - settings.Width - 18, 10);
            settings.Anchor = AnchorStyles.Top | AnchorStyles.Right; header.Controls.Add(settings);
            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = Bg };
            AttachPage(header, body);
            helpPanel = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = Surface, Padding = new Padding(18), AutoScroll = true };
            body.Controls.Add(helpPanel);
            Panel left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0,0,18,0), BackColor = Bg };
            body.Controls.Add(left); left.BringToFront();
            Panel top = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Bg };
            top.Controls.Add(Label(T("工具主页", "工具主頁", "Tools"), Settings.FontSize + 8, true, TextColor)); left.Controls.Add(top);
            toolArea = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
            left.Controls.Add(toolArea); toolArea.BringToFront();
            Button collapse = new Button { Name = "HelpCollapseButton", Text = "▶", AutoSize = false, Size = new Size(32,54), Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = TextColor, Font = new Font("Segoe UI Symbol", 14F, FontStyle.Bold), TabStop = false };
            collapse.FlatAppearance.BorderSize = 0;
            Action positionArrow = delegate { collapse.Location = new Point(Math.Max(0, body.ClientSize.Width - collapse.Width - 1), Math.Max(0, (body.ClientSize.Height - collapse.Height) / 2)); collapse.BringToFront(); };
            collapse.Click += delegate { helpPanel.Visible = !helpPanel.Visible; collapse.Text = helpPanel.Visible ? "▶" : "◀"; positionArrow(); };
            body.Controls.Add(collapse); body.Resize += delegate { positionArrow(); }; positionArrow();
            RenderTools(); RenderHelp();
        }

        void RenderTools()
        {
            toolArea.Controls.Clear(); int y = 0;
            List<ToolInfo> pinned = Settings.Pinned.Select(k => Catalog.Tools.FirstOrDefault(t => t.Key == k)).Where(t => t != null).ToList();
            y = AddGroup(T("常用工具", "常用工具", "Pinned"), pinned, y);
            foreach (string group in Catalog.Groups) y = AddGroup(L(group), Catalog.Tools.Where(t => t.Group == group).ToList(), y);
        }

        int AddGroup(string name, List<ToolInfo> tools, int y)
        {
            Label heading = Label(name, Settings.FontSize + 3, true, TextColor); heading.Location = new Point(4,y); toolArea.Controls.Add(heading); y += 38;
            int width = Math.Max(250, (toolArea.ClientSize.Width - 45) / 3); const int rowHeight = 66;
            for (int i=0;i<tools.Count;i++) {
                ToolInfo tool = tools[i]; ToolCardButton b = new ToolCardButton { Text = tool.Icon + "  " + L(tool.Name),
                    TextAlign = ContentAlignment.MiddleLeft, Width = width, Height = 56, BackColor = Surface, ForeColor = TextColor,
                    FlatStyle = FlatStyle.Flat, Font = Font, Location = new Point((i%3)*(width+10), y+(i/3)*rowHeight), Tag = tool };
                b.FlatAppearance.BorderColor = Border; b.Click += delegate(object s, EventArgs e) { selected = (ToolInfo)((Button)s).Tag; RenderHelp(); };
                b.MouseDoubleClick += delegate(object s, MouseEventArgs e) { OpenTool(((ToolInfo)((Button)s).Tag).Key); }; toolArea.Controls.Add(b);
            }
            return y + ((tools.Count + 2) / 3) * rowHeight + 18;
        }

        void RenderHelp()
        {
            helpPanel.Controls.Clear();
            Panel inner = new Panel { BackColor = Surface, Width = 250, Height = 470 };
            Action center = delegate { inner.Location = new Point(Math.Max(0,(helpPanel.ClientSize.Width-inner.Width)/2-4), Math.Max(8,(helpPanel.ClientSize.Height-inner.Height)/2)); };
            helpPanel.Controls.Add(inner); helpPanel.Resize += delegate { center(); }; center(); int y = 8;
            Label title = Label(L(selected.Name), Settings.FontSize + 3, true, TextColor); title.Location = new Point(0,y); inner.Controls.Add(title);
            bool isPinned = Settings.Pinned.Contains(selected.Key);
            Button pin = Button(isPinned ? "★" : "☆", delegate {
                if (Settings.Pinned.Contains(selected.Key)) Settings.Pinned.Remove(selected.Key); else Settings.Pinned.Add(selected.Key);
                Settings.Save(); RenderTools(); RenderHelp();
            }, false); pin.Name="PinButton"; pin.AutoSize=false; pin.Size=new Size(44,40); pin.Location = new Point(201,0); pin.Font=new Font("Segoe UI Symbol",16F,FontStyle.Regular); pin.AccessibleName=isPinned?T("取消置顶","取消置頂","Unpin"):T("置顶","置頂","Pin"); ToolTip pinTip=new ToolTip(); pinTip.SetToolTip(pin,pin.AccessibleName); pin.Tag=pinTip; inner.Controls.Add(pin); y += 62;
            Label h1 = Label(T("功能说明", "功能說明", "Features"), Settings.FontSize + 1, true, TextColor); h1.Location = new Point(0,y); inner.Controls.Add(h1); y += 32;
            Label summary = Label(selected.Summary, Settings.FontSize, false, Muted); summary.MaximumSize = new Size(245,0); summary.Location = new Point(0,y); inner.Controls.Add(summary); y += summary.PreferredHeight + 25;
            Label h2 = Label(T("使用说明", "使用說明", "How to use"), Settings.FontSize + 1, true, TextColor); h2.Location = new Point(0,y); inner.Controls.Add(h2); y += 34;
            for (int i=0;i<selected.Instructions.Length;i++) { Label line = Label((i+1)+". "+selected.Instructions[i], Settings.FontSize, false, Muted); line.MaximumSize = new Size(245,0); line.Location = new Point(0,y); inner.Controls.Add(line); y += line.PreferredHeight + 10; }
            Button open = Button(T("打开工具", "開啟工具", "Open tool"), delegate { OpenTool(selected.Key); }, true); open.Width = 245; open.Location = new Point(0,y+14); inner.Controls.Add(open); inner.Height = y + 70; center();
        }

        public void OpenTool(string key)
        {
            ResetContent(); ToolInfo info = Catalog.Find(key); Panel header = Header(L(info.Name), false);
            Control page = ToolPages.Create(this, key); TranslateTree(page); AttachPage(header, page);
        }

        public void ShowSettings()
        {
            ResetContent(); Panel header = Header(T("设置", "設定", "Settings"), false);
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = Bg, AutoScroll = true }; AttachPage(header, body);
            Panel card = new Panel { BackColor = Bg, Size = new Size(920,520) }; body.Controls.Add(card);
            Action center = delegate { card.Location = new Point(Math.Max(20,(body.ClientSize.Width-card.Width)/2), Math.Max(20,(body.ClientSize.Height-card.Height)/2)); };
            body.Resize += delegate { center(); }; center();
            Label title = Label(T("设置", "設定", "Settings"), Settings.FontSize + 8, true, TextColor); title.Location = new Point(0,0); card.Controls.Add(title);
            GroupBox ui = new GroupBox { Text = T("界面设置", "介面設定", "Interface"), ForeColor = TextColor, BackColor = Bg, Font = Font, Location = new Point(0,60), Size = new Size(920,275) }; card.Controls.Add(ui);
            Label fontLabel = Label(T("字体大小", "字型大小", "Font size"), Settings.FontSize, false, TextColor); fontLabel.Location = new Point(28,42); ui.Controls.Add(fontLabel);
            ComboBox font = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175,38), Width = 180 };
            font.Items.AddRange(new object[] { T("小（9）", "小（9）", "Small (9)"), T("标准（10）", "標準（10）", "Standard (10)"), T("大（12）", "大（12）", "Large (12)") }); font.SelectedIndex = Settings.FontSize <= 9 ? 0 : Settings.FontSize >= 12 ? 2 : 1; ui.Controls.Add(font);
            Label modeLabel = Label(T("外观模式", "外觀模式", "Appearance"), Settings.FontSize, false, TextColor); modeLabel.Location = new Point(28,92); ui.Controls.Add(modeLabel);
            RadioButton light = new RadioButton { Text = T("浅色", "淺色", "Light"), Location = new Point(175,90), ForeColor = TextColor, Checked = Settings.Theme == "light", AutoSize = true };
            RadioButton dark = new RadioButton { Text = T("深色", "深色", "Dark"), Location = new Point(265,90), ForeColor = TextColor, Checked = Settings.Theme == "dark", AutoSize = true };
            RadioButton system = new RadioButton { Text = T("跟随系统", "跟隨系統", "Use system"), Location = new Point(355,90), ForeColor = TextColor, Checked = Settings.Theme == "system", AutoSize = true }; ui.Controls.Add(light); ui.Controls.Add(dark); ui.Controls.Add(system);
            Label scaleLabel = Label(T("界面缩放", "介面縮放", "Scale"), Settings.FontSize, false, TextColor); scaleLabel.Location = new Point(28,142); ui.Controls.Add(scaleLabel);
            ComboBox scale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175,138), Width = 180 };
            scale.Items.AddRange(new object[] { "90%", "100%", "110%", "125%" }); scale.SelectedItem = Settings.Scale + "%"; if (scale.SelectedIndex < 0) scale.SelectedIndex = 1; ui.Controls.Add(scale);
            Label languageLabel = Label(T("语言", "語言", "Language"), Settings.FontSize, false, TextColor); languageLabel.Location = new Point(28,192); ui.Controls.Add(languageLabel);
            ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(175,188), Width = 180 };
            language.Items.AddRange(new object[] { "中文简体", "中文繁體", "English" }); language.SelectedIndex = Settings.Language == "zh-TW" ? 1 : Settings.Language == "en" ? 2 : 0; ui.Controls.Add(language);
            GroupBox general = new GroupBox { Text = T("通用设置", "一般設定", "General"), ForeColor = TextColor, BackColor = Bg, Font = Font, Location = new Point(0,355), Size = new Size(920,95) }; card.Controls.Add(general);
            CheckBox hide = new CheckBox { Text = T("截图时默认隐藏客户端界面", "截圖時預設隱藏用戶端介面", "Hide client window while capturing"), Checked = Settings.HideCapture, ForeColor = TextColor, AutoSize = true, Location = new Point(28,40) }; general.Controls.Add(hide);
            Button apply = Button(T("应用设置", "套用設定", "Apply"), delegate {
                Settings.FontSize = font.SelectedIndex == 0 ? 9F : font.SelectedIndex == 2 ? 12F : 10F;
                Settings.Theme = dark.Checked ? "dark" : light.Checked ? "light" : "system"; Settings.Scale = int.Parse(scale.Text.Replace("%", "")); Settings.Language = language.SelectedIndex == 1 ? "zh-TW" : language.SelectedIndex == 2 ? "en" : "zh-CN"; Settings.HideCapture = hide.Checked; Settings.Save(); ApplyPalette(); ShowSettings();
            }, true); apply.Location = new Point(795,470); card.Controls.Add(apply);
        }
    }
}
