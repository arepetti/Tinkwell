import sys
import time
from lib.colors import *
from lib.formatting import calculate_elapsed_time

def generate_report(results, start_time):
    overall_success = True
    passed_count = 0
    failed_count = 0
    total_count = len(results)

    print(f"{COLOR_YELLOW}\n--- Test Results ---{COLOR_RESET}")
    for test, result_data in results.items():
        status = result_data["status"]
        message = result_data["message"]

        if status == "PASSED":
            print(f"{COLOR_CYAN}{test}{COLOR_RESET}: {COLOR_GREEN}{status}{COLOR_RESET}")
            passed_count += 1
        else:
            print(f"{COLOR_CYAN}{test}{COLOR_RESET}: {COLOR_RED}{status}{COLOR_RESET}")
            if message:
                print(f"  {COLOR_RED}{message}{COLOR_RESET}")
            overall_success = False
            failed_count += 1

    print(f"\n{COLOR_YELLOW}--- Summary ---{COLOR_RESET}")
    print(f"Tests: {COLOR_GREEN}{passed_count} passed{COLOR_RESET}, {COLOR_RED}{failed_count} failed{COLOR_RESET}, {total_count} total")
    print(f"Time: {calculate_elapsed_time(start_time, time.time())}")

    if overall_success:
        print(f"\n{COLOR_GREEN}All tests PASSED!{COLOR_RESET}")
        sys.exit(0)
    else:
        print(f"\n{COLOR_RED}Some tests FAILED.{COLOR_RESET}")
        sys.exit(1)