from __future__ import annotations

import io
import os
import tempfile
import tkinter as tk
import webbrowser
from pathlib import Path
from typing import Callable


def copy_image_to_clipboard(image) -> None:
    if os.name != "nt":
        return
    import ctypes

    output = io.BytesIO()
    image.convert("RGB").save(output, "BMP")
    data = output.getvalue()[14:]
    GMEM_MOVEABLE, CF_DIB = 0x0002, 8
    kernel32, user32 = ctypes.windll.kernel32, ctypes.windll.user32
    handle = kernel32.GlobalAlloc(GMEM_MOVEABLE, len(data))
    pointer = kernel32.GlobalLock(handle)
    ctypes.memmove(pointer, data, len(data))
    kernel32.GlobalUnlock(handle)
    if user32.OpenClipboard(None):
        user32.EmptyClipboard()
        user32.SetClipboardData(CF_DIB, handle)
        user32.CloseClipboard()


def recognize(image) -> str:
    try:
        import pytesseract

        return pytesseract.image_to_string(image, lang="chi_sim+eng").strip()
    except Exception as exc:
        return f"OCR 组件尚未就绪：{exc}\n可运行 install-enhancements.ps1，并安装 Tesseract OCR。"


def search_image(image) -> None:
    copy_image_to_clipboard(image)
    webbrowser.open("https://www.bing.com/visualsearch")


class CaptureOverlay:
    def __init__(self, master: tk.Misc, callback: Callable, cancel: Callable | None = None):
        from PIL import ImageGrab, ImageTk

        self.callback = callback
        self.cancel_callback = cancel
        self.image = ImageGrab.grab(all_screens=True)
        self.window = tk.Toplevel(master)
        self.window.attributes("-fullscreen", True)
        self.window.attributes("-topmost", True)
        self.window.configure(cursor="crosshair")
        screen_w, screen_h = self.window.winfo_screenwidth(), self.window.winfo_screenheight()
        preview = self.image.resize((screen_w, screen_h))
        self.photo = ImageTk.PhotoImage(preview)
        self.canvas = tk.Canvas(self.window, width=screen_w, height=screen_h, highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)
        self.canvas.create_image(0, 0, image=self.photo, anchor="nw")
        self.canvas.create_rectangle(0, 0, screen_w, screen_h, fill="black", stipple="gray50", outline="")
        self.start = None
        self.rect = None
        self.canvas.bind("<ButtonPress-1>", self._press)
        self.canvas.bind("<B1-Motion>", self._drag)
        self.canvas.bind("<ButtonRelease-1>", self._release)
        self.window.bind("<Escape>", self._cancel)

    def _press(self, event):
        self.start = (event.x, event.y)
        if self.rect:
            self.canvas.delete(self.rect)
        self.rect = self.canvas.create_rectangle(event.x, event.y, event.x, event.y, outline="#16a3c7", width=3)

    def _drag(self, event):
        if self.start:
            self.canvas.coords(self.rect, self.start[0], self.start[1], event.x, event.y)

    def _release(self, event):
        if not self.start:
            return
        x1, y1 = self.start
        x2, y2 = event.x, event.y
        if abs(x2 - x1) < 5 or abs(y2 - y1) < 5:
            return
        scale_x = self.image.width / self.window.winfo_screenwidth()
        scale_y = self.image.height / self.window.winfo_screenheight()
        box = tuple(map(int, (min(x1, x2) * scale_x, min(y1, y2) * scale_y, max(x1, x2) * scale_x, max(y1, y2) * scale_y)))
        cropped = self.image.crop(box)
        self.window.destroy()
        self.callback(cropped)

    def _cancel(self, _event=None):
        self.window.destroy()
        if self.cancel_callback:
            self.cancel_callback()


def save_temp_image(image) -> Path:
    path = Path(tempfile.gettempdir()) / "muxia_capture.png"
    image.save(path)
    return path

