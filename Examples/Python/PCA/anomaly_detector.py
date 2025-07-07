import time
import csv

from tw_integration import inspect_measure, TwMeasuresSubscriber
from pca_detector import PcaAnomalyDetector
from common_utils import run_until_key_press
from measure_sampler import MeasureSampler

# These are the measures we want to subscribe to and monitor
# You can modify this list to include any measures available in your Tinkwell setup.
# Ensure these measures are available in your Tinkwell system.
MEASURES_TO_SUBSCRIBE = ["voltage", "current", "power"]

# PCA configuration
# Adjust these parameters based on your requirements and available data
PCA_BUFFER_SIZE = 100  # Number of samples to collect before training PCA
ANOMALY_THRESHOLD_PERCENTILE = 99  # Percentile for anomaly threshold (e.g., 99 for top 1%)
N_COMPONENTS = 2  # Number of principal components for PCA

# Output configuration
CSV_FILE_PATH = "measures.csv" # Path to the CSV log file
SAMPLE_INTERVAL_SEC = 1.0 # Sample generation interval

class Measure:
    def __init__(self, name):
        self.name = name
        self.current_value = None
        self.min_val = None
        self.max_val = None

    def set_range(self, min_val, max_val):
        self.min_val = min_val
        self.max_val = max_val

    def normalize(self, value):
        # Cannot normalize if range is unknown or zero
        if self.min_val is None or self.max_val is None or self.min_val == self.max_val:
            return value

        return (value - self.min_val) / (self.max_val - self.min_val)

def main(stop_event):
    measures = {name: Measure(name) for name in MEASURES_TO_SUBSCRIBE}

    print("Initializing anomaly detection...")

    # We know which measures we want to subscribe to, now we need to know what their min/max ranges are.
    for measure_name, measure_obj in measures.items():
        try:
            min_val, max_val = inspect_measure(measure_name)
            if min_val is not None and max_val is not None:
                measure_obj.set_range(min_val, max_val)
                print(f"  {measure_name}: Min={min_val}, Max={max_val}")
            else:
                print(f"  Could not determine range for {measure_name}. Will use raw values.")
        except Exception as e:
            print(f"Error inspecting measure {measure_name}: {e}")
            return

    # Now we can subscribe to the measures
    tw_process_manager = TwMeasuresSubscriber(MEASURES_TO_SUBSCRIBE)
    try:
        tw_process_manager.start_subscription()
    except Exception as e:
        print(f"Failed to start tw subscription: {e}")
        return

    # Setup data sampler and PCA anomaly detector
    pca_buffer = []
    pca_detector = PcaAnomalyDetector(N_COMPONENTS, ANOMALY_THRESHOLD_PERCENTILE)
    
    sampler = MeasureSampler(MEASURES_TO_SUBSCRIBE, SAMPLE_INTERVAL_SEC)
    sampler.start() # Start the sampling thread

    print("Monitoring for anomalies (press Enter to exit)...")

    csv_file = None
    csv_writer = None
    try:
        csv_file = open(CSV_FILE_PATH, 'w', newline='')
        csv_writer = csv.writer(csv_file)
        
        # Write CSV header
        header = MEASURES_TO_SUBSCRIBE + ['anomaly']
        csv_writer.writerow(header)
        while not stop_event.is_set():
            # First, process any incoming raw measure updates from tw
            raw_measure_data = tw_process_manager.get_latest_output(timeout=0.01)
            if raw_measure_data is not None:
                name = list(raw_measure_data.keys())[0]
                value = raw_measure_data[name]
                sampler.update_measure(name, value)
            elif not tw_process_manager.is_alive():
                print("Subscription process ended unexpectedly.")
                break

            # We cannot process a single measure at a time, we need to wait for the sampler to collect a full sample
            # This is to ensure we have a complete set of measures before processing (sample = all the measures we care about).
            sample = sampler.get_next_sample(timeout=0.01)
            if sample is None:
                time.sleep(0.01)
                continue

            # At this point, 'sample' contains a complete, throttled set of measure values
            current_measure_values = {MEASURES_TO_SUBSCRIBE[i]: sample[i] for i in range(len(MEASURES_TO_SUBSCRIBE))}

            pca_buffer.append(sample)

            is_anomaly = 0

            # Perform PCA anomaly detection if the detector is trained
            if pca_detector.is_trained():
                try:
                    is_anomaly, current_reconstruction_error, reconstructed_sample = pca_detector.detect(sample)

                    if is_anomaly:
                        print("ANOMALY DETECTED")
                        print(f"  Reconstruction Error: {current_reconstruction_error:.4f} (Threshold: {pca_detector.anomaly_threshold:.4f})")
                        print("  Current Raw Values:")
                        for i, name in enumerate(MEASURES_TO_SUBSCRIBE):
                            print(f"    {name}: {current_measure_values[name]:f}")
                        print("  Current Normalized Values:")
                        for i, name in enumerate(MEASURES_TO_SUBSCRIBE):
                            print(f"    {name}: {sample[i]:.4n}")
                        print("  Reconstructed Normalized Values:")
                        for i, name in enumerate(MEASURES_TO_SUBSCRIBE):
                            print(f"    {name}: {reconstructed_sample[i]:.4f}")
                        print("  Difference (Normalized):")
                        for i, name in enumerate(MEASURES_TO_SUBSCRIBE):
                            print(f"    {name}: {(sample[i] - reconstructed_sample[i]):.4f}")
                        print("\n")
                except Exception as e:
                    print(f"Error during anomaly detection: {e}")
                    is_anomaly = 0

            # Log to CSV
            if csv_writer:
                row = [f'{current_measure_values[name]:n}' for name in MEASURES_TO_SUBSCRIBE] + [str(int(is_anomaly))]
                csv_writer.writerow(row)
                csv_file.flush()

            # Train PCA if buffer is full
            if len(pca_buffer) >= PCA_BUFFER_SIZE:
                print(f"Training PCA with {len(pca_buffer)} samples")
                try:
                    anomaly_threshold = pca_detector.train(pca_buffer)
                    print(f"PCA trained. Anomaly Threshold (Reconstruction Error): {anomaly_threshold:.4f}")
                    pca_buffer.clear() # Clear buffer after training, we keep up!
                except Exception as e:
                    print(f"Resetting detector because of an error during PCA training: {e}")
                    pca_detector = PcaAnomalyDetector(N_COMPONENTS, ANOMALY_THRESHOLD_PERCENTILE)
                    pca_buffer.clear()

            if stop_event.is_set():
                break

    except KeyboardInterrupt:
        pass  # Allow graceful exit on Ctrl+C
    finally:
        if csv_file:
            csv_file.close()

        sampler.stop()
        tw_process_manager.stop_subscription()

if __name__ == "__main__":
    run_until_key_press(main)
