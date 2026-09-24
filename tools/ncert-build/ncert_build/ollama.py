"""A minimal client for Ollama running on the Windows host.

WSL2 in NAT mode can't reach the host's ``localhost``, so besides OLLAMA_HOST this tries the
default gateway, which is the Windows host. Ollama must then listen beyond loopback: set
``OLLAMA_HOST=0.0.0.0`` in Windows' environment variables and restart Ollama.
"""

from __future__ import annotations

import json
import socket
import struct
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_PORT = 11434


def _wsl_gateway() -> str | None:
    try:
        for line in Path("/proc/net/route").read_text().splitlines()[1:]:
            fields = line.split()
            if fields[1] == "00000000":
                return socket.inet_ntoa(struct.pack("<L", int(fields[2], 16)))
    except (OSError, IndexError, ValueError):
        pass
    return None


def candidate_hosts(configured: str | None) -> list[str]:
    if configured:
        return [configured if configured.startswith("http") else f"http://{configured}"]
    hosts = [f"http://localhost:{DEFAULT_PORT}"]
    gateway = _wsl_gateway()
    if gateway:
        hosts.append(f"http://{gateway}:{DEFAULT_PORT}")
    return hosts


class Ollama:
    def __init__(self, host: str, timeout: float = 600.0):
        self.host = host.rstrip("/")
        self.timeout = timeout

    @staticmethod
    def connect(configured: str | None) -> "Ollama":
        tried = candidate_hosts(configured)
        for host in tried:
            client = Ollama(host)
            try:
                client.models()
                return client
            except (urllib.error.URLError, ConnectionError, TimeoutError, OSError):
                continue
        raise SystemExit(
            "Can't reach Ollama at " + ", ".join(tried) + ". Start Ollama on Windows with "
            "OLLAMA_HOST=0.0.0.0, or set OLLAMA_HOST here to where it listens."
        )

    def _post(self, path: str, body: dict) -> dict:
        request = urllib.request.Request(
            self.host + path,
            data=json.dumps(body).encode(),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(request, timeout=self.timeout) as response:
            return json.loads(response.read())

    def models(self) -> list[str]:
        with urllib.request.urlopen(self.host + "/api/tags", timeout=3) as response:
            return [m["name"] for m in json.loads(response.read()).get("models", [])]

    def require(self, model: str) -> None:
        available = self.models()
        if model not in available and f"{model}:latest" not in available:
            raise SystemExit(
                f"Model {model!r} isn't pulled. Run `ollama pull {model}` on Windows. "
                f"Available: {', '.join(available) or 'none'}"
            )

    def chat_json(self, model: str, system: str, user: str, schema: dict, num_ctx: int) -> dict:
        """One chat turn whose reply is constrained to ``schema``."""
        reply = self._post(
            "/api/chat",
            {
                "model": model,
                "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
                "format": schema,
                "stream": False,
                "options": {"temperature": 0.2, "num_ctx": num_ctx},
            },
        )
        return json.loads(reply["message"]["content"])
