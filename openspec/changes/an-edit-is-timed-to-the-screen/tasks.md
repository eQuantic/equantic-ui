# Tasks

## 1. Measured by hand first

- [x] 1.1 Read the path a save takes on the web and on the desktop. Verify: on the web a 400ms debounce, a new `dotnet msbuild -t:CompileEQuanticUI` per save (eqc, then bun), an SSE `reload`, and a client mount; on the desktop a metadata update `dotnet watch` applies in process and a wake of the host
- [x] 1.2 Time the web rebuild alone, six saves in a row on this machine. Verify: 11 to 48 seconds at a load average of 22, so the absolute number has to come from a clean machine
