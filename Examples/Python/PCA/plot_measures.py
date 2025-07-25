import matplotlib.pyplot as plt
import pandas as pd
import sys
import os
import locale
from tw_integration import inspect_measure

CSV_FILE_PATH = "measures.csv" # Path to the CSV log file generated by anomaly_detector.py
REFRESH_INTERVAL_SEC = 5 # How often to check for file changes and refresh the plot
MAX_SAMPLES_TO_PLOT = 500 # Maximum number of latest samples to display
AXIS_PADDING_PERCENT = 0.10 # 10% padding for y-axis limits
MAX_MEASURES_FOR_SINGLE_PLOT = 5 # Max measures to show on a single plot, otherwise stack vertically

def main():
    locale.setlocale(locale.LC_ALL, 'en_US.UTF-8')

    last_mod_time = 0
    fig = None
    axes = None
    measure_ranges = {}
    measure_columns = []

    print(f"Monitoring {CSV_FILE_PATH} for changes. Auto-refresh every {REFRESH_INTERVAL_SEC} seconds.")
    print("Close the plot window to exit.")

    # Get measure ranges once at the beginning
    # This assumes the measures in the CSV header are the ones we want to inspect
    try:
        with open(CSV_FILE_PATH, 'r') as f:
            header_line = f.readline().strip()
            measures_from_header = [m.strip('" ') for m in header_line.split(',') if m.strip('" ') != 'anomaly']

        if not measures_from_header:
            print("Error: could not determine measures from CSV header.")
            sys.exit(1)

        for measure_name in measures_from_header:
            min_val, max_val = inspect_measure(measure_name)
            if min_val is not None and max_val is not None:
                measure_ranges[measure_name] = (min_val, max_val)

    except FileNotFoundError:
        pass # If the CSV does not exist yet, just wait for new data
    except Exception as e:
        print(f"Error during initial measure inspection: {e}")
        sys.exit(1)

    while True:
        try:
            # This process exits when the plot window is closed
            if fig and not plt.get_fignums():
                break

            current_mod_time = os.path.getmtime(CSV_FILE_PATH)
        except FileNotFoundError:
            # If the CSV does not exist yet, just wait for new data
            plt.pause(REFRESH_INTERVAL_SEC)
            continue

        if current_mod_time > last_mod_time:
            print(f"File {CSV_FILE_PATH} changed. Reloading data...")
            last_mod_time = current_mod_time

            try:
                df = pd.read_csv(CSV_FILE_PATH, header=0)
                
                if df.empty:
                    # If the CSV is empty, just wait for new data
                    plt.pause(REFRESH_INTERVAL_SEC)
                    continue

                if len(df) < 1: # At least one data row is needed!
                    plt.pause(REFRESH_INTERVAL_SEC)
                    continue

            except pd.errors.EmptyDataError:
                # If the CSV is empty, just wait for new data
                plt.pause(REFRESH_INTERVAL_SEC)
                continue
            except Exception as e:
                print(f"Error reading CSV: {e}")
                plt.pause(REFRESH_INTERVAL_SEC)
                continue

            # We display the latest MAX_SAMPLES_TO_PLOT samples
            if len(df) > MAX_SAMPLES_TO_PLOT:
                df = df.tail(MAX_SAMPLES_TO_PLOT)

            # Assuming the last column is 'anomaly'
            measure_columns = [col for col in df.columns if col != 'anomaly']
            anomaly_column = 'anomaly'

            if anomaly_column not in df.columns:
                print(f"Error: '{anomaly_column}' column not found in {CSV_FILE_PATH}")
                sys.exit(1)

            # Create a "time index" using  original index values
            df['time'] = df.index

            # Determine plotting style: single plot or stacked subplots. When there are
            # few measures it's easier to read them in a single plot, but when there are many
            # measures we stack them vertically to avoid cluttering. It's not optimal because the y-axis are
            # shared (so a very big measure could skew the view of smaller measures) but it's better than nothing
            # FOR NOW. We still need to improve this, maybe group measures by type, range or something similar.
            if len(measure_columns) <= MAX_MEASURES_FOR_SINGLE_PLOT:
                if fig is None or axes is None or len(axes) != 1: # Single plot
                    if fig: plt.close(fig)
                    fig, ax = plt.subplots(1, 1, figsize=(12, 8))
                    axes = [ax]
                    plt.show(block=False)
                else:
                    ax = axes[0]
                    ax.clear()

                for measure in measure_columns:
                    ax.plot(df['time'], df[measure], label=measure)
                
                # Mark anomalies with vertical lines
                anomalies = df[df[anomaly_column] == 1]
                if not anomalies.empty:
                    for x_pos in anomalies['time']:
                        ax.axvline(x=x_pos, color='red', linestyle='-', linewidth=1, label='Anomaly' if x_pos == anomalies['time'].iloc[0] else "")

                ax.set_ylabel('Value')
                ax.legend()
                ax.grid(True)
                ax.set_xlabel('Sample Index')
                ax.set_xlim(df['time'].min(), df['time'].max())

            else:
                # Stacked subplots for multiple measures
                if fig is None or axes is None or len(axes) != len(measure_columns): # Recreate if not stacked setup
                    if fig: plt.close(fig)
                    fig, axes = plt.subplots(len(measure_columns), 1, figsize=(12, 4 * len(measure_columns)), sharex=True)
                    if len(measure_columns) == 1:
                        axes = [axes]
                    plt.show(block=False)
                else:
                    for ax in axes:
                        ax.clear()

                for i, measure in enumerate(measure_columns):
                    ax = axes[i]
                    ax.plot(df['time'], df[measure], label=measure)
                    
                    # Mark anomalies with vertical lines
                    anomalies = df[df[anomaly_column] == 1]
                    if not anomalies.empty:
                        for x_pos in anomalies['time']:
                            ax.axvline(x=x_pos, color='red', linestyle='-', linewidth=1, label='Anomaly' if x_pos == anomalies['time'].iloc[0] else "")

                    ax.set_ylabel(measure)
                    ax.legend()
                    ax.grid(True)

                    # Set y-axis limits if range is available (it should because we can't use this
                    # program without knowing the measure ranges!!!)
                    if measure in measure_ranges:
                        min_val, max_val = measure_ranges[measure]
                        
                        # Calculate "padding", it's a bit of extra space around the min/max values
                        # to avoid the lines being too close to the plot edges.
                        range_diff = max_val - min_val
                        if range_diff == 0:
                            padding = abs(min_val * AXIS_PADDING_PERCENT) if min_val != 0 else 0.1
                        else:
                            padding = range_diff * AXIS_PADDING_PERCENT

                        ax.set_ylim(min_val - padding, max_val + padding)

                        # Draw horizontal lines for min/max range
                        ax.axhline(min_val, color='gray', linestyle='--', linewidth=1, label='Min/Max Range')
                        ax.axhline(max_val, color='gray', linestyle='--', linewidth=1)

                axes[-1].set_xlabel('Sample Index')
                axes[-1].set_xlim(df['time'].min(), df['time'].max())

            fig.suptitle('Values and Anomalies', fontsize=16)
            plt.tight_layout(rect=[0, 0.03, 1, 0.96]) # Otherwise they overlap a little
            plt.draw()
            plt.pause(0.1)
        
        plt.pause(REFRESH_INTERVAL_SEC)

if __name__ == "__main__":
    main()
