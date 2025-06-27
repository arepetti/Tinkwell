namespace Tinkwell.HealthCheck;

public interface IRegistry
{
    void Enqueue(DataSample data);
    (DataSample[] Data, DataSample Average) Snapshot();
}