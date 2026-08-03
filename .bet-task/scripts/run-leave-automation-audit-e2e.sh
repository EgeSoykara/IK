#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
e2e_root="$repo_root/.bet-task/e2e"
evidence_root="$repo_root/.bet-task/evidence"
preferred_dotnet="/Users/egesoykara/usr/local/share/dotnet/dotnet"
if [[ -n "${DOTNET_HOST_PATH:-}" ]]; then
  dotnet_host="$DOTNET_HOST_PATH"
elif [[ -x "$preferred_dotnet" ]]; then
  dotnet_host="$preferred_dotnet"
else
  dotnet_host="$(command -v dotnet || true)"
fi
fixture_dll="$e2e_root/FixtureTool/bin/Debug/net10.0/FixtureTool.dll"
e2e_app_root="$repo_root/obj/e2e-leave-automation"
e2e_app_dll="$e2e_app_root/IK.Web.dll"
base_url="${IK_E2E_BASE_URL:-http://127.0.0.1:5117}"
connection_string="${IK_E2E_CONNECTION_STRING:-}"
server_pid=""
fixture_ready="false"

cleanup() {
  stop_server
  if [[ "$fixture_ready" == "true" ]]; then
    IK_E2E_CONNECTION_STRING="$connection_string" \
      "$dotnet_host" "$fixture_dll" teardown
  fi
}

stop_server() {
  if [[ -n "$server_pid" ]] && kill -0 "$server_pid" 2>/dev/null; then
    kill "$server_pid"
    wait "$server_pid" 2>/dev/null || true
  fi
  server_pid=""
}

start_server() {
  ASPNETCORE_ENVIRONMENT="Development" \
  ASPNETCORE_URLS="$base_url" \
  ConnectionStrings__HumanResources="$connection_string" \
    "$dotnet_host" "$e2e_app_dll" \
    >"$evidence_root/leave-automation-audit-e2e-server.log" 2>&1 &
  server_pid="$!"

  for _ in {1..30}; do
    if ! kill -0 "$server_pid" 2>/dev/null; then
      wait "$server_pid" || true
      printf '%s\n' "E2E sunucusu readiness tamamlanmadan sonlandı." >&2
      exit 2
    fi
    set +e
    curl --fail --silent "$base_url/login" >/dev/null 2>&1
    readiness_status="$?"
    set -e
    if [[ "$readiness_status" -eq 0 ]]; then
      return
    fi
    sleep 1
  done

  printf '%s\n' "E2E sunucusu belirtilen sürede hazır olmadı." >&2
  exit 2
}

trap cleanup EXIT
mkdir -p "$evidence_root"

if [[ -z "$dotnet_host" ]] || [[ ! -x "$dotnet_host" ]]; then
  printf '%s\n' "dotnet bulunamadı; DOTNET_HOST_PATH veya PATH ayarlayın." >&2
  exit 2
fi

if [[ -z "$connection_string" ]]; then
  connection_string="$(node -e '
    const fs = require("node:fs");
    const path = process.argv[1];
    const data = JSON.parse(fs.readFileSync(path, "utf8"));
    const source = data?.ConnectionStrings?.HumanResources;
    if (!source) process.exit(2);
    const database = `IK_E2E_LEAVE_${process.pid}`;
    const replaced = /(?:Database|Initial Catalog)=[^;]*/i.test(source)
      ? source.replace(/(?:Database|Initial Catalog)=[^;]*/i, `Database=${database}`)
      : `${source};Database=${database}`;
    process.stdout.write(replaced);
  ' "$repo_root/appsettings.json")"
fi

if node -e '
  const net = require("node:net");
  const target = new URL(process.argv[1]);
  const socket = net.createConnection({
    host: target.hostname,
    port: Number(target.port || 80)
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
  printf '%s\n' "IK_E2E_BASE_URL zaten kullanımda." >&2
  exit 2
fi

if [[ ! -d "$e2e_root/node_modules/playwright" ]]; then
  npm ci --prefix "$e2e_root"
fi

"$dotnet_host" build "$e2e_root/FixtureTool/FixtureTool.csproj" -c Debug \
  --disable-build-servers
IK_E2E_CONNECTION_STRING="$connection_string" \
  "$dotnet_host" "$fixture_dll" validate
fixture_ready="true"
IK_E2E_CONNECTION_STRING="$connection_string" \
  "$dotnet_host" "$fixture_dll" validate-balance-backed-cutover
IK_E2E_CONNECTION_STRING="$connection_string" \
  "$dotnet_host" "$fixture_dll" setup

"$dotnet_host" build "$repo_root/IK.Web.csproj" -c Debug --no-restore \
  --disable-build-servers -p:DefineConstants=IK_E2E_PERSONNEL_OPTIONS \
  -o "$e2e_app_root"

start_server
cd "$repo_root"
IK_E2E_BASE_URL="$base_url" \
  node "$e2e_root/leave-automation-audit-e2e.mjs"

stop_server
start_server
IK_E2E_BASE_URL="$base_url" \
IK_E2E_PHASE="restart" \
  node "$e2e_root/leave-automation-audit-e2e.mjs"
