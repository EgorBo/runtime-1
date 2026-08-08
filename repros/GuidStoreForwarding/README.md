# Repro

On Linux x64, `Test` homes the by-value `Guid` using two 8-byte stores before `Guid` formatting
reads it with one 16-byte load:

```asm
mov      qword ptr [rsp+8], rdi
mov      qword ptr [rsp+0x10], rsi
...
vmovups  xmm0, xmmword ptr [rdi]
```

```bash
DOTNET_TieredCompilation=0 DOTNET_JitDisasm=Test dotnet run -c Release
DOTNET_ReadyToRun=0 DOTNET_TieredCompilation=0 \
  DOTNET_JitDisasm='System.Guid:TryFormatCore*' dotnet run -c Release
```
