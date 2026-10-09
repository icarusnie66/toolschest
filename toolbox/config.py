from __future__ import annotations

import json
import os
from dataclasses import asdict, dataclass, field
from pathlib import Path


APP_DIR = Path(os.getenv("APPDATA", Path.home())) / "MuxiaToolbox"
SETTINGS_FILE = APP_DIR / "settings.json"


@dataclass
class AppSettings:
    theme: str = "light"
    font_size: int = 11
    scale: int = 100
    pinned: list[str] = field(
        default_factory=lambda: ["screenshot_ocr", "duplicate_files", "smart_photos"]
    )
    hide_during_capture: bool = True
    minimize_to_tray: bool = False


def load_settings() -> AppSettings:
    try:
        raw = json.loads(SETTINGS_FILE.read_text(encoding="utf-8"))
        allowed = set(AppSettings.__dataclass_fields__)
        return AppSettings(**{key: value for key, value in raw.items() if key in allowed})
    except (OSError, ValueError, TypeError):
        return AppSettings()


def save_settings(settings: AppSettings) -> None:
    APP_DIR.mkdir(parents=True, exist_ok=True)
    SETTINGS_FILE.write_text(
        json.dumps(asdict(settings), ensure_ascii=False, indent=2), encoding="utf-8"
    )

