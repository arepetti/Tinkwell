<#
.SYNOPSIS
  Send a single command to the Tinkwell Supervisor over a bidirectional named pipe.

.PARAMETER Command
  The text command to send (required).

.PARAMETER MachineName
  The target machine (defaults to local ".").

.PARAMETER PipeName
  The named-pipe endpoint.
  (defaults to "tinkwell-supervisor-command-server").

.EXAMPLE
  .\Send-SupervisorCommand.ps1 "runners list""
  #=> sends "runners list" to \\.\pipe\tinkwell-supervisor-command-server.
#>

param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$Command,

  [Parameter(Position = 1)]
  [string]$MachineName = '.',

  [Parameter(Position = 2)]
  [string]$PipeName = 'tinkwell-supervisor-command-server'
)

try {
  $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
    $MachineName, $PipeName,
    [System.IO.Pipes.PipeDirection]::InOut
  )
  $pipe.Connect(5000)

  $writer = [System.IO.StreamWriter]::new($pipe)
  $writer.AutoFlush = $true
  $reader = [System.IO.StreamReader]::new($pipe)

  $writer.WriteLine($Command)
  $response = $reader.ReadLine()

  if ($null -ne $response) {
    Write-Output $response 
  }

  $writer.WriteLine("exit")
}
catch {
  Write-Error "Failed to send '$Command' to $MachineName\pipe\$PipeName`: $_"
}
