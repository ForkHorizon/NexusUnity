#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(git -C "$SCRIPT_DIR/.." rev-parse --show-toplevel)

# CI Scope installs one global core.hooksPath. A repo-local core.hooksPath replaces it, so the CI Scope
# check would never run on commit here. With a global CI Scope hook, drop the local override and put the
# tracked hooks into .git/hooks, which the CI Scope hook runs after its own check.
global_path=$(git config --global --get core.hooksPath 2>/dev/null || true)
case "$global_path" in
  *ci-scope/hooks*)
    git -C "$REPO_ROOT" config --local --unset core.hooksPath 2>/dev/null || true
    hooks_dir=$(git -C "$REPO_ROOT" rev-parse --path-format=absolute --git-common-dir)/hooks
    mkdir -p "$hooks_dir"
    for hook in "$REPO_ROOT"/.githooks/*; do
      ln -sf "$hook" "$hooks_dir/$(basename "$hook")"
    done
    printf 'Nexus Unity git hooks installed in %s (CI Scope hook runs first)\n' "$hooks_dir"
    ;;
  *)
    git -C "$REPO_ROOT" config core.hooksPath .githooks
    printf 'Nexus Unity git hooks installed: core.hooksPath=.githooks\n'
    ;;
esac
