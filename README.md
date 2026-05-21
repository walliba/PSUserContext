# PSUserContext

> [!WARNING]
> This project is under development and is not production-ready. APIs may change without notice.

**PSUserContext** is a compiled PowerShell module designed to execute commands within different user contexts.  
A *user context* refers to a Windows terminal session — for example, the local console session or any active Remote Desktop (RDP) session.

This module provides fine-grained control over executing processes and scripts as specific users, making it useful for system administration, automation, and remote management scenarios.

## Installation

> [!NOTE]
> This module **must** be run as `NT AUTHORITY\SYSTEM` or from an account granted the `SeDelegateSessionUserImpersonatePrivilege` privilege.
>
> A common use case is execution through an RMM platform running under `SYSTEM`, or via tools such as PsExec.

You can install this module directly with PowerShellGet from PSGallery:

```powershell
Install-Module -Name PSUserContext
```

## Examples

### 1. Refreshing Group Policy for the active console session

```powershell
Invoke-UserContext -Console -ScriptBlock { gpupdate /target:user /force }
```

### 2. Get a list of applicable sessions (similar to `quser`)

```powershell
Get-UserContext
```

### 3. Empty recycle bin for all active sessions

```powershell
Get-UserContext | Invoke-UserContext -ScriptBlock { Clear-RecycleBin -Force }
```

### 4. Delete a stored credential for a particular user

```powershell
Get-UserContext -Name walliba | Invoke-UserContext -ScriptBlock { cmdkey /delete:LegacyGeneric:target=MicrosoftOffice16_Data:SSPI }
```

## Planned Features

- [x] Deserialized output with interactable objects
- [ ] Elevation token support (if necessary)
- [ ] Asynchronous batch execution across multiple sessions
- [ ] Centralized and structured logging

## Credit

Special thanks to [KelvinTegelaar](https://github.com/KelvinTegelaar/RunAsUser) for the foundational work that inspired this project and helped shape its direction.
