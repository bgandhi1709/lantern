#!/usr/bin/env bash
# The checks a commit must pass, so a build that fails in CI fails here first. Run by .husky/pre-commit.
# Skips what the commit does not touch. Bypass in an emergency with `git commit --no-verify`.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

# Git runs hooks without the login shell, so find the tools the way the dev environment does.
[ -s "$HOME/.config/nvm/nvm.sh" ] && . "$HOME/.config/nvm/nvm.sh" >/dev/null
[ -d "$HOME/.dotnet" ] && export PATH="$HOME/.dotnet:$PATH"

staged=$(git diff --cached --name-only --diff-filter=ACMR)

if grep -qE '\.(cs|csproj|props|slnx|editorconfig)$|^NuGet.config$|^global.json$' <<<"$staged"; then
  echo "pre-commit: .NET changes. Build (warnings are errors, no unused usings), format, unit tests"
  command -v azurite >/dev/null || { echo "azurite is not on PATH: npm install -g azurite@3 (the storage tests need it)" >&2; exit 1; }
  dotnet build Lantern.slnx --nologo -v q
  dotnet format Lantern.slnx --verify-no-changes --no-restore -v q || {
    echo "Formatting differs. Fix it with: dotnet format Lantern.slnx" >&2
    exit 1
  }
  # The E2E tests need the Docker stack and run in CI's e2e-docker job. Exit code 8 is an assembly with no tests left.
  dotnet test --solution Lantern.slnx --no-build --filter-not-trait "Category=E2E" --ignore-exit-code 8
fi

if grep -q '^client/lantern-android/' <<<"$staged"; then
  echo "pre-commit: app changes. Typecheck, lint, format, unit tests"
  cd client/lantern-android
  npm run typecheck --silent
  npm run lint --silent
  npm run format:check --silent
  npm test --silent
fi
