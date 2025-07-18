import subprocess
import os
import tempfile
import uuid
import time

from lib.tw_cli import TwCli
from lib.colors import *

def create_temp_tinkwell_env(cli_tool_path, test_name):
    """
    Creates a temporary directory for a Tinkwell test environment with required subfolders.
    Returns the path to the created temporary directory.
    """
    print(f"{COLOR_DARK_GRAY}Creating isolated environment...{COLOR_RESET}")

    short_uuid = str(uuid.uuid4()).replace("-", "")[:8] # Short hash-like UUID
    prefix = f"Tinkwell.{test_name}.{short_uuid}."
    temp_dir = tempfile.mkdtemp(prefix=prefix)

    print(f"{COLOR_DARK_GRAY}Isolated environment at {COLOR_BLUE}{temp_dir}{COLOR_RESET}")
    os.makedirs(os.path.join(temp_dir, "User"), exist_ok=True)
    os.makedirs(os.path.join(temp_dir, "App"), exist_ok=True)
    os.makedirs(os.path.join(temp_dir, "Cert"), exist_ok=True)

    print(f"{COLOR_DARK_GRAY}Creating self-signed certificate...{COLOR_RESET}")
    tw_cli = TwCli(cli_tool_path)
    tw_cli.create_cert("Tinkwell-Int-Tests", "tinkwell", os.path.join(temp_dir, "Cert"), False, "1234")
    print(f"{COLOR_DARK_GRAY}Certificate saved as {COLOR_BLUE}{os.path.join(temp_dir, "Certs", "tinkwell.pfx")}{COLOR_RESET}")
    return temp_dir

def start_tinkwell_app(app_dll_path, app_path, temp_dir, verbose=False):
    """
    Starts the Tinkwell application.
    Its output (stdout/stderr) will be live-streamed to the console if verbose is True.
    """
    env = os.environ.copy()
    env["TINKWELL_WORKING_DIR_PATH"] = temp_dir
    env["TINKWELL_APP_DATA_PATH"] = os.path.join(temp_dir, "App")
    env["TINKWELL_USER_DATA_PATH"] = os.path.join(temp_dir, "User")
    env["TINKWELL_CERT_PATH"] = os.path.join(temp_dir, "Certs", "tinkwell.pfx")
    env["TINKWELL_CERT_PASS"] = "1234"

    command = ["dotnet", app_dll_path]
    stdout_redirect = None if verbose else subprocess.PIPE
    stderr_redirect = None if verbose else subprocess.PIPE

    # Set cwd to app_path
    process = subprocess.Popen(command, stdout=stdout_redirect, stderr=stderr_redirect, env=env, cwd=app_path)
    print(f"{COLOR_DARK_GRAY}Tinkwell application started with PID: {process.pid}{COLOR_RESET}")
    return process

def stop_tinkwell_app(process, cli_tool_path):
    """
    Stops the Tinkwell application subprocess, attempting graceful shutdown first.
    """
    if process.poll() is None: # Check if the process is still running
        try:
            # Attempt graceful shutdown via CLI
            tw_cli = TwCli(cli_tool_path)
            tw_cli.send_shutdown()
            time.sleep(5) # Wait for graceful shutdown

            if process.poll() is None: # Check if still running after graceful attempt
                print(f"{COLOR_DARK_GRAY}Tinkwell application (PID: {process.pid}) did not terminate gracefully, killing...{COLOR_RESET}")
                process.kill() # Use portable kill()
                process.wait()
            else:
                print(f"{COLOR_DARK_GRAY}Tinkwell application (PID: {process.pid}) terminated gracefully.{COLOR_RESET}")

        except Exception as e: # Catch general Exception for robustness
            print(f"{COLOR_RED}Error during graceful shutdown attempt for PID {process.pid}: {e}. Forcing kill...{COLOR_RESET}")
            process.kill() # Use portable kill()
            process.wait()
    else:
        print(f"{COLOR_DARK_GRAY}Tinkwell application (PID: {process.pid}) was already stopped.{COLOR_RESET}")
    # env["TINKWELL_WORKING_DIR_PATH"] = ""
    # env["TINKWELL_APP_DATA_PATH"] = ""
    # env["TINKWELL_USER_DATA_PATH"] = ""
    # env["TINKWELL_CERT_PATH"] = ""
    # env["TINKWELL_CERT_PASS"] = ""
