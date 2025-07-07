import subprocess
import threading
import queue
import re

# Default path to the 'tw' executable. Use absolute path if necessary.
# If 'tw' is in your PATH, you can leave this as is.
TW_PATH = "tw"  

class TwMeasuresSubscriber:
    """Manages the lifecycle of a 'tw measures subscribe' subprocess."""
    def __init__(self, measures_to_subscribe):
        self._measures_to_subscribe = measures_to_subscribe
        self._process = None
        self._output_queue = queue.Queue()
        self._stdout_thread = None
        self._is_running = False

    def _read_stdout(self):
        """Reads stdout from the subprocess and puts lines into a queue."""
        for line in iter(self._process.stdout.readline, ''):
            if not line:
                break
            self._output_queue.put(line.strip())
        self._process.stdout.close()

    def start_subscription(self):
        """Starts the 'tw measures subscribe' subprocess."""
        if self._is_running:
            return

        subscribe_command = [TW_PATH, "measures", "subscribe"] + self._measures_to_subscribe
        try:
            self._process = subprocess.Popen(
                subscribe_command,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                bufsize=1,
                universal_newlines=True
            )
            self._is_running = True
            self._stdout_thread = threading.Thread(target=self._read_stdout)
            self._stdout_thread.daemon = True
            self._stdout_thread.start()
            print("tw subscription process started.")
        except FileNotFoundError:
            print(f"Error: '{TW_PATH}' command not found. Please ensure 'tw' is in your PATH.")
            self._is_running = False
            raise
        except Exception as e:
            print(f"Failed to start 'tw measures subscribe' process: {e}")
            self._is_running = False
            raise

    def get_latest_output(self, timeout=0.1):
        """Retrieves the latest parsed measure from the subscribed process's stdout."""
        if not self._is_running:
            return None
        
        # The output is expected to be in the format "MeasureName=Value"
        # where Value is a float.
        # If the unit of measure is needed then the caller can call inspect_measure_value()
        # to obtain it separately.
        # Example:
        #   Temperature=23.5
        try:
            line = self._output_queue.get(timeout=timeout)
            parts = line.split('=')
            if len(parts) == 2:
                name, value_str = parts[0], parts[1]
                try:
                    value = float(value_str)
                    return {name: value}
                except ValueError:
                    print(f"Warning: Could not parse value '{value_str}' from line '{line}'")
                    return None
            else:
                print(f"Warning: Unrecognized line format: '{line}'")
                return None
        except queue.Empty:
            if self._process and self._process.poll() is not None:
                print("Subscription process ended unexpectedly.")
                self.stop_subscription()
            return None

    def stop_subscription(self):
        """Terminates the 'tw measures subscribe' subprocess."""
        if self._process and self._process.poll() is None:
            self._process.terminate()
            try:
                self._process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self._process.kill()
        self._is_running = False
        self._process = None
        self._stdout_thread = None

    def is_alive(self):
        """Checks if the subscribed process is still running."""
        return self._is_running and (self._process and self._process.poll() is None)

def inspect_measure(measure_name):
    """Executes 'tw measures inspect' to get min/max for a measure."""
    command = [TW_PATH, "measures", "inspect", measure_name]
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    output = result.stdout.strip().split('\n')

    min_val = None
    max_val = None

    # The output is a set of properties in the form "Property=Value"
    # We need to find "Minimum" and "Maximum" properties.
    # Example output:
    #   Minimum=0.0
    #   Maximum=100.0
    #   Something else=OtherValue
    for line in output:
        if '=' in line:
            key, value = line.split('=', 1)
            if key == "Minimum":
                try:
                    min_val = float(value) if value else None
                except ValueError:
                    min_val = None
            elif key == "Maximum":
                try:
                    max_val = float(value) if value else None
                except ValueError:
                    max_val = None
    return min_val, max_val

def inspect_measure_value(measure_name):
    """Executes 'tw measures inspect MEASURE_NAME --value' to get current value and unit."""
    command = [TW_PATH, "measures", "inspect", measure_name, "--value"]
    result = subprocess.run(command, capture_output=True, text=True, check=True)
    output = result.stdout.strip().split('\n')

    # The output is a set of properties in the form "Property=Value"
    # We need to find "Value" which contains the current value and unit. We need to
    # know both the value and unit, so we will parse it accordingly. For example:
    #   Value=5.5 V
    for line in output:
        match = re.match(r"Value=(.+)", line)
        if match:
            full_value_str = match.group(1).strip()
            value_match = re.match(r"([+-]?\d*\.?\d+)\s*(.*)", full_value_str)
            if value_match:
                value = float(value_match.group(1))
                unit = value_match.group(2).strip()
                return value, unit
            else:
                try:
                    value = float(full_value_str)
                    return value, ""
                except ValueError:
                    return None, None
    return None, None

def write_measure(measure_name, value, unit):
    """Writes a measure value using 'tw measures write'."""
    value_with_unit = f"{value}" if not unit else f"{value} {unit}"
    command = [TW_PATH, "measures", "write", measure_name, value_with_unit]
    subprocess.run(command, check=True, capture_output=True)
