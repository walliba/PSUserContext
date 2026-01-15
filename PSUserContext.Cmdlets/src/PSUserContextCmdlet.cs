using System.Management.Automation;

namespace PSUserContext.Cmdlets;

/// <summary>
/// Base class for any cmdlet which has to execute within a separate user context.
/// </summary>
public abstract class UserContextCmdlet : PSCmdlet
{
    
}