#!/usr/bin/env bash
# Buat repo GitHub baru (private) + push seluruh proyek DeviceMonitor.
# Pemakaian:  GITHUB_TOKEN=ghp_xxxx ./push-to-github.sh   atau   ./push-to-github.sh ghp_xxxx
set -euo pipefail

TOKEN="${1:-${GITHUB_TOKEN:-}}"
REPO_NAME="${REPO_NAME:-DeviceMonitor}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [[ -z "$TOKEN" ]]; then
  read -r -s -p "Personal Access Token GitHub: " TOKEN; echo
fi

api() { # method path [json-body]
  python3 - "$1" "$2" "${3:-}" "$TOKEN" <<'PY'
import json, sys, urllib.request
method, path, body, token = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
req = urllib.request.Request(f"https://api.github.com{path}", method=method)
req.add_header("Authorization", f"Bearer {token}")
req.add_header("Accept", "application/vnd.github+json")
req.add_header("User-Agent", "devicemonitor-push")
data = body.encode() if body else None
try:
    with urllib.request.urlopen(req, data) as r:
        print(json.dumps(json.load(r)))
except urllib.error.HTTPError as e:
    if e.code == 404:
        print("null")
    else:
        print(e.read().decode(), file=sys.stderr); sys.exit(1)
PY
}

echo "Mengambil username GitHub..."
LOGIN=$(api GET /user | python3 -c 'import json,sys; print(json.load(sys.stdin)["login"])')
echo "  user: $LOGIN"

REPO=$(api GET "/repos/$LOGIN/$REPO_NAME")
if [[ "$REPO" == "null" ]]; then
  echo "Membuat repo $LOGIN/$REPO_NAME (private)..."
  api POST /user/repos "{\"name\":\"$REPO_NAME\",\"private\":true,\"auto_init\":false,\"description\":\"Dashboard monitoring perangkat kantor - ASP.NET Core 8 + SQL Server\"}" > /dev/null
else
  echo "Repo $LOGIN/$REPO_NAME sudah ada, push ke repo tersebut."
fi

cd "$ROOT"
git remote remove origin 2>/dev/null || true
git remote add origin "https://x-access-token:$TOKEN@github.com/$LOGIN/$REPO_NAME.git"
git push -u origin main
git remote set-url origin "https://github.com/$LOGIN/$REPO_NAME.git"

cat <<EOF

SELESAI  ->  https://github.com/$LOGIN/$REPO_NAME
Remote sudah dibersihkan dari token. Revoke token bila perlu:
GitHub > Settings > Developer settings > Personal access tokens.
EOF
