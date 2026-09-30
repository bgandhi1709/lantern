"""A minimal client for the Anthropic API.

Reusable beyond this one fix-up script: the same ``complete()`` call is the primitive for
refining a new state board's chapters, or for a runtime "answer this question with this
context" call before it reaches a child - see docs/ideation/decision-log.md, API cost pilot.
Keep task-specific prompting in the caller; this module only knows how to talk to the API and
report what a call actually cost, so an admin-side HTTP layer can wrap it later without change.

Reads ANTHROPIC_API_KEY from the environment - source a key file before running, this module
never reads one itself.
"""

from __future__ import annotations

import base64
import json
import os
import urllib.error
import urllib.request
from dataclasses import dataclass

API_URL = "https://api.anthropic.com/v1/messages"
API_VERSION = "2023-06-01"

# $ per million tokens, (input, output). Update if Anthropic's pricing changes.
PRICES = {
    "claude-haiku-4-5": (1.0, 5.0),
    "claude-sonnet-5-5": (2.0, 10.0),
    "claude-opus-5-5": (4.0, 20.0),
}


@dataclass(frozen=True)
class Usage:
    input_tokens: int
    output_tokens: int

    def cost_usd(self, model: str) -> float:
        price_in, price_out = PRICES.get(model, (0.0, 0.0))
        return (self.input_tokens * price_in + self.output_tokens * price_out) / 1_000_000


class Claude:
    def __init__(self, api_key: str | None = None):
        self.api_key = api_key or os.environ.get("ANTHROPIC_API_KEY")
        if not self.api_key:
            raise SystemExit("ANTHROPIC_API_KEY not set - source your key file first.")

    def complete(
        self,
        model: str,
        user: str,
        system: str | None = None,
        max_tokens: int = 256,
        images: list[bytes] | None = None,
    ) -> tuple[str, Usage]:
        """One non-streaming turn. Returns (reply_text, usage). Raises on error - callers decide
        whether a failed call is worth retrying; this method never retries.

        ``images`` (PNG bytes) go in before the text block, so the text can refer to "the image
        above" - used for the maths/vision correction pass (page render + the flattened text
        extracted from it, asking for a corrected transcription).

        Sonnet 5.5 and Opus 5.5 run adaptive thinking by default and can't fully disable it;
        Sonnet 5.5 accepts ``{"type": "between_tools"}`` to turn off visible reasoning, which
        matters here because thinking tokens count against max_tokens - a small max_tokens on a
        thinking-on model can consume the whole budget on invisible reasoning and return empty
        text (see docs/ideation/decision-log.md, API cost pilot). Haiku 4.5 has no thinking to
        disable, so this is a no-op there."""
        content: list[dict] = [
            {"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": base64.b64encode(img).decode()}}
            for img in (images or [])
        ]
        content.append({"type": "text", "text": user})
        body: dict = {"model": model, "max_tokens": max_tokens, "messages": [{"role": "user", "content": content}]}
        if system:
            body["system"] = system
        if model == "claude-sonnet-5-5":
            body["thinking"] = {"type": "between_tools"}
        request = urllib.request.Request(
            API_URL,
            data=json.dumps(body).encode(),
            headers={
                "x-api-key": self.api_key,
                "anthropic-version": API_VERSION,
                "content-type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                reply = json.loads(response.read())
        except urllib.error.HTTPError as error:
            raise RuntimeError(json.loads(error.read())["error"]["message"]) from error
        text = "".join(block["text"] for block in reply["content"] if block["type"] == "text")
        usage = Usage(input_tokens=reply["usage"]["input_tokens"], output_tokens=reply["usage"]["output_tokens"])
        return text.strip(), usage
