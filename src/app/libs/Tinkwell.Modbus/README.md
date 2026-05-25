# Tinkwell.Modbus

> Part of the [Tinkwell](https://github.com/arepetti/Tinkwell) platform.
> This library can be used independently in any .NET application — no Tinkwell installation required.

Minimal Modbus client library for .NET.
Supports RTU (serial/RS-485) and TCP transports with typed register decoding.

Implements a subset of the [Modbus Application Protocol V1.1b3](https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf), the [Modbus over Serial Line V1.02](https://modbus.org/docs/Modbus_over_serial_line_V1_02.pdf), and the [Modbus TCP/IP Implementation Guide V1.0b](https://modbus.org/docs/Modbus_Messaging_Implementation_Guide_V1_0b.pdf).

## Supported

| Feature | Spec reference |
|---------|---------------|
| **Modbus TCP transport** | TCP/IP Implementation Guide V1.0b, Section 3.1.3 |
| **Modbus RTU transport** | Serial Line V1.02, Section 2.5.1 |
| **Read Holding Registers (FC 03)** | Application Protocol V1.1b3, Section 6.3 |
| **Read Input Registers (FC 04)** | Application Protocol V1.1b3, Section 6.4 |
| **Write Single Register (FC 06)** | Application Protocol V1.1b3, Section 6.6 |
| **CRC-16/Modbus** | Serial Line V1.02, Section 2.5.1.2 |
| **Exception responses** | Application Protocol V1.1b3, Section 7 |
| **Data types** | int16, uint16, int32 (BE/LE), uint32 (BE/LE), float32 (BE/LE/word-swapped) |
| **Platforms** | Windows, Linux, macOS (cross-platform serial via `System.IO.Ports`) |

## Not supported

- Modbus ASCII transport (Serial Line V1.02, Section 2.5.2)
- Write Multiple Registers FC 16 (Application Protocol, Section 6.12)
- Read Coils FC 01 / Read Discrete Inputs FC 02 / Write Coils FC 05, FC 15
- File record access (FC 20/21), diagnostics (FC 08), device identification (FC 43/14)
- Modbus gateway / server mode (this library is a **client** only; you can still address multiple units behind a Modbus TCP gateway by varying `slaveId` — see [Multi-slave over one TCP connection](#multi-slave-over-one-tcp-connection))
- **Modbus TCP with unit ID 0 (broadcast)** — not supported; use unicast 1–247

These may be added in future versions.

> **Note on 32-bit writes:** Writing a 32-bit value (float, int32) to two consecutive registers requires FC 16 (Write Multiple Registers), which is not supported in this version.
> As a workaround, you can issue two `WriteSingleRegisterAsync` calls, but these are **not atomic** — the device will see each register update independently.
> Check your device documentation before relying on this approach.

## Public API overview

| Type | Role |
|------|------|
| `IModbusClient` | Contract for connect, read holding/input registers, write single register, and `IsConnected`. |
| `ModbusClientBase` | Abstract base: shared validation, exception mapping, FC 0x03/0x04/0x06 flows, and broadcast/unicast policy. Subclass for a custom transport. |
| `ModbusTcpClientBase` | Abstract Modbus TCP: MBAP framing, `TcpClient` lifecycle, transaction IDs, and response MBAP checks. **Does not** serialize concurrent I/O. |
| `ModbusTcpClient` | Modbus TCP for general use: same as `ModbusTcpClientBase` but wraps each `SendRequestAsync` in a `SemaphoreSlim(1,1)` so concurrent callers do not interleave on one stream. |
| `UnsynchronizedModbusTcpClient` | Modbus TCP without an internal I/O lock—use only when a single async flow owns the client or you serialize externally. |
| `ModbusRtuClient` | Modbus RTU over `SerialPort` (CRC-16, inter-frame delay). Inherits `ModbusClientBase`. |
| `ModbusException` | Communication or parse errors; for Modbus exception responses, `ExceptionCode` is set when known. |
| `ModbusDataType` | Enum selecting decode layout for `RegisterDecoder`. |
| `RegisterDecoder` | Static helpers to decode `ushort[]` registers into numeric values (with optional `scale`). |

`Crc16` and other helpers are `internal` to the assembly.

### Class hierarchy (TCP and RTU)

```text
IModbusClient
    ← ModbusClientBase (abstract)
          ← ModbusTcpClientBase (abstract)
                ← ModbusTcpClient
                ← UnsynchronizedModbusTcpClient
          ← ModbusRtuClient
```

## Thread safety and concurrency

- **`ModbusTcpClient`:** Concurrent calls to read/write methods are **serialized** (one request/response at a time on the socket).
  Safe to share one instance across tasks as long as you accept throughput limited to a single in-flight request.
- **`UnsynchronizedModbusTcpClient` and `ModbusTcpClientBase`:** No internal lock.
  Overlapping awaits can **interleave** reads/writes on the same `NetworkStream` and corrupt framing—use one logical owner per instance (e.g. one loop, a channel consumer, or your own `SemaphoreSlim`).
- **`ModbusRtuClient`:** Synchronous serial I/O with no request-level mutex.
  **Do not** issue overlapping operations on the same instance from multiple threads/tasks unless you add external serialization.

`ModbusClientBase` and `ModbusTcpClientBase` are public so you can build alternative transports; typical applications use the concrete clients above.

## Parameters, validation, and spec checks

- **Register count (FC 0x03 / 0x04):** `count` must be **1–125** (`ArgumentOutOfRangeException` if not).
  Matches the Modbus ADU quantity limits.
- **Unicast unit ID (slave):** For operations that require a response, the address must be **1–247** (`ArgumentOutOfRangeException`).
  **0** is reserved for broadcast (see below); **248–255** are rejected for unicast in this library.
- **Register address:** `ushort` (0x0000–0xFFFF).
- **Write value:** `ushort` (0x0000–0xFFFF).
- **Modbus TCP host:** non-null, non-empty `host` string.
- **Modbus TCP port:** **1–65535** (default **502** per the TCP guide).
- **Modbus RTU:** non-empty `portName`; **positive** `baudRate` and `readTimeoutMs` (default **1000** ms; used for read and write timeouts on the `SerialPort`).

**Response validation (implementation details):**

- **TCP (MBAP):** Responses must match the request **transaction ID**, **protocol ID 0x0000**, and **unit ID**; length is validated before reading the PDU.
- **FC 0x06 (write single register):** The normal response must **echo** the 5-byte request PDU (function, address, value).
  A mismatch throws `ModbusException`.

## Unit ID 0 (broadcast) by transport

| Operation | Modbus TCP | Modbus RTU |
|-----------|------------|------------|
| **Read holding (FC 0x03)** | **Not supported** — `NotSupportedException` (spec expects a unicast unit ID in the MBAP; broadcast is a serial-line concept). | Request is sent on the wire; **no response is read**. The API returns a **zero-filled** `ushort[count]` (there is no bus data to populate). |
| **Read input (FC 0x04)** | **Not supported** — `ArgumentOutOfRangeException` for `slaveId == 0`. | **Not supported** — same as TCP (`ArgumentOutOfRangeException` for `slaveId == 0`). |
| **Write single (FC 0x06)** | **Not supported** — `NotSupportedException`. | Frame is sent with address **0**; **no** response is read (serial broadcast). |

## Cancellation

- **Modbus TCP:** `CancellationToken` is honored on `ConnectAsync`, `WriteAsync`/`ReadAsync` on the network stream, and on `SemaphoreSlim.WaitAsync` in `ModbusTcpClient`.
- **Modbus RTU:** In-flight work uses synchronous `SerialPort` reads and writes.
  When cancellation is requested, the library **closes the serial port** to unblock I/O, then the operation ends with `OperationCanceledException` where applicable.
  **After** cancel-driven closure, the client can be in an **undefined** state: **dispose** the client and create a **new** instance to reconnect.
  This is called out in the `ModbusRtuClient` API remarks.

## Connection lifetime

`ModbusTcpClient` and `ModbusRtuClient` are **`IAsyncDisposable`**.
After **`ConnectAsync`**, a second `ConnectAsync` on the same instance throws `InvalidOperationException` (one connect per instance; use a new client for a new connection).

### Reconnection after errors

The library does not include built-in reconnection logic.
When a connection drops (`SocketException`, `IOException`, or `ModbusException` during I/O), dispose the current client and create a new one:

```csharp
while (!ct.IsCancellationRequested)
{
    await using var client = new ModbusTcpClient("192.168.1.100");
    try
    {
        await client.ConnectAsync(ct);
        while (!ct.IsCancellationRequested)
        {
            var regs = await client.ReadHoldingRegistersAsync(1, 0, 2, ct);
            Console.WriteLine(RegisterDecoder.Decode(regs, ModbusDataType.Float32BigEndian));
            await Task.Delay(500, ct);
        }
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Connection lost: {ex.Message}. Reconnecting...");
        await Task.Delay(2000, ct);
    }
}
```

## Multi-slave over one TCP connection

Many Modbus TCP gateways route requests to downstream RTU/serial devices using the **unit ID** (the `slaveId` parameter).
You can address all of them through a single `ModbusTcpClient` instance — each call simply sets a different `slaveId`:

```csharp
await using var client = new ModbusTcpClient("192.168.1.200");
await client.ConnectAsync();

// Sensor on unit 1
var temp = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: 0, count: 2);

// Drive on unit 5
var speed = await client.ReadHoldingRegistersAsync(slaveId: 5, startAddress: 100, count: 1);
```

The MBAP header includes the unit ID and the library validates that each response's unit ID matches the request.

## Register addressing

Modbus uses **0-based** register addresses on the wire (0x0000–0xFFFF).
Vendor documentation often uses different numbering:

- **1-based offset** — Some vendors number registers starting at 1.
  Wire address = documented number − 1 (e.g. "register 1" = address 0x0000).
- **Modicon 4xxxx convention** — Holding registers are documented as 40001–49999.
  Wire address = documented number − 40001 (e.g. "40001" = address 0x0000, "40100" = address 0x0063).

## RTU parity and stop bits

The Modbus serial line specification (Section 2.5.1) recommends even parity with 1 stop bit.
When `Parity.None` is used, the spec calls for 2 stop bits — but many devices accept 1 stop bit with no parity.
The `ModbusRtuClient` constructor defaults to `Parity.None` and `StopBits.One` as the most common field configuration.
Adjust to match your device.

## Usage

### Read a float32 temperature sensor over TCP

```csharp
using Tinkwell.Modbus;

await using var client = new ModbusTcpClient("192.168.1.100", 502);
await client.ConnectAsync();

// Read 2 holding registers starting at address 0x0000.
// Many temperature sensors store IEEE 754 float values across two registers.
var registers = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: 0, count: 2);
var temperature = RegisterDecoder.Decode(registers, ModbusDataType.Float32BigEndian);

Console.WriteLine($"Temperature: {temperature:F1} °C");
```

### Modbus TCP: synchronized vs unsynchronized

Use `ModbusTcpClient` by default.
Use `UnsynchronizedModbusTcpClient` when you are sure only one operation runs at a time (or you coordinate externally) and want to avoid the lock overhead.

```csharp
// Same MBAP/validation; no internal SemaphoreSlim—only one caller at a time.
await using var client = new UnsynchronizedModbusTcpClient("192.168.1.100", 502);
await client.ConnectAsync();

var registers = await client.ReadHoldingRegistersAsync(1, 0, 2);
var temperature = RegisterDecoder.Decode(registers, ModbusDataType.Float32BigEndian);
```

### Read a fixed-point int16 value over RTU (serial)

Some sensors report values in tenths of a unit (e.g. 251 = 25.1 °C).
Use `scale` to convert:

```csharp
using System.IO.Ports;
using Tinkwell.Modbus;

await using var client = new ModbusRtuClient(
    portName: "/dev/ttyUSB0",    // COM3 on Windows
    baudRate: 9600,
    parity: Parity.None,
    dataBits: 8,
    stopBits: StopBits.One);
await client.ConnectAsync();

var registers = await client.ReadHoldingRegistersAsync(slaveId: 1, startAddress: 0x0010, count: 1);
var temperature = RegisterDecoder.Decode(registers, ModbusDataType.Int16, scale: 0.1);

Console.WriteLine($"Temperature: {temperature:F1} °C");
```

Optional constructor parameters: `readTimeoutMs` (default 1000) controls `SerialPort` read/write timeouts and maps to I/O errors vs. `ModbusException` on timeout.

### Write a register

```csharp
// Set register 100 to value 1234 on slave 1
await client.WriteSingleRegisterAsync(slaveId: 1, address: 100, value: 1234);
```

### Read input registers

Input registers (FC 04) are read-only registers, often used for analog inputs:

```csharp
var registers = await client.ReadInputRegistersAsync(slaveId: 1, startAddress: 0, count: 4);

// Decode each pair as a different type
var pressure = RegisterDecoder.Decode(registers[0..2], ModbusDataType.Float32BigEndian);
var rpm = RegisterDecoder.Decode(registers[2..4], ModbusDataType.UInt32BigEndian);
```

### Polling loop with error handling

```csharp
using Tinkwell.Modbus;

// ct is a CancellationToken, e.g. from IHostApplicationLifetime or a CancellationTokenSource.
await using var client = new ModbusTcpClient("192.168.1.100");
await client.ConnectAsync(ct);

while (!ct.IsCancellationRequested)
{
    try
    {
        var regs = await client.ReadHoldingRegistersAsync(1, 0x0000, 2, ct);
        var vibration = RegisterDecoder.Decode(regs, ModbusDataType.Float32BigEndian);
        Console.WriteLine($"Vibration: {vibration:F2} mm/s");
    }
    catch (ModbusException ex) when (ex.ExceptionCode == 0x02)
    {
        // Illegal Data Address — register doesn't exist on this device
        Console.Error.WriteLine($"Register not available: {ex.Message}");
        break;
    }
    catch (ModbusException ex)
    {
        Console.Error.WriteLine($"Modbus error: {ex.Message}");
    }

    await Task.Delay(1000, ct);
}
```

### Using the generic IModbusClient interface

```csharp
async Task ReadSensor(IModbusClient client, byte slave, ushort address, ModbusDataType type)
{
    var count = (ushort)RegisterDecoder.RegisterCount(type);
    var registers = await client.ReadHoldingRegistersAsync(slave, address, count);
    var value = RegisterDecoder.Decode(registers, type);
    Console.WriteLine($"Register 0x{address:X4} = {value}");
}
```

### Individual register decode methods

For maximum control, use the type-specific methods directly:

```csharp
var regs = await client.ReadHoldingRegistersAsync(1, 0x0100, 2);

// Big-endian float (ABCD) — most common
float valueBE = RegisterDecoder.ToFloat32BigEndian(regs[0], regs[1]);

// Word-swapped float (BADC) — Schneider/Modicon PLCs
float valueWS = RegisterDecoder.ToFloat32WordSwapped(regs[0], regs[1]);

// Little-endian float (DCBA)
float valueLE = RegisterDecoder.ToFloat32LittleEndian(regs[0], regs[1]);
```

## Data type reference

| Type | Registers | Byte order | Description |
|------|-----------|------------|-------------|
| `Int16` | 1 | — | Signed 16-bit (two's complement) |
| `UInt16` | 1 | — | Unsigned 16-bit |
| `Int32BigEndian` | 2 | ABCD | Signed 32-bit, big-endian |
| `Int32LittleEndian` | 2 | DCBA | Signed 32-bit, little-endian |
| `UInt32BigEndian` | 2 | ABCD | Unsigned 32-bit, big-endian |
| `UInt32LittleEndian` | 2 | DCBA | Unsigned 32-bit, little-endian |
| `Float32BigEndian` | 2 | ABCD | IEEE 754 float, big-endian (most common) |
| `Float32LittleEndian` | 2 | DCBA | IEEE 754 float, little-endian |
| `Float32WordSwapped` | 2 | BADC | IEEE 754 float, word-swapped (Schneider/Modicon) |

The optional `scale` parameter in `RegisterDecoder.Decode()` multiplies the decoded value after conversion (e.g. `scale: 0.1` when the device reports temperature in tenths of a degree).

## Exception codes

When a device returns an error, `ModbusException.ExceptionCode` contains one of these standard codes (Application Protocol V1.1b3, Section 7, Table 2):

| Code | Name | Meaning |
|------|------|-------------|
| 0x01 | Illegal Function | The function code is not supported |
| 0x02 | Illegal Data Address | The register address is not available |
| 0x03 | Illegal Data Value | The value in the request is not allowed |
| 0x04 | Slave Device Failure | Unrecoverable error on the device |
| 0x05 | Acknowledge | Request accepted, processing in progress |
| 0x06 | Slave Device Busy | Device is busy, retry later |
| 0x08 | Memory Parity Error | Extended file area parity check failed |
| 0x0A | Gateway Path Unavailable | Gateway misconfigured or overloaded |
| 0x0B | Gateway Target Device Failed to Respond | No response from the target device behind the gateway |
