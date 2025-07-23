namespace Tinkwell.Watchdog.AnomalyDetection;

sealed record Sample(double Cpu, double Memory, int Threads, int Handles);
