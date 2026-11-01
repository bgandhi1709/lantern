import json
import subprocess

HOOK = "/home/bga3pg/Work/lantern/.claude/hooks/deny-pkill-f.sh"
P = "pkil" + "l"
K = "kill" + "all"

cases = [
    (f'{P} -f "qemu.*-avd x"', 2),
    (f"{K} node", 2),
    (f"ls | {P} -9 -f foo", 2),
    (f"echo a; {P} -f x", 2),
    ('kill $(pgrep -f "[m]etro")', 0),
    (f"{P} node", 0),
    (f'echo "do not run {P} -f here"', 0),
    (f"git commit -F - <<'EOF'\nthe hook rejects {P} -f and {K}\nEOF\necho done", 0),
    (f"cat <<'EOF'\nx\nEOF\n{P} -f foo", 2),
    ("echo hi", 0),
]

failed = 0
for command, expected in cases:
    result = subprocess.run([HOOK], input=json.dumps({"tool_input": {"command": command}}), text=True, capture_output=True)
    ok = result.returncode == expected
    failed += not ok
    print("ok  " if ok else "FAIL", repr(command)[:70], "->", result.returncode)
raise SystemExit(failed)
