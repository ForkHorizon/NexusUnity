#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(git -C "$SCRIPT_DIR/.." rev-parse --show-toplevel)

git -C "$REPO_ROOT" config core.hooksPath .githooks
printf 'Nexus Unity git hooks installed: core.hooksPath=.githooks\n'
