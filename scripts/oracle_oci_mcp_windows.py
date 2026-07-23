"""Launch Oracle's OCI MCP server with a Windows-safe audit-log path.

Oracle OCI MCP 2.1.0 opens ``/tmp/audit.log`` while importing its server
module. On Windows that resolves to ``C:\\tmp\\audit.log`` and startup fails
when the directory does not exist. Redirect only that exact handler path to an
ignored repository-local tools directory, then run Oracle's normal entry point.
"""

from __future__ import annotations

import logging.handlers
import os
from pathlib import Path
from typing import Any


_ORACLE_AUDIT_PATH = "/tmp/audit.log"
_REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
_LOCAL_AUDIT_PATH = _REPOSITORY_ROOT / ".tools" / "oracle-oci-mcp" / "audit.log"
_OriginalRotatingFileHandler = logging.handlers.RotatingFileHandler


class _WindowsSafeRotatingFileHandler(_OriginalRotatingFileHandler):
    """Redirect Oracle's hardcoded Unix audit path without affecting others."""

    def __init__(self, filename: os.PathLike[str] | str, *args: Any, **kwargs: Any) -> None:
        if os.fspath(filename) == _ORACLE_AUDIT_PATH:
            filename = _LOCAL_AUDIT_PATH
        super().__init__(filename, *args, **kwargs)


def main() -> None:
    _LOCAL_AUDIT_PATH.parent.mkdir(parents=True, exist_ok=True)
    # MCP stdio stdout must contain JSON only; FastMCP enables a banner by
    # default and Codex correctly rejects those non-JSON lines.
    os.environ.setdefault("FASTMCP_SHOW_SERVER_BANNER", "false")

    # utils.py imports this class while server.py is imported, so patch before
    # importing Oracle and restore the standard-library module immediately after.
    logging.handlers.RotatingFileHandler = _WindowsSafeRotatingFileHandler
    try:
        from oracle.oci_cloud_mcp_server.server import main as oracle_main
    finally:
        logging.handlers.RotatingFileHandler = _OriginalRotatingFileHandler

    oracle_main()


if __name__ == "__main__":
    main()
