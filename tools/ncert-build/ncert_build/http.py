"""Polite HTTP for the NCERT site: a browser-like user agent, retries with backoff, atomic writes."""

from __future__ import annotations

import time
import urllib.error
import urllib.request
from pathlib import Path

from .config import USER_AGENT


class NotFound(Exception):
    pass


def fetch(url: str, attempts: int = 4, timeout: float = 60.0) -> bytes:
    delay = 2.0
    for attempt in range(1, attempts + 1):
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            if error.code == 404:
                raise NotFound(url) from error
            if attempt == attempts or error.code < 500:
                raise
        except (urllib.error.URLError, ConnectionError, TimeoutError):
            if attempt == attempts:
                raise
        time.sleep(delay)
        delay *= 2
    raise AssertionError("unreachable")


def download(url: str, target: Path) -> bool:
    """Downloads to ``target`` unless it already holds a PDF. Returns True if it downloaded."""
    if target.exists() and target.stat().st_size > 0:
        return False
    body = fetch(url)
    if not body.startswith(b"%PDF"):
        # NCERT answers some missing files with an HTML page and status 200.
        raise NotFound(url)
    target.parent.mkdir(parents=True, exist_ok=True)
    partial = target.with_suffix(target.suffix + ".part")
    partial.write_bytes(body)
    partial.replace(target)
    return True
