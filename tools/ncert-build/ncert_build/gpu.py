"""Keeps a days-long GPU run at a safe temperature, and inside its hours.

The run only uses the GPU inside LANTERN_RUN_WINDOW (default 20:00-08:00 India time): outside it,
the next call waits until the window opens again, so the PC is free during the day.

Before each model call the GPU's temperature is read with nvidia-smi (WSL sees the Windows
driver). Above PAUSE_AT the run waits until the card has cooled to RESUME_AT. The RTX 3060 starts
throttling itself around 83 °C, so pausing at 84 °C leaves it only its own safety margin to use,
never the edge. A short rest between calls lowers the average load a little more.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import time
from datetime import datetime, time as clock, timedelta
from zoneinfo import ZoneInfo

PAUSE_AT = int(os.environ.get("LANTERN_GPU_PAUSE_AT", "84"))
RESUME_AT = int(os.environ.get("LANTERN_GPU_RESUME_AT", "76"))
REST_SECONDS = float(os.environ.get("LANTERN_GPU_REST", "1.0"))
CHECK_EVERY = 20.0
RUN_WINDOW = os.environ.get("LANTERN_RUN_WINDOW", "20:00-08:00")  # "" runs at any hour
ZONE = ZoneInfo(os.environ.get("LANTERN_RUN_ZONE", "Asia/Kolkata"))
# A one-off pass to use the GPU outside the window until then, e.g. "2026-09-25T20:00" (India time)
# on a day nobody needs the PC.
FREE_UNTIL = os.environ.get("LANTERN_FREE_UNTIL", "")


def _window(text: str) -> tuple[clock, clock] | None:
    if not text:
        return None
    start, end = (clock.fromisoformat(part.strip()) for part in text.split("-"))
    return start, end


def seconds_until_open(now: datetime, window: tuple[clock, clock] | None) -> float:
    """0 inside the window, else how long until it opens. A window may run past midnight."""
    if window is None:
        return 0.0
    start, end = window
    moment = now.time()
    inside = start <= moment < end if start < end else (moment >= start or moment < end)
    if inside:
        return 0.0
    opens = now.replace(hour=start.hour, minute=start.minute, second=0, microsecond=0)
    if opens <= now:
        opens += timedelta(days=1)
    return (opens - now).total_seconds()


def temperature() -> int | None:
    tool = shutil.which("nvidia-smi") or "/usr/lib/wsl/lib/nvidia-smi"
    try:
        out = subprocess.run(
            [tool, "--query-gpu=temperature.gpu", "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=10,
        ).stdout
        return int(out.split()[0])
    except (OSError, ValueError, IndexError, subprocess.TimeoutExpired):
        return None


class Guard:
    def __init__(self, log=print):
        self.log = log
        self.hottest = 0
        self.paused_seconds = 0.0
        self.calls = 0

    def wait_for_window(self) -> None:
        now = datetime.now(ZONE)
        if FREE_UNTIL and now < datetime.fromisoformat(FREE_UNTIL).replace(tzinfo=ZONE):
            return
        wait = seconds_until_open(now, _window(RUN_WINDOW))
        if wait > 0:
            self.log(f"  outside the run window {RUN_WINDOW} ({ZONE.key}): waiting {round(wait / 3600, 1)} h")
            time.sleep(wait)
            self.log("  run window open: carrying on")

    def before_call(self) -> None:
        self.wait_for_window()
        self.calls += 1
        if REST_SECONDS:
            time.sleep(REST_SECONDS)
        now = temperature()
        if now is None:
            return
        self.hottest = max(self.hottest, now)
        if now < PAUSE_AT:
            return
        self.log(f"  GPU at {now} °C: pausing until {RESUME_AT} °C")
        started = time.monotonic()
        while now is not None and now > RESUME_AT:
            time.sleep(CHECK_EVERY)
            now = temperature()
        self.paused_seconds += time.monotonic() - started
        self.log(f"  GPU at {now} °C after {round(time.monotonic() - started)} s: resuming")

    def summary(self) -> str:
        return f"GPU: {self.calls} calls, hottest {self.hottest} °C, paused {round(self.paused_seconds / 60)} min"
