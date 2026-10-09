from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

from .files import PHOTO_EXTENSIONS


@dataclass
class PhotoResult:
    source: Path
    category: str
    confidence: int
    captured_at: str
    device: str
    origin: str
    new_name: str


def _safe(value: str) -> str:
    value = re.sub(r"[<>:\"/\\|?*]+", "_", value.strip())
    return value or "未知"


def inspect_photo(path: Path, index: int = 1) -> PhotoResult:
    captured = datetime.fromtimestamp(path.stat().st_mtime).strftime("%Y%m%d_%H%M%S")
    device = "未知设备"
    software = ""
    width = height = 0
    has_exif = False
    try:
        from PIL import Image, ExifTags

        with Image.open(path) as image:
            width, height = image.size
            exif = image.getexif()
            has_exif = bool(exif)
            mapped = {ExifTags.TAGS.get(key, key): value for key, value in exif.items()}
            captured_raw = str(mapped.get("DateTimeOriginal", ""))
            if captured_raw:
                captured = datetime.strptime(captured_raw, "%Y:%m:%d %H:%M:%S").strftime("%Y%m%d_%H%M%S")
            device = _safe("_".join(filter(None, [str(mapped.get("Make", "")).strip(), str(mapped.get("Model", "")).strip()])))
            software = str(mapped.get("Software", ""))
    except Exception:
        pass

    name = path.stem.lower()
    if any(token in name for token in ("screenshot", "截图", "snipaste", "screen_")) or (not has_exif and width and height and width / max(height, 1) in (16 / 9, 9 / 16)):
        category, confidence, origin = "截图", 95, "屏幕"
    elif software and any(token in software.lower() for token in ("photoshop", "lightroom", "snapseed", "meitu")):
        category, confidence, origin = "后期照片", 92, "后期"
    elif any(token in name for token in ("cat", "dog", "animal", "猫", "狗", "宠物")):
        category, confidence, origin = "动物照片", 72, "拍摄"
    elif any(token in name for token in ("portrait", "selfie", "face", "人像", "自拍")):
        category, confidence, origin = "人物照片", 72, "拍摄"
    elif not has_exif:
        category, confidence, origin = "网络存图", 68, "网络"
    else:
        category, confidence, origin = "其他拍照", 80, "拍摄"
    new_name = f"{captured}_{_safe(device)}_{origin}_{index:04d}{path.suffix.lower()}"
    return PhotoResult(path, category, confidence, captured, device, origin, new_name)


def scan_photos(root: Path) -> list[PhotoResult]:
    files = [path for path in root.rglob("*") if path.is_file() and path.suffix.lower() in PHOTO_EXTENSIONS]
    return [inspect_photo(path, index) for index, path in enumerate(files, 1)]

