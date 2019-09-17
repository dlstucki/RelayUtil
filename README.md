# RelayUtil

```
C:\> RelayUtil.exe

Usage: RelayUtil [options] [command]
Options:
  -?|-h|--help  Show help information
  -v|--verbose  Verbose output
Commands:
  diag  Operations for diagnosing relay/hc issues (Analyze)
  hc    Operations for HybridConnections (CRUD, Test)
  wcf   Operations for WcfRelays (CRUD, Test)


C:\> RelayUtil.exe wcf -?

Usage: RelayUtil wcf [options] [command]
Options:
  -?|-h|--help  Show help information
Commands:
  create  Create a WcfRelay
  delete  Delete a WcfRelay
  list    List WcfRelay(s)
  listen  WcfRelay listen command
  send    WcfRelay send command


C:\> RelayUtil.exe hc -?

Usage: RelayUtil hc [options] [command]
Options:
  -?|-h|--help  Show help information
Commands:
  create  Create a HybridConnection
  delete  Delete a HybridConnection
  list    List HybridConnection(s)
  listen  HybridConnection listen command
  send    HybridConnection send command
  test    HybridConnection tests

```