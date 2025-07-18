import subprocess
import os
from lib.colors import *

class PingStatus:
    OK = "OK"
    Loading = "Loading"
    Error = "Error"

class TwCli:
    def __init__(self, context):
        self.context = context

    def run_command(self, *args, input_data=None):
        """
        Runs a command using the Tinkwell CLI tool (tw.dll) and captures its output.
        Returns a dictionary with 'stdout', 'stderr', and 'returncode'.
        'input_data' can be a string to send to the command's stdin.
        """
        command = ["dotnet", self.context.cli_tool_dll_path] + list(args)
        
        env = os.environ.copy()
        if self.context.client_certificate_path:
            env["TINKWELL_CLIENT_CERT_PATH"] = self.context.client_certificate_path

        try:
            result = subprocess.run(
                command,
                capture_output=True,
                text=True, # Decode stdout/stderr as text
                check=False, # Do not raise an exception for non-zero exit codes
                timeout=30, # Add a timeout to prevent hanging tests
                input=input_data, # Pass input data to stdin
                env=env # Pass the modified environment
            )
            if result.returncode != 0:
                print(f"{COLOR_DARK_GRAY}Command: {' '.join(command)}{COLOR_RESET}")
                print(f"{COLOR_DARK_GRAY}Exit code: {result.returncode}{COLOR_RESET}")
                print(f"{COLOR_DARK_GRAY}stdout (length {len(result.stdout)}):\n{result.stdout}{COLOR_RESET}") # Removed .strip()
                if result.stderr:
                    print(f"{COLOR_DARK_GRAY}stderr (length {len(result.stderr)}):\n{COLOR_RED}{result.stderr}{COLOR_RESET}") # Removed .strip()
            return {
                "stdout": result.stdout,
                "stderr": result.stderr,
                "returncode": result.returncode
            }
        except subprocess.TimeoutExpired:
            print(f"{COLOR_RED}Command timed out after 30 seconds: {' '.join(command)}{COLOR_RESET}")
            return {
                "stdout": "",
                "stderr": "Command timed out.",
                "returncode": -1 # Indicate a timeout error
            }
        except Exception as e:
            print(f"{COLOR_RED}Error running CLI command: {e}{COLOR_RESET}")
            return {
                "stdout": "",
                "stderr": str(e),
                "returncode": -2 # Indicate a general error
            }

    def send_ping(self):
        """
        Sends a 'tw supervisor send ping' command and interprets the result.
        Returns a PingStatus enum value.
        """
        result = self.run_command("supervisor", "send", "ping", "-y", "--stdout-format=tooling")
        if result["returncode"] != 0:
            return PingStatus.Error
        
        stdout_stripped = result["stdout"].strip()
        if stdout_stripped == "OK":
            return PingStatus.OK
        elif stdout_stripped == "Loading":
            return PingStatus.Loading
        else:
            return PingStatus.Error

    def send_shutdown(self):
        """
        Sends a 'tw supervisor send shutdown' command.
        """
        self.run_command("supervisor", "send", "shutdown", "-y", "--stdout-format=tooling")

    def create_cert(self, common_name, export_name, export_path, export_pem, password):
        """
        Calls 'tw certs create' to generate a new self-signed certificate.
        """
        command_args = ["certs", "create"]
        if common_name:
            command_args.append(common_name)
        command_args.append("--stdout-format=tooling")
        command_args.append("--set-environment=false")
        command_args.extend(["--export-name", export_name])
        command_args.extend(["--export-path", export_path])
        if export_pem:
            command_args.append("--export-pem")
        if password:
            command_args.append(f"--unsafe-password={password}")
        
        return self.run_command(*command_args)
