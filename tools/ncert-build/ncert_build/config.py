"""Settings shared by every stage. Everything can be overridden by environment variable."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

NCERT_BASE = "https://ncert.nic.in"
# The NCERT site resets connections from clients without a browser-like user agent.
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LanternNcertBuild/0.1"

# Subjects that carry concepts a child asks about. Arts, physical education and vocational books
# are left out of the default build; pass --subjects to include them.
CORE_SUBJECTS = (
    "English",
    "Mathematics",
    "Environmental Studies",
    "The World Around Us",
    "Science",
    "Social Science",
)


def _default_data_dir() -> Path:
    # A sibling of the repository, never inside it: NCERT text is copyrighted and the repository
    # is public. tools/ncert-build/ncert_build/config.py -> repo root is three levels up.
    repo_root = Path(__file__).resolve().parents[3]
    return repo_root.parent / "lantern-data"


@dataclass(frozen=True)
class Settings:
    data_dir: Path
    ollama_host: str | None
    draft_model: str
    request_delay_seconds: float

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            data_dir=Path(os.environ.get("LANTERN_DATA_DIR") or _default_data_dir()),
            ollama_host=os.environ.get("OLLAMA_HOST") or None,
            draft_model=os.environ.get("LANTERN_DRAFT_MODEL", "qwen3:8b"),
            request_delay_seconds=float(os.environ.get("LANTERN_REQUEST_DELAY", "1.0")),
        )

    def ensure_outside_repo(self) -> None:
        repo_root = Path(__file__).resolve().parents[3]
        data = self.data_dir.resolve()
        if data == repo_root or repo_root in data.parents:
            raise SystemExit(
                f"Data directory {data} is inside the repository. NCERT content must stay out of "
                "the public repo: set LANTERN_DATA_DIR to a folder outside it."
            )
