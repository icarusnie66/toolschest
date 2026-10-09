from __future__ import annotations

import os
import shutil
import socket
import subprocess
import threading
import tkinter as tk
from collections import Counter
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .catalog import GROUPS, TOOLS, TOOL_BY_KEY
from .config import AppSettings, load_settings, save_settings
from .services.content import summarize
from .services.files import (
    find_duplicates,
    find_empty_files,
    find_empty_folders,
    iter_files,
    media_kind,
    organize_media,
    recycle,
    unique_destination,
)
from .services.ftp import FtpService, local_ip
from .services.photos import PhotoResult, scan_photos
from .services.screenshot import CaptureOverlay, recognize, search_image


LIGHT = {
    "bg": "#f4f6f8", "surface": "#ffffff", "panel": "#f8fafb", "text": "#18212b",
    "muted": "#65717f", "border": "#dce3e8", "accent": "#0799bd", "accent2": "#e7f7fb",
    "danger": "#b42318", "success": "#159947",
}
DARK = {
    "bg": "#161a1e", "surface": "#20252a", "panel": "#252b31", "text": "#edf2f5",
    "muted": "#a9b3bc", "border": "#39424a", "accent": "#28b7d6", "accent2": "#153f49",
    "danger": "#ff7168", "success": "#52cf79",
}


def human_size(size: int) -> str:
    value = float(size)
    for unit in ("B", "KB", "MB", "GB", "TB"):
        if value < 1024 or unit == "TB":
            return f"{value:.1f} {unit}"
        value /= 1024
    return f"{size} B"


class ScrollFrame(ttk.Frame):
    def __init__(self, master, **kwargs):
        super().__init__(master, **kwargs)
        self.canvas = tk.Canvas(self, highlightthickness=0)
        self.scrollbar = ttk.Scrollbar(self, orient="vertical", command=self.canvas.yview)
        self.body = ttk.Frame(self.canvas)
        self.body.bind("<Configure>", lambda _: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.window_id = self.canvas.create_window((0, 0), window=self.body, anchor="nw")
        self.canvas.bind("<Configure>", lambda e: self.canvas.itemconfigure(self.window_id, width=e.width))
        self.canvas.configure(yscrollcommand=self.scrollbar.set)
        self.canvas.pack(side="left", fill="both", expand=True)
        self.scrollbar.pack(side="right", fill="y")


class ToolboxApp:
    def __init__(self):
        self.settings = load_settings()
        self.root = tk.Tk()
        self.root.title("木匣工具箱")
        self.root.geometry("1280x800")
        self.root.minsize(1000, 650)
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.style = ttk.Style(self.root)
        self.palette = LIGHT
        self.content: ttk.Frame | None = None
        self.current_page = "home"
        self.ftp_service = FtpService()
        self.apply_theme()
        self.show_home()

    def run(self):
        self.root.mainloop()

    def close(self):
        self.ftp_service.stop()
        save_settings(self.settings)
        self.root.destroy()

    def apply_theme(self):
        dark = self.settings.theme == "dark"
        self.palette = DARK if dark else LIGHT
        p = self.palette
        self.root.configure(bg=p["bg"])
        self.root.tk.call("tk", "scaling", max(0.8, self.settings.scale / 100))
        size = self.settings.font_size
        self.style.theme_use("clam")
        self.style.configure(".", background=p["bg"], foreground=p["text"], font=("Microsoft YaHei UI", size))
        self.style.configure("TFrame", background=p["bg"])
        self.style.configure("Surface.TFrame", background=p["surface"])
        self.style.configure("Panel.TFrame", background=p["panel"])
        self.style.configure("TLabel", background=p["bg"], foreground=p["text"])
        self.style.configure("Surface.TLabel", background=p["surface"], foreground=p["text"])
        self.style.configure("Muted.TLabel", background=p["surface"], foreground=p["muted"])
        self.style.configure("Title.TLabel", background=p["bg"], foreground=p["text"], font=("Microsoft YaHei UI", size + 8, "bold"))
        self.style.configure("Heading.TLabel", background=p["bg"], foreground=p["text"], font=("Microsoft YaHei UI", size + 3, "bold"))
        self.style.configure("CardTitle.TLabel", background=p["surface"], foreground=p["text"], font=("Microsoft YaHei UI", size + 1, "bold"))
        self.style.configure("Accent.TButton", background=p["accent"], foreground="white", borderwidth=0, padding=(16, 9))
        self.style.map("Accent.TButton", background=[("active", p["accent"]), ("pressed", p["accent"])])
        self.style.configure("TButton", background=p["surface"], foreground=p["text"], padding=(12, 8), bordercolor=p["border"])
        self.style.configure("Tool.TButton", background=p["surface"], foreground=p["text"], padding=(15, 14), anchor="w")
        self.style.map("Tool.TButton", background=[("active", p["accent2"])])
        self.style.configure("Treeview", background=p["surface"], fieldbackground=p["surface"], foreground=p["text"], rowheight=30, bordercolor=p["border"])
        self.style.configure("Treeview.Heading", background=p["panel"], foreground=p["text"], font=("Microsoft YaHei UI", size, "bold"))
        self.style.configure("TLabelframe", background=p["bg"], bordercolor=p["border"])
        self.style.configure("TLabelframe.Label", background=p["bg"], foreground=p["text"], font=("Microsoft YaHei UI", size, "bold"))

    def clear(self):
        if self.content:
            self.content.destroy()
        self.content = ttk.Frame(self.root)
        self.content.pack(fill="both", expand=True)

    def show_home(self):
        self.current_page = "home"
        self.clear()
        HomePage(self.content, self).pack(fill="both", expand=True)

    def show_settings(self):
        self.current_page = "settings"
        self.clear()
        SettingsPage(self.content, self).pack(fill="both", expand=True)

    def show_tool(self, key: str):
        self.current_page = key
        self.clear()
        classes = {
            "screenshot_ocr": ScreenshotPage,
            "screenshot_search": ScreenshotPage,
            "empty_folders": FileScanPage,
            "duplicate_files": DuplicatePage,
            "empty_files": FileScanPage,
            "ftp": FtpPage,
            "article": ArticlePage,
            "video": VideoPage,
            "media_sort": MediaSortPage,
            "smart_photos": SmartPhotoPage,
        }
        classes[key](self.content, self, key).pack(fill="both", expand=True)

    def toggle_pin(self, key: str):
        if key in self.settings.pinned:
            self.settings.pinned.remove(key)
        else:
            self.settings.pinned.append(key)
        save_settings(self.settings)


class Header(ttk.Frame):
    def __init__(self, master, app: ToolboxApp, title: str, home=False):
        super().__init__(master, style="Surface.TFrame", padding=(20, 12))
        if not home:
            ttk.Button(self, text="← 返回", command=app.show_home).pack(side="left", padx=(0, 12))
        ttk.Label(self, text="▣  木匣工具箱", style="CardTitle.TLabel").pack(side="left")
        if title:
            ttk.Label(self, text=f"  /  {title}", style="Muted.TLabel").pack(side="left")


class SettingsCorner(ttk.Frame):
    def __init__(self, master, app: ToolboxApp):
        super().__init__(master)
        ttk.Button(self, text="⚙ 设置", command=app.show_settings).pack(side="left")
        ttk.Label(self, text="  所有删除操作仅移入回收站", foreground=app.palette["muted"]).pack(side="left", padx=10)


class HomePage(ttk.Frame):
    def __init__(self, master, app: ToolboxApp):
        super().__init__(master)
        self.app = app
        self.selected = TOOLS[0].key
        self.help_visible = True
        Header(self, app, "", home=True).pack(fill="x")
        body = ttk.Frame(self, padding=(22, 18))
        body.pack(fill="both", expand=True)
        self.main = ttk.Frame(body)
        self.main.pack(side="left", fill="both", expand=True)
        self.help = ttk.Frame(body, style="Surface.TFrame", padding=20, width=300)
        self.help.pack(side="right", fill="y", padx=(18, 0))
        self.help.pack_propagate(False)
        top = ttk.Frame(self.main)
        top.pack(fill="x", pady=(0, 14))
        ttk.Label(top, text="工具主页", style="Title.TLabel").pack(side="left")
        ttk.Button(top, text="收起说明  〉", command=self.toggle_help).pack(side="right")
        self.search = tk.StringVar()
        entry = ttk.Entry(self.main, textvariable=self.search)
        entry.pack(fill="x", pady=(0, 16), ipady=6)
        entry.insert(0, "搜索工具")
        entry.bind("<KeyRelease>", lambda _: self.render_tools())
        self.scroll = ScrollFrame(self.main)
        self.scroll.pack(fill="both", expand=True)
        SettingsCorner(self.main, app).pack(fill="x", pady=(12, 0))
        self.render_tools()
        self.render_help()

    def toggle_help(self):
        self.help_visible = not self.help_visible
        if self.help_visible:
            self.help.pack(side="right", fill="y", padx=(18, 0))
        else:
            self.help.pack_forget()

    def render_tools(self):
        for child in self.scroll.body.winfo_children():
            child.destroy()
        query = self.search.get().strip()
        if query == "搜索工具":
            query = ""
        pinned = [TOOL_BY_KEY[key] for key in self.app.settings.pinned if key in TOOL_BY_KEY]
        self._section("常用工具", pinned, 0)
        row = 1
        for group in GROUPS:
            tools = [tool for tool in TOOLS if tool.group == group and (not query or query.lower() in tool.name.lower())]
            if tools:
                self._section(group, tools, row)
                row += 1

    def _section(self, title, tools, row):
        frame = ttk.Frame(self.scroll.body)
        frame.pack(fill="x", pady=(0, 18))
        ttk.Label(frame, text=title, style="Heading.TLabel").grid(row=0, column=0, sticky="w", pady=(0, 8))
        for index in range(3):
            frame.columnconfigure(index, weight=1, uniform="tool")
        for index, tool in enumerate(tools):
            label = f"{tool.icon}  {tool.name}\n     {tool.summary}"
            button = ttk.Button(frame, text=label, style="Tool.TButton", command=lambda key=tool.key: self.select_tool(key))
            button.grid(row=1 + index // 3, column=index % 3, sticky="nsew", padx=(0 if index % 3 == 0 else 6, 6), pady=5)
            button.bind("<Double-Button-1>", lambda _, key=tool.key: self.app.show_tool(key))

    def select_tool(self, key):
        if self.selected == key:
            self.app.show_tool(key)
            return
        self.selected = key
        self.render_help()

    def render_help(self):
        for child in self.help.winfo_children():
            child.destroy()
        tool = TOOL_BY_KEY[self.selected]
        top = ttk.Frame(self.help, style="Surface.TFrame")
        top.pack(fill="x")
        ttk.Label(top, text=tool.name, style="CardTitle.TLabel").pack(side="left")
        pinned = tool.key in self.app.settings.pinned
        ttk.Button(top, text="取消置顶" if pinned else "置顶", command=lambda: self.pin(tool.key)).pack(side="right")
        ttk.Separator(self.help).pack(fill="x", pady=16)
        ttk.Label(self.help, text="功能说明", style="CardTitle.TLabel").pack(anchor="w")
        ttk.Label(self.help, text=tool.summary, style="Muted.TLabel", wraplength=250, justify="left").pack(anchor="w", pady=(8, 18))
        ttk.Label(self.help, text="使用说明", style="CardTitle.TLabel").pack(anchor="w")
        for index, line in enumerate(tool.instructions, 1):
            ttk.Label(self.help, text=f"{index}. {line}", style="Muted.TLabel", wraplength=250, justify="left").pack(anchor="w", pady=5)
        ttk.Button(self.help, text="打开工具", style="Accent.TButton", command=lambda: self.app.show_tool(tool.key)).pack(fill="x", pady=(24, 0))

    def pin(self, key):
        self.app.toggle_pin(key)
        self.render_tools()
        self.render_help()


class SettingsPage(ttk.Frame):
    def __init__(self, master, app: ToolboxApp):
        super().__init__(master)
        self.app = app
        Header(self, app, "设置").pack(fill="x")
        body = ttk.Frame(self, padding=28)
        body.pack(fill="both", expand=True)
        ttk.Label(body, text="设置", style="Title.TLabel").pack(anchor="w", pady=(0, 18))
        interface = ttk.LabelFrame(body, text="界面设置", padding=20)
        interface.pack(fill="x", pady=(0, 16))
        self._row(interface, "字体大小", self._font_control(interface))
        self._row(interface, "外观模式", self._theme_control(interface))
        self._row(interface, "界面缩放", self._scale_control(interface))
        general = ttk.LabelFrame(body, text="通用设置", padding=20)
        general.pack(fill="x")
        self.hide = tk.BooleanVar(value=app.settings.hide_during_capture)
        ttk.Checkbutton(general, text="截图时默认隐藏客户端", variable=self.hide, command=self.save).pack(anchor="w", pady=6)
        ttk.Label(general, text="清理操作固定使用 Windows 回收站，不提供永久删除。", foreground=app.palette["muted"]).pack(anchor="w", pady=6)
        ttk.Button(body, text="恢复默认设置", command=self.reset).pack(anchor="e", pady=20)

    def _row(self, parent, label, control):
        row = ttk.Frame(parent)
        row.pack(fill="x", pady=8)
        ttk.Label(row, text=label, width=16).pack(side="left")
        control.pack(in_=row, side="left")

    def _font_control(self, parent):
        frame = ttk.Frame(parent)
        self.font = tk.IntVar(value=self.app.settings.font_size)
        for text, value in (("小", 10), ("标准", 11), ("大", 13)):
            ttk.Radiobutton(frame, text=text, value=value, variable=self.font, command=self.apply).pack(side="left", padx=8)
        return frame

    def _theme_control(self, parent):
        frame = ttk.Frame(parent)
        self.theme = tk.StringVar(value=self.app.settings.theme)
        for text, value in (("浅色", "light"), ("深色", "dark")):
            ttk.Radiobutton(frame, text=text, value=value, variable=self.theme, command=self.apply).pack(side="left", padx=8)
        return frame

    def _scale_control(self, parent):
        frame = ttk.Frame(parent)
        self.scale = tk.IntVar(value=self.app.settings.scale)
        ttk.Combobox(frame, textvariable=self.scale, values=(90, 100, 110, 125), state="readonly", width=8).pack(side="left")
        ttk.Button(frame, text="应用", command=self.apply).pack(side="left", padx=8)
        return frame

    def save(self):
        self.app.settings.hide_during_capture = self.hide.get()
        save_settings(self.app.settings)

    def apply(self):
        self.app.settings.font_size = self.font.get()
        self.app.settings.theme = self.theme.get()
        self.app.settings.scale = self.scale.get()
        save_settings(self.app.settings)
        self.app.apply_theme()
        self.app.show_settings()

    def reset(self):
        pinned = self.app.settings.pinned
        self.app.settings = AppSettings(pinned=pinned)
        save_settings(self.app.settings)
        self.app.apply_theme()
        self.app.show_settings()


class ToolPage(ttk.Frame):
    def __init__(self, master, app: ToolboxApp, key: str):
        super().__init__(master)
        self.app = app
        self.key = key
        self.info = TOOL_BY_KEY[key]
        Header(self, app, self.info.name).pack(fill="x")
        self.body = ttk.Frame(self, padding=24)
        self.body.pack(fill="both", expand=True)
        ttk.Label(self.body, text=self.info.name, style="Title.TLabel").pack(anchor="w", pady=(0, 4))
        ttk.Label(self.body, text=self.info.summary, foreground=app.palette["muted"]).pack(anchor="w", pady=(0, 18))

    def run_thread(self, task, done, failed=None):
        def worker():
            try:
                result = task()
                self.after(0, lambda: done(result))
            except Exception as exc:
                self.after(0, lambda: (failed or self.show_error)(exc))
        threading.Thread(target=worker, daemon=True).start()

    def show_error(self, exc):
        messagebox.showerror("操作失败", str(exc), parent=self)


class ScreenshotPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.image = None
        self.hide = tk.BooleanVar(value=app.settings.hide_during_capture)
        controls = ttk.Frame(self.body)
        controls.pack(fill="x")
        ttk.Checkbutton(controls, text="截图时隐藏客户端", variable=self.hide).pack(side="left")
        ttk.Button(controls, text="开始截图  Ctrl+Shift+A", style="Accent.TButton", command=self.capture).pack(side="left", padx=15)
        ttk.Button(controls, text="保存到本地", command=self.save_image).pack(side="left")
        if key == "screenshot_search":
            ttk.Button(controls, text="搜索图片", command=self.search).pack(side="left", padx=8)
        self.preview = ttk.Label(self.body, text="截图预览区\n\n点击“开始截图”后拖动鼠标框选屏幕区域", anchor="center")
        self.preview.pack(fill="both", expand=True, pady=18)
        self.preview.bind("<Button-3>", self.popup_menu)
        self.result = tk.Text(self.body, height=7, relief="flat", wrap="word")
        if key == "screenshot_ocr":
            self.result.pack(fill="x")
        self.app.root.bind("<Control-Shift-a>", lambda _: self.capture())

    def destroy(self):
        self.app.root.unbind("<Control-Shift-a>")
        super().destroy()

    def capture(self):
        self.app.settings.hide_during_capture = self.hide.get()
        save_settings(self.app.settings)
        if self.hide.get():
            self.app.root.withdraw()
        self.after(180, lambda: CaptureOverlay(self, self.captured, self.cancel_capture))

    def cancel_capture(self):
        self.app.root.deiconify()

    def captured(self, image):
        self.image = image
        if self.hide.get():
            self.show_hidden_preview()
        else:
            self.app.root.deiconify()
            self.display_image()
        if self.key == "screenshot_ocr":
            self.run_thread(lambda: recognize(image), self.set_text)

    def display_image(self):
        from PIL import ImageTk
        image = self.image.copy()
        image.thumbnail((900, 420))
        self.tk_image = ImageTk.PhotoImage(image)
        self.preview.configure(image=self.tk_image, text="")

    def show_hidden_preview(self):
        from PIL import ImageTk
        window = tk.Toplevel(self)
        window.overrideredirect(True)
        window.attributes("-topmost", True)
        image = self.image.copy()
        image.thumbnail((720, 480))
        photo = ImageTk.PhotoImage(image)
        label = tk.Label(window, image=photo, bd=2, relief="solid")
        label.image = photo
        label.pack()
        window.geometry(f"+{max(10, window.winfo_screenwidth() - image.width - 30)}+30")
        menu = tk.Menu(window, tearoff=False)
        menu.add_command(label="复制文字", command=lambda: self.copy_hidden_text(window))
        menu.add_command(label="搜索图片", command=lambda: search_image(self.image))
        menu.add_separator()
        menu.add_command(label="退出", command=lambda: self.exit_hidden(window))
        label.bind("<Button-3>", lambda e: menu.tk_popup(e.x_root, e.y_root))
        label.bind("<Double-Button-1>", lambda _: self.exit_hidden(window))

    def copy_hidden_text(self, window):
        text = recognize(self.image)
        self.clipboard_clear()
        self.clipboard_append(text)
        self.update()
        window.title("文字已复制")

    def exit_hidden(self, window):
        window.destroy()
        self.app.root.deiconify()
        self.display_image()

    def set_text(self, text):
        self.result.delete("1.0", "end")
        self.result.insert("1.0", text)
        if not text.startswith("OCR 组件尚未就绪"):
            self.clipboard_clear()
            self.clipboard_append(text)

    def popup_menu(self, event):
        menu = tk.Menu(self, tearoff=False)
        menu.add_command(label="复制文字", command=lambda: self.set_text(recognize(self.image)) if self.image else None)
        menu.add_command(label="搜索图片", command=self.search)
        menu.add_command(label="退出截图", command=self.app.show_home)
        menu.tk_popup(event.x_root, event.y_root)

    def save_image(self):
        if not self.image:
            return messagebox.showinfo("提示", "请先截图。")
        path = filedialog.asksaveasfilename(defaultextension=".png", filetypes=[("PNG 图片", "*.png"), ("JPEG 图片", "*.jpg")])
        if path:
            self.image.save(path)

    def search(self):
        if self.image:
            search_image(self.image)
        else:
            messagebox.showinfo("提示", "请先截图。")


class PathChooser(ttk.Frame):
    def __init__(self, master, label="选择文件夹或磁盘"):
        super().__init__(master)
        self.value = tk.StringVar()
        ttk.Label(self, text=label, width=16).pack(side="left")
        ttk.Entry(self, textvariable=self.value).pack(side="left", fill="x", expand=True, ipady=5)
        ttk.Button(self, text="浏览", command=self.choose).pack(side="left", padx=(8, 0))

    def choose(self):
        path = filedialog.askdirectory()
        if path:
            self.value.set(path)

    def path(self) -> Path | None:
        path = Path(self.value.get()) if self.value.get() else None
        return path if path and path.exists() else None


class FileScanPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.chooser = PathChooser(self.body)
        self.chooser.pack(fill="x")
        ttk.Button(self.body, text="开始扫描", style="Accent.TButton", command=self.scan).pack(anchor="e", pady=12)
        self.status = ttk.Label(self.body, text="等待扫描")
        self.status.pack(anchor="w")
        columns = ("path", "time")
        self.tree = ttk.Treeview(self.body, columns=columns, show="headings", selectmode="extended")
        self.tree.heading("path", text="路径")
        self.tree.heading("time", text="修改时间")
        self.tree.column("path", width=800)
        self.tree.column("time", width=180)
        self.tree.pack(fill="both", expand=True, pady=10)
        ttk.Button(self.body, text="将选中项移入回收站", command=self.remove).pack(anchor="e")

    def scan(self):
        root = self.chooser.path()
        if not root:
            return messagebox.showwarning("请选择位置", "请选择有效的文件夹或磁盘。")
        self.status.configure(text="正在扫描……")
        finder = find_empty_folders if self.key == "empty_folders" else find_empty_files
        self.run_thread(lambda: finder(root, lambda n, p: self.after(0, lambda: self.status.configure(text=f"已检查 {n} 项：{p}"))), self.loaded)

    def loaded(self, paths):
        self.tree.delete(*self.tree.get_children())
        for path in paths:
            try:
                modified = str(path.stat().st_mtime_ns)[:10]
            except OSError:
                modified = "-"
            self.tree.insert("", "end", values=(str(path), modified))
        self.status.configure(text=f"扫描完成，共发现 {len(paths)} 项。")

    def remove(self):
        paths = [Path(self.tree.item(item, "values")[0]) for item in self.tree.selection()]
        if not paths:
            return messagebox.showinfo("提示", "请先选择需要清理的项目。")
        if not messagebox.askyesno("移入回收站", f"确定将选中的 {len(paths)} 项移入回收站吗？"):
            return
        deleted, failures = recycle(paths)
        for item in self.tree.selection():
            self.tree.delete(item)
        messagebox.showinfo("清理完成", f"已移入回收站：{deleted}\n失败：{len(failures)}")


class DuplicatePage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.chooser = PathChooser(self.body)
        self.chooser.pack(fill="x")
        ttk.Button(self.body, text="开始扫描", style="Accent.TButton", command=self.scan).pack(anchor="e", pady=12)
        self.status = ttk.Label(self.body, text="通过文件大小和 SHA-256 内容哈希确认重复文件")
        self.status.pack(anchor="w")
        self.tree = ttk.Treeview(self.body, columns=("group", "path", "size", "keep"), show="headings", selectmode="extended")
        for key_name, title, width in (("group", "重复组", 80), ("path", "文件路径", 720), ("size", "大小", 100), ("keep", "建议", 120)):
            self.tree.heading(key_name, text=title)
            self.tree.column(key_name, width=width)
        self.tree.pack(fill="both", expand=True, pady=10)
        controls = ttk.Frame(self.body)
        controls.pack(fill="x")
        ttk.Button(controls, text="自动选择重复副本", command=self.auto_select).pack(side="right", padx=8)
        ttk.Button(controls, text="将选中项移入回收站", command=self.remove).pack(side="right")

    def scan(self):
        root = self.chooser.path()
        if not root:
            return messagebox.showwarning("请选择位置", "请选择有效的文件夹或磁盘。")
        self.status.configure(text="正在计算内容哈希……")
        self.run_thread(lambda: find_duplicates(root, lambda n, p: self.after(0, lambda: self.status.configure(text=f"正在比对第 {n} 个候选文件：{p}"))), self.loaded)

    def loaded(self, groups):
        self.tree.delete(*self.tree.get_children())
        releasable = 0
        for group_index, paths in enumerate(groups, 1):
            for index, path in enumerate(paths):
                try:
                    size = path.stat().st_size
                except OSError:
                    size = 0
                if index:
                    releasable += size
                self.tree.insert("", "end", values=(group_index, str(path), human_size(size), "保留" if index == 0 else "可清理"), tags=("copy" if index else "keep",))
        self.status.configure(text=f"发现 {len(groups)} 组重复文件，预计可释放 {human_size(releasable)}。")

    def auto_select(self):
        self.tree.selection_set([item for item in self.tree.get_children() if self.tree.item(item, "values")[3] == "可清理"])

    def remove(self):
        paths = [Path(self.tree.item(item, "values")[1]) for item in self.tree.selection()]
        if not paths:
            return messagebox.showinfo("提示", "请先选择重复副本。")
        if not messagebox.askyesno("移入回收站", f"每组应至少保留一份。确定将 {len(paths)} 个文件移入回收站吗？"):
            return
        deleted, failures = recycle(paths)
        for item in self.tree.selection():
            self.tree.delete(item)
        messagebox.showinfo("清理完成", f"已移入回收站：{deleted}\n失败：{len(failures)}")


class FtpPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.chooser = PathChooser(self.body, "共享文件夹")
        self.chooser.pack(fill="x", pady=5)
        options = ttk.LabelFrame(self.body, text="连接与权限", padding=15)
        options.pack(fill="x", pady=12)
        self.port = tk.IntVar(value=2121)
        self.anonymous = tk.BooleanVar(value=True)
        self.upload = tk.BooleanVar(value=False)
        self.username = tk.StringVar(value="muxia")
        self.password = tk.StringVar(value="123456")
        ttk.Label(options, text="端口").grid(row=0, column=0, sticky="w")
        ttk.Entry(options, textvariable=self.port, width=10).grid(row=0, column=1, padx=8)
        ttk.Checkbutton(options, text="匿名访问", variable=self.anonymous).grid(row=0, column=2, padx=15)
        ttk.Checkbutton(options, text="允许上传", variable=self.upload).grid(row=0, column=3, padx=15)
        ttk.Label(options, text="账号").grid(row=1, column=0, sticky="w", pady=10)
        ttk.Entry(options, textvariable=self.username, width=18).grid(row=1, column=1, padx=8)
        ttk.Label(options, text="密码").grid(row=1, column=2)
        ttk.Entry(options, textvariable=self.password, show="•", width=18).grid(row=1, column=3)
        self.address = ttk.Label(self.body, text=f"访问地址：ftp://{local_ip()}:2121", style="Heading.TLabel")
        self.address.pack(anchor="w", pady=8)
        controls = ttk.Frame(self.body)
        controls.pack(fill="x")
        self.toggle = ttk.Button(controls, text="启动服务", style="Accent.TButton", command=self.toggle_service)
        self.toggle.pack(side="left")
        self.log = tk.Text(self.body, height=16, relief="flat")
        self.log.pack(fill="both", expand=True, pady=14)

    def toggle_service(self):
        if self.app.ftp_service.running:
            self.app.ftp_service.stop()
            self.toggle.configure(text="启动服务")
            self.write_log("服务已停止。")
            return
        folder = self.chooser.path()
        if not folder:
            return messagebox.showwarning("请选择位置", "请选择要共享的文件夹。")
        try:
            self.app.ftp_service.start(folder, self.port.get(), self.username.get(), self.password.get(), self.anonymous.get(), self.upload.get(), self.write_log)
            self.address.configure(text=f"访问地址：ftp://{local_ip()}:{self.port.get()}")
            self.toggle.configure(text="停止服务")
        except Exception as exc:
            self.show_error(exc)

    def write_log(self, text):
        self.log.insert("end", text + "\n")
        self.log.see("end")


class ArticlePage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        split = ttk.Panedwindow(self.body, orient="horizontal")
        split.pack(fill="both", expand=True)
        left, right = ttk.Frame(split), ttk.Frame(split)
        split.add(left, weight=3)
        split.add(right, weight=2)
        controls = ttk.Frame(left)
        controls.pack(fill="x", pady=(0, 8))
        ttk.Button(controls, text="导入文档", command=self.load).pack(side="left")
        self.mode = tk.StringVar(value="核心要点")
        ttk.Combobox(controls, textvariable=self.mode, values=("一句话总结", "核心要点", "完整摘要"), state="readonly", width=14).pack(side="left", padx=8)
        self.ratio = tk.DoubleVar(value=0.25)
        ttk.Scale(controls, variable=self.ratio, from_=0.1, to=0.6).pack(side="left", fill="x", expand=True, padx=8)
        ttk.Button(controls, text="开始提炼", style="Accent.TButton", command=self.extract).pack(side="right")
        self.input = tk.Text(left, wrap="word", relief="flat")
        self.input.pack(fill="both", expand=True, padx=(0, 10))
        ttk.Label(right, text="提炼结果", style="Heading.TLabel").pack(anchor="w")
        self.output = tk.Text(right, wrap="word", relief="flat")
        self.output.pack(fill="both", expand=True, pady=8)
        actions = ttk.Frame(right)
        actions.pack(fill="x")
        ttk.Button(actions, text="复制结果", command=self.copy).pack(side="left")
        ttk.Button(actions, text="导出", command=self.export).pack(side="left", padx=8)

    def load(self):
        path = filedialog.askopenfilename(filetypes=[("文本文件", "*.txt *.md"), ("所有文件", "*.*")])
        if path:
            self.input.delete("1.0", "end")
            self.input.insert("1.0", Path(path).read_text(encoding="utf-8", errors="ignore"))

    def extract(self):
        result = summarize(self.input.get("1.0", "end"), self.ratio.get(), self.mode.get())
        self.output.delete("1.0", "end")
        self.output.insert("1.0", result)

    def copy(self):
        self.clipboard_clear()
        self.clipboard_append(self.output.get("1.0", "end").strip())

    def export(self):
        path = filedialog.asksaveasfilename(defaultextension=".txt", filetypes=[("文本文件", "*.txt")])
        if path:
            Path(path).write_text(self.output.get("1.0", "end").strip(), encoding="utf-8")


class VideoPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.video = tk.StringVar()
        row = ttk.Frame(self.body)
        row.pack(fill="x")
        ttk.Entry(row, textvariable=self.video).pack(side="left", fill="x", expand=True, ipady=5)
        ttk.Button(row, text="选择视频", command=self.choose).pack(side="left", padx=8)
        self.mode = tk.StringVar(value="逐字稿")
        ttk.Combobox(row, textvariable=self.mode, values=("逐字稿", "精简文稿", "字幕 SRT"), state="readonly", width=12).pack(side="left")
        ttk.Button(row, text="开始转写", style="Accent.TButton", command=self.transcribe).pack(side="left", padx=8)
        self.status = ttk.Label(self.body, text="需要安装 faster-whisper，并确保 FFmpeg 可用。基础应用仍可正常使用。")
        self.status.pack(anchor="w", pady=12)
        self.output = tk.Text(self.body, wrap="word", relief="flat")
        self.output.pack(fill="both", expand=True)
        actions = ttk.Frame(self.body)
        actions.pack(fill="x", pady=10)
        ttk.Button(actions, text="导出 TXT", command=lambda: self.export(".txt")).pack(side="right")
        ttk.Button(actions, text="导出 SRT", command=lambda: self.export(".srt")).pack(side="right", padx=8)

    def choose(self):
        path = filedialog.askopenfilename(filetypes=[("视频", "*.mp4 *.mov *.avi *.mkv *.wmv *.m4v"), ("所有文件", "*.*")])
        if path:
            self.video.set(path)

    def transcribe(self):
        path = Path(self.video.get())
        if not path.exists():
            return messagebox.showwarning("请选择视频", "请选择有效的视频文件。")
        self.status.configure(text="正在加载语音识别模型……")
        def task():
            try:
                from faster_whisper import WhisperModel
            except ImportError as exc:
                raise RuntimeError("视频转写组件未安装。请安装 faster-whisper，并配置 FFmpeg。") from exc
            model = WhisperModel("small", device="cpu", compute_type="int8")
            segments, _ = model.transcribe(str(path), vad_filter=True)
            rows = list(segments)
            if self.mode.get() == "字幕 SRT":
                return "\n\n".join(f"{i}\n{self._srt_time(s.start)} --> {self._srt_time(s.end)}\n{s.text.strip()}" for i, s in enumerate(rows, 1))
            text = "\n".join(f"[{s.start:07.2f}] {s.text.strip()}" for s in rows)
            if self.mode.get() == "精简文稿":
                return summarize(text, 0.45, "完整摘要")
            return text
        self.run_thread(task, self.transcribed)

    @staticmethod
    def _srt_time(value):
        ms = int((value % 1) * 1000)
        seconds = int(value)
        return f"{seconds // 3600:02d}:{seconds % 3600 // 60:02d}:{seconds % 60:02d},{ms:03d}"

    def transcribed(self, text):
        self.output.delete("1.0", "end")
        self.output.insert("1.0", text)
        self.status.configure(text="转写完成。")

    def export(self, suffix):
        path = filedialog.asksaveasfilename(defaultextension=suffix, filetypes=[("文件", f"*{suffix}")])
        if path:
            Path(path).write_text(self.output.get("1.0", "end").strip(), encoding="utf-8")


class MediaSortPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.source = PathChooser(self.body, "源文件夹")
        self.source.pack(fill="x", pady=4)
        self.destination = PathChooser(self.body, "目标文件夹")
        self.destination.pack(fill="x", pady=4)
        controls = ttk.Frame(self.body)
        controls.pack(fill="x", pady=10)
        self.move = tk.BooleanVar(value=False)
        ttk.Radiobutton(controls, text="复制到目标文件夹", variable=self.move, value=False).pack(side="left")
        ttk.Radiobutton(controls, text="移动到目标文件夹", variable=self.move, value=True).pack(side="left", padx=12)
        ttk.Button(controls, text="预览分类", command=self.preview).pack(side="right", padx=8)
        ttk.Button(controls, text="开始整理", style="Accent.TButton", command=self.organize).pack(side="right")
        self.count_label = ttk.Label(self.body, text="照片 0    GIF 动图 0    视频 0")
        self.count_label.pack(anchor="w")
        self.tree = ttk.Treeview(self.body, columns=("file", "kind", "target"), show="headings")
        for key_name, title, width in (("file", "源文件", 520), ("kind", "类型", 120), ("target", "目标文件夹", 300)):
            self.tree.heading(key_name, text=title)
            self.tree.column(key_name, width=width)
        self.tree.pack(fill="both", expand=True, pady=10)
        self.files = []

    def preview(self):
        root = self.source.path()
        target = self.destination.path()
        if not root or not target:
            return messagebox.showwarning("请选择位置", "请选择有效的源文件夹和目标文件夹。")
        self.files = [path for path in iter_files(root) if media_kind(path)]
        counts = Counter(media_kind(path) for path in self.files)
        self.count_label.configure(text=f"照片 {counts['照片']}    GIF 动图 {counts['GIF 动图']}    视频 {counts['视频']}")
        self.tree.delete(*self.tree.get_children())
        for path in self.files:
            kind = media_kind(path)
            self.tree.insert("", "end", values=(str(path), kind, str(target / kind)))

    def organize(self):
        if not self.files:
            self.preview()
        target = self.destination.path()
        if not target or not self.files:
            return
        action = "移动" if self.move.get() else "复制"
        if not messagebox.askyesno("开始整理", f"确定{action} {len(self.files)} 个文件吗？重名文件会自动编号。"):
            return
        self.run_thread(lambda: organize_media(self.files, target, self.move.get()), lambda count: messagebox.showinfo("整理完成", f"已处理 {count} 个文件。"))


class SmartPhotoPage(ToolPage):
    def __init__(self, master, app, key):
        super().__init__(master, app, key)
        self.source = PathChooser(self.body, "照片文件夹")
        self.source.pack(fill="x")
        controls = ttk.Frame(self.body)
        controls.pack(fill="x", pady=10)
        ttk.Label(controls, text="命名规则：拍摄时间_设备_来源_序号").pack(side="left")
        ttk.Button(controls, text="预览分类", command=self.preview).pack(side="right", padx=8)
        ttk.Button(controls, text="开始整理", style="Accent.TButton", command=self.organize).pack(side="right")
        self.low_only = tk.BooleanVar(value=False)
        ttk.Checkbutton(controls, text="仅看低置信度", variable=self.low_only, command=self.render).pack(side="right", padx=12)
        self.summary = ttk.Label(self.body, text="截图 0  网络存图 0  人物照片 0  动物照片 0  其他拍照 0  后期照片 0")
        self.summary.pack(anchor="w")
        self.tree = ttk.Treeview(self.body, columns=("source", "category", "confidence", "new_name"), show="headings", selectmode="extended")
        for key_name, title, width in (("source", "原文件", 420), ("category", "分类", 110), ("confidence", "置信度", 90), ("new_name", "新文件名", 420)):
            self.tree.heading(key_name, text=title)
            self.tree.column(key_name, width=width)
        self.tree.pack(fill="both", expand=True, pady=10)
        self.results: list[PhotoResult] = []

    def preview(self):
        root = self.source.path()
        if not root:
            return messagebox.showwarning("请选择位置", "请选择有效的照片文件夹。")
        self.run_thread(lambda: scan_photos(root), self.loaded)

    def loaded(self, results):
        self.results = results
        counts = Counter(item.category for item in results)
        categories = ("截图", "网络存图", "人物照片", "动物照片", "其他拍照", "后期照片")
        self.summary.configure(text="    ".join(f"{category} {counts[category]}" for category in categories))
        self.render()

    def render(self):
        self.tree.delete(*self.tree.get_children())
        for item in self.results:
            if self.low_only.get() and item.confidence >= 75:
                continue
            self.tree.insert("", "end", values=(str(item.source), item.category, f"{item.confidence}%", item.new_name))

    def organize(self):
        if not self.results:
            return messagebox.showinfo("提示", "请先预览分类结果。")
        if not messagebox.askyesno("开始整理", "将在照片文件夹内建立 6 个分类子文件夹并复制照片。原文件不会删除。是否继续？"):
            return
        def task():
            count = 0
            for item in self.results:
                folder = item.source.parents[0] / item.category
                folder.mkdir(exist_ok=True)
                target = unique_destination(folder, item.new_name)
                shutil.copy2(item.source, target)
                count += 1
            return count
        self.run_thread(task, lambda count: messagebox.showinfo("整理完成", f"已复制并分类 {count} 张照片。"))
