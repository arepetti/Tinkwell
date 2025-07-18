import os
import sys
import importlib.util
from lib.colors import *

def find_tests(test_dir, trait=None, test_name=None):
    """
    Discovers test files based on specified criteria.
    Returns a list of (test_name, test_file_path) tuples.
    """
    test_files_to_run = []

    if test_name:
        # Try exact match first (test_example.py for test_name='test_example')
        test_file_path_exact = os.path.join(test_dir, f"{test_name}.py")
        
        # Try prepending 'test_' if exact match not found (test_example.py for test_name='example')
        test_file_path_prefixed = os.path.join(test_dir, f"test_{test_name}.py")

        if os.path.exists(test_file_path_exact):
            test_files_to_run.append((test_name, test_file_path_exact))
        elif os.path.exists(test_file_path_prefixed):
            # Use the actual file name as the test_name for consistency in reporting
            test_files_to_run.append((f"test_{test_name}", test_file_path_prefixed))
        else:
            print(f"{COLOR_RED}Error: Test file '{test_name}.py' or 'test_{test_name}.py' not found in '{test_dir}'.{COLOR_RESET}")
            sys.exit(1)
    else:
        all_test_files = sorted([f for f in os.listdir(test_dir) if f.startswith('test_') and f.endswith('.py')])
        for tf in all_test_files:
            current_test_name = os.path.splitext(tf)[0]
            test_path = os.path.join(test_dir, tf)
            
            # Temporarily load module to check for TRAIT attribute
            spec = importlib.util.spec_from_file_location(current_test_name, test_path)
            test_module = importlib.util.module_from_spec(spec)
            sys.modules[current_test_name] = test_module
            spec.loader.exec_module(test_module)
            
            if trait:
                if hasattr(test_module, 'TRAIT') and test_module.TRAIT == trait:
                    test_files_to_run.append((current_test_name, test_path))
            else:
                test_files_to_run.append((current_test_name, test_path))

    return test_files_to_run