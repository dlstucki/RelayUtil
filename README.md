# RelayUtil

```
C:\> RelayUtil.exe

Usage: RelayUtil [options] [command]
Options:
  -?|-h|--help  Show help information
  -v|--verbose  Show verbose output
Commands:
  diag  Operations for diagnosing relay/hc issues (Analyze)
  hc    Operations for HybridConnections (CRUD, Test)
  wcf   Operations for WcfRelays (CRUD, Test)

C:\> RelayUtil.exe wcf -?

Usage: RelayUtil wcf [options] [command]
Options:
  -?|-h|--help  Show help information
  -v|--verbose  Show verbose output
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
  -v|--verbose  Show verbose output
Commands:
  create  Create a HybridConnection
  delete  Delete a HybridConnection
  list    List HybridConnection(s)
  listen  HybridConnection listen command
  send    HybridConnection send command
  test    HybridConnection tests

C:\> RelayUtil.exe diag -?

Usage: RelayUtil diag [arguments] [options]
Arguments:
  namespaceOrConnectionString  Relay Namespace or ConnectionString
Options:
  -?|-h|--help                          Show help information
  -v|--verbose                          Show verbose output
  -a|--all                              Show all details
  -n|-ns|--namespace                    Show namespace details
  --netstat                             Show netstat output
  -p|--ports                            Probe Relay Ports
  -ip|--instance-ports <instanceCount>  Probe Relay Instance Level Ports
  -o|--os                               Display Platform/OS/.NET information
  --security-protocol                   Security Protocol (ssl3|tls|tls11|tls12|tls13)
```
