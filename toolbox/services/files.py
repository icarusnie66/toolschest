from __future__ import annotations

import hashlib
import os
import shutil
from collections import defaultdict
from pathlib import Path
from typing import Callable, Iterable


Progress = Callable[[int, str], None]


def iter_files(root: Path) -> Iterable[Path]:
    for dirpath, _, filenames in os.walk(root, onerror=lambda _: None):
        base = Path(dirpath)
        for filename in filenames:
            yield base / filename


def find_empty_files(root: Path, progress: Progress | None = None) -> list[Path]:
    found: list[Path] = []
    for index, path in enumerate(iter_files(root), 1):
        try:
            if path.stat().st_size == 0:
                found.append(path)
        except OSError:
            pass
        if progress and index % 100 == 0:
            progress(index, str(path))
    return found


def find_empty_folders(root: Path, progress: Progress | None = None) -> list[Path]:
    found: list[Path] = []
    count = 0
    for dirpath, dirnames, filenames in os.walk(root, topdown=False, onerror=lambda _: None):
        count += 1
        path = Path(dirpath)
        try:
            if path != root and not dirnames and not filenames and not any(path.iterdir()):
                found.append(path)
        except OSError:
            pass
        if progress and count % 100 == 0:
            progress(count, str(path))
    return found


def _digest(path: Path, chunk_size: int = 1024 * 1024) -> str:
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(chunk_size), b""):
            value.update(chunk)
    return value.hexdigest()


def find_duplicates(root: Path, progress: Progress | None = None) -> list[list[Path]]:
    by_size: dict[int, list[Path]] = defaultdict(list)
    files = list(iter_files(root))
    for path in files:
        try:
            size = path.stat().st_size
            if size > 0:
                by_size[size].append(path)
        except OSError:
            pass
    candidates = [path for paths in by_size.values() if len(paths) > 1 for path in paths]
    by_hash: dict[tuple[int, str], list[Path]] = defaultdict(list)
    for index, path in enumerate(candidates, 1):
        try:
            by_hash[(path.stat().st_size, _digest(path))].append(path)
        except OSError:
            pass
        if progress:
            progress(index, str(path))
    return [paths for paths in by_hash.values() if len(paths) > 1]


def recycle(paths: Iterable[Path]) -> tuple[int, list[str]]:
    materialized = [Path(path) for path in paths]
    try:
        from send2trash import send2trash

        failures: list[str] = []
        deleted = 0
        for path in materialized:
            try:
                send2trash(str(path))
                deleted += 1
            except OSError as exc:
                failures.append(f"{path}: {exc}")
        return deleted, failures
    except ImportError:
        return _recycle_windows(materialized)


def _recycle_windows(paths: list[Path]) -> tuple[int, list[str]]:
    if os.name != "nt":
        return 0, ["当前系统未安装 send2trash，无法安全移入回收站。"]
    import ctypes
    from ctypes import wintypes

    class SHFILEOPSTRUCTW(ctypes.Structure):
        _fields_ = [
            ("hwnd", wintypes.HWND), ("wFunc", wintypes.UINT),
            ("pFrom", wintypes.LPCWSTR), ("pTo", wintypes.LPCWSTR),
            ("fFlags", wintypes.WORD), ("fAnyOperationsAborted", wintypes.BOOL),
            ("hNameMappings", ctypes.c_void_p), ("lpszProgressTitle", wintypes.LPCWSTR),
        ]

    failures: list[str] = []
    deleted = 0
    for path in paths:
        source = str(path.resolve()) + "\0\0"
        operation = SHFILEOPSTRUCTW()
        operation.wFunc = 3  # FO_DELETE
        operation.pFrom = source
        operation.fFlags = 0x40 | 0x10 | 0x400  # undo, no confirmation, no error UI
        result = ctypes.windll.shell32.SHFileOperationW(ctypes.byref(operation))
        if result == 0 and not operation.fAnyOperationsAborted:
            deleted += 1
        else:
            failures.append(f"{path}: 无法移入回收站（错误 {result}）")
    return deleted, failures


PHOTO_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff", ".heic"}
VIDEO_EXTENSIONS = {".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".webm", ".flv"}


def media_kind(path: Path) -> str | None:
    suffix = path.suffix.lower()
    if suffix == ".gif":
        return "GIF 动图"
    if suffix in PHOTO_EXTENSIONS:
        return "照片"
    if suffix in VIDEO_EXTENSIONS:
        return "视频"
    return None


def unique_destination(folder: Path, filename: str) -> Path:
    target = folder / filename
    index = 1
    while target.exists():
        target = folder / f"{Path(filename).stem}_{index}{Path(filename).suffix}"
        index += 1
    return target


def organize_media(files: Iterable[Path], destination: Path, move: bool = False) -> int:
    count = 0
    for source in files:
        kind = media_kind(source)
        if not kind:
            continue
        folder = destination / kind
        folder.mkdir(parents=True, exist_ok=True)
        target = unique_destination(folder, source.name)
        (shutil.move if move else shutil.copy2)(source, target)
        count += 1
    return count

