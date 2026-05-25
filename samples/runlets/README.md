# Tinkwell Runlet Samples

Sample projects showing how to build custom Tinkwell runlets.
Open `Samples.slnx` to work with all samples, or browse individual projects below.

| Sample | Type | What it shows |
|--------|------|---------------|
| [Sample.GrpcKeyValue](Sample.GrpcKeyValue/) | gRPC | Minimal gRPC runlet — in-memory key/value store with `Get`/`Set` RPCs |
| [Sample.GrpcMeasureReader](Sample.GrpcMeasureReader/) | gRPC | Cross-runner service discovery — reads a measure via the Measures gRPC API |
| [Sample.HeadlessMeasureWatcher](Sample.HeadlessMeasureWatcher/) | Headless | gRPC server-streaming — watches measure changes via the `Watch` RPC |
| [Sample.AnomalyDetector](Sample.AnomalyDetector/) | Headless | Z-score anomaly detection on measures — publishes events on outliers |

Each project has its own README with configuration, usage, and implementation details.
