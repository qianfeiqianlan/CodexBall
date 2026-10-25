#!/bin/bash
# Local test installer: ad-hoc signing, no Developer ID or notarization claim.
set -euo pipefail

source_dir="$(cd "$(dirname "$0")" && pwd -P)"
source_app="$source_dir/CodexBall.app"
if [ "$(uname -s)" != "Darwin" ]; then
  echo "This installer must run on the target Mac."
  exit 1
fi
if [ ! -f "$source_app/Contents/MacOS/CodexBall.Desktop" ]; then
  echo "CodexBall.app is missing. Extract the entire ZIP beside this installer."
  exit 1
fi
if /usr/bin/pgrep -f '/CodexBall[.]app/Contents/MacOS/CodexBall[.]Desktop([[:space:]]|$)' >/dev/null; then
  echo "请先从菜单栏退出正在运行的 CodexBall，再运行安装脚本。"
  exit 1
fi

parent="/Applications"
if [ ! -w "$parent" ]; then
  parent="$HOME/Applications"
  /bin/mkdir -p "$parent"
fi
target="$parent/CodexBall.app"
staging="$(/usr/bin/mktemp -d "$parent/.CodexBall-install.XXXXXX")"
staged_app="$staging/CodexBall.app"
echo "正在准备并签名：$target"
/usr/bin/ditto "$source_app" "$staged_app"
/bin/chmod u+x "$staged_app/Contents/MacOS/CodexBall.Desktop"
# Only this trusted local build is changed; global security policy is untouched.
/usr/bin/xattr -r -d com.apple.quarantine "$staged_app" 2>/dev/null || true

# Sign every native file inside-out, including libraries outside standard bundle folders.
if ! (
  /usr/bin/find "$staged_app/Contents/MacOS" -type f -print0 |
  while IFS= read -r -d '' native_file; do
    if /usr/bin/file -b "$native_file" | /usr/bin/grep -q 'Mach-O'; then
      /usr/bin/codesign --force --sign - "$native_file" || exit 1
    fi
  done || exit 1
  /usr/bin/codesign --force --sign - "$staged_app" || exit 1
  /usr/bin/codesign --verify --deep --strict --verbose=2 "$staged_app" || exit 1
) >"$staging/signing.log" 2>&1; then
  echo "签名失败，旧版本未被替换。诊断日志：$staging/signing.log"
  /usr/bin/tail -n 25 "$staging/signing.log"
  exit 1
fi

backup=""
if [ -e "$target" ]; then
  backup="$target.backup-$(date +%Y%m%d-%H%M%S)-$$"
  /bin/mv "$target" "$backup"
fi
if ! /bin/mv "$staged_app" "$target"; then
  if [ -n "$backup" ]; then /bin/mv "$backup" "$target"; fi
  echo "安装失败，已恢复原版本。"
  exit 1
fi
echo "已安装：$target"
if [ -n "$backup" ]; then echo "旧版备份：$backup"; fi
echo "设置与角色选择保留。此程序在菜单栏运行，不显示 Dock 图标。"
/usr/bin/open "$target"
