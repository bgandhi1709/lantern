"""Stage 6: copy the build output to the private `ncert` container in Azure Blob Storage.

Uses the Azure CLI with your own sign-in (`az login`); the storage account has shared keys turned
off, so your account needs Storage Blob Data Contributor on it (DATA_UPLOADER_OBJECT_ID in the
Bicep parameters). PDFs never change once downloaded, so existing ones are skipped; the JSON
stages are overwritten, because re-running a stage (a new draft prompt) changes them.
"""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

CONTAINER = "ncert"
# (folder, file pattern, overwrite existing blobs)
FOLDERS = [
    ("pdf", "*.pdf", False),
    ("text", "*.json", True),
    ("chapters", "*.json", True),
    ("drafts", "*.json", True),
]


def commands(data_dir: Path, account: str) -> list[list[str]]:
    common = ["--account-name", account, "--auth-mode", "login", "--only-show-errors"]
    batches = [
        [
            "az", "storage", "blob", "upload-batch",
            "--destination", CONTAINER,
            "--destination-path", folder,
            "--source", str(data_dir / folder),
            "--pattern", f"*/{pattern}",
            "--overwrite", str(overwrite).lower(),
            *common,
        ]
        for folder, pattern, overwrite in FOLDERS
        if (data_dir / folder).exists()
    ]
    catalog = [
        "az", "storage", "blob", "upload",
        "--container-name", CONTAINER,
        "--name", "catalog.json",
        "--file", str(data_dir / "catalog.json"),
        "--overwrite", "true",
        *common,
    ]
    return batches + ([catalog] if (data_dir / "catalog.json").exists() else [])


def run(data_dir: Path, account: str) -> None:
    if shutil.which("az") is None:
        raise SystemExit("The Azure CLI (az) isn't installed; it's needed for upload.")
    for command in commands(data_dir, account):
        print("  " + " ".join(command[3:7]))
        subprocess.run(command, check=True)
