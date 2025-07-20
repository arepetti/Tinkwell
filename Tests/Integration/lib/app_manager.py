import subprocess
import os
import tempfile
import uuid
import time

from lib.tw_cli import TwCli
from lib.colors import *

class TestContext:
    def __init__(self, app_path, app_dll_path, cli_tool_dll_path):
        self.app_path = app_path
        self.app_dll_path = app_dll_path
        self.cli_tool_dll_path = cli_tool_dll_path
        self.temp_dir = "" # Will be set by create_temp_tinkwell_env
        self.server_certificate_path = "" # Will be set by create_temp_tinkwell_env
        self.client_certificate_path = "" # Will be set by create_temp_tinkwell_env

def create_temp_tinkwell_env(context, test_name):
    """
    Creates a temporary directory for a Tinkwell test environment with required subfolders.
    Updates the context with temp_dir and client_certificate_path.
    """
    print(f"{COLOR_DARK_GRAY}Creating isolated environment...{COLOR_RESET}")

    short_uuid = str(uuid.uuid4()).replace("-", "")[:8] # Short hash-like UUID
    prefix = f"Tinkwell.{test_name}.{short_uuid}."
    temp_dir = tempfile.mkdtemp(prefix=prefix)
    context.temp_dir = temp_dir

    print(f"{COLOR_DARK_GRAY}Isolated environment at {COLOR_BLUE}{context.temp_dir}{COLOR_RESET}")
    os.makedirs(os.path.join(context.temp_dir, "User"), exist_ok=True)
    os.makedirs(os.path.join(context.temp_dir, "App"), exist_ok=True)
    os.makedirs(os.path.join(context.temp_dir, "Cert"), exist_ok=True)

    print(f"{COLOR_DARK_GRAY}Creating self-signed certificate...{COLOR_RESET}")
    tw_cli = TwCli(context)
    tw_cli.create_cert("Tinkwell-Int-Tests", "tinkwell", os.path.join(context.temp_dir, "Cert"), True, "1234")
    
    context.server_certificate_path = os.path.join(context.temp_dir, "Cert", "tinkwell.pfx")
    context.client_certificate_path = os.path.join(context.temp_dir, "Cert", "tinkwell-cert.pem")

    print(f"{COLOR_DARK_GRAY}Certificate (server) saved as {COLOR_BLUE}{context.server_certificate_path}{COLOR_RESET}")
    print(f"{COLOR_DARK_GRAY}Certificate (client) saved as {COLOR_BLUE}{context.client_certificate_path}{COLOR_RESET}")

def start_tinkwell_app(context, verbose=False):
    """
    Starts the Tinkwell application (DLL) as a subprocess using 'dotnet'.
    The application's working directory will be set to app_path.
    Its output (stdout/stderr) will be live-streamed to the console if verbose is True.
    Returns the subprocess Popen object.
    """
    env = os.environ.copy()
    env["TINKWELL_WORKING_DIR_PATH"] = context.temp_dir
    env["TINKWELL_APP_DATA_PATH"] = os.path.join(context.temp_dir, "App")
    env["TINKWELL_USER_DATA_PATH"] = os.path.join(context.temp_dir, "User")
    env["TINKWELL_CERT_PATH"] = context.server_certificate_path
    env["TINKWELL_CERT_PASS"] = "1234"
    env["TINKWELL_CLIENT_CERT_PATH"] = context.client_certificate_path

    command = ["dotnet", context.app_dll_path, "--Supervisor:StartingPort=6000"]
    stdout_redirect = None if verbose else subprocess.PIPE
    stderr_redirect = None if verbose else subprocess.PIPE

    process = subprocess.Popen(command, stdout=stdout_redirect, stderr=stderr_redirect, env=env, cwd=context.temp_dir)
    print(f"{COLOR_DARK_GRAY}Application started with PID{COLOR_RESET} {process.pid}")
    return process

def stop_tinkwell_app(process, context):
    """
    Stops the Tinkwell application subprocess, attempting graceful shutdown first.
    """
    if process.poll() is None: # Check if the process is still running
        try:
            # Attempt graceful shutdown via CLI
            tw_cli = TwCli(context)
            tw_cli.send_shutdown()
            time.sleep(5) # Wait for graceful shutdown

            if process.poll() is None: # Check if still running after graceful attempt
                print(f"{COLOR_DARK_GRAY}Application (PID: {process.pid}) did not terminate gracefully, killing...{COLOR_RESET}")
                process.kill()
                process.wait()
            else:
                print(f"{COLOR_DARK_GRAY}Application (PID: {process.pid}) terminated gracefully.{COLOR_RESET}")

        except Exception as e: # Catch general Exception for robustness
            print(f"{COLOR_RED}Error during graceful shutdown attempt for PID {process.pid}: {e}. Forcing kill...{COLOR_RESET}")
            process.kill()
            process.wait()
    else:
        print(f"{COLOR_DARK_GRAY}Application (PID: {process.pid}) was already stopped.{COLOR_RESET}")