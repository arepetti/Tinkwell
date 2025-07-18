import argparse
import os
import sys
import importlib.util
import time
import shutil

from lib.test_scout import find_tests
from lib.test_executor import execute_test
from lib.test_reporter import generate_report
from lib.colors import * # Import all colors
from lib.tw_cli import TwCli, PingStatus
from lib.app_manager import TestContext # Import TestContext

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
            print(f"{COLOR_RED}Tinkwell supervisor ping failed with an error. Aborting test run.{COLOR_RESET}")
            return False

    print(f"{COLOR_RED}Tinkwell supervisor did not become OK after {MAX_PING_RETRIES} attempts. Aborting test run.{COLOR_RESET}")
    return False

def main():
    parser = argparse.ArgumentParser(description="Run integration tests for Tinkwell application.")
    parser.add_argument("--app-path", required=True, help="Path to the directory containing Tinkwell.Supervisor.dll and tw.dll.")
    parser.add_argument("--test-dir", default=os.path.join(os.path.dirname(__file__), "tests"), help="Directory containing test files (e.g., 'tests/').") # Updated default path
    parser.add_argument("--trait", help="Only run tests with this trait (e.g., 'smoke', 'integration').")
    parser.add_argument("--test-name", help="Run only a single test file (e.g., 'test_feature_a').")
    parser.add_argument("--keep-temp-dir", action="store_true", help="Do not delete the temporary directory after tests.")
    parser.add_argument("--verbose", action="store_true", help="Show verbose output from the Tinkwell application.")
    args = parser.parse_args()

    # Construct full paths to the DLLs
    tinkwell_supervisor_dll_path = os.path.abspath(os.path.join(args.app_path, "Tinkwell.Supervisor.dll"))
    tw_cli_dll_path = os.path.abspath(os.path.join(args.app_path, "tw.dll"))

    if not os.path.exists(tinkwell_supervisor_dll_path):
        print(f"{COLOR_RED}Error: Tinkwell.Supervisor.dll not found at expected path: {tinkwell_supervisor_dll_path}{COLOR_RESET}")
        sys.exit(1)
    if not os.path.exists(tw_cli_dll_path):
        print(f"{COLOR_RED}Error: tw.dll not found at expected path: {tw_cli_dll_path}{COLOR_RESET}")
        sys.exit(1)

    # Discover tests
    tests_to_run = find_tests(args.test_dir, args.trait, args.test_name)

    if not tests_to_run:
        print("No tests found matching the criteria.")
        sys.exit(0)

    results = {}
    start_time = time.time()
    test_count = len(tests_to_run)

    for test_index, (priority, test_name, test_file_path) in enumerate(tests_to_run, 1):
        print(f"\n{COLOR_YELLOW}--- Running Test: {test_name} ---{COLOR_RESET}")
        print(f"{COLOR_DARK_GRAY}Test {COLOR_RESET}{test_index}{COLOR_DARK_GRAY} of {test_count}")
        
        context = TestContext(app_path=args.app_path, app_dll_path=tinkwell_supervisor_dll_path, cli_tool_dll_path=tw_cli_dll_path)

        test_passed, failure_message = execute_test(test_name, test_file_path, context, args.keep_temp_dir, args.verbose)
        results[test_name] = {"status": "PASSED" if test_passed else "FAILED", "message": failure_message}

    generate_report(results, start_time)

if __name__ == "__main__":
    main()