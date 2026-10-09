using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MuxiaToolbox
{
    public class ScreenCaptureForm : Form
    {
        Bitmap desktop;
        Point start;
        Rectangle selection;
        bool dragging;
        Action<Bitmap, Rectangle> completed;
        Action cancelled;

        public ScreenCaptureForm(Action<Bitmap, Rectangle> completed, Action cancelled)
        {
            this.completed = completed; this.cancelled = cancelled;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = SystemInformation.VirtualScreen; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = true; Cursor = Cursors.Cross; DoubleBuffered = true;
            desktop = new Bitmap(Bounds.Width, Bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(desktop)) g.CopyFromScreen(Bounds.Left, Bounds.Top, 0, 0, Bounds.Size);
            KeyPreview = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(desktop, 0, 0);
            using (Brush shade = new SolidBrush(Color.FromArgb(105, Color.Black))) e.Graphics.FillRectangle(shade, ClientRectangle);
            if (selection.Width > 0 && selection.Height > 0) {
                e.Graphics.DrawImage(desktop, selection, selection, GraphicsUnit.Pixel);
                using (Pen pen = new Pen(Color.FromArgb(20,170,210), 3)) e.Graphics.DrawRectangle(pen, selection);
                string size = selection.Width + " × " + selection.Height;
                using (Brush b = new SolidBrush(Color.FromArgb(220,20,25,30))) e.Graphics.FillRectangle(b, selection.Left, Math.Max(0,selection.Top-28), 110, 25);
                e.Graphics.DrawString(size, Font, Brushes.White, selection.Left+7, Math.Max(2,selection.Top-24));
            }
        }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left){start=e.Location;selection=Rectangle.Empty;dragging=true;Invalidate();} }
        protected override void OnMouseMove(MouseEventArgs e) { if(dragging){selection=Rectangle.FromLTRB(Math.Min(start.X,e.X),Math.Min(start.Y,e.Y),Math.Max(start.X,e.X),Math.Max(start.Y,e.Y));Invalidate();} }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if(!dragging)return;dragging=false;if(selection.Width<5||selection.Height<5)return;
            Bitmap crop=new Bitmap(selection.Width,selection.Height);using(Graphics g=Graphics.FromImage(crop))g.DrawImage(desktop,new Rectangle(0,0,crop.Width,crop.Height),selection,GraphicsUnit.Pixel);
            Rectangle screenSelection = new Rectangle(Bounds.Left + selection.Left, Bounds.Top + selection.Top, selection.Width, selection.Height);
            Close(); completed(crop, screenSelection);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        { if(keyData==Keys.Escape){Close();cancelled();return true;}return base.ProcessCmdKey(ref msg,keyData); }
        protected override void Dispose(bool disposing){if(disposing&&desktop!=null)desktop.Dispose();base.Dispose(disposing);}
    }

    public static class ScreenshotSupport
    {
        public static string Ocr(Bitmap image)
        {
            string exe;
            try { exe = FindOnPath("tesseract.exe"); }
            catch (Exception ex) { return "OCR 组件释放失败：" + ex.Message; }
            if (exe == null) return "内置 OCR 组件不可用，请重新获取完整的工具宝匣 EXE。";
            string input = Path.Combine(Path.GetTempPath(), "toolchest_ocr_" + Guid.NewGuid().ToString("N") + ".png");
            image.Save(input, ImageFormat.Png);
            try {
                string localData=Path.Combine(Path.GetDirectoryName(exe),"tessdata");string dataArg=File.Exists(Path.Combine(localData,"chi_sim.traineddata"))&&File.Exists(Path.Combine(localData,"eng.traineddata"))?" --tessdata-dir \""+localData+"\"":"";
                Process p = new Process(); p.StartInfo = new ProcessStartInfo(exe, "\""+input+"\" stdout -l chi_sim+eng"+dataArg) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8 };
                p.Start(); string text=p.StandardOutput.ReadToEnd(); string error=p.StandardError.ReadToEnd(); p.WaitForExit();
                if(p.ExitCode!=0&&string.IsNullOrWhiteSpace(text))return "OCR 识别失败："+error.Trim();
                return text.Trim();
            } catch(Exception ex) { return "OCR 识别失败："+ex.Message; }
            finally { try { File.Delete(input); } catch { } }
        }

        public static void Search(Bitmap image)
        {
            try { Clipboard.SetImage(image); Process.Start("https://www.bing.com/visualsearch"); }
            catch(Exception ex) { MessageBox.Show("无法打开图片搜索："+ex.Message); }
        }

        public static void ShowFloating(MainForm owner, Bitmap image, Rectangle screenBounds, Action restore)
        {
            Form floating = new Form { FormBorderStyle=FormBorderStyle.None, ShowInTaskbar=false, TopMost=true, BackColor=Color.Black, Padding=new Padding(2), StartPosition=FormStartPosition.Manual };
            floating.ClientSize=new Size(image.Width+4,image.Height+4);floating.Location=new Point(screenBounds.Left-2,screenBounds.Top-2);
            PictureBox picture=new PictureBox{Dock=DockStyle.Fill,Image=image,SizeMode=PictureBoxSizeMode.Zoom};floating.Controls.Add(picture);
            ContextMenuStrip menu=new ContextMenuStrip();
            menu.Items.Add("复制识别",null,delegate{string text=Ocr(image);Clipboard.SetText(text);});
            menu.Items.Add("搜索图片",null,delegate{Search(image);});
            menu.Items.Add("保存本地",null,delegate{using(SaveFileDialog d=new SaveFileDialog{Filter="PNG 图片|*.png|JPEG 图片|*.jpg",DefaultExt="png"})if(d.ShowDialog()==DialogResult.OK)image.Save(d.FileName);});
            menu.Items.Add("退出截图",null,delegate{floating.Close();restore();});
            picture.ContextMenuStrip=menu;picture.DoubleClick+=delegate{floating.Close();restore();};floating.Show();
        }

        static string FindOnPath(string name)
        {
            string embedded = EmbeddedOcrRuntime.EnsureAvailable(); if(File.Exists(embedded))return embedded;
            string root=ProjectRoot();string[] preferred={Path.Combine(root,"runtime","tesseract",name),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tesseract-OCR",name),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Tesseract-OCR",name)};
            foreach(string candidate in preferred)if(File.Exists(candidate))return candidate;
            string path=Environment.GetEnvironmentVariable("PATH")??"";
            foreach(string folder in path.Split(Path.PathSeparator)){try{string file=Path.Combine(folder.Trim(),name);if(File.Exists(file))return file;}catch{}}
            return null;
        }
        static string ProjectRoot(){string configured=Environment.GetEnvironmentVariable("TOOL_CHEST_ROOT");if(!string.IsNullOrWhiteSpace(configured)&&Directory.Exists(configured))return configured;return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));}
    }
}
