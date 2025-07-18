import os
import sys
import importlib.util
from lib.colors import *

def find_tests(test_dir, trait=None, test_name=None):
    """
    Discovers test files based on specified criteria.
    Returns a list of (priority, test_name, test_file_path) tuples, sorted by priority.
    """
    discovered_tests = []

    if test_name:
        # Try exact match first (test_example.py for test_name='test_example')
        test_file_path_exact = os.path.join(test_dir, f"{test_name}.py")
        
        # Try prepending 'test_' if exact match not found (test_example.py for test_name='example')
        test_file_path_prefixed = os.path.join(test_dir, f"test_{test_name}.py")

        print(f"{COLOR_DARK_GRAY}  Debug: Checking exact path: {test_file_path_exact} (exists: {os.path.exists(test_file_path_exact)}){COLOR_RESET}")
        print(f"{COLOR_DARK_GRAY}  Debug: Checking prefixed path: {test_file_path_prefixed} (exists: {os.path.exists(test_file_path_prefixed)}){COLOR_RESET}")

        target_test_file = None
        if os.path.exists(test_file_path_exact):
            target_test_file = (test_name, test_file_path_exact)
        elif os.path.exists(test_file_path_prefixed):
            target_test_file = (f"test_{test_name}", test_file_path_prefixed)
        
        if target_test_file:
            current_test_name, test_path = target_test_file
            spec = importlib.util.spec_from_file_location(current_test_name, test_path)
            test_module = importlib.util.module_from_spec(spec)
            sys.modules[current_test_name] = test_module
            spec.loader.exec_module(test_module)
            priority = getattr(test_module, 'TEST_PRIORITY', 100)
            discovered_tests.append((priority, current_test_name, test_path))
        else:
            print(f"{COLOR_RED}Error: Test file '{test_name}.py' or 'test_{test_name}.py' not found in '{test_dir}'.{COLOR_RESET}")
            sys.exit(1)
    else:
        all_test_files = sorted([f for f in os.listdir(test_dir) if f.startswith('test_') and f.endswith('.py')])
        for tf in all_test_files:
            current_test_name = os.path.splitext(tf)[0]
            test_path = os.path.join(test_dir, tf)
            
            # Temporarily load module to check for TRAIT attribute and TEST_PRIORITY
            spec = importlib.util.spec_from_file_location(current_test_name, test_path)
            test_module = importlib.util.module_from_spec(spec)
            sys.modules[current_test_name] = test_module
            spec.loader.exec_module(test_module)
            
            priority = getattr(test_module, 'TEST_PRIORITY', 100)

            if trait:
                if hasattr(test_module, 'TRAIT') and test_module.TRAIT == trait:
                    discovered_tests.append((priority, current_test_name, test_path))
            else:
                discovered_tests.append((priority, current_test_name, test_path))

    # Sort tests by priority
    discovered_tests.sort(key=lambda x: x[0])

    return discovered_tests