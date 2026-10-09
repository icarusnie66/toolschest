using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace MuxiaToolbox
{
    public static class ToolPages
    {
        public static Control Create(MainForm app, string key)
        {
            if (key == "screenshot") return ScreenshotPage(app, key);
            if (key == "empty_folders" || key == "empty_files") return FileCleanupPage(app, key);
            if (key == "duplicate_files") return DuplicatePage(app);
            if (key == "ftp") return FtpPage(app);
            if (key == "article") return ArticlePage(app);
            if (key == "video") return VideoPage(app);
            if (key == "media_sort") return MediaPage(app);
            return SmartPhotoPage(app);
        }

        static Panel Base(MainForm app, string key, out Panel work)
        {
            ToolInfo info = Catalog.Find(key);
            Panel root = new Panel { BackColor = app.Bg, Padding = new Padding(25), AutoScroll = true };
            Label title = app.Label(info.Name, app.Settings.FontSize + 8, true, app.TextColor); title.Location = new Point(25,20); root.Controls.Add(title);
            Label sub = app.Label(info.Summary, app.Settings.FontSize, false, app.Muted); sub.Location = new Point(25,60); root.Controls.Add(sub);
            Panel workPanel = new Panel { BackColor = app.Bg, Location = new Point(25,95), Anchor = AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right };
            workPanel.Size = new Size(1180, 590); root.Controls.Add(workPanel);
            root.Resize += delegate { workPanel.Size = new Size(Math.Max(600,root.ClientSize.Width-50), Math.Max(400,root.ClientSize.Height-120)); };
            work = workPanel;
            return root;
        }

        static TextBox PathBox(Panel row, MainForm app, string label, int y, EventHandler browse)
        {
            Label l = app.Label(label, app.Settings.FontSize, false, app.TextColor); l.Location = new Point(0,y+8); row.Controls.Add(l);
            TextBox box = new TextBox { Location = new Point(145,y), Width = 720, Font = app.Font, BackColor=app.Surface, ForeColor=app.TextColor };
            row.Controls.Add(box); Button b = app.Button("浏览", browse, false); b.Location = new Point(875,y-4); row.Controls.Add(b); return box;
        }

        static string PickFolder()
        { using (FolderBrowserDialog d = new FolderBrowserDialog()) return d.ShowDialog() == DialogResult.OK ? d.SelectedPath : null; }

        static void Recycle(IEnumerable<string> paths, bool folders)
        {
            foreach (string path in paths) {
                try {
                    if (folders) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                } catch { }
            }
        }

        static Control FileCleanupPage(MainForm app, string key)
        {
            Panel work; Panel root = Base(app,key,out work); TextBox path = null;
            path = PathBox(work,app,"选择文件夹或磁盘",0,delegate { string p=PickFolder(); if(p!=null) path.Text=p; });
            Button scan=app.Button("开始扫描",null,true); scan.Location=new Point(980,-4); work.Controls.Add(scan);
            Label status=app.Label("等待扫描",app.Settings.FontSize,false,app.Muted); status.Location=new Point(0,55); work.Controls.Add(status);
            ListView list=new ListView { Location=new Point(0,85),Size=new Size(1120,420),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,View=View.Details,CheckBoxes=true,FullRowSelect=true,BackColor=app.Surface,ForeColor=app.TextColor };
            list.Columns.Add("路径",850); list.Columns.Add("修改时间",220); work.Controls.Add(list);
            Button remove=app.Button("将勾选项移入回收站",delegate {
                List<string> selected=list.Items.Cast<ListViewItem>().Where(x=>x.Checked).Select(x=>x.Text).ToList();
                if(selected.Count==0){MessageBox.Show("请先勾选需要清理的项目。");return;}
                if(MessageBox.Show("确定将选中的 "+selected.Count+" 项移入回收站吗？","移入回收站",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                Recycle(selected,key=="empty_folders"); foreach(ListViewItem i in list.Items.Cast<ListViewItem>().Where(x=>x.Checked).ToList())list.Items.Remove(i);
            },false); remove.Anchor=AnchorStyles.Bottom|AnchorStyles.Right; remove.Location=new Point(910,520); work.Controls.Add(remove);
            scan.Click += async delegate {
                if(!Directory.Exists(path.Text)){MessageBox.Show("请选择有效位置。");return;} scan.Enabled=false; status.Text="正在扫描……"; list.Items.Clear();
                List<string> found=await Task.Run(delegate {
                    if(key=="empty_files") return SafeFiles(path.Text).Where(f=>{try{return new FileInfo(f).Length==0;}catch{return false;}}).ToList();
                    return SafeDirs(path.Text).Where(d=>{try{return !Directory.EnumerateFileSystemEntries(d).Any();}catch{return false;}}).ToList();
                });
                foreach(string f in found){DateTime t; try{t=key=="empty_files"?File.GetLastWriteTime(f):Directory.GetLastWriteTime(f);}catch{t=DateTime.MinValue;} list.Items.Add(new ListViewItem(new[]{f,t==DateTime.MinValue?"-":t.ToString("yyyy-MM-dd HH:mm")}));}
                status.Text="扫描完成，共发现 "+found.Count+" 项。"; scan.Enabled=true;
            };
            return root;
        }

        static IEnumerable<string> SafeFiles(string root)
        {
            Stack<string> pending=new Stack<string>();HashSet<string> visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);pending.Push(root);
            while(pending.Count>0){string d=pending.Pop();string full;try{full=Path.GetFullPath(d);}catch{continue;}if(!visited.Add(full))continue;string[] files=new string[0],dirs=new string[0];try{files=Directory.GetFiles(d);dirs=Directory.GetDirectories(d);}catch{}
                foreach(string f in files)yield return f;foreach(string child in dirs){try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;}catch{}pending.Push(child);}}
        }
        static IEnumerable<string> SafeDirs(string root){return SafeFilesAndDirs(root).Item2;}
        static Tuple<List<string>,List<string>> SafeFilesAndDirs(string root)
        {List<string> files=new List<string>(),dirs=new List<string>();HashSet<string> visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);Stack<string> pending=new Stack<string>();pending.Push(root);while(pending.Count>0){string d=pending.Pop();string full;try{full=Path.GetFullPath(d);}catch{continue;}if(!visited.Add(full))continue;try{foreach(string f in Directory.GetFiles(d))files.Add(f);foreach(string c in Directory.GetDirectories(d)){dirs.Add(c);try{if((File.GetAttributes(c)&FileAttributes.ReparsePoint)!=0)continue;}catch{}pending.Push(c);}}catch{}}return Tuple.Create(files,dirs);}

        static Control DuplicatePage(MainForm app)
        {
            Panel work;Panel root=Base(app,"duplicate_files",out work);TextBox path=null;
            path=PathBox(work,app,"选择文件夹或磁盘",0,delegate{string p=PickFolder();if(p!=null)path.Text=p;});
            Button scan=app.Button("开始扫描",null,true);scan.Location=new Point(980,-4);work.Controls.Add(scan);
            Label status=app.Label("按大小预筛选，再使用 SHA-256 内容哈希确认。",app.Settings.FontSize,false,app.Muted);status.Location=new Point(0,55);work.Controls.Add(status);
            ListView list=new ListView{Location=new Point(0,85),Size=new Size(1120,420),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,View=View.Details,CheckBoxes=true,FullRowSelect=true,BackColor=app.Surface,ForeColor=app.TextColor};
            list.Columns.Add("重复组",80);list.Columns.Add("文件路径",780);list.Columns.Add("大小",100);list.Columns.Add("建议",100);work.Controls.Add(list);
            Button auto=app.Button("自动勾选重复副本",delegate{foreach(ListViewItem i in list.Items)i.Checked=i.SubItems[3].Text=="可清理";},false);auto.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;auto.Location=new Point(720,520);work.Controls.Add(auto);
            Button remove=app.Button("将勾选项移入回收站",delegate{List<ListViewItem> items=list.Items.Cast<ListViewItem>().Where(i=>i.Checked).ToList();if(items.Count==0)return;if(MessageBox.Show("请确保每组至少保留一份。确定移入回收站吗？","确认",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;Recycle(items.Select(i=>i.SubItems[1].Text),false);foreach(ListViewItem i in items)list.Items.Remove(i);},false);remove.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;remove.Location=new Point(920,520);work.Controls.Add(remove);
            scan.Click+=async delegate{if(!Directory.Exists(path.Text)){MessageBox.Show("请选择有效位置。");return;}scan.Enabled=false;status.Text="正在计算哈希……";list.Items.Clear();
                List<List<string>> groups=await Task.Run(delegate{return FileAlgorithms.FindDuplicates(path.Text);});
                long bytes=0;int n=0;foreach(List<string> g in groups){n++;for(int i=0;i<g.Count;i++){long size=new FileInfo(g[i]).Length;if(i>0)bytes+=size;list.Items.Add(new ListViewItem(new[]{n.ToString(),g[i],HumanSize(size),i==0?"保留":"可清理"}));}}status.Text="发现 "+groups.Count+" 组，预计可释放 "+HumanSize(bytes)+"。";scan.Enabled=true;};return root;
        }
        static string HumanSize(long size){string[] u={"B","KB","MB","GB","TB"};double v=size;int i=0;while(v>=1024&&i<u.Length-1){v/=1024;i++;}return v.ToString("0.0")+" "+u[i];}

        static Control ScreenshotPage(MainForm app,string key)
        {
            Panel work;Panel root=Base(app,key,out work);CheckBox hide=new CheckBox{Text="截图时隐藏客户端",Checked=app.Settings.HideCapture,AutoSize=true,ForeColor=app.TextColor,Location=new Point(0,8)};work.Controls.Add(hide);
            PictureBox preview=new PictureBox{Location=new Point(0,60),Size=new Size(1120,400),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,BackColor=app.Surface,SizeMode=PictureBoxSizeMode.Zoom,BorderStyle=BorderStyle.FixedSingle};work.Controls.Add(preview);
            TextBox result=new TextBox{Location=new Point(0,475),Size=new Size(1120,90),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=app.Surface,ForeColor=app.TextColor};work.Controls.Add(result);
            Bitmap captured=null;Button start=app.Button("开始截图  Ctrl+Shift+A",null,true);start.Location=new Point(190,0);work.Controls.Add(start);
            Button save=app.Button("保存到本地",delegate{if(captured==null)return;using(SaveFileDialog d=new SaveFileDialog{Filter="PNG 图片|*.png|JPEG 图片|*.jpg",DefaultExt="png"})if(d.ShowDialog()==DialogResult.OK)captured.Save(d.FileName);},false);save.Location=new Point(390,0);work.Controls.Add(save);
            Button search=app.Button("搜索图片",delegate{if(captured!=null)ScreenshotSupport.Search(captured);},false);search.Location=new Point(520,0);work.Controls.Add(search);
            Rectangle originalBounds=Rectangle.Empty;FormWindowState originalState=FormWindowState.Normal;
            Action restore=delegate{app.WindowState=originalState;app.Bounds=originalBounds;app.Show();app.Activate();};
            Action startOverlay=delegate{ScreenCaptureForm overlay=new ScreenCaptureForm(delegate(Bitmap bmp,Rectangle selectionBounds){captured=bmp;preview.Image=bmp;ScreenshotSupport.ShowFloating(app,bmp,selectionBounds,restore);Task.Run(delegate{return ScreenshotSupport.Ocr(bmp);}).ContinueWith(task=>{if(app.IsDisposed)return;app.BeginInvoke((Action)delegate{if(task.IsFaulted){result.Text="OCR 识别失败："+task.Exception.GetBaseException().Message;return;}result.Text=task.Result;if(!task.Result.StartsWith("OCR")){Clipboard.SetText(task.Result);}});});},delegate{restore();});overlay.Show();};
            Action captureAction=delegate{app.Settings.HideCapture=hide.Checked;app.Settings.Save();originalBounds=app.Bounds;originalState=app.WindowState;if(hide.Checked){app.WindowState=FormWindowState.Minimized;Timer delay=new Timer();delay.Interval=250;delay.Tick+=delegate{delay.Stop();delay.Dispose();startOverlay();};delay.Start();}else startOverlay();};start.Click+=delegate{captureAction();};
            ContextMenuStrip menu=new ContextMenuStrip();menu.Items.Add("复制识别",null,delegate{if(captured!=null)Clipboard.SetText(ScreenshotSupport.Ocr(captured));});menu.Items.Add("搜索图片",null,delegate{if(captured!=null)ScreenshotSupport.Search(captured);});menu.Items.Add("保存本地",null,delegate{if(captured==null)return;using(SaveFileDialog d=new SaveFileDialog{Filter="PNG 图片|*.png|JPEG 图片|*.jpg",DefaultExt="png"})if(d.ShowDialog()==DialogResult.OK)captured.Save(d.FileName);});menu.Items.Add("退出截图",null,delegate{app.ShowHome();});preview.ContextMenuStrip=menu;
            return root;
        }

        static Control FtpPage(MainForm app)
        {
            Panel work;Panel root=Base(app,"ftp",out work);TextBox folder=null;folder=PathBox(work,app,"共享文件夹",0,delegate{string p=PickFolder();if(p!=null)folder.Text=p;});
            Label pLabel=app.Label("端口",app.Settings.FontSize,false,app.TextColor);pLabel.Location=new Point(0,60);work.Controls.Add(pLabel);NumericUpDown port=new NumericUpDown{Location=new Point(70,55),Minimum=1024,Maximum=65535,Value=2121};work.Controls.Add(port);
            CheckBox anonymous=new CheckBox{Text="匿名访问",Checked=true,AutoSize=true,ForeColor=app.TextColor,Location=new Point(190,58)};CheckBox upload=new CheckBox{Text="允许上传",AutoSize=true,ForeColor=app.TextColor,Location=new Point(310,58)};work.Controls.Add(anonymous);work.Controls.Add(upload);
            Label address=app.Label("访问地址：ftp://"+LocalIP()+":2121",app.Settings.FontSize+3,true,app.Accent);address.Location=new Point(0,105);work.Controls.Add(address);
            TextBox log=new TextBox{Location=new Point(0,155),Size=new Size(1120,360),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=app.Surface,ForeColor=app.TextColor};work.Controls.Add(log);
            Process process=null;Button toggle=app.Button("启动服务",null,true);toggle.Location=new Point(520,52);work.Controls.Add(toggle);
            toggle.Click+=delegate{if(process!=null&&!process.HasExited){process.Kill();process=null;toggle.Text="启动服务";log.AppendText("服务已停止。\r\n");return;}if(!Directory.Exists(folder.Text)){MessageBox.Show("请选择共享文件夹。");return;}string py=FindPython();if(py==null){MessageBox.Show("FTP 组件不可用，请使用包含增强环境的完整项目包。");return;}string args="-m pyftpdlib --port="+port.Value+" --directory=\""+folder.Text+"\""+(anonymous.Checked?"":" --username=muxia --password=123456")+(upload.Checked?" --write":"");try{process=new Process();process.StartInfo=new ProcessStartInfo(py,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};process.EnableRaisingEvents=true;process.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null&&!log.IsDisposed)log.BeginInvoke((Action)delegate{log.AppendText(e.Data+"\r\n");});};process.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(e.Data!=null&&!log.IsDisposed)log.BeginInvoke((Action)delegate{log.AppendText(e.Data+"\r\n");});};process.Exited+=delegate{if(!toggle.IsDisposed)toggle.BeginInvoke((Action)delegate{toggle.Text="启动服务";});};process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();toggle.Text="停止服务";address.Text="访问地址：ftp://"+LocalIP()+":"+port.Value;log.AppendText("服务已启动。首次使用请允许 Windows 防火墙访问。\r\n");}catch(Exception ex){MessageBox.Show(ex.Message);}};return root;
        }
        static string LocalIP(){try{using(Socket s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){s.Connect("8.8.8.8",80);return ((IPEndPoint)s.LocalEndPoint).Address.ToString();}}catch{return "127.0.0.1";}}
        static string ProjectRoot(){string configured=Environment.GetEnvironmentVariable("TOOL_CHEST_ROOT");if(!string.IsNullOrWhiteSpace(configured)&&Directory.Exists(configured))return configured;return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));}
        static string FindPython(){string p=Path.Combine(ProjectRoot(),".venv","Scripts","python.exe");if(File.Exists(p))return p;return null;}

        static Control ArticlePage(MainForm app)
        {
            Panel work;Panel root=Base(app,"article",out work);Label autoMode=app.Label("自动判定体裁与复杂度",app.Settings.FontSize,false,app.Muted);autoMode.Location=new Point(0,8);work.Controls.Add(autoMode);
            Button import=app.Button("导入文档",null,false);import.Location=new Point(210,-4);work.Controls.Add(import);Button extract=app.Button("按规则提炼",null,true);extract.Location=new Point(345,-4);work.Controls.Add(extract);
            TextBox input=new TextBox{Location=new Point(0,55),Size=new Size(550,480),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=app.Surface,ForeColor=app.TextColor};TextBox output=new TextBox{Location=new Point(575,55),Size=new Size(545,480),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=app.Surface,ForeColor=app.TextColor};work.Controls.Add(input);work.Controls.Add(output);
            import.Click+=delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="文本文件|*.txt;*.md|所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)input.Text=File.ReadAllText(d.FileName,Encoding.UTF8);};extract.Click+=delegate{output.Text=TechnicalArticleExtractor.Extract(input.Text);};
            Button copy=app.Button("复制结果",delegate{if(output.TextLength>0)Clipboard.SetText(output.Text);},false);copy.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;copy.Location=new Point(930,545);work.Controls.Add(copy);return root;
        }
        static string Summarize(string text,string mode){string[] sentences=Regex.Split(text,@"(?<=[。！？!?；;])\s*|[\r\n]+").Where(x=>x.Trim().Length>0).ToArray();if(sentences.Length==0)return "";if(mode=="一句话总结")return sentences.OrderByDescending(x=>x.Length).First();int count=Math.Max(1,(int)Math.Ceiling(sentences.Length*(mode=="完整摘要"?.45:.25)));IEnumerable<string> pick=sentences.Select((s,i)=>new{S=s,I=i,Score=s.Distinct().Count()/(double)Math.Max(1,s.Length)}).OrderByDescending(x=>x.Score).Take(count).OrderBy(x=>x.I).Select(x=>x.S);return mode=="核心要点"?string.Join("\r\n",pick.Select((s,i)=>(i+1)+". "+s).ToArray()):string.Join("",pick.ToArray());}

        static Control VideoPage(MainForm app)
        {
            Panel work;Panel root=Base(app,"video",out work);TextBox file=new TextBox{Location=new Point(0,0),Width=720,BackColor=app.Surface,ForeColor=app.TextColor};work.Controls.Add(file);Button choose=app.Button("选择视频",delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="视频文件|*.mp4;*.mov;*.avi;*.mkv;*.wmv|所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)file.Text=d.FileName;},false);choose.Location=new Point(730,-5);work.Controls.Add(choose);
            ComboBox mode=new ComboBox{Location=new Point(850,0),Width=120,DropDownStyle=ComboBoxStyle.DropDownList};mode.Items.AddRange(new object[]{"逐字稿","精简文稿","字幕 SRT"});mode.SelectedIndex=0;work.Controls.Add(mode);Button start=app.Button("开始转写",null,true);start.Location=new Point(980,-5);work.Controls.Add(start);
            Label status=app.Label("视频转写需要增强组件 faster-whisper 和 FFmpeg。",app.Settings.FontSize,false,app.Muted);status.Location=new Point(0,50);work.Controls.Add(status);TextBox output=new TextBox{Location=new Point(0,85),Size=new Size(1120,450),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=app.Surface,ForeColor=app.TextColor};work.Controls.Add(output);
            Button export=app.Button("导出结果",delegate{using(SaveFileDialog d=new SaveFileDialog{Filter="文本或字幕|*.txt;*.srt",DefaultExt=mode.Text=="字幕 SRT"?"srt":"txt"})if(d.ShowDialog()==DialogResult.OK)File.WriteAllText(d.FileName,output.Text,Encoding.UTF8);},false);export.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;export.Location=new Point(1000,545);work.Controls.Add(export);
            start.Click+=async delegate{
                if(!File.Exists(file.Text)){MessageBox.Show("请选择有效的视频文件。");return;}
                string py=FindPython();string helper=Path.Combine(ProjectRoot(),"helpers","video_transcribe.py");
                if(py==null||!File.Exists(helper)){MessageBox.Show("视频转写组件不可用，请使用包含增强环境的完整项目包。");return;}
                start.Enabled=false;status.Text="正在本地转写，首次运行会下载语音模型……";
                string selectedMode=mode.Text=="字幕 SRT"?"srt":mode.Text=="精简文稿"?"compact":"text";
                try{string text=await Task.Run(delegate{Process p=new Process();p.StartInfo=new ProcessStartInfo(py,"\""+helper+"\" \""+file.Text+"\" --mode "+selectedMode){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};p.Start();string stdout=p.StandardOutput.ReadToEnd();string stderr=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(stderr);return stdout;});output.Text=text;status.Text="转写完成。";}catch(Exception ex){MessageBox.Show(ex.Message,"转写失败");status.Text="转写失败。";}finally{start.Enabled=true;}
            };return root;
        }

        static readonly string[] PhotoExt={".jpg",".jpeg",".png",".webp",".bmp",".tif",".tiff",".heic"}; static readonly string[] VideoExt={".mp4",".mov",".avi",".mkv",".wmv",".m4v",".webm",".flv"};
        static string MediaKind(string f){string e=Path.GetExtension(f).ToLowerInvariant();if(e==".gif")return "GIF 动图";if(PhotoExt.Contains(e))return "照片";if(VideoExt.Contains(e))return "视频";return null;}
        static Control MediaPage(MainForm app)
        {
            Panel work;Panel root=Base(app,"media_sort",out work);TextBox src=null,dst=null;src=PathBox(work,app,"源文件夹",0,delegate{string p=PickFolder();if(p!=null)src.Text=p;});dst=PathBox(work,app,"目标文件夹",48,delegate{string p=PickFolder();if(p!=null)dst.Text=p;});
            RadioButton copy=new RadioButton{Text="复制",Checked=true,AutoSize=true,ForeColor=app.TextColor,Location=new Point(0,105)};RadioButton move=new RadioButton{Text="移动",AutoSize=true,ForeColor=app.TextColor,Location=new Point(90,105)};work.Controls.Add(copy);work.Controls.Add(move);
            Label counts=app.Label("照片 0    GIF 动图 0    视频 0",app.Settings.FontSize+1,true,app.TextColor);counts.Location=new Point(200,103);work.Controls.Add(counts);ListView list=new ListView{Location=new Point(0,145),Size=new Size(1120,365),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,View=View.Details,BackColor=app.Surface,ForeColor=app.TextColor};list.Columns.Add("源文件",700);list.Columns.Add("类型",150);list.Columns.Add("目标文件夹",250);work.Controls.Add(list);List<string> files=new List<string>();
            Action preview=delegate{if(!Directory.Exists(src.Text)||!Directory.Exists(dst.Text)){MessageBox.Show("请选择有效的源和目标文件夹。");return;}files=SafeFiles(src.Text).Where(f=>MediaKind(f)!=null).ToList();list.Items.Clear();foreach(string f in files)list.Items.Add(new ListViewItem(new[]{f,MediaKind(f),Path.Combine(dst.Text,MediaKind(f))}));counts.Text="照片 "+files.Count(f=>MediaKind(f)=="照片")+"    GIF 动图 "+files.Count(f=>MediaKind(f)=="GIF 动图")+"    视频 "+files.Count(f=>MediaKind(f)=="视频");};
            Button pre=app.Button("预览分类",delegate{preview();},false);pre.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;pre.Location=new Point(880,520);work.Controls.Add(pre);Button go=app.Button("开始整理",delegate{if(files.Count==0)preview();if(files.Count==0)return;if(MessageBox.Show("重名文件会自动编号。是否继续？","开始整理",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;foreach(string f in files){string folder=Path.Combine(dst.Text,MediaKind(f));Directory.CreateDirectory(folder);string target=Unique(folder,Path.GetFileName(f));if(move.Checked)File.Move(f,target);else File.Copy(f,target);}MessageBox.Show("整理完成，共处理 "+files.Count+" 个文件。");},true);go.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;go.Location=new Point(1010,520);work.Controls.Add(go);return root;
        }
        static string Unique(string folder,string name){string p=Path.Combine(folder,name);int i=1;while(File.Exists(p)){p=Path.Combine(folder,Path.GetFileNameWithoutExtension(name)+"_"+i+Path.GetExtension(name));i++;}return p;}

        static Control SmartPhotoPage(MainForm app)
        {
            Panel work;Panel root=Base(app,"smart_photos",out work);TextBox src=null;src=PathBox(work,app,"照片文件夹",0,delegate{string p=PickFolder();if(p!=null)src.Text=p;});
            Label rule=app.Label("命名规则：拍摄时间_设备_来源_序号（右键可修正分类）",app.Settings.FontSize,false,app.Muted);rule.Location=new Point(0,55);work.Controls.Add(rule);ListView list=new ListView{Location=new Point(0,90),Size=new Size(1120,420),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,View=View.Details,FullRowSelect=true,BackColor=app.Surface,ForeColor=app.TextColor};list.Columns.Add("原文件",480);list.Columns.Add("分类",120);list.Columns.Add("置信度",90);list.Columns.Add("新文件名",400);work.Controls.Add(list);List<PhotoRow> rows=new List<PhotoRow>();
            string[] categories={"截图","网络存图","人物照片","动物照片","其他拍照","后期照片"};ContextMenuStrip categoryMenu=new ContextMenuStrip();foreach(string category in categories){string selectedCategory=category;categoryMenu.Items.Add(selectedCategory,null,delegate{foreach(ListViewItem item in list.SelectedItems){PhotoRow row=item.Tag as PhotoRow;if(row!=null){row.Category=selectedCategory;row.Confidence=100;item.SubItems[1].Text=selectedCategory;item.SubItems[2].Text="100%";}}});}list.ContextMenuStrip=categoryMenu;
            Button preview=app.Button("预览分类",delegate{if(!Directory.Exists(src.Text)){MessageBox.Show("请选择有效位置。");return;}rows.Clear();list.Items.Clear();int i=0;HashSet<string> categoryFolders=new HashSet<string>(categories.Select(c=>Path.Combine(src.Text,c)),StringComparer.OrdinalIgnoreCase);foreach(string f in SafeFiles(src.Text).Where(x=>PhotoExt.Contains(Path.GetExtension(x).ToLowerInvariant())&&!categoryFolders.Any(folder=>x.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))){try{i++;PhotoRow r=ClassifyPhoto(f,i);rows.Add(r);ListViewItem item=new ListViewItem(new[]{f,r.Category,r.Confidence+"%",r.NewName});item.Tag=r;list.Items.Add(item);}catch{}}MessageBox.Show("已识别 "+rows.Count+" 张照片。低置信度项目可在列表中右键修正分类。");},false);preview.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;preview.Location=new Point(880,520);work.Controls.Add(preview);
            Button organize=app.Button("开始整理",delegate{if(rows.Count==0){MessageBox.Show("请先预览分类。");return;}if(MessageBox.Show("将在源文件夹内建立 6 个分类子文件夹并复制照片，原文件不会删除。是否继续？","照片整理",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;int ok=0,failed=0;foreach(PhotoRow r in rows){try{string folder=Path.Combine(src.Text,r.Category);Directory.CreateDirectory(folder);File.Copy(r.Path,Unique(folder,r.NewName));ok++;}catch{failed++;}}MessageBox.Show("已分类复制 "+ok+" 张照片，失败 "+failed+" 张。");},true);organize.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;organize.Location=new Point(1010,520);work.Controls.Add(organize);return root;
        }
        class PhotoRow{public string Path,Category,NewName;public int Confidence;}
        static PhotoRow ClassifyPhoto(string f,int index){string name=Path.GetFileNameWithoutExtension(f).ToLowerInvariant();bool screenshot=name.Contains("screenshot")||name.Contains("截图")||name.Contains("snipaste");string cat,origin,device="未知设备";int confidence;DateTime captured=File.GetLastWriteTime(f);if(screenshot){cat="截图";origin="屏幕";confidence=95;}else if(name.Contains("cat")||name.Contains("dog")||name.Contains("猫")||name.Contains("狗")||name.Contains("宠物")){cat="动物照片";origin="拍摄";confidence=72;}else if(name.Contains("portrait")||name.Contains("selfie")||name.Contains("自拍")||name.Contains("人像")){cat="人物照片";origin="拍摄";confidence=72;}else{try{using(Image img=Image.FromFile(f)){string software=ExifText(img,0x0131).ToLowerInvariant();string make=ExifText(img,0x010F),model=ExifText(img,0x0110);device=SafeName((make+"_"+model).Trim('_'));string date=ExifText(img,0x9003);DateTime parsed;if(DateTime.TryParseExact(date,"yyyy:MM:dd HH:mm:ss",null,System.Globalization.DateTimeStyles.None,out parsed))captured=parsed;if(software.Contains("photoshop")||software.Contains("lightroom")||software.Contains("snapseed")||software.Contains("meitu")){cat="后期照片";origin="后期";confidence=92;}else if(img.PropertyItems.Length==0){cat="网络存图";origin="网络";confidence=68;}else{cat="其他拍照";origin="拍摄";confidence=80;}}}catch{cat="网络存图";origin="网络";confidence=60;}}string dateText=captured.ToString("yyyyMMdd_HHmmss");return new PhotoRow{Path=f,Category=cat,Confidence=confidence,NewName=dateText+"_"+device+"_"+origin+"_"+index.ToString("0000")+Path.GetExtension(f).ToLowerInvariant()};}
        static string ExifText(Image image,int id){try{return Encoding.UTF8.GetString(image.GetPropertyItem(id).Value).Trim('\0',' ');}catch{return "";}}
        static string SafeName(string value){if(string.IsNullOrWhiteSpace(value))return "未知设备";foreach(char c in Path.GetInvalidFileNameChars())value=value.Replace(c,'_');return value;}
    }
}
