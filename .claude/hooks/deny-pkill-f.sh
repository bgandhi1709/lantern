#!/usr/bin/env python3
# Rejects a Bash command that runs pkill -f or killall: the pattern also matches the shell running the command, which dies with it.
# Heredoc bodies and quoted strings are text, not commands, so they are ignored.
import json
import re
import sys

command = json.load(sys.stdin).get("tool_input", {}).get("command", "")

lines, delimiter = [], None
for line in command.splitlines():
    if delimiter:
        if line.strip() == delimiter:
            delimiter = None
        continue
    lines.append(line)
    found = re.search(r"<<-?\s*[\"']?([A-Za-z_][A-Za-z_0-9]*)[\"']?", line)
    delimiter = found.group(1) if found else None

code = re.sub(r"\"[^\"]*\"|'[^']*'", '""', "\n".join(lines))

if re.search(r"(^|[;&|(`\s])(pkill\b[^|;&\n]*\s-[A-Za-z]*f|killall)(\s|$)", code):
    print("Stop a process by PID: kill $(pgrep -f '[n]ame'). The brackets keep the pattern out of your own command line. Metro: scripts/verify-env.sh metro.", file=sys.stderr)
    sys.exit(2)
