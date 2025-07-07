# Anomaly Detector using Randomized PCA

This Python script monitors real-time measure data from the `tw` command-line tool, performs normalization, and uses Randomized Principal Component Analysis (PCA) to detect anomalies.

This example consists of three main scripts you're going to run concurrently:

* `anomaly_detector.py`: it detects anomalies in the input data and save a CSV file with the inputs and its findings.
* `feed_synthetic_data.py`: generates synthetic data to test the anomaly detector.
* `plot_measures.py`: plots the results saved in the CSV file exported by `anomaly_detector.py`.

## Prerequisites

*   Python 3.x installed.
*   The `tw` command-line tool must be installed and accessible in your system's PATH, if not then search for `TW_PATH` and adjust it accordingly.

## Installation

Navigate to the directory containing this script and install the required libraries:
```bash
cd ./Examples/Python/PCA
pip install -r requirements.txt
```

Copy the configuration files (`ensamble.tw`, `measures.twm` and `constants.twm`) to the directory where you installed/copied/built Tinkwell (usually the same directory where `tw` is located).

## Configuration

You can modify the following parameters directly in the `anomaly_detector.py` file:

*   `MEASURES_TO_SUBSCRIBE`: List of measures to subscribe to (default: `["voltage", "current", "power"]`).
*   `PCA_BUFFER_SIZE`: Number of samples to collect before training the PCA model (default: `100`).
*   `ANOMALY_THRESHOLD_PERCENTILE`: Percentile for setting the anomaly threshold based on reconstruction errors (default: `99`).
*   `N_COMPONENTS`: Number of principal components for PCA (default: `2`).

You can also modify parameters in `feed_synthetic_data.py`:

*   `MEASURES_TO_GENERATE`: List of measure names to generate synthetic data for (default: `["voltage", "current"]`).
*   `MEASURE_VALUE_FUNCTIONS`: A list of lambda functions, one for each measure in `MEASURES_TO_GENERATE`, defining how its target value is calculated. Each lambda function receives three arguments:
    *   `initial_val`: The initial value of the measure.
    *   `rnd_variation`: A randomly generated variation (between -`INITIAL_VARIATION_PERCENT` and `INITIAL_VARIATION_PERCENT` for normal samples, or -`OUTLIER_VARIATION_PERCENT` and `OUTLIER_VARIATION_PERCENT` for outliers) that represents the independent random component for the current measure.
    *   `rnd_variations`: A list of the *random variations* (not final values) that have been calculated for measures *earlier* in the `MEASURES_TO_GENERATE` list for the current sample. This allows for defining relationships where one measure's value depends on another's.
    
    The lambda function should return the *target value* for the current measure.
    
*   `INITIAL_VARIATION_PERCENT`: Percentage of variation for normal samples (default: `0.10`).
*   `OUTLIER_VARIATION_PERCENT`: Percentage of variation for outlier samples (default: `0.40`).
*   `NORMAL_SAMPLE_COUNT`: Number of normal samples before outliers start (default: `120`).
*   `OUTLIER_INTERVAL`: How often an outlier sample is generated after `NORMAL_SAMPLE_COUNT` (default: `10`).
*   `BASE_SAMPLE_INTERVAL_SEC`: Base time between samples in seconds (default: `2.0`).
*   `RANDOM_INTERVAL_ADD_SEC`: Additional random time to add to the sample interval (default: `1.0`).
*   `MIN_SMOOTHING_FACTOR`: Minimum smoothing factor for measures (default: `0.1`).
*   `MAX_SMOOTHING_FACTOR`: Maximum smoothing factor for measures (default: `0.8`).

You can also modify parameters in `plot_measures.py`:

*   `MAX_SAMPLES_TO_PLOT`: Maximum number of latest samples to display in the plot (default: `500`).
*   `MAX_MEASURES_FOR_SINGLE_PLOT`: Maximum number of measures to display on a single plot before switching to stacked subplots (default: `5`).

## Usage

### Running the Anomaly Detector

To run the anomaly detector, execute the script from your terminal:

```bash
python anomaly_detector.py
```

The script will start monitoring the specified measures. It will print messages when it trains the PCA model and when it detects an anomaly. It will also log the measure values and anomaly status to `measures.csv`.

To stop the script, simply press the `Enter` key in the terminal where the script is running.

### Plotting the Data

After running the `anomaly_detector.py` and generating `measures.csv`, you can visualize the data and anomalies using the plotting script:

```bash
python plot_measures.py
```

This will open a plot displaying the measure values over time, with detected anomalies highlighted by vertical red lines. The plot will automatically refresh every 5 seconds if the `measures.csv` file content changes. Close the plot window to exit the script.

### Generating Synthetic Data

To feed synthetic data to the `tw` measures, you can use the `feed_synthetic_data.py` script:

```bash
python feed_synthetic_data.py
```

This script will continuously generate random variations for the configured measures (`voltage` and `current` by default) and write them using the `tw measures write` command. It will introduce occasional outliers to simulate anomalous behavior. The generation speed is set to approximately one sample every 2 to 3 seconds. Press `Enter` to exit the script.

## How it Works

### Tinkwell Integration Module (`tw_integration.py`)

To abstract the direct interaction with the `tw` command-line tool, a dedicated module `tw_integration.py` has been created. This module provides a Python interface for:

*   `inspect_measure(measure_name)`: Retrieves the minimum and maximum values for a given measure.
*   `inspect_measure_value(measure_name)`: Retrieves the current value and unit for a given measure.
*   `write_measure(measure_name, value, unit)`: Writes a specified value with its unit to a measure.
*   `TwMeasuresSubscriber`: A class to manage the lifecycle of the `tw measures subscribe` subprocess. Its `get_latest_output()` method  directly returns parsed measure name-value pairs.

You can reuse this module if you need to integrate your Python code with Tinkwell using the `tw` command line utility.

### Measure Sampler Module (`measure_sampler.py`)

This module introduces the `MeasureSampler` class, which is responsible for collecting individual measure updates and periodically emitting complete samples. This addresses the challenge of measures updating at different rates by ensuring a sample is generated at a fixed interval, always using the latest known value for each measure.

*   `MeasureSampler(measure_names, sample_interval_sec)`: Initializes the sampler with a list of measure names and a sample interval.
*   `update_measure(name, value)`: Called when a new value for a specific measure arrives. It updates the internal state.
*   `get_next_sample(timeout)`: Retrieves a complete sample (a list of latest known values for all measures) from an internal queue. A sample is generated periodically by an internal thread.
*   `start()`: Starts the internal sampling thread.
*   `stop()`: Stops the internal sampling thread.

### Anomaly Detection Process (`anomaly_detector.py`)

The `anomaly_detector.py` script orchestrates the anomaly detection. It leverages the `tw_integration.py` module for interacting with the `tw` command, the `measure_sampler.py` for debounced sample collection, and the `pca_detector.py` for the core PCA logic.

1.  **Measure Inspection**: For each configured measure, the script uses `tw_integration.inspect_measure()` to retrieve its `Minimum` and `Maximum` values. These are used for normalizing the incoming data.
2.  **Data Subscription**: It then uses `tw_integration.TwMeasuresSubscriber` to start and manage the `tw measures subscribe <MEASURE_NAMES>` subprocess to receive real-time data.
3.  **Sample Collection**: Raw measure updates from `tw` are fed into a `measure_sampler.MeasureSampler` instance. This sampler collects individual measure updates and, at a fixed `SAMPLE_INTERVAL_SEC`, provides a complete sample for processing.
4.  **Normalization**: Incoming measure values are normalized to a 0-1 range using the inspected min/max values.
5.  **PCA Anomaly Detection**: The core anomaly detection is handled by an instance of `pca_detector.PcaAnomalyDetector`.
    *   **Training**: Once `PCA_BUFFER_SIZE` samples are collected, the `PcaAnomalyDetector.train()` method is called with the normalized data buffer. This trains the PCA model and calculates the anomaly threshold based on reconstruction errors.
    *   **Detection**: For every new incoming sample, its reconstruction error is calculated using `PcaAnomalyDetector.detect()`. If this error exceeds the established threshold, the sample is flagged as an anomaly.
6.  **Logging and Output**: The script logs the measure values and anomaly status to `measures.csv` and prints detailed anomaly information to the console when detected.

## Anomaly Detection Algorithm Details

### Introduction to Principal Component Analysis (PCA)

Principal Component Analysis (PCA) is a statistical technique that transforms a set of possibly correlated variables into a smaller set of new variables called principal components, which are linearly uncorrelated. The first component grabs the most variance it can from the data, and each one after it captures as much of what's left while staying independent from the others.

In even simpler terms: when your data has a bunch of features, PCA figures out which combinations of them explain the most variation. It then turns those into new features that summarize your data without all the extra clutter. This helps when you want to make the data easier to visualize or feed into a machine learning model without losing too much of what makes it useful.

#### Randomized PCA

Randomized PCA is based on **randomized algorithms for matrix decomposition**. Instead of directly computing the full covariance matrix and solving for eigenvectors (which can be slow for big datasets), it approximates the principal components using a random projection method.

This gives you an efficient way to estimate the top k principal components of a data matrix without needing to compute the full decomposition, especially useful when the matrix is huge or sparse.

### Why PCA is a Good Fit for Anomaly Detection

PCA is particularly well-suited for anomaly detection in multivariate time series data (like our measures) for several reasons:

1.  **Dimensionality Reduction**: Real-world systems often involve many correlated sensors or measures. PCA can reduce this high-dimensional data into a lower-dimensional subspace, making the anomaly detection task more manageable and computationally efficient.
2.  **Normal Behavior Modeling**: PCA effectively captures the "normal" operating patterns and correlations within the data. When the system behaves normally, its data points will lie close to the subspace spanned by the principal components.
3.  **Reconstruction Error as Anomaly Score**: Anomalies often represent deviations from these normal patterns. When an anomalous data point is projected onto the PCA subspace and then reconstructed back to the original dimension, the difference between the original and reconstructed point (the "reconstruction error") will be significantly larger than for normal data points. This error serves as an effective anomaly score.
4.  **Unsupervised Learning**: PCA is an unsupervised learning technique, meaning it does not require labeled anomaly data for training. It learns the normal behavior from the available data, which is often abundant, and then flags anything that deviates significantly from this learned normal.

### Detailed Anomaly Detection Algorithm

The anomaly detection process in this script leverages the reconstruction error property of PCA:

1.  **Data Normalization**: Each incoming measure value is first normalized using its pre-determined minimum and maximum values. This ensures that all measures contribute equally to the PCA, regardless of their original scale.
2.  **PCA Model Training**:
    *   The script collects a `PCA_BUFFER_SIZE` number of normalized data samples. Each sample is a vector representing the current values of all subscribed measures.
    *   A Randomized PCA model is then trained on this buffer of "normal" data.
    *   After training, the model can project new data points onto its principal components and reconstruct them.
3.  **Reconstruction Error Calculation**: For each data sample in the training buffer, its reconstruction error is calculated. This error is the Euclidean distance (or L2 norm) between the original data point and its reconstructed version after being projected onto the PCA subspace and then inverse-transformed.
4.  **Anomaly Threshold Determination**: A statistical threshold for anomaly detection is established from the distribution of these reconstruction errors. Specifically, the `ANOMALY_THRESHOLD_PERCENTILE` (e.g., 99th percentile) of the reconstruction errors from the training data is chosen as the threshold. This means that `ANOMALY_THRESHOLD_PERCENTILE`% of the "normal" training data will have a reconstruction error below this threshold.
5.  **Real-time Anomaly Detection**:
    *   As new measure data arrives, it is normalized and formed into a new sample vector.
    *   This new sample is then passed through the trained PCA model to calculate its reconstruction error.
    *   If the calculated reconstruction error for the new sample exceeds the pre-determined anomaly threshold, the sample is flagged as an anomaly.
    *   When an anomaly is detected, the script prints detailed information, including the raw and normalized values, the reconstruction error, and the anomaly threshold, to help in understanding the nature of the deviation.

### References

*   **PCA Basics**:
    *   Jolliffe, I. T. (2002). *Principal Component Analysis*. Springer.
*   **Randomized PCA**:
    *   Halko, N., Martinsson, P. G., & Tropp, J. A. (2011). Finding structure with randomness: Probabilistic algorithms for constructing approximate matrix decompositions. *SIAM Review*, 53(2), 217-288.
*   **PCA for Anomaly Detection**:
    *   Shyu, M. L., Chen, S. C., Sarinnapakorn, M., & Chang, L. (2003). A novel anomaly detection scheme based on principal component classifier. *Proceedings of the IEEE International Conference on Data Mining (ICDM)*, 2003, 347-354.
    *   Wang, S., & Ma, J. (2018). Anomaly detection based on PCA and reconstruction error. *Journal of Physics: Conference Series*, 1087(6), 062029.