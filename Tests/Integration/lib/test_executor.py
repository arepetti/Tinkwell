import os
import time
import shutil
import importlib.util
import sys

from lib.app_manager import start_tinkwell_app, stop_tinkwell_app, create_temp_tinkwell_env
from lib.tw_cli import TwCli, PingStatus
from lib.colors import *

# Health Check Constants
INITIAL_WAIT_SECONDS = 5
RETRY_WAIT_SECONDS = 2
MAX_PING_RETRIES = 5

def wait_for_tinkwell_ready(tw_cli):
    print(f"{COLOR_DARK_GRAY}Waiting {INITIAL_WAIT_SECONDS} seconds for Tinkwell to initialize...{COLOR_RESET}")
    time.sleep(INITIAL_WAIT_SECONDS)

    for attempt in range(1, MAX_PING_RETRIES + 1):
        print(f"{COLOR_DARK_GRAY}Pinging Tinkwell supervisor (Attempt {attempt}/{MAX_PING_RETRIES})...{COLOR_RESET}")
        status = tw_cli.send_ping()

        if status == PingStatus.OK:
            print(f"{COLOR_DARK_GRAY}Tinkwell supervisor is OK.{COLOR_RESET}")
            return True
        elif status == PingStatus.Loading:
            print(f"{COLOR_DARK_GRAY}Tinkwell supervisor is Loading. Retrying in {RETRY_WAIT_SECONDS} seconds...{COLOR_RESET}")
            time.sleep(RETRY_WAIT_SECONDS)
        else: # PingStatus.Error
            print(f"{COLOR_RED}Tinkwell supervisor ping failed with an error. Aborting test.{COLOR_RESET}")
            return False

    print(f"  {COLOR_RED}Tinkwell supervisor did not become OK after {MAX_PING_RETRIES} attempts. Aborting test.{COLOR_RESET}")
    return False

def execute_test(test_name, test_file_path, app_dll_path, cli_tool_dll_path, keep_temp_dir, verbose, app_path):
    """
    Executes a single integration test.
    Returns a tuple (bool_passed, message_or_none).
    """
    app_process = None
    temp_dir = None
    test_passed = False
    failure_message = None

    try:
        # Create isolated environment
        temp_dir = create_temp_tinkwell_env(cli_tool_dll_path, test_name)

        # Copy test-specific data if available
        test_data_dir = os.path.join(os.path.dirname(test_file_path), test_name)
        if os.path.isdir(test_data_dir):
            print(f"{COLOR_DARK_GRAY}Copying test data from {COLOR_BLUE}{test_data_dir}{COLOR_RESET} to {COLOR_BLUE}{temp_dir}{COLOR_RESET}")
            for item in os.listdir(test_data_dir):
                s = os.path.join(test_data_dir, item)
                d = os.path.join(temp_dir, item)
                if os.path.isdir(s):
                    shutil.copytree(s, d, dirs_exist_ok=True)
                else:
                    shutil.copy2(s, d)

        # Start Tinkwell application
        print(f"{COLOR_DARK_GRAY}Starting Tinkwell application {COLOR_BLUE}{app_dll_path}{COLOR_RESET}")
        app_process = start_tinkwell_app(app_dll_path, app_path, temp_dir, verbose)
        time.sleep(3) # Give the application time to fully start up

        # Perform health check for this specific test run
        tw_cli = TwCli(cli_tool_dll_path)
        if not wait_for_tinkwell_ready(tw_cli):
            return False, "Tinkwell application did not become ready for this test."

        # Load and execute the individual test module
        spec = importlib.util.spec_from_file_location(test_name, test_file_path)
        test_module = importlib.util.module_from_spec(spec)
        sys.modules[test_name] = test_module
        spec.loader.exec_module(test_module)

        if hasattr(test_module, 'run_test'):
            print(f"Executing test logic for {COLOR_CYAN}{test_name}{COLOR_RESET}...")
            test_result = test_module.run_test(tw_cli)
            
            if isinstance(test_result, bool):
                test_passed = test_result
            elif isinstance(test_result, str):
                test_passed = False
                failure_message = test_result
            else:
                test_passed = False
                failure_message = f"Invalid return type from run_test: {type(test_result)}"

        else:
            test_passed = False
            failure_message = f"Test file '{test_file_path}' does not contain a 'run_test' function."

    except Exception as e:
        test_passed = False
        failure_message = f"An unexpected error occurred: {e}"
        print(f"  {COLOR_RED}{failure_message}{COLOR_RESET}")
    finally:
        print(f"{COLOR_DARK_GRAY}Shutting down...{COLOR_RESET}")
        # Stop Tinkwell application
        if app_process:
            print(f"{COLOR_DARK_GRAY}Stopping Tinkwell application...{COLOR_RESET}")
            stop_tinkwell_app(app_process, cli_tool_dll_path) # Pass cli_tool_path here
            time.sleep(1) # Give the application time to shut down

        # Clean up temporary directory
        print(f"{COLOR_DARK_GRAY}Removing the isolated environment...{COLOR_RESET}")
        if temp_dir and os.path.exists(temp_dir) and not keep_temp_dir:
            print(f"{COLOR_DARK_GRAY}Deleting temporary directory {COLOR_BLUE}{temp_dir}{COLOR_RESET}")
            shutil.rmtree(temp_dir)
        elif temp_dir and keep_temp_dir:
            print(f"{COLOR_DARK_GRAY}Keeping temporary directory {COLOR_BLUE}{temp_dir}{COLOR_RESET}")

    return test_passed, failure_message
