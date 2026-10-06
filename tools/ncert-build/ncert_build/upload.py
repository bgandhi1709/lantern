"""Stage 6: copy the build output to the Board's private blob container (`ncert`, `ssc`).

Uses the Azure CLI with your own sign-in (`az login`); the storage account has shared keys turned
off, so your account needs Storage Blob Data Contributor on it (DATA_UPLOADER_OBJECT_ID in the
Bicep parameters). Every stage is overwritten, because re-running one (a new prompt) changes it.
The publisher's PDFs stay local: they can be downloaded again, and nothing reads them from Azure.
"""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

# The container names in infra/params/*.bicepparam; --container overrides.
CONTAINERS = {"cbse": "ncert", "ssc": "ssc"}
# (folder, file pattern relative to it)
FOLDERS = [
    ("text", "*/*.json"),
    ("chapters", "*/*.json"),
    ("drafts", "*/*.json"),
    ("pictures", "*/*"),  # <chapter>.json and <chapter>/pN-i.png
    ("bundles", "*/*.md"),
    ("refined", "*/*.json"),
    ("summaries", "*/*.json"),
    ("toc", "*.json"),
    ("guidance", "*.json"),
]
FILES = ["catalog.json", "board.json"]


def commands(data_dir: Path, account: str, container: str) -> list[list[str]]:
    common = ["--account-name", account, "--auth-mode", "login", "--only-show-errors", "--output", "none"]
    batches = [
        [
            "az", "storage", "blob", "upload-batch",
            "--destination", container,
            "--destination-path", folder,
            "--source", str(data_dir / folder),
            "--pattern", pattern,
            "--overwrite", "true",
            *common,
        ]
        for folder, pattern in FOLDERS
        if (data_dir / folder).exists()
    ]
    files = [
        [
            "az", "storage", "blob", "upload",
            "--container-name", container,
            "--name", name,
            "--file", str(data_dir / name),
            "--overwrite", "true",
            *common,
        ]
        for name in FILES
        if (data_dir / name).exists()
    ]
    return batches + files


def run(data_dir: Path, account: str, container: str) -> None:
    if shutil.which("az") is None:
        raise SystemExit("The Azure CLI (az) isn't installed; it's needed for upload.")
    for command in commands(data_dir, account, container):
        print("  " + " ".join(command[3:8]))
        subprocess.run(command, check=True)
