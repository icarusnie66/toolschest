from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from toolbox.services.content import summarize
from toolbox.services.files import find_duplicates, find_empty_files, find_empty_folders, media_kind


class ServiceTests(unittest.TestCase):
    def test_empty_and_duplicate_scans(self):
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw)
            (root / "empty-dir").mkdir()
            (root / "zero.txt").write_bytes(b"")
            (root / "a.bin").write_bytes(b"same")
            (root / "b.bin").write_bytes(b"same")
            self.assertIn(root / "zero.txt", find_empty_files(root))
            self.assertIn(root / "empty-dir", find_empty_folders(root))
            groups = find_duplicates(root)
            self.assertEqual({root / "a.bin", root / "b.bin"}, set(groups[0]))

    def test_media_kind(self):
        self.assertEqual("照片", media_kind(Path("a.jpg")))
        self.assertEqual("GIF 动图", media_kind(Path("a.gif")))
        self.assertEqual("视频", media_kind(Path("a.mp4")))

    def test_summary(self):
        text = "工具可以提高效率。好的工具应当简单可靠。复杂工具会增加学习成本。"
        self.assertTrue(summarize(text, mode="一句话总结"))
        self.assertIn("1.", summarize(text, mode="核心要点"))


if __name__ == "__main__":
    unittest.main()
