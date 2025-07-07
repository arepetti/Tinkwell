import time
import random
from tw_integration import inspect_measure_value, write_measure
from common_utils import run_until_key_press

# These are the measures we want to generate synthetic data for. Because values cannot be simply random
# we take a multi-step approach:
# 1. Get the initial value and unit for each measure. That's our baseline to which we will apply variations.
# 2. For each measure we define a function that takes the initial value and a proposed random variation and the function
#    will calculate the final value. This enables us to have different logic for each measure or to correlate them as needed.
# 3. The variation for "normal" samples and for "outlier" samples is different.
# 4. On the top of that, we apply a smoothing factor to the final value to avoid abrupt changes. It's random (in a range) and
#    different for each measure.
# 5. After an initial spell where we generate only "normal" sample then we switch to randomly generate outliers
#    but not too often, one every few samples. An outlier is a sample is a value which COULD be significantly different
#    from the initial value.
MEASURES_TO_GENERATE = ["voltage", "current"]
MEASURE_VALUE_FUNCTIONS = [
    lambda initial_val, rnd_variation, rnd_variations: initial_val * (1 + rnd_variation),
    lambda initial_val, rnd_variation, rnd_variations: initial_val * (1 - rnd_variations[0]) # Opposite variation for current
]

INITIAL_VARIATION_PERCENT = 0.10  # +/- 10% for normal variations
OUTLIER_VARIATION_PERCENT = 0.40  # +/- 40% for outlier variations
MIN_SMOOTHING_FACTOR = 0.1
MAX_SMOOTHING_FACTOR = 0.8

NORMAL_SAMPLE_COUNT = 120  # Number of samples before outliers start
OUTLIER_INTERVAL = 10  # Every 10th sample after NORMAL_SAMPLE_COUNT is an outlier

BASE_SAMPLE_INTERVAL_SEC = 2.0  # Base time between samples
RANDOM_INTERVAL_ADD_SEC = 1.0  # Additional random time (up to this value) to add to BASE_SAMPLE_INTERVAL_SEC

initial_measure_data = {}

def main(stop_event):
    # Initialization to find our baseline and to setup the smoothing factors (points #1 and #4)
    for measure_name in MEASURES_TO_GENERATE:
        try:
            value, unit = inspect_measure_value(measure_name)
            if value is not None and unit is not None:
                smoothing_factor = random.uniform(MIN_SMOOTHING_FACTOR, MAX_SMOOTHING_FACTOR)
                initial_measure_data[measure_name] = {"value": value, "unit": unit, "current_smoothed_value": value, "smoothing_factor": smoothing_factor}
            else:
                print(f"Could not get initial value for {measure_name}.")
                return
        except Exception as e:
            print(f"Error during initial measure inspection for {measure_name}: {e}")
            return

    print("\nGenerating synthetic data (press Enter to exit)...")

    sample_count = 0
    try:
        while not stop_event.is_set():
            sample_count += 1
            is_outlier_sample = False
            if sample_count > NORMAL_SAMPLE_COUNT and (sample_count - NORMAL_SAMPLE_COUNT) % OUTLIER_INTERVAL == 0: # Point #5
                is_outlier_sample = True
                print(f"Generating outlier sample ({sample_count})")

            current_sample_random_variations = []
            
            for i, measure_name in enumerate(MEASURES_TO_GENERATE):
                initial_value = initial_measure_data[measure_name]["value"]
                variation_percent = OUTLIER_VARIATION_PERCENT if is_outlier_sample else INITIAL_VARIATION_PERCENT # Point #3

                # Generate a base random variation for this measure. then call the custom generation function.
                # This is point #2
                rnd_variation = random.uniform(-variation_percent, variation_percent)

                target_value = MEASURE_VALUE_FUNCTIONS[i](initial_value, rnd_variation, current_sample_random_variations)
                
                # Note that we append values, lambdas can access only values already generated
                current_sample_random_variations.append(rnd_variation)

            # Point #4, apply smoothing and write measures
            for i, measure_name in enumerate(MEASURES_TO_GENERATE):
                data = initial_measure_data[measure_name]
                unit = data["unit"]
                current_smoothed_value = data["current_smoothed_value"]
                smoothing_factor = data["smoothing_factor"]

                # Apply smoothing for normal samples, or if outlier is within normal range but no smoothing
                # for significant outliers (if it happens then let it be abrupt!)
                if not is_outlier_sample or (abs(target_value - initial_measure_data[measure_name]["value"]) / initial_measure_data[measure_name]["value"] <= INITIAL_VARIATION_PERCENT):
                    new_value = current_smoothed_value * (1 - smoothing_factor) + target_value * smoothing_factor
                else:
                    new_value = target_value
                
                initial_measure_data[measure_name]["current_smoothed_value"] = new_value
                
                # Format the value based on its magnitude, we do not want to write too many decimal places
                if abs(initial_measure_data[measure_name]["value"] * INITIAL_VARIATION_PERCENT) < 0.01:
                    format_string = "{:.6f}" 
                else:
                    format_string = "{:.2f}"

                formatted_new_value = format_string.format(new_value)

                try:
                    write_measure(measure_name, formatted_new_value, unit)
                except Exception as e:
                    print(f"Error writing measure {measure_name}: {e}")
                    stop_event.set()
                    break

            if stop_event.is_set():
                break

            # Good job, we generated a sample. Now can take a nap.
            sleep_time = BASE_SAMPLE_INTERVAL_SEC + random.uniform(0, RANDOM_INTERVAL_ADD_SEC)
            time.sleep(sleep_time)

            if stop_event.is_set():
                break

    except KeyboardInterrupt:
        pass # Handle graceful exit on Ctrl+C
    except Exception as e:
        print(f"An unexpected error occurred: {e}")

if __name__ == "__main__":
    run_until_key_press(main)