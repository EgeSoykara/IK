#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
e2e_root="$repo_root/.bet-task/e2e"
evidence_root="$repo_root/.bet-task/evidence"
dotnet_host="${DOTNET_HOST_PATH:-$(command -v dotnet || true)}"
fixture_dll="$e2e_root/FixtureTool/bin/Debug/net10.0/FixtureTool.dll"
e2e_app_root="$repo_root/obj/e2e-personnel-controls"
e2e_app_dll="$e2e_app_root/IK.Web.dll"
base_url="${IK_E2E_BASE_URL:-http://127.0.0.1:5107}"
connection_string="${IK_E2E_CONNECTION_STRING:-}"
server_pid=""
fixture_ready="false"
server_ready="false"

cleanup() {
  if [[ -n "$server_pid" ]] && kill -0 "$server_pid" 2>/dev/null; then
    kill "$server_pid"
    wait "$server_pid" 2>/dev/null || true
  fi
  if [[ "$fixture_ready" == "true" ]]; then
    IK_E2E_CONNECTION_STRING="$connection_string" \
      "$dotnet_host" "$fixture_dll" teardown
  fi
}
trap cleanup EXIT

mkdir -p "$evidence_root"

if [[ -z "$dotnet_host" ]] || [[ ! -x "$dotnet_host" ]]; then
  printf '%s\n' "dotnet bulunamadı; DOTNET_HOST_PATH veya PATH ayarlayın." >&2
  exit 2
fi

if [[ -z "$connection_string" ]]; then
  printf '%s\n' "IK_E2E_CONNECTION_STRING zorunludur ve veritabanı adı IK_E2E_ ile başlamalıdır." >&2
  exit 2
fi

if node -e '
  const net = require("node:net");
  const target = new URL(process.argv[1]);
  const socket = net.createConnection({
    host: target.hostname,
    port: Number(target.port || (target.protocol === "https:" ? 443 : 80))
  });
  socket.once("connect", () => {
    socket.destroy();
    process.exit(0);
  });
  socket.once("error", () => process.exit(1));
  socket.setTimeout(1000, () => {
    socket.destroy();
    process.exit(1);
  });
' "$base_url"; then
  printf '%s\n' "IK_E2E_BASE_URL zaten kullanımda; mevcut bir uygulamaya bağlanmayı reddediyorum." >&2
  exit 2
fi

if [[ ! -d "$e2e_root/node_modules/playwright" ]]; then
  npm ci --prefix "$e2e_root"
fi

if ! "$e2e_root/node_modules/.bin/playwright" install chromium; then
  printf '%s\n' "Playwright Chromium kurulamadı; E2E ortam bağımlılığı eksik." >&2
  exit 2
fi

"$dotnet_host" build "$e2e_root/FixtureTool/FixtureTool.csproj" -c Debug --no-restore \
  --disable-build-servers

IK_E2E_CONNECTION_STRING="$connection_string" \
  "$dotnet_host" "$fixture_dll" validate
fixture_ready="true"
IK_E2E_CONNECTION_STRING="$connection_string" \
  "$dotnet_host" "$fixture_dll" setup

"$dotnet_host" build "$repo_root/IK.Web.csproj" -c Debug --no-restore \
  --disable-build-servers -p:DefineConstants=IK_E2E_PERSONNEL_OPTIONS \
  -o "$e2e_app_root"

ASPNETCORE_ENVIRONMENT="Development" \
ASPNETCORE_URLS="$base_url" \
ConnectionStrings__HumanResources="$connection_string" \
  "$dotnet_host" "$e2e_app_dll" \
  >"$evidence_root/employee-information-e2e-server.log" 2>&1 &
server_pid="$!"

for _ in {1..30}; do
  if ! kill -0 "$server_pid" 2>/dev/null; then
    wait "$server_pid" || true
    printf '%s\n' "E2E sunucusu readiness tamamlanmadan sonlandı." >&2
    exit 2
  fi
  if curl --fail --silent --show-error "$base_url/login" >/dev/null; then
    if ! kill -0 "$server_pid" 2>/dev/null; then
      wait "$server_pid" || true
      printf '%s\n' "E2E sunucusu readiness sırasında sonlandı." >&2
      exit 2
    fi
    server_ready="true"
    break
  fi
  sleep 1
done

if [[ "$server_ready" != "true" ]] || ! kill -0 "$server_pid" 2>/dev/null; then
  printf '%s\n' "Başlatılan E2E sunucusu belirtilen sürede hazır olmadı." >&2
  exit 2
fi

cd "$repo_root"
IK_E2E_BASE_URL="$base_url" node "$e2e_root/employee-information-e2e.mjs"
