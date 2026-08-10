#!/usr/bin/env bash

set -Eeuo pipefail

readonly env_file="${DEPLOY_DIRECTORY:-}/mandarinbot.env"
readonly data_directory="${DEPLOY_DIRECTORY:-}/data"
readonly health_state_file="$data_directory/health-state.json"
readonly rollback_name="${CONTAINER_NAME:-}-rollback"

fail() {
  printf '::error::%s\n' "$1" >&2
  exit 1
}

container_exists() {
  docker container inspect "$1" >/dev/null 2>&1
}

rollback() {
  local reason="$1"
  set +e

  printf '::error::Deployment failed: %s\n' "$reason" >&2
  if container_exists "$CONTAINER_NAME"; then
    docker logs --tail 100 "$CONTAINER_NAME" >&2
    if ! docker rm --force "$CONTAINER_NAME" >/dev/null; then
      printf '::error::Could not remove the failed container; rollback was not attempted.\n' >&2
      exit 1
    fi
  fi

  if container_exists "$rollback_name"; then
    if docker rename "$rollback_name" "$CONTAINER_NAME" &&
      docker start "$CONTAINER_NAME" >/dev/null; then
      printf 'Previous container restored.\n'
    else
      printf '::error::The previous container could not be restored.\n' >&2
    fi
  else
    printf '::error::No previous container was available to restore.\n' >&2
  fi

  exit 1
}

[[ -n "${IMAGE:-}" ]] || fail "IMAGE is required."
[[ "$IMAGE" =~ ^ghcr\.io/[a-z0-9._/-]+:[0-9a-f]{40}$ ]] ||
  fail "IMAGE must be an immutable GHCR image tagged with a full commit SHA."
[[ "${CONTAINER_NAME:-}" =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$ ]] ||
  fail "CONTAINER_NAME is invalid."
[[ "${DEPLOY_DIRECTORY:-}" == /* ]] || fail "DEPLOY_DIRECTORY must be absolute."
[[ "${STARTUP_TIMEOUT_SECONDS:-}" =~ ^[0-9]+$ ]] ||
  fail "STARTUP_TIMEOUT_SECONDS must be an integer."
(( STARTUP_TIMEOUT_SECONDS >= 10 && STARTUP_TIMEOUT_SECONDS <= 300 )) ||
  fail "STARTUP_TIMEOUT_SECONDS must be between 10 and 300."
[[ "${READINESS_STABILIZATION_SECONDS:-}" =~ ^[0-9]+$ ]] ||
  fail "READINESS_STABILIZATION_SECONDS must be an integer."
(( READINESS_STABILIZATION_SECONDS >= 5 && READINESS_STABILIZATION_SECONDS <= 60 )) ||
  fail "READINESS_STABILIZATION_SECONDS must be between 5 and 60."
(( READINESS_STABILIZATION_SECONDS < STARTUP_TIMEOUT_SECONDS )) ||
  fail "READINESS_STABILIZATION_SECONDS must be less than STARTUP_TIMEOUT_SECONDS."

command -v docker >/dev/null 2>&1 || fail "Docker is not installed."
docker info >/dev/null 2>&1 || fail "The runner cannot access the Docker daemon."
[[ -r "$env_file" ]] || fail "$env_file is missing or unreadable."
[[ -d "$data_directory" ]] || fail "$data_directory is missing."
grep -Eq '^[[:space:]]*Bot__Discord__Token=.+$' "$env_file" ||
  fail "$env_file does not contain a non-empty Bot__Discord__Token."

printf 'Pulling %s\n' "$IMAGE"
docker pull "$IMAGE"

if [[ ! -w "$data_directory" ]]; then
  data_group="$(stat -c '%G' "$data_directory")"
  [[ "$data_group" == "docker" ]] ||
    fail "$data_directory is not writable and is not owned by the docker group."

  printf 'Granting the docker group write access to %s\n' "$data_directory"
  if ! docker run \
    --rm \
    --entrypoint chmod \
    --mount "type=bind,src=$data_directory,dst=/app/data" \
    "$IMAGE" \
    0770 /app/data; then
    fail "$data_directory permissions could not be repaired."
  fi

  [[ -w "$data_directory" ]] ||
    fail "$data_directory is still not writable after permission repair."
fi

if container_exists "$rollback_name"; then
  if container_exists "$CONTAINER_NAME"; then
    docker rm --force "$rollback_name" >/dev/null
  else
    fail "$rollback_name exists while $CONTAINER_NAME is missing; recover it before deploying."
  fi
fi

previous_image=""
if container_exists "$CONTAINER_NAME"; then
  previous_image="$(docker container inspect --format '{{.Image}}' "$CONTAINER_NAME")"
  if ! docker stop --time 30 "$CONTAINER_NAME" >/dev/null; then
    fail "The current container could not be stopped."
  fi
  if ! docker rename "$CONTAINER_NAME" "$rollback_name"; then
    docker start "$CONTAINER_NAME" >/dev/null || true
    fail "The current container could not be staged for rollback."
  fi
fi

# A state file from the previous container can still be fresh during the first
# probe of the replacement. Remove it so only the new process can become ready.
rm -f "$health_state_file" || rollback "Stale health state could not be cleared."

if ! docker run \
  --detach \
  --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  --env-file "$env_file" \
  --mount "type=bind,src=$data_directory,dst=/app/data" \
  "$IMAGE" >/dev/null; then
  rollback "Docker could not start the new container."
fi

deadline=$((SECONDS + STARTUP_TIMEOUT_SECONDS))
ready=false
stable_since=""

while (( SECONDS < deadline )); do
  running="$(docker container inspect --format '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null || true)"
  [[ "$running" == "true" ]] || rollback "The new container stopped during startup."

  if docker exec "$CONTAINER_NAME" \
    dotnet MandarinBotNet.dll \
      --health-check /app/data/health-state.json 15 >/dev/null 2>&1; then
    if [[ -z "$stable_since" ]]; then
      stable_since=$SECONDS
      printf 'Readiness observed; starting %s-second stabilization period.\n' \
        "$READINESS_STABILIZATION_SECONDS"
    elif (( SECONDS - stable_since >= READINESS_STABILIZATION_SECONDS )); then
      ready=true
      break
    fi
  else
    stable_since=""
  fi

  sleep 2
done

[[ "$ready" == "true" ]] ||
  rollback "The bot did not sustain readiness for ${READINESS_STABILIZATION_SECONDS} seconds within the ${STARTUP_TIMEOUT_SECONDS}-second startup timeout."

if container_exists "$rollback_name"; then
  docker rm "$rollback_name" >/dev/null ||
    printf 'Previous container could not be removed and was retained as %s.\n' "$rollback_name"
fi

if [[ -n "$previous_image" ]]; then
  current_image="$(docker container inspect --format '{{.Image}}' "$CONTAINER_NAME")"
  if [[ "$previous_image" != "$current_image" ]]; then
    docker image rm "$previous_image" >/dev/null 2>&1 ||
      printf 'Previous image is still referenced and was retained.\n'
  fi
fi

printf 'Deployment succeeded: %s is ready on %s\n' "$CONTAINER_NAME" "$IMAGE"
