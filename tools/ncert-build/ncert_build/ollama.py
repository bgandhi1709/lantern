"""A minimal client for Ollama running on the Windows host.

WSL2 in NAT mode can't reach the host's ``localhost``, so besides OLLAMA_HOST this tries the
default gateway, which is the Windows host. Ollama must then listen beyond loopback: set
``OLLAMA_HOST=0.0.0.0`` in Windows' environment variables and restart Ollama.
"""

from __future__ import annotations

import base64
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
        empty = None
        for host in tried:
            client = Ollama(host)
            try:
                if client.models():
                    return client
                empty = empty or client  # e.g. a second Ollama inside WSL with nothing pulled
            except (urllib.error.URLError, ConnectionError, TimeoutError, OSError):
                continue
        if empty is not None:
            return empty
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
        """One chat turn whose reply is constrained to ``schema``.

        Thinking is off: reasoning models such as qwen3 otherwise write hidden reasoning before
        every reply (about 20 times the tokens of the answer in a test), and drafting is
        extraction, not reasoning. Claude does the careful pass later.
        """
        reply = self._post(
            "/api/chat",
            {
                "model": model,
                "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
                "format": schema,
                "think": False,
                "stream": False,
                "options": {"temperature": 0.2, "num_ctx": num_ctx},
            },
        )
        return json.loads(reply["message"]["content"])

    def read_image(self, model: str, prompt: str, image: bytes, schema: dict, temperature: float, num_ctx: int) -> dict:
        """One vision turn whose reply is constrained to ``schema``. Use an instruct build
        (qwen3-vl:8b-instruct): the default qwen3-vl build reasons at length before answering even
        with thinking off, which made each figure take a minute. An empty or broken reply is
        asked once more, a little warmer."""
        for attempt in range(2):
            reply = self._post(
                "/api/chat",
                {
                    "model": model,
                    "messages": [{"role": "user", "content": prompt, "images": [base64.b64encode(image).decode()]}],
                    "format": schema,
                    "think": False,
                    "stream": False,
                    "options": {"temperature": temperature + 0.2 * attempt, "num_ctx": num_ctx},
                },
            )
            try:
                return json.loads(reply["message"]["content"])
            except (ValueError, KeyError):
                continue
        raise ValueError("the vision model gave no readable answer twice")
