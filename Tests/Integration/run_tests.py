import argparse
import os
import sys

from lib.test_scout import find_tests
from lib.test_executor import execute_test
from lib.test_reporter import generate_report
from lib.colors import *

# Health Check Constants
INITIAL_WAIT_SECONDS = 5
RETRY_WAIT_SECONDS = 2
MAX_PING_RETRIES = 5

def main():
    parser = argparse.ArgumentParser(description="Run integration tests for Tinkwell application.")
    parser.add_argument("--app-path", required=True, help="Path to the directory containing Tinkwell.Supervisor.dll and tw.dll.")
    parser.add_argument("--test-dir", default="tests", help="Directory containing test files (e.g., 'tests/').")
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
    for test_name, test_file_path in tests_to_run:
        print(f"\n{COLOR_YELLOW}--- Running Test: {test_name} ---{COLOR_RESET}")
        test_passed, failure_message = execute_test(test_name, test_file_path, tinkwell_supervisor_dll_path, tw_cli_dll_path, args.keep_temp_dir, args.verbose, args.app_path)
        results[test_name] = {"status": "PASSED" if test_passed else "FAILED", "message": failure_message}

    generate_report(results)

if __name__ == "__main__":
    main()