#!/usr/bin/env bash

set -Eeuo pipefail

readonly repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly deployment_script="$repository_root/scripts/deploy-oci-container.sh"
readonly image="ghcr.io/example/mandarinbot:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"

if [[ "$(uname -s)" != "Linux" ]]; then
  printf 'SKIP: deploy-oci-container permission tests require Linux permissions\n'
  exit 0
fi

readonly test_root="$(mktemp -d)"

cleanup() {
  chmod -R u+w "$test_root"
  rm -rf "$test_root"
}

trap cleanup EXIT

fail() {
  printf 'FAIL: %s\n' "$1" >&2
  exit 1
}

create_docker_mock() {
  local mock_directory="$1"

  cat >"$mock_directory/docker" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

printf '%s\n' "$*" >>"$TEST_DOCKER_LOG"

if [[ "$1" == "info" || "$1" == "pull" ]]; then
  exit 0
fi

if [[ "$1" == "run" && "$*" == *"--entrypoint chown"* ]]; then
  exit 0
fi

if [[ "$1" == "run" && "$*" == *"--entrypoint chmod"* ]]; then
  chmod 0770 "$TEST_DATA_DIRECTORY"
  exit 0
fi

if [[ "$1" == "run" ]]; then
  : >"$TEST_CONTAINER_STATE"
  exit 0
fi

if [[ "$1" == "container" && "$2" == "inspect" ]]; then
  [[ "${*: -1}" == "mandarinbot" && -f "$TEST_CONTAINER_STATE" ]] || exit 1
  if [[ "$*" == *".State.Running"* ]]; then
    printf 'true\n'
  elif [[ "$*" == *".Image"* ]]; then
    printf '%s\n' "$TEST_IMAGE"
  fi
  exit 0
fi

if [[ "$1" == "exec" ]]; then
  exit 0
fi

printf 'Unexpected docker invocation: %s\n' "$*" >&2
exit 1
EOF
  chmod +x "$mock_directory/docker"
}

create_stat_mock() {
  local mock_directory="$1"

  cat >"$mock_directory/stat" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "$1" == "-c" && "$2" == "%G" ]]; then
  printf '%s\n' "$TEST_STAT_GROUP_NAME"
  exit 0
fi

if [[ "$1" == "-c" && "$2" == "%g" ]]; then
  printf '%s\n' "$TEST_STAT_GROUP_ID"
  exit 0
fi

exec /usr/bin/stat "$@"
EOF
  chmod +x "$mock_directory/stat"
}

run_deployment() {
  local test_directory="$1"
  local stat_group_name="$2"
  local output_file="$3"

  local deploy_directory="$test_directory/deploy"
  local data_directory="$deploy_directory/data"
  local mock_directory="$test_directory/bin"
  mkdir -p "$data_directory" "$mock_directory"
  printf 'Bot__Discord__Token=test-token\n' >"$deploy_directory/mandarinbot.env"
  printf '{}\n' >"$data_directory/health-state.json"
  chmod 0550 "$data_directory"

  create_docker_mock "$mock_directory"
  create_stat_mock "$mock_directory"

  TEST_DATA_DIRECTORY="$data_directory" \
    TEST_DOCKER_LOG="$test_directory/docker.log" \
    TEST_CONTAINER_STATE="$test_directory/container.state" \
    TEST_IMAGE="$image" \
    TEST_STAT_GROUP_NAME="$stat_group_name" \
    TEST_STAT_GROUP_ID="999" \
    PATH="$mock_directory:$PATH" \
    IMAGE="$image" \
    CONTAINER_NAME="mandarinbot" \
    DEPLOY_DIRECTORY="$deploy_directory" \
    STARTUP_TIMEOUT_SECONDS="10" \
    READINESS_STABILIZATION_SECONDS="5" \
    bash "$deployment_script" >"$output_file" 2>&1
}

test_repairs_docker_group_permissions_before_deployment() {
  local test_directory="$test_root/repair"
  mkdir -p "$test_directory"

  if ! run_deployment "$test_directory" "docker" "$test_directory/output.log"; then
    cat "$test_directory/output.log" >&2
    fail "deployment did not recover an unwritable docker-group data directory"
  fi

  if [[ "$(uname -s)" == "Linux" ]]; then
    [[ "$(stat -c '%a' "$test_directory/deploy/data")" == "770" ]] ||
      fail "data directory mode was not repaired to 0770"
  fi
  [[ ! -e "$test_directory/deploy/data/health-state.json" ]] ||
    fail "stale health state was not removed"
  grep -Fq -- '--user 0:0 --entrypoint chown' "$test_directory/docker.log" ||
    fail "permission repair did not use a root-only helper container"
  grep -Fq -- '--recursive app:999 /app/data' "$test_directory/docker.log" ||
    fail "permission repair did not assign the mounted data to the app user"
  grep -Fq -- '--entrypoint chmod' "$test_directory/docker.log" ||
    fail "permission repair container was not run"
  grep -Eq -- '--user app:[0-9]+' "$test_directory/docker.log" ||
    fail "application container did not run as the dedicated non-root user"
  for required_option in \
    '--read-only' \
    '--tmpfs /tmp:rw,noexec,nosuid,size=16m' \
    '--security-opt no-new-privileges:true' \
    '--cap-drop ALL' \
    '--memory 512m' \
    '--memory-reservation 256m' \
    '--cpus 0.75' \
    '--pids-limit 128'; do
    grep -Fq -- "$required_option" "$test_directory/docker.log" ||
      fail "application container is missing runtime option: $required_option"
  done
  grep -Fq 'Deployment succeeded' "$test_directory/output.log" ||
    fail "deployment did not report success"
}

test_rejects_unexpected_data_directory_group() {
  local test_directory="$test_root/unexpected-group"
  mkdir -p "$test_directory"

  if run_deployment "$test_directory" "unexpected-group" "$test_directory/output.log"; then
    fail "deployment accepted an unwritable directory outside the docker group"
  fi

  grep -Fq 'must be owned by the docker group' \
    "$test_directory/output.log" ||
    fail "unexpected group failure was not explained"
  if grep -Fq -- '--entrypoint chown' "$test_directory/docker.log"; then
    fail "permission repair was attempted for an unexpected group"
  fi
}

test_repairs_docker_group_permissions_before_deployment
test_rejects_unexpected_data_directory_group
printf 'PASS: deploy-oci-container permission tests\n'
