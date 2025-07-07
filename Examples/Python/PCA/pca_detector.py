import numpy as np
from sklearn.decomposition import PCA

class PcaAnomalyDetector:
    def __init__(self, n_components, anomaly_threshold_percentile):
        self.n_components = n_components
        self.anomaly_threshold_percentile = anomaly_threshold_percentile
        self.pca_model = None
        self.anomaly_threshold = None

    def train(self, data_buffer):
        if not data_buffer:
            raise ValueError("Data buffer cannot be empty for training.")

        self.pca_model = PCA(n_components=self.n_components, svd_solver='randomized')
        self.pca_model.fit(np.array(data_buffer))

        # Calculate reconstruction errors for training data
        reconstructed_data = self.pca_model.inverse_transform(self.pca_model.transform(np.array(data_buffer)))
        reconstruction_errors = np.linalg.norm(np.array(data_buffer) - reconstructed_data, axis=1)

        self.anomaly_threshold = np.percentile(reconstruction_errors, self.anomaly_threshold_percentile)
        return self.anomaly_threshold

    def detect(self, sample):
        if self.pca_model is None or self.anomaly_threshold is None:
            raise RuntimeError("PCA model not trained. Call train() first.")

        current_sample_np = np.array([sample])
        transformed_sample = self.pca_model.transform(current_sample_np)
        reconstructed_sample = self.pca_model.inverse_transform(transformed_sample)
        current_reconstruction_error = np.linalg.norm(current_sample_np - reconstructed_sample)

        is_anomaly = current_reconstruction_error > self.anomaly_threshold
        return is_anomaly, current_reconstruction_error, reconstructed_sample[0]

    def is_trained(self):
        return self.pca_model is not None and self.anomaly_threshold is not None
