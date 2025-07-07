import threading
import time
import queue

# MeasureSampler collects individual measure updates and periodically emits complete samples.
# A sample is emitted at a fixed interval, using the latest known value for each
# measure. If a measure has not updated, its last known value is used.

class MeasureSampler:
    def __init__(self, measure_names, sample_interval_sec=1.0):
        self._measure_names = list(measure_names) # Keep order for consistent sample vectors
        self._latest_values = {name: None for name in measure_names}
        self._sample_interval_sec = sample_interval_sec
        self._sample_queue = queue.Queue() # Complete samples!
        self._sampling_thread = None
        self._stop_sampling_event = threading.Event()
        self._lock = threading.Lock()
        self._all_measures_initialized = False

    def update_measure(self, name, value):
        with self._lock:
            if name not in self._measure_names:
                return

            self._latest_values[name] = value

            # Check if all measures have received an initial value. Note that tw measures subscribe
            # gives all the initial values at once but in this code we do not want to assume how measures are generated.
            if not self._all_measures_initialized:
                if all(v is not None for v in self._latest_values.values()):
                    self._all_measures_initialized = True
                    print("All measures initialized. Starting periodic sampling.")

    def _sampling_loop(self):
        last_sample_time = time.monotonic()

        while not self._stop_sampling_event.is_set():
            current_time = time.monotonic()
            time_since_last_sample = current_time - last_sample_time

            if time_since_last_sample >= self._sample_interval_sec:
                with self._lock:
                    if self._all_measures_initialized:
                        # Form the sample using the latest known values
                        current_sample = [self._latest_values[name] for name in self._measure_names]
                        self._sample_queue.put(current_sample)
                        last_sample_time = current_time # Reset timer for next sample
                    else:
                        # If not all measures have been initialized, just reset timer and wait
                        last_sample_time = current_time

            remaining_time = self._sample_interval_sec - (time.monotonic() - last_sample_time)
            if remaining_time > 0:
                time.sleep(min(remaining_time, 0.05))
            else:
                time.sleep(0.001)

    def start(self):
        if self._sampling_thread is None or not self._sampling_thread.is_alive():
            self._stop_sampling_event.clear()
            self._sampling_thread = threading.Thread(target=self._sampling_loop)
            self._sampling_thread.daemon = True
            self._sampling_thread.start()

    def stop(self):
        if self._sampling_thread and self._sampling_thread.is_alive():
            self._stop_sampling_event.set()
            self._sampling_thread.join(timeout=1.0)

    def get_next_sample(self, timeout=None):
        try:
            return self._sample_queue.get(timeout=timeout)
        except queue.Empty:
            return None

    def is_ready_for_sampling(self):
        with self._lock:
            return self._all_measures_initialized