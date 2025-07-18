import sys
from lib.colors import *

def generate_report(results):
    overall_success = True
    print(f"{COLOR_YELLOW}\n--- Summary ---{COLOR_RESET}")
    for test, result_data in results.items():
        status = result_data["status"]
        message = result_data["message"]
        
        if status == "PASSED":
            print(f"{COLOR_CYAN}{test}{COLOR_RESET}: {COLOR_GREEN}{status}{COLOR_RESET}")
        else:
            print(f"{COLOR_CYAN}{test}{COLOR_RESET}: {COLOR_RED}{status}{COLOR_RESET}")
            if message:
                print(f"  {COLOR_RED}{message}{COLOR_RESET}")
            overall_success = False

    if overall_success:
        print(f"\n{COLOR_GREEN}All tests PASSED!{COLOR_RESET}")
        sys.exit(0)
    else:
        print(f"\n{COLOR_RED}Some tests FAILED.{COLOR_RESET}")
        sys.exit(1)
