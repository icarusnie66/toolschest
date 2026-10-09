from __future__ import annotations

import socket
import threading
from pathlib import Path


def local_ip() -> str:
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.connect(("8.8.8.8", 80))
        return sock.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        sock.close()


class FtpService:
    def __init__(self):
        self.server = None
        self.thread: threading.Thread | None = None

    @property
    def running(self) -> bool:
        return bool(self.thread and self.thread.is_alive())

    def start(self, folder: Path, port: int, username: str, password: str, anonymous: bool, upload: bool, logger):
        try:
            from pyftpdlib.authorizers import DummyAuthorizer
            from pyftpdlib.handlers import FTPHandler
            from pyftpdlib.servers import FTPServer
        except ImportError as exc:
            raise RuntimeError("请先运行 install-enhancements.ps1 安装 FTP 组件。") from exc
        authorizer = DummyAuthorizer()
        permission = "elradfmwMT" if upload else "elr"
        if anonymous:
            authorizer.add_anonymous(str(folder), perm=permission)
        else:
            authorizer.add_user(username, password, str(folder), perm=permission)
        handler = FTPHandler
        handler.authorizer = authorizer
        handler.banner = "Muxia Toolbox FTP"
        self.server = FTPServer(("0.0.0.0", port), handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        logger(f"服务已启动：ftp://{local_ip()}:{port}")

    def stop(self):
        if self.server:
            self.server.close_all()
        self.server = None
        self.thread = None

