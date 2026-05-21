# PSUserContext

> [!WARNING]
> This project is under development and is not production-ready. APIs may change without notice.

**PSUserContext** is a compiled PowerShell module designed to execute commands within different user contexts.  
A *user context* refers to a Windows terminal session — for example, the local console session or any active Remote Desktop (RDP) session.

This module provides fine-grained control over executing processes and scripts as specific users, making it useful for system administration, automation, and remote management scenarios.

## Installation

> [!NOTE]
> This module **MUST** be ran as `NT AUTHORITY\SYSTEM` or from an account with the `SeDelegateSessionUserImpersonatePrivilege` privilege.
> A typical scenario is running from an RMM tool which executes under SYSTEM, or by using PsExec.

You can install this module directly with PowerShellGet from PSGallery:

`Install-Module -Name PSUserContext`

## Use Case

### 1. Refreshing Group Policy in a User Session
System or RMM tasks often run under the **SYSTEM** account and can’t directly interact with the logged-on user’s environment.  
With PSUserContext, you can trigger user-specific updates such as:
```powershell
Invoke-UserContext -Console -ScriptBlock {gpupdate /target:user /force}
```

### 2. Empty recycle bin for all active sessions
```powershell
Get-UserContext | Invoke-UserContext -ScriptBlock {Clear-RecycleBin -Force}
```

## Planned Features

- [x] Deserialized output with interactable objects
- [ ] Asynchronous batch execution across multiple sessions
- [ ] Centralized and structured logging

## Credit

Special thanks to [KelvinTegelaar](https://github.com/KelvinTegelaar/RunAsUser) for the foundational work that inspired this project and helped shape its direction.
