from __future__ import annotations

import argparse
from pathlib import Path

from faster_whisper import WhisperModel


def srt_time(value: float) -> str:
    millis = int((value % 1) * 1000)
    seconds = int(value)
    return f"{seconds // 3600:02d}:{seconds % 3600 // 60:02d}:{seconds % 60:02d},{millis:03d}"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input")
    parser.add_argument("--mode", choices=("text", "compact", "srt"), default="text")
    args = parser.parse_args()
    model = WhisperModel("small", device="cpu", compute_type="int8")
    segments, _ = model.transcribe(args.input, vad_filter=True)
    rows = list(segments)
    if args.mode == "srt":
        print("\n\n".join(
            f"{index}\n{srt_time(row.start)} --> {srt_time(row.end)}\n{row.text.strip()}"
            for index, row in enumerate(rows, 1)
        ))
    elif args.mode == "compact":
        print("".join(row.text.strip() for row in rows))
    else:
        print("\n".join(f"[{row.start:07.2f}] {row.text.strip()}" for row in rows))


if __name__ == "__main__":
    main()
